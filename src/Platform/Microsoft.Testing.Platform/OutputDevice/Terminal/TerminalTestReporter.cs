// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal test reporter that outputs test progress and is capable of writing ANSI or non-ANSI output via the given terminal.
/// </summary>
[UnsupportedOSPlatform("browser")]
[Embedded]
internal sealed partial class TerminalTestReporter : IDisposable
{
    /// <summary>
    /// The two directory separator characters to be passed to methods like <see cref="string.IndexOfAny(char[])"/>.
    /// </summary>
    private static readonly string[] NewLineStrings = ["\r\n", "\n"];

    internal const string SingleIndentation = "  ";

    internal const string DoubleIndentation = $"{SingleIndentation}{SingleIndentation}";

    internal Func<IStopwatch> CreateStopwatch { get; set; } = SystemStopwatch.StartNew;

    internal event EventHandler OnProgressStartUpdate
    {
        add => _terminalWithProgress.OnProgressStartUpdate += value;
        remove => _terminalWithProgress.OnProgressStartUpdate -= value;
    }

    internal event EventHandler OnProgressStopUpdate
    {
        add => _terminalWithProgress.OnProgressStopUpdate += value;
        remove => _terminalWithProgress.OnProgressStopUpdate -= value;
    }

    private readonly Func<bool> _isCancellationRequested;

    private readonly List<TestRunArtifact> _artifacts = [];

    private readonly TerminalTestReporterOptions _options;

    private readonly TestProgressStateAwareTerminal _terminalWithProgress;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private readonly uint? _originalConsoleMode;

    /// <summary>
    /// Per-assembly run state, keyed by the caller-provided execution id. The in-process Microsoft.Testing.Platform
    /// host registers a single assembly; the <c>dotnet test</c> orchestrator registers one entry per child test
    /// assembly. Progress rendering already supports N workers (slots), so this only generalizes the bookkeeping.
    /// </summary>
    private readonly ConcurrentDictionary<string, TestProgressState> _assemblies = new();

    private bool _isDiscovery;
    private DateTimeOffset? _testExecutionStartTime;

    private DateTimeOffset? _testExecutionEndTime;

    /// <summary>Gets the total number of tests across all registered assemblies.</summary>
    public int TotalTests => _assemblies.Values.Sum(static a => a.TotalTests);

    private bool WasCancelled
    {
        get => field || _isCancellationRequested();
        set;
    }

    private bool? _shouldShowPassedTests;

    private int _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalTestReporter"/> class for orchestrator callers that
    /// drive cancellation out-of-band (via <see cref="StartCancelling"/>) rather than through a cancellation token.
    /// </summary>
    public TerminalTestReporter(IConsole console, TerminalTestReporterOptions options)
        : this(console, static () => false, options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalTestReporter"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    public TerminalTestReporter(
        IConsole console,
        Func<bool> isCancellationRequested,
        TerminalTestReporterOptions options)
        : this(console, isCancellationRequested, options, new EmbeddedNopLogger())
    {
    }

    private sealed class EmbeddedNopLogger : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalTestReporter"/> class with cancellation state, reporter options,
    /// and a logger for low-noise diagnostics of unexpected progress rendering/erase failures.
    /// </summary>
    public TerminalTestReporter(
        IConsole console,
        Func<bool> isCancellationRequested,
        TerminalTestReporterOptions options,
        ILogger logger)
    {
        _isCancellationRequested = isCancellationRequested;
        _options = options;

        Func<bool?> showProgress = options.ShowProgress;
        ITerminal terminal;
        bool useCursorRenderer;
        if (_options.AnsiMode == AnsiMode.SimpleAnsi)
        {
            // We are told externally that we are in CI, use simplified ANSI mode.
            terminal = new SimpleAnsiTerminal(console);
            useCursorRenderer = false;
        }
        else
        {
            // We are not in CI, or in CI non-compatible with simple ANSI, autodetect terminal capabilities
            (bool consoleAcceptsAnsiCodes, bool _, uint? originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
            _originalConsoleMode = originalConsoleMode;
            bool useAnsi = _options.AnsiMode switch
            {
                AnsiMode.ForceAnsi => true,
                AnsiMode.NoAnsi => false,
                AnsiMode.AnsiIfPossible => consoleAcceptsAnsiCodes,
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            terminal = useAnsi ? new AnsiTerminal(console) : new NonAnsiTerminal(console);

            // Only cursor-capable ANSI terminals can redraw progress in place. Anything that resolved to a
            // non-ANSI terminal (explicit --no-ansi, or AnsiIfPossible on a console that can't do ANSI) uses
            // the silence-driven heartbeat renderer instead, so it still gets a progress signal in CI / piped
            // / redirected runs without spamming a fixed-cadence summary.
            useCursorRenderer = useAnsi;
        }

        IProgressRenderer renderer = useCursorRenderer
            ? new CursorProgressRenderer()
            : new SilenceDrivenHeartbeatRenderer(_options.HeartbeatSilenceThreshold, _options.SlowTestThreshold, () => CreateStopwatch());

        _terminalWithProgress = new TestProgressStateAwareTerminal(terminal, showProgress, renderer, logger);
    }

    public void PrintOutOfProcessArtifacts()
    {
        if (_artifacts.Count == 0)
        {
            return;
        }

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(TerminalResources.OutOfProcessArtifactsProduced);

            foreach (TestRunArtifact artifact in _artifacts)
            {
                terminal.Append(DoubleIndentation);
                terminal.Append("- ");
                terminal.AppendLink(artifact.Path, lineNumber: null);
                terminal.AppendLine();
            }

            terminal.AppendLine();
        });
    }

    public void Dispose()
    {
        _terminalWithProgress.Dispose();

        // Restore the console mode that we may have changed when enabling ANSI output. If
        // TestExecutionCompleted already ran, the saved mode was consumed there; if it did not
        // (e.g. on an abnormal teardown path where Dispose is the only thing called), this is the
        // last chance to leave the user's terminal in the state we found it in.
        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
    }

    public void ArtifactAdded(bool outOfProcess, string? assembly, string? targetFramework, string? architecture, string? executionId, string? testName, string path)
        => _artifacts.Add(new TestRunArtifact(outOfProcess, assembly, targetFramework, architecture, executionId, testName, path));

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    public void StartCancelling()
    {
        WasCancelled = true;
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.AppendLine();
            terminal.AppendLine(TerminalResources.CancellingTestSession);
            terminal.AppendLine(TerminalResources.PressCtrlCAgainToForceExit);
            terminal.AppendLine();
        });
    }
}
