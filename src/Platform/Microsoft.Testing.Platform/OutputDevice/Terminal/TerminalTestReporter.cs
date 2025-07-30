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
    private readonly Lock _lock = new();

    private readonly uint? _originalConsoleMode;

    private TestProgressState? _testProgressState;

    private bool _isDiscovery;
    private DateTimeOffset? _testExecutionStartTime;

    private DateTimeOffset? _testExecutionEndTime;

    private int _buildErrorsCount;

    private bool WasCancelled
    {
        get => field || _testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested;
        set => field = value;
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

        Func<bool?> showProgress = _options.ShowProgress;
        TestProgressStateAwareTerminal terminalWithProgress;

        // When not writing to ANSI we write the progress to screen and leave it there so we don't want to write it more often than every few seconds.
        int nonAnsiUpdateCadenceInMs = 3_000;
        // When writing to ANSI we update the progress in place and it should look responsive so we update every half second, because we only show seconds on the screen, so it is good enough.
        int ansiUpdateCadenceInMs = 500;
        if (!_options.UseAnsi || _options.ForceAnsi is false)
        {
            terminalWithProgress = new TestProgressStateAwareTerminal(new NonAnsiTerminal(console), showProgress, writeProgressImmediatelyAfterOutput: false, updateEvery: nonAnsiUpdateCadenceInMs);
        }
        else
        {
            if (_options.UseCIAnsi)
            {
                // We are told externally that we are in CI, use simplified ANSI mode.
                terminalWithProgress = new TestProgressStateAwareTerminal(new SimpleAnsiTerminal(console), showProgress, writeProgressImmediatelyAfterOutput: true, updateEvery: nonAnsiUpdateCadenceInMs);
            }
            else
            {
                // We are not in CI, or in CI non-compatible with simple ANSI, autodetect terminal capabilities
                (bool consoleAcceptsAnsiCodes, bool _, uint? originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
                _originalConsoleMode = originalConsoleMode;
                terminalWithProgress = consoleAcceptsAnsiCodes || _options.ForceAnsi is true
                    ? new TestProgressStateAwareTerminal(new AnsiTerminal(console), showProgress, writeProgressImmediatelyAfterOutput: true, updateEvery: ansiUpdateCadenceInMs)
                        : new TestProgressStateAwareTerminal(new NonAnsiTerminal(console), showProgress, writeProgressImmediatelyAfterOutput: false, updateEvery: nonAnsiUpdateCadenceInMs);
            }
        }

        _terminalWithProgress = terminalWithProgress;
    }

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount, bool isDiscovery)
    {
        _isDiscovery = isDiscovery;
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted()
        => GetOrAddAssemblyRun();

    private TestProgressState GetOrAddAssemblyRun()
    {
        if (_testProgressState is not null)
        {
            return _testProgressState;
        }

        lock (_lock)
        {
            if (_testProgressState is not null)
            {
                return _testProgressState;
            }

            IStopwatch sw = CreateStopwatch();
            var assemblyRun = new TestProgressState(Interlocked.Increment(ref _counter), _assembly, _targetFramework, _architecture, sw);
            int slotIndex = _terminalWithProgress.AddWorker(assemblyRun);
            assemblyRun.SlotIndex = slotIndex;
            _testProgressState = assemblyRun;
            return assemblyRun;
        }
    }

    public void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        _terminalWithProgress.WriteToTerminal(_isDiscovery ? AppendTestDiscoverySummary : AppendTestRunSummary);

        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
        _buildErrorsCount = 0;
        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }

    private void AppendTestRunSummary(ITerminal terminal)
    {
        IEnumerable<IGrouping<bool, TestRunArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);
        if (artifactGroups.Any())
        {
            // Add extra empty line when we will be writing any artifacts, to split it from previous output.
            terminal.AppendLine();
        }

        foreach (IGrouping<bool, TestRunArtifact> artifactGroup in artifactGroups)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(artifactGroup.Key ? PlatformResources.OutOfProcessArtifactsProduced : PlatformResources.InProcessArtifactsProduced);
            foreach (TestRunArtifact artifact in artifactGroup)
            {
                terminal.Append(DoubleIndentation);
                terminal.Append("- ");
                if (!RoslynString.IsNullOrWhiteSpace(artifact.TestName))
                {
                    terminal.Append(PlatformResources.ForTest);
                    terminal.Append(" '");
                    terminal.Append(artifact.TestName);
                    terminal.Append("': ");
                }

                terminal.AppendLink(artifact.Path, lineNumber: null);
                terminal.AppendLine();
            }
        }

        terminal.AppendLine();

        int totalTests = _testProgressState?.TotalTests ?? 0;
        int totalFailedTests = _testProgressState?.FailedTests ?? 0;
        int totalSkippedTests = _testProgressState?.SkippedTests ?? 0;

        bool notEnoughTests = totalTests < _options.MinimumExpectedTests;
        bool allTestsWereSkipped = totalTests == 0 || totalTests == totalSkippedTests;
        bool anyTestFailed = totalFailedTests > 0;
        bool runFailed = anyTestFailed || notEnoughTests || allTestsWereSkipped || WasCancelled;
        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);

        terminal.Append(PlatformResources.TestRunSummary);
        terminal.Append(' ');

        if (WasCancelled)
        {
            terminal.Append(PlatformResources.Aborted);
        }
        else if (notEnoughTests)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.MinimumExpectedTestsPolicyViolation, totalTests, _options.MinimumExpectedTests));
        }
        else if (allTestsWereSkipped)
        {
            terminal.Append(PlatformResources.ZeroTestsRan);
        }
        else if (anyTestFailed)
        {
            terminal.Append($"{PlatformResources.Failed}!");
        }
        else
        {
            terminal.Append($"{PlatformResources.Passed}!");
        }

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(" - ");
        terminal.ResetColor();
        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal);

        terminal.AppendLine();

        int total = _testProgressState?.TotalTests ?? 0;
        int failed = _testProgressState?.FailedTests ?? 0;
        int passed = _testProgressState?.PassedTests ?? 0;
        int skipped = _testProgressState?.SkippedTests ?? 0;
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizePassed = passed > 0 && _buildErrorsCount == 0 && failed == 0;
        bool colorizeSkipped = skipped > 0 && skipped == total && _buildErrorsCount == 0 && failed == 0;

        string totalText = $"{SingleIndentation}total: {total}";
        string failedText = $"{SingleIndentation}failed: {failed}";
        string passedText = $"{SingleIndentation}succeeded: {passed}";
        string skippedText = $"{SingleIndentation}skipped: {skipped}";
        string durationText = $"{SingleIndentation}duration: ";

        terminal.ResetColor();
        terminal.AppendLine(totalText);
        if (colorizeFailed)
        {
            terminal.SetColor(TerminalColor.DarkRed);
        }

        terminal.AppendLine(failedText);

        if (colorizeFailed)
        {
            terminal.ResetColor();
        }

        if (colorizePassed)
        {
            terminal.SetColor(TerminalColor.DarkGreen);
        }

        terminal.AppendLine(passedText);

        if (colorizePassed)
        {
            terminal.ResetColor();
        }

        if (colorizeSkipped)
        {
            terminal.SetColor(TerminalColor.DarkYellow);
        }

        terminal.AppendLine(skippedText);

        if (colorizeSkipped)
        {
            terminal.ResetColor();
        }

        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();
    }

    internal void TestCompleted(
       string testNodeUid,
       string displayName,
       TestOutcome outcome,
       TimeSpan duration,
       string? informativeMessage,
       string? errorMessage,
       Exception? exception,
       string? expected,
       string? actual,
       string? standardOutput,
       string? errorOutput)
    {
        FlatException[] flatExceptions = ExceptionFlattener.Flatten(errorMessage, exception);
        TestCompleted(
            testNodeUid,
            displayName,
            outcome,
            duration,
            informativeMessage,
            flatExceptions,
            expected,
            actual,
            standardOutput,
            errorOutput);
    }

    private void TestCompleted(
        string testNodeUid,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        string? informativeMessage,
        FlatException[] exceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        if (_testProgressState is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        TestProgressState asm = _testProgressState;

        if (_options.ShowActiveTests)
        {
            asm.TestNodeResultsState?.RemoveRunningTestNode(testNodeUid);
        }

        switch (outcome)
        {
            case TestOutcome.Error:
            case TestOutcome.Timeout:
            case TestOutcome.Canceled:
            case TestOutcome.Fail:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Passed:
                asm.PassedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Skipped:
                asm.SkippedTests++;
                asm.TotalTests++;
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        if (outcome != TestOutcome.Passed || GetShowPassedTests())
        {
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                displayName,
                outcome,
                duration,
                informativeMessage,
                exceptions,
                expected,
                actual,
                standardOutput,
                errorOutput));
        }
    }

    private bool GetShowPassedTests()
    {
        _shouldShowPassedTests ??= _options.ShowPassedTests();
        return _shouldShowPassedTests.Value;
    }

    private void RenderTestCompleted(
        ITerminal terminal,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        string? informativeMessage,
        FlatException[] flatExceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        if (outcome == TestOutcome.Passed && !GetShowPassedTests())
        {
            return;
        }

        TerminalColor color = outcome switch
        {
            TestOutcome.Error or TestOutcome.Fail or TestOutcome.Canceled or TestOutcome.Timeout => TerminalColor.DarkRed,
            TestOutcome.Skipped => TerminalColor.DarkYellow,
            TestOutcome.Passed => TerminalColor.DarkGreen,
            _ => throw new NotSupportedException(),
        };
        string outcomeText = outcome switch
        {
            TestOutcome.Fail or TestOutcome.Error => PlatformResources.FailedLowercase,
            TestOutcome.Skipped => PlatformResources.SkippedLowercase,
            TestOutcome.Canceled or TestOutcome.Timeout => $"{PlatformResources.FailedLowercase} ({PlatformResources.CancelledLowercase})",
            TestOutcome.Passed => PlatformResources.PassedLowercase,
            _ => throw new NotSupportedException(),
        };

        terminal.SetColor(color);
        terminal.Append(outcomeText);
        terminal.ResetColor();
        terminal.Append(' ');
        terminal.Append(NormalizeSpecialCharacters(displayName, true));
        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(' ');
        AppendLongDuration(terminal, duration);

        terminal.AppendLine();

        AppendIndentedLine(terminal, informativeMessage, SingleIndentation);
        FormatErrorMessage(terminal, flatExceptions, outcome, 0);
        FormatExpectedAndActual(terminal, expected, actual);
        FormatStackTrace(terminal, flatExceptions, 0);
        FormatInnerExceptions(terminal, flatExceptions);
        FormatStandardAndErrorOutput(terminal, standardOutput, errorOutput);
    }

    private static void FormatInnerExceptions(ITerminal terminal, FlatException[] exceptions)
    {
        if (exceptions.Length == 0)
        {
            return;
        }

        for (int i = 1; i < exceptions.Length; i++)
        {
            terminal.SetColor(TerminalColor.DarkRed);
            terminal.Append(SingleIndentation);
            terminal.Append("--->");
            FormatErrorMessage(terminal, exceptions, TestOutcome.Error, i);
            FormatStackTrace(terminal, exceptions, i);
        }
    }

    private static void FormatErrorMessage(ITerminal terminal, FlatException[] exceptions, TestOutcome outcome, int index)
    {
        string? firstErrorMessage = GetStringFromIndexOrDefault(exceptions, e => e.ErrorMessage, index);
        string? firstErrorType = GetStringFromIndexOrDefault(exceptions, e => e.ErrorType, index);
        string? firstStackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (RoslynString.IsNullOrWhiteSpace(firstErrorMessage) && RoslynString.IsNullOrWhiteSpace(firstErrorType) && RoslynString.IsNullOrWhiteSpace(firstStackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkRed);

        if (firstStackTrace is null)
        {
            AppendIndentedLine(terminal, firstErrorMessage, SingleIndentation);
        }
        else if (outcome == TestOutcome.Fail)
        {
            // For failed tests, we don't prefix the message with the exception type because it is most likely an assertion specific exception like AssertionFailedException, and we prefer to show that without the exception type to avoid additional noise.
            AppendIndentedLine(terminal, firstErrorMessage, SingleIndentation);
        }
        else
        {
            AppendIndentedLine(terminal, $"{firstErrorType}: {firstErrorMessage}", SingleIndentation);
        }

        terminal.ResetColor();
    }

    private static string? GetStringFromIndexOrDefault(FlatException[] exceptions, Func<FlatException, string?> property, int index) =>
        exceptions.Length >= index + 1 ? property(exceptions[index]) : null;

    private static void FormatExpectedAndActual(ITerminal terminal, string? expected, string? actual)
    {
        if (RoslynString.IsNullOrWhiteSpace(expected) && RoslynString.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkRed);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.Expected);
        AppendIndentedLine(terminal, expected, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.Actual);
        AppendIndentedLine(terminal, actual, DoubleIndentation);
        terminal.ResetColor();
    }

    private static void FormatStackTrace(ITerminal terminal, FlatException[] exceptions, int index)
    {
        string? stackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (RoslynString.IsNullOrWhiteSpace(stackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);

        string[] lines = stackTrace.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            AppendStackFrame(terminal, line);
        }

        terminal.ResetColor();
    }

    private static void FormatStandardAndErrorOutput(ITerminal terminal, string? standardOutput, string? standardError)
    {
        if (RoslynString.IsNullOrWhiteSpace(standardOutput) && RoslynString.IsNullOrWhiteSpace(standardError))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.StandardOutput);
        string? standardOutputWithoutSpecialChars = NormalizeSpecialCharacters(standardOutput, false);
        AppendIndentedLine(terminal, standardOutputWithoutSpecialChars, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.StandardError);
        string? standardErrorWithoutSpecialChars = NormalizeSpecialCharacters(standardError, false);
        AppendIndentedLine(terminal, standardErrorWithoutSpecialChars, DoubleIndentation);
        terminal.ResetColor();
    }

    private void AppendAssemblyLinkTargetFrameworkAndArchitecture(ITerminal terminal)
    {
        terminal.AppendLink(_assembly, lineNumber: null);
        if (_targetFramework != null || _architecture != null)
        {
            terminal.Append(" (");
            if (_targetFramework != null)
            {
                terminal.Append(_targetFramework);
                terminal.Append('|');
            }

            if (_architecture != null)
            {
                terminal.Append(_architecture);
            }

            terminal.Append(')');
        }
    }

    internal /* for testing */ static void AppendStackFrame(ITerminal terminal, string stackTraceLine)
    {
        terminal.Append(DoubleIndentation);
        Match match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
        if (match.Success)
        {
            bool weHaveFilePathAndCodeLine = !RoslynString.IsNullOrWhiteSpace(match.Groups["code"].Value);
            terminal.Append(PlatformResources.StackFrameAt);
            terminal.Append(' ');

            if (weHaveFilePathAndCodeLine)
            {
                terminal.Append(match.Groups["code"].Value);
            }
            else
            {
                terminal.Append(match.Groups["code1"].Value);
            }

            if (weHaveFilePathAndCodeLine)
            {
                terminal.Append(' ');
                terminal.Append(PlatformResources.StackFrameIn);
                terminal.Append(' ');
                if (!RoslynString.IsNullOrWhiteSpace(match.Groups["file"].Value))
                {
                    int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
                    terminal.AppendLink(match.Groups["file"].Value, line);

                    // AppendLink finishes by resetting color
                    terminal.SetColor(TerminalColor.DarkGray);
                }
            }

            terminal.AppendLine();
        }
        else
        {
            terminal.AppendLine(stackTraceLine);
        }
    }

    private static void AppendIndentedLine(ITerminal terminal, string? message, string indent)
    {
        if (RoslynString.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!message.Contains('\n'))
        {
            terminal.Append(indent);
            terminal.AppendLine(message);
            return;
        }

        string[] lines = message.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            // Here we could check if the messages are longer than then line, and reflow them so a long line is split into multiple
            // and prepended by the respective indentation.
            // But this does not play nicely with ANSI escape codes. And if you
            // run in narrow terminal and then widen it the text does not reflow correctly. And you also have harder time copying
            // values when the assertion message is longer.
            terminal.Append(indent);
            terminal.Append(line);
            terminal.AppendLine();
        }
    }

    internal void AssemblyRunCompleted()
    {
        TestProgressState assemblyRun = GetOrAddAssemblyRun();
        assemblyRun.Stopwatch.Stop();

        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);
    }

    [return: NotNullIfNotNull(nameof(text))]
    private static string? NormalizeSpecialCharacters(string? text, bool normalizeWhitespaceCharacters)
    {
        string? normalized = text?
            .Replace('\0', '\x2400') // NULL → ␀
            .Replace('\x0001', '\x2401') // START OF HEADING → ␁
            .Replace('\x0002', '\x2402') // START OF TEXT → ␂
            .Replace('\x0003', '\x2403') // END OF TEXT → ␃
            .Replace('\x0004', '\x2404') // END OF TRANSMISSION → ␄
            .Replace('\x0005', '\x2405') // ENQUIRY → ␅
            .Replace('\x0006', '\x2406') // ACKNOWLEDGE → ␆
            .Replace('\x0007', '\x2407') // BELL → ␇
            .Replace('\x0008', '\x2408') // BACKSPACE → ␈
            .Replace('\x000B', '\x240B') // VERTICAL TAB → ␋
            .Replace('\x000C', '\x240C') // FORM FEED → ␌
            .Replace('\x000E', '\x240E') // SHIFT OUT → ␎
            .Replace('\x000F', '\x240F') // SHIFT IN → ␏
            .Replace('\x0010', '\x2410') // DATA LINK ESCAPE → ␐
            .Replace('\x0011', '\x2411') // DEVICE CONTROL ONE → ␑
            .Replace('\x0012', '\x2412') // DEVICE CONTROL TWO → ␒
            .Replace('\x0013', '\x2413') // DEVICE CONTROL THREE → ␓
            .Replace('\x0014', '\x2414') // DEVICE CONTROL FOUR → ␔
            .Replace('\x0015', '\x2415') // NEGATIVE ACKNOWLEDGE → ␕
            .Replace('\x0016', '\x2416') // SYNCHRONOUS IDLE → ␖
            .Replace('\x0017', '\x2417') // END OF TRANSMISSION BLOCK → ␗
            .Replace('\x0018', '\x2418') // CANCEL → ␘
            .Replace('\x0019', '\x2419') // END OF MEDIUM → ␙
            .Replace('\x001A', '\x241A') // SUBSTITUTE → ␚
            .Replace('\x001B', '\x241B') // ESCAPE → ␛
            .Replace('\x001C', '\x241C') // FILE SEPARATOR → ␜
            .Replace('\x001D', '\x241D') // GROUP SEPARATOR → ␝
            .Replace('\x001E', '\x241E') // RECORD SEPARATOR → ␞
            .Replace('\x001F', '\x241F'); // UNIT SEPARATOR → ␟

        if (normalizeWhitespaceCharacters)
        {
            normalized = normalized?
                .Replace('\t', '\x2409') // TAB → ␉
                .Replace('\n', '\x240A') // LINE FEED → ␊
                .Replace('\r', '\x240D'); // CARRIAGE RETURN → ␍
        }

        return normalized;
    }

    /// <summary>
    /// Appends a long duration in human readable format such as 1h 23m 500ms.
    /// </summary>
    private static void AppendLongDuration(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true, bool colorize = true)
    {
        if (colorize)
        {
            terminal.SetColor(TerminalColor.DarkGray);
        }

        HumanReadableDurationFormatter.Append(terminal, duration, wrapInParentheses);

        if (colorize)
        {
            terminal.ResetColor();
        }
    }

    public void Dispose() => _terminalWithProgress.Dispose();

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

    internal void WriteErrorMessage(string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun();
        asm.AddError(text);

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.DarkRed);
            if (padding == null)
            {
                terminal.AppendLine(text);
            }
            else
            {
                AppendIndentedLine(terminal, text, new string(' ', padding.Value));
            }

            terminal.ResetColor();
        });
    }

    internal void WriteWarningMessage(string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun();
        asm.AddWarning(text);
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.DarkYellow);
            if (padding == null)
            {
                terminal.AppendLine(text);
            }
            else
            {
                AppendIndentedLine(terminal, text, new string(' ', padding.Value));
            }

            terminal.ResetColor();
        });
    }

    internal void WriteErrorMessage(Exception exception)
        => WriteErrorMessage(exception.ToString(), padding: null);

    public void WriteMessage(string text, SystemConsoleColor? color = null, int? padding = null)
    {
        if (color != null)
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                terminal.SetColor(ToTerminalColor(color.ConsoleColor));
                if (padding == null)
                {
                    terminal.AppendLine(text);
                }
                else
                {
                    AppendIndentedLine(terminal, text, new string(' ', padding.Value));
                }

                terminal.ResetColor();
            });
        }
        else
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                if (padding == null)
                {
                    terminal.AppendLine(text);
                }
                else
                {
                    AppendIndentedLine(terminal, text, new string(' ', padding.Value));
                }
            });
        }
    }

    internal void TestDiscovered(string displayName)
    {
        if (_testProgressState is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        TestProgressState asm = _testProgressState;
        if (_isDiscovery)
        {
            // TODO: add mode for discovered tests to the progress bar, to get rid of the hack here that allows updating the
            // progress, but also breaks the total counts if not done only in discovery.
            asm.PassedTests++;
            asm.TotalTests++;
        }

        asm.DiscoveredTestDisplayNames.Add(NormalizeSpecialCharacters(displayName, true));

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    public void AppendTestDiscoverySummary(ITerminal terminal)
    {
        TestProgressState? assembly = _testProgressState;
        terminal.AppendLine();

        int totalTests = assembly?.TotalTests ?? 0;
        bool runFailed = WasCancelled;

        if (assembly is not null)
        {
            foreach (string displayName in assembly.DiscoveredTestDisplayNames)
            {
                terminal.Append(SingleIndentation);
                terminal.AppendLine(displayName);
            }
        }

        terminal.AppendLine();

        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);
        terminal.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.TestDiscoverySummarySingular, totalTests));

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(" - ");
        terminal.ResetColor();
        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal);

        terminal.ResetColor();
        terminal.AppendLine();

        if (WasCancelled)
        {
            terminal.Append(PlatformResources.Aborted);
            terminal.AppendLine();
        }

        string durationText = $"{SingleIndentation}duration: ";
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;
        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();
    }

    private static TerminalColor ToTerminalColor(ConsoleColor consoleColor)
        => consoleColor switch
        {
            ConsoleColor.Black => TerminalColor.Black,
            ConsoleColor.DarkBlue => TerminalColor.DarkBlue,
            ConsoleColor.DarkGreen => TerminalColor.DarkGreen,
            ConsoleColor.DarkCyan => TerminalColor.DarkCyan,
            ConsoleColor.DarkRed => TerminalColor.DarkRed,
            ConsoleColor.DarkMagenta => TerminalColor.DarkMagenta,
            ConsoleColor.DarkYellow => TerminalColor.DarkYellow,
            ConsoleColor.DarkGray => TerminalColor.DarkGray,
            ConsoleColor.Gray => TerminalColor.Gray,
            ConsoleColor.Blue => TerminalColor.Blue,
            ConsoleColor.Green => TerminalColor.Green,
            ConsoleColor.Cyan => TerminalColor.Cyan,
            ConsoleColor.Red => TerminalColor.Red,
            ConsoleColor.Magenta => TerminalColor.Magenta,
            ConsoleColor.Yellow => TerminalColor.Yellow,
            ConsoleColor.White => TerminalColor.White,
            _ => TerminalColor.Default,
        };

    public void TestInProgress(
        string testNodeUid,
        string displayName)
    {
        if (_testProgressState is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        TestProgressState asm = _testProgressState;
        if (_options.ShowActiveTests)
        {
            asm.TestNodeResultsState ??= new(Interlocked.Increment(ref _counter));
            asm.TestNodeResultsState.AddRunningTestNode(
                Interlocked.Increment(ref _counter), testNodeUid, NormalizeSpecialCharacters(displayName, true), CreateStopwatch());
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }
}
