// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;
#if !NET7_0_OR_GREATER
using System.Reflection;

#endif

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice.Console;

/// <summary>
/// Console logger that outputs test progress and is capable of writing ANSI or non-ANSI output via the given terminal.
/// </summary>
internal partial class ConsoleLogger : IDisposable
{
    private static readonly string[] NewLineStrings = { "\r\n", "\n" };

    internal const string SingleIndentation = "  ";

    internal const string DoubleIndentation = $"{SingleIndentation}{SingleIndentation}";

    internal const string TripleIndentation = $"{SingleIndentation}{SingleIndentation}{SingleIndentation}";

    internal Func<StopwatchAbstraction> CreateStopwatch { get; set; } = SystemStopwatch.StartNew;

    /// <summary>
    /// The two directory separator characters to be passed to methods like <see cref="string.IndexOfAny(char[])"/>.
    /// </summary>
    private readonly Dictionary<string, TestModule> _assemblies = new();
    private readonly List<TestRunArtifact> _artifacts = new();

    private readonly ConsoleLoggerOptions _options;

    private readonly ConsoleWithProgress _consoleWithProgress;

    private readonly uint? _originalConsoleMode;

    private DateTimeOffset? _testExecutionStartTime;

    private DateTimeOffset? _testExecutionEndTime;

    private int _buildErrorsCount;

    private bool _wasCancelled;

#if NET7_0_OR_GREATER
    [GeneratedRegex(@$"^   at (?<code>.+) in (?<file>.+):line (?<line>\d+)$", RegexOptions.ExplicitCapture, 1000)]
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

        s_regex = new Regex(@$"^   {atString} (?<code>.+) {inPattern}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture, matchTimeout: TimeSpan.FromSeconds(1));
        return s_regex;
    }
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogger"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    internal ConsoleLogger(IConsole console, ConsoleLoggerOptions options)
    {
        _options = options;

        Func<bool?> showProgress = _options.ShowProgress;
        ConsoleWithProgress consoleWithProgress;

        // When not writing to ANSI we write the progress to screen and leave it there so we don't want to write it more often than every few seconds.
        int nonAnsiUpdateCadenceInMs = 3_000;
        // When writing to ANSI we update the progress in place and it should look responsive so we update every few ms, roughly 30 FPS.
        int ansiUpdateCadenceInMs = 33;
        if (!_options.UseAnsi)
        {
            consoleWithProgress = new ConsoleWithProgress(new NonAnsiTerminal(console), showProgress, updateEvery: nonAnsiUpdateCadenceInMs);
        }
        else
        {
            // Autodetect.
            (bool consoleAcceptsAnsiCodes, bool _, uint? originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
            _originalConsoleMode = originalConsoleMode;
            consoleWithProgress = consoleAcceptsAnsiCodes
                ? new ConsoleWithProgress(new AnsiTerminal(console, _options.BaseDirectory), showProgress, updateEvery: ansiUpdateCadenceInMs)
                : new ConsoleWithProgress(new NonAnsiTerminal(console), showProgress, updateEvery: nonAnsiUpdateCadenceInMs);
        }

        _consoleWithProgress = consoleWithProgress;
    }

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount)
    {
        _testExecutionStartTime = testStartTime;
        _consoleWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture)
    {
        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _consoleWithProgress.WriteToTerminal(terminal =>
            {
                terminal.Append(PlatformResources.RunningTestsFrom);
                terminal.Append(' ');
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
            });
        }

