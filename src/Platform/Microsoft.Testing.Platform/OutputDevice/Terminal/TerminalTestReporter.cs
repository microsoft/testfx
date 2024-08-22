// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
#endif

using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal test reporter that outputs test progress and is capable of writing ANSI or non-ANSI output via the given terminal.
/// </summary>
internal sealed partial class TerminalTestReporter : IDisposable
{
    /// <summary>
    /// The two directory separator characters to be passed to methods like <see cref="string.IndexOfAny(char[])"/>.
    /// </summary>
    private static readonly string[] NewLineStrings = { "\r\n", "\n" };

    internal const string SingleIndentation = "  ";

    internal const string DoubleIndentation = $"{SingleIndentation}{SingleIndentation}";

    internal const string TripleIndentation = $"{SingleIndentation}{SingleIndentation}{SingleIndentation}";

    internal Func<IStopwatch> CreateStopwatch { get; set; } = SystemStopwatch.StartNew;

    private readonly Dictionary<string, TestProgressState> _assemblies = new();

    private readonly List<TestRunArtifact> _artifacts = new();

    private readonly TerminalTestReporterOptions _options;

    private readonly TestProgressStateAwareTerminal _terminalWithProgress;

    private readonly uint? _originalConsoleMode;

    private DateTimeOffset? _testExecutionStartTime;

    private DateTimeOffset? _testExecutionEndTime;

    private int _buildErrorsCount;

