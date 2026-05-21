// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal test reporter that outputs test progress and is capable of writing ANSI or non-ANSI output via the given terminal.
/// </summary>
[UnsupportedOSPlatform("browser")]
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

    private readonly string _assembly;
    private readonly string? _targetFramework;
    private readonly string? _architecture;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;

    private readonly List<TestRunArtifact> _artifacts = [];

    private readonly TerminalTestReporterOptions _options;

    private readonly TestProgressStateAwareTerminal _terminalWithProgress;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private readonly uint? _originalConsoleMode;

    private TestProgressState? _testProgressState;

    private bool _isDiscovery;
    private DateTimeOffset? _testExecutionStartTime;

    private DateTimeOffset? _testExecutionEndTime;

    private bool WasCancelled
    {
        get => field || _testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested;
        set;
    }

    private bool? _shouldShowPassedTests;

    private int _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalTestReporter"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    public TerminalTestReporter(
        string assembly,
        string? targetFramework,
        string? architecture,
        IConsole console,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        TerminalTestReporterOptions options)
    {
        _assembly = assembly;
        _targetFramework = targetFramework;
        _architecture = architecture;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _options = options;

        Func<bool?> showProgress = options.ShowProgress;
        ITerminal terminal;
        if (_options.AnsiMode == AnsiMode.SimpleAnsi)
        {
            // We are told externally that we are in CI, use simplified ANSI mode.
            terminal = new SimpleAnsiTerminal(console);
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
            if (!useAnsi)
            {
                showProgress = () => false;
            }
        }

        _terminalWithProgress = new TestProgressStateAwareTerminal(terminal, showProgress);
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
            terminal.AppendLine(PlatformResources.OutOfProcessArtifactsProduced);

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

    public void ArtifactAdded(bool outOfProcess, string? testName, string path)
        => _artifacts.Add(new TestRunArtifact(outOfProcess, testName, path));

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    public void StartCancelling()
    {
        WasCancelled = true;
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.AppendLine();
            terminal.AppendLine(PlatformResources.CancellingTestSession);
            terminal.AppendLine();
        });
    }
}