        GetOrAddAssemblyRun(assembly, targetFramework, architecture);
    }

    private TestModule GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture)
    {
        string key = $"{assembly}|{targetFramework}|{architecture}";
        if (_assemblies.TryGetValue(key, out TestModule? asm))
        {
            return asm;
        }

        StopwatchAbstraction sw = CreateStopwatch();
        var progress = new TestProgressState(0, 0, 0, Path.GetFileName(assembly), targetFramework, architecture, sw, detail: null);
        int slotIndex = _consoleWithProgress.AddWorker(progress);

        var assemblyRun = new TestModule(slotIndex, assembly, targetFramework, architecture, sw);
        _assemblies.Add(key, assemblyRun);

        return assemblyRun;
    }

    internal void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testExecutionEndTime = endTime;
        _consoleWithProgress.StopShowingProgress();

        _consoleWithProgress.WriteToTerminal(AppendTestRunSummary);

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

        terminal.AppendLine();
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
            TestModule testModule = _assemblies.Values.Single();
            terminal.SetColor(TerminalColor.Gray);
            terminal.Append(" - ");
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, testModule.Assembly, testModule.TargetFramework, testModule.Architecture);
        }

        terminal.AppendLine();

        if (_options.ShowAssembly && _assemblies.Count > 1)
        {
            foreach (TestModule assemblyRun in _assemblies.Values)
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
        LoggerOutcome outcome,
        TimeSpan duration,
        string? errorMessage,
        string? errorStackTrace,
        string? expected,
        string? actual)
    {
        TestModule asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}"];

        switch (outcome)
        {
            case LoggerOutcome.Error:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case LoggerOutcome.Fail:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case LoggerOutcome.Passed:
                asm.PassedTests++;
                asm.TotalTests++;
                break;
            case LoggerOutcome.Skipped:
                asm.SkippedTests++;
                asm.TotalTests++;
                break;
            case LoggerOutcome.Timeout:
                asm.TimedOutTests++;
                asm.TotalTests++;
                break;
            case LoggerOutcome.Cancelled:
                asm.CancelledTests++;
                asm.TotalTests++;
                break;
        }

        var update = new TestProgressState(asm.PassedTests, asm.FailedTests, asm.SkippedTests, Path.GetFileName(asm.Assembly), asm.TargetFramework, asm.Architecture, asm.Stopwatch, null);
        _consoleWithProgress.UpdateWorker(asm.SlotIndex, update);
        if (outcome != LoggerOutcome.Passed || _options.ShowPassedTests)
        {
            _consoleWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
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
        LoggerOutcome outcome,
        TimeSpan duration,
        string? errorMessage,
        string? errorStackTrace,
        string? expected,
        string? actual)
    {
        if (outcome == LoggerOutcome.Passed && !_options.ShowPassedTests)
        {
            return;
        }

        TerminalColor color = outcome switch
        {
            LoggerOutcome.Error or LoggerOutcome.Fail or LoggerOutcome.Cancelled or LoggerOutcome.Timeout => TerminalColor.DarkRed,
            LoggerOutcome.Skipped => TerminalColor.DarkYellow,
            LoggerOutcome.Passed => TerminalColor.DarkGreen,
            _ => throw new NotSupportedException(),
        };
        string outcomeText = outcome switch
        {
            LoggerOutcome.Fail or LoggerOutcome.Error => PlatformResources.FailedLowercase,
            LoggerOutcome.Skipped => PlatformResources.SkippedLowercase,
            LoggerOutcome.Cancelled or LoggerOutcome.Timeout => $"{PlatformResources.FailedLowercase} ({PlatformResources.CancelledLowercase})",
            LoggerOutcome.Passed => PlatformResources.PassedLowercase,
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
        if (!RoslynString.IsNullOrWhiteSpace(architecture) && !RoslynString.IsNullOrWhiteSpace(targetFramework))
        {
            terminal.Append(" (");
            terminal.Append(targetFramework);
            terminal.Append('|');
            terminal.Append(architecture);
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

    private static void AppendStackFrame(ITerminal terminal, string stackTraceLine)
    {
        terminal.Append(DoubleIndentation);
        Match match = GetFrameRegex().Match(stackTraceLine);
        if (match.Success)
        {
            terminal.SetColor(TerminalColor.Gray);
            terminal.Append(PlatformResources.StackFrameIn);
            terminal.Append(' ');
            terminal.ResetColor();
            terminal.SetColor(TerminalColor.Red);
            terminal.Append(match.Groups["code"].Value);
            terminal.SetColor(TerminalColor.Gray);
            terminal.Append(' ');
            terminal.Append(PlatformResources.StackFrameIn);
            terminal.Append(' ');
            int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
            terminal.AppendLink(match.Groups["file"].Value, line);
            terminal.AppendLine();
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
        AppendIndentedMessage(terminal, expected, DoubleIndentation);
        terminal.AppendLine();
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.Actual);
        AppendIndentedMessage(terminal, actual, DoubleIndentation);
        terminal.ResetColor();
        terminal.AppendLine();
    }

    private static void FormatErrorMessage(ITerminal terminal, string? errorMessage)
    {
        if (RoslynString.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        terminal.SetColor(TerminalColor.Red);
        AppendIndentedMessage(terminal, errorMessage, SingleIndentation);
        terminal.ResetColor();
        terminal.AppendLine();
    }

    private static void AppendIndentedMessage(ITerminal terminal, string? message, string indent)
    {
        if (RoslynString.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!message.Contains('\n'))
        {
            terminal.Append(indent);
            terminal.Append(message);
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
        TestModule assemblyRun = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        assemblyRun.Stopwatch.Stop();

        _consoleWithProgress.RemoveWorker(assemblyRun.SlotIndex);

        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _consoleWithProgress.WriteToTerminal(terminal => AppendAssemblySummary(assemblyRun, terminal));
        }
    }

    private static void AppendAssemblySummary(TestModule assemblyRun, ITerminal terminal)
    {
        int failedTests = assemblyRun.FailedTests + assemblyRun.CancelledTests + assemblyRun.TimedOutTests;
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

        if (wrapInParentheses)
        {
            terminal.Append('(');
        }

        bool hasParentValue = false;

        if (duration.Days > 0)
        {
            terminal.Append($"{duration.Days}d");
            hasParentValue = true;
        }

        if (duration.Hours > 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Hours.ToString(CultureInfo.InvariantCulture))}h");
            hasParentValue = true;
        }

        if (duration.Minutes > 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Minutes.ToString(CultureInfo.InvariantCulture))}m");
            hasParentValue = true;
        }

        if (duration.Seconds > 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Seconds.ToString(CultureInfo.InvariantCulture))}s");
            hasParentValue = true;
        }

        if (duration.Milliseconds >= 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Milliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') : duration.Milliseconds.ToString(CultureInfo.InvariantCulture))}ms");
        }

        if (wrapInParentheses)
        {
            terminal.Append(')');
        }

        if (colorize)
        {
            terminal.ResetColor();
        }
    }

    public void Dispose() => _consoleWithProgress.Dispose();

    public void ArtifactAdded(bool outOfProcess, string? assembly, string? targetFramework, string? architecture, string? testName, string path)
        => _artifacts.Add(new TestRunArtifact(outOfProcess, assembly, targetFramework, architecture, testName, path));

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    internal void StartCancelling()
    {
        _wasCancelled = true;
        _consoleWithProgress.WriteToTerminal(terminal =>
        {
            terminal.AppendLine();
            terminal.AppendLine(PlatformResources.CancellingTestSession);
            terminal.AppendLine();
        });
    }

    internal void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, string text)
    {
        TestModule asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        asm.AddError(text);

        _consoleWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Red);
            terminal.AppendLine(text);
            terminal.ResetColor();
        });
    }

    internal void WriteWarningMessage(string assembly, string? targetFramework, string? architecture, string text)
    {
        TestModule asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        asm.AddWarning(text);
        _consoleWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Yellow);
            terminal.AppendLine(text);
            terminal.ResetColor();
        });
    }

    internal void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, Exception exception)
        => WriteErrorMessage(assembly, targetFramework, architecture, exception.ToString());

    internal void WriteMessage(string text)
        => _consoleWithProgress.WriteToTerminal(terminal => terminal.AppendLine(text));
}