    private bool _wasCancelled;

#if NET7_0_OR_GREATER
    [GeneratedRegex(@$"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex GetFrameRegex();
#else
    private static Regex? s_regex;

    [MemberNotNull(nameof(s_regex))]
    private static Regex GetFrameRegex()
    {
        if (s_regex != null)
        {
            return s_regex;
        }

        string atResourceName = "Word_At";
        string inResourceName = "StackTrace_InFileLineNumber";

        string? atString = null;
        string? inString = null;

        // Grab words from localized resource, in case the stack trace is localized.
        try
        {
            // Get these resources: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/Resources/Strings.resx
#pragma warning disable RS0030 // Do not use banned APIs
            MethodInfo? getResourceStringMethod = typeof(Environment).GetMethod("GetResourceString", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(string)], null);
#pragma warning restore RS0030 // Do not use banned APIs
            if (getResourceStringMethod is not null)
            {
                // <value>at</value>
                atString = (string?)getResourceStringMethod.Invoke(null, [atResourceName]);

                // <value>in {0}:line {1}</value>
                inString = (string?)getResourceStringMethod.Invoke(null, [inResourceName]);
            }
        }
        catch
        {
            // If we fail, populate the defaults below.
        }

        atString = atString == null || atString == atResourceName ? "at" : atString;
        inString = inString == null || inString == inResourceName ? "in {0}:line {1}" : inString;

        string inPattern = string.Format(CultureInfo.InvariantCulture, inString, "(?<file>.+)", @"(?<line>\d+)");

        s_regex = new Regex(@$"^   {atString} ((?<code>.+) {inPattern}|(?<code1>.+))$", RegexOptions.Compiled | RegexOptions.ExplicitCapture, matchTimeout: TimeSpan.FromSeconds(1));
        return s_regex;
    }
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalTestReporter"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    internal TerminalTestReporter(IConsole console, TerminalTestReporterOptions options)
    {
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
            // Autodetect.
            (bool consoleAcceptsAnsiCodes, bool _, uint? originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
            _originalConsoleMode = originalConsoleMode;
            terminalWithProgress = consoleAcceptsAnsiCodes || _options.ForceAnsi is true
                ? new TestProgressStateAwareTerminal(new AnsiTerminal(console, _options.BaseDirectory), showProgress, writeProgressImmediatelyAfterOutput: true, updateEvery: ansiUpdateCadenceInMs)
                : new TestProgressStateAwareTerminal(new NonAnsiTerminal(console), showProgress, writeProgressImmediatelyAfterOutput: false, updateEvery: nonAnsiUpdateCadenceInMs);
        }

        _terminalWithProgress = terminalWithProgress;
    }

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount)
    {
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture)
    {
        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                terminal.Append(PlatformResources.RunningTestsFrom);
                terminal.Append(' ');
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
                terminal.AppendLine();
            });
        }

        GetOrAddAssemblyRun(assembly, targetFramework, architecture);
    }

    private TestProgressState GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture)
    {
        string key = $"{assembly}|{targetFramework}|{architecture}";
        if (_assemblies.TryGetValue(key, out TestProgressState? asm))
        {
            return asm;
        }

        IStopwatch sw = CreateStopwatch();
        var assemblyRun = new TestProgressState(assembly, targetFramework, architecture, sw);
        int slotIndex = _terminalWithProgress.AddWorker(assemblyRun);
        assemblyRun.SlotIndex = slotIndex;

        _assemblies.Add(key, assemblyRun);

        return assemblyRun;
    }

    internal void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        _terminalWithProgress.WriteToTerminal(AppendTestRunSummary);

        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
        _assemblies.Clear();
        _buildErrorsCount = 0;
        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }

    private void AppendTestRunSummary(ITerminal terminal)
    {
        terminal.AppendLine();

        IEnumerable<IGrouping<bool, TestRunArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);
        if (artifactGroups.Any())
        {
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

        int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
        int totalFailedTests = _assemblies.Values.Sum(a => a.FailedTests);
        int totalSkippedTests = _assemblies.Values.Sum(a => a.SkippedTests);

        bool notEnoughTests = totalTests < _options.MinimumExpectedTests;
        bool allTestsWereSkipped = totalTests == 0 || totalTests == totalSkippedTests;
        bool anyTestFailed = totalFailedTests > 0;
        bool runFailed = anyTestFailed || notEnoughTests || allTestsWereSkipped || _wasCancelled;
        terminal.SetColor(runFailed ? TerminalColor.Red : TerminalColor.Green);

        terminal.Append(PlatformResources.TestRunSummary);
        terminal.Append(' ');

        if (_wasCancelled)
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
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", PlatformResources.Failed));
        }
        else
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", PlatformResources.Passed));
        }

        if (!_options.ShowAssembly && _assemblies.Count == 1)
        {
            TestProgressState testProgressState = _assemblies.Values.Single();
            terminal.SetColor(TerminalColor.Gray);
            terminal.Append(" - ");
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, testProgressState.Assembly, testProgressState.TargetFramework, testProgressState.Architecture);
        }

        terminal.AppendLine();

        if (_options.ShowAssembly && _assemblies.Count > 1)
        {
            foreach (TestProgressState assemblyRun in _assemblies.Values)
            {
                terminal.Append(SingleIndentation);
                AppendAssemblySummary(assemblyRun, terminal);
            }

            terminal.AppendLine();
        }

        int total = _assemblies.Values.Sum(t => t.TotalTests);
        int failed = _assemblies.Values.Sum(t => t.FailedTests);
        int passed = _assemblies.Values.Sum(t => t.PassedTests);
        int skipped = _assemblies.Values.Sum(t => t.SkippedTests);
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
            terminal.SetColor(TerminalColor.Red);
        }

        terminal.AppendLine(failedText);

        if (colorizeFailed)
        {
            terminal.ResetColor();
        }

        if (colorizePassed)
        {
            terminal.SetColor(TerminalColor.Green);
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

    /// <summary>
    /// Print a build result summary to the output.
    /// </summary>
    private static void AppendAssemblyResult(ITerminal terminal, bool succeeded, int countErrors, int countWarnings)
    {
        if (!succeeded)
        {
            terminal.SetColor(TerminalColor.DarkRed);
            // If the build failed, we print one of three red strings.
            string text = (countErrors > 0, countWarnings > 0) switch
            {
                (true, true) => string.Format(CultureInfo.CurrentCulture, PlatformResources.FailedWithErrorsAndWarnings, countErrors, countWarnings),
                (true, _) => string.Format(CultureInfo.CurrentCulture, PlatformResources.FailedWithErrors, countErrors),
                (false, true) => string.Format(CultureInfo.CurrentCulture, PlatformResources.FailedWithWarnings, countWarnings),
                _ => PlatformResources.FailedLowercase,
            };
            terminal.Append(text);
            terminal.ResetColor();
        }
        else if (countWarnings > 0)
        {
            terminal.SetColor(TerminalColor.DarkYellow);
            terminal.Append($"succeeded with {countWarnings} warning(s)");
            terminal.ResetColor();
        }
        else
        {
            terminal.SetColor(TerminalColor.DarkGreen);
            terminal.Append(PlatformResources.PassedLowercase);
            terminal.ResetColor();
        }
    }

    internal void TestCompleted(
        string assembly,
        string? targetFramework,
        string? architecture,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        string? errorMessage,
        string? errorStackTrace,
        string? expected,
        string? actual)
    {
        TestProgressState asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}"];

        switch (outcome)
        {
            case TestOutcome.Error:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
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
            case TestOutcome.Timeout:
                asm.TimedOutTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Canceled:
                asm.CanceledTests++;
                asm.TotalTests++;
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        if (outcome != TestOutcome.Passed || _options.ShowPassedTests)
        {
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                assembly,
                targetFramework,
                architecture,
                displayName,
                outcome,
                duration,
                errorMessage,
                errorStackTrace,
                expected,
                actual));
        }
    }

    internal /* for testing */ void RenderTestCompleted(
        ITerminal terminal,
        string assembly,
        string? targetFramework,
        string? architecture,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        string? errorMessage,
        string? errorStackTrace,
        string? expected,
        string? actual)
    {
        if (outcome == TestOutcome.Passed && !_options.ShowPassedTests)
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
        terminal.Append(displayName);
        terminal.SetColor(TerminalColor.Gray);
        terminal.Append(' ');
        AppendLongDuration(terminal, duration);
        if (_options.ShowAssembly)
        {
            terminal.AppendLine();
            terminal.Append(SingleIndentation);
            terminal.Append(PlatformResources.FromFile);
            terminal.Append(' ');
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
        }

        terminal.AppendLine();

        FormatErrorMessage(terminal, errorMessage);
        FormatExpectedAndActual(terminal, expected, actual);
        FormatStackTrace(terminal, errorStackTrace);
    }

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(ITerminal terminal, string assembly, string? targetFramework, string? architecture)
    {
        terminal.AppendLink(assembly, lineNumber: null);
        if (targetFramework != null || architecture != null)
        {
            terminal.Append(" (");
            if (targetFramework != null)
            {
                terminal.Append(targetFramework);
                terminal.Append('|');
            }

            if (architecture != null)
            {
                terminal.Append(architecture);
            }

            terminal.Append(')');
        }
    }

    private static void FormatStackTrace(ITerminal terminal, string? errorStackTrace)
    {
        if (RoslynString.IsNullOrWhiteSpace(errorStackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkRed);
        terminal.Append(SingleIndentation);
        terminal.Append(PlatformResources.StackTrace);
        terminal.AppendLine(":");

        if (!errorStackTrace.Contains('\n'))
        {
            AppendStackFrame(terminal, errorStackTrace);
            return;
        }

        string[] lines = errorStackTrace.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            AppendStackFrame(terminal, line);
        }

        terminal.ResetColor();
    }

    internal /*for testing */ static void AppendStackFrame(ITerminal terminal, string stackTraceLine)
    {
        terminal.Append(DoubleIndentation);
        Match match = GetFrameRegex().Match(stackTraceLine);
        if (match.Success)
        {
            bool weHaveFilePathAndCodeLine = !RoslynString.IsNullOrWhiteSpace(match.Groups["code"].Value);
            terminal.SetColor(TerminalColor.Gray);
            terminal.Append(PlatformResources.StackFrameAt);
            terminal.Append(' ');
            terminal.ResetColor();
            terminal.SetColor(TerminalColor.Red);
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
                terminal.SetColor(TerminalColor.Gray);
                terminal.Append(' ');
                terminal.Append(PlatformResources.StackFrameIn);
                terminal.Append(' ');
                if (!RoslynString.IsNullOrWhiteSpace(match.Groups["file"].Value))
                {
                    int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
                    terminal.AppendLink(match.Groups["file"].Value, line);
                }
            }

            terminal.AppendLine();
        }
        else
        {
            terminal.AppendLine(stackTraceLine);
        }
    }

    private static void FormatExpectedAndActual(ITerminal terminal, string? expected, string? actual)
    {
        if (RoslynString.IsNullOrWhiteSpace(expected) && RoslynString.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        terminal.SetColor(TerminalColor.Red);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.Expected);
        AppendIndentedLine(terminal, expected, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.Actual);
        AppendIndentedLine(terminal, actual, DoubleIndentation);
        terminal.ResetColor();
    }

    private static void FormatErrorMessage(ITerminal terminal, string? errorMessage)
    {
        if (RoslynString.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        terminal.SetColor(TerminalColor.Red);
        AppendIndentedLine(terminal, errorMessage, SingleIndentation);
        terminal.ResetColor();
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

    internal void AssemblyRunCompleted(string assembly, string? targetFramework, string? architecture)
    {
        TestProgressState assemblyRun = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        assemblyRun.Stopwatch.Stop();

        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);

        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal => AppendAssemblySummary(assemblyRun, terminal));
        }
    }

    private static void AppendAssemblySummary(TestProgressState assemblyRun, ITerminal terminal)
    {
        int failedTests = assemblyRun.FailedTests + assemblyRun.CanceledTests + assemblyRun.TimedOutTests;
        int warnings = 0;

        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture);
        terminal.Append(' ');
        AppendAssemblyResult(terminal, assemblyRun.FailedTests == 0, failedTests, warnings);
        terminal.Append(' ');
        AppendLongDuration(terminal, assemblyRun.Stopwatch.Elapsed);
    }

    /// <summary>
    /// Appends a long duration in human readable format such as 1h 23m 500ms.
    /// </summary>
    private static void AppendLongDuration(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true, bool colorize = true)
    {
        if (colorize)
        {
            terminal.SetColor(TerminalColor.Gray);
        }

        HumanReadableDurationFormatter.Append(terminal, duration, wrapInParentheses);

        if (colorize)
        {
            terminal.ResetColor();
        }
    }

    public void Dispose() => _terminalWithProgress.Dispose();

    public void ArtifactAdded(bool outOfProcess, string? assembly, string? targetFramework, string? architecture, string? testName, string path)
        => _artifacts.Add(new TestRunArtifact(outOfProcess, assembly, targetFramework, architecture, testName, path));

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    internal void StartCancelling()
    {
        _wasCancelled = true;
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.AppendLine();
            terminal.AppendLine(PlatformResources.CancellingTestSession);
            terminal.AppendLine();
        });
    }

    internal void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        asm.AddError(text);

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Red);
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

    internal void WriteWarningMessage(string assembly, string? targetFramework, string? architecture, string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        asm.AddWarning(text);
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Yellow);
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

    internal void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, Exception exception)
        => WriteErrorMessage(assembly, targetFramework, architecture, exception.ToString(), padding: null);

    internal void WriteMessage(string text, SystemConsoleColor? color = null, int? padding = null)
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
            ConsoleColor.Gray => TerminalColor.Gray,
            ConsoleColor.DarkGray => TerminalColor.Gray,
            ConsoleColor.Blue => TerminalColor.Blue,
            ConsoleColor.Green => TerminalColor.Green,
            ConsoleColor.Cyan => TerminalColor.Cyan,
            ConsoleColor.Red => TerminalColor.Red,
            ConsoleColor.Magenta => TerminalColor.Magenta,
            ConsoleColor.Yellow => TerminalColor.Yellow,
            ConsoleColor.White => TerminalColor.White,
            _ => TerminalColor.Default,
        };
}
