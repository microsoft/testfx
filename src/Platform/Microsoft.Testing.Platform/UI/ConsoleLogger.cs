﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

namespace Microsoft.Testing.Platform.UI;

/// <summary>
/// A logger which updates the console output "live" during the build.
/// </summary>
/// <remarks>
/// Uses ANSI/VT100 control codes to erase and overwrite lines as the build is progressing.
/// </remarks>
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
    private readonly Dictionary<string, AssemblyRun> _assemblies = new();
    private readonly List<LoggerArtifact> _artifacts = new();

    private readonly ConsoleLoggerOptions _options;

    private readonly ConsoleWithProgress _consoleWithProgress;

    private readonly uint? _originalConsoleMode;

    /// <summary>
    /// Time of the oldest observed test target start.
    /// </summary>
    private DateTimeOffset? _testStartTime;

    /// <summary>
    /// Time of the most recently observed test target finished.
    /// </summary>
    private DateTimeOffset? _testEndTime;

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

        bool showProgress = _options.ShowProgress;
        ConsoleWithProgress consoleWithProgress;
        if (_options.UseAnsi == YesNoAuto.No)
        {
            consoleWithProgress = new ConsoleWithProgress(new NonAnsiTerminal(console), showProgress, updateEvery: 3_000);
        }
        else if (_options.UseAnsi == YesNoAuto.Yes)
        {
            consoleWithProgress = new ConsoleWithProgress(new AnsiTerminal(console, _options.BaseDirectory), showProgress, updateEvery: 33);
        }
        else
        {
            (bool consoleAcceptsAnsiCodes, bool _, uint? originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
            _originalConsoleMode = originalConsoleMode;
            consoleWithProgress = consoleAcceptsAnsiCodes
                ? new ConsoleWithProgress(new AnsiTerminal(console, _options.BaseDirectory), showProgress, updateEvery: 33)
                : new ConsoleWithProgress(new NonAnsiTerminal(console), showProgress, updateEvery: 3_000);
        }

        _consoleWithProgress = consoleWithProgress;
    }

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount)
    {
        _testStartTime = testStartTime;
        _consoleWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture)
    {
        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _consoleWithProgress.WriteToTerminal(terminal =>
            {
                terminal.Append("Running tests from ");
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
            });
        }

        GetOrAddAssemblyRun(assembly, targetFramework, architecture);
    }

    private AssemblyRun GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture)
    {
        string key = $"{assembly}|{targetFramework}|{architecture}";
        if (_assemblies.TryGetValue(key, out AssemblyRun? asm))
        {
            return asm;
        }

        StopwatchAbstraction sw = CreateStopwatch();
        var progress = new TestWorker(0, 0, 0, Path.GetFileName(assembly), targetFramework, architecture, sw, detail: null);
        int slotIndex = _consoleWithProgress.AddWorker(progress);

        var assemblyRun = new AssemblyRun(slotIndex, assembly, targetFramework, architecture, sw);
        _assemblies.Add(key, assemblyRun);

        return assemblyRun;
    }

    internal void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testEndTime = endTime;
        _consoleWithProgress.StopShowingProgress();

        _consoleWithProgress.WriteToTerminal(AppendTestRunSummary);

        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
        _assemblies.Clear();
        _buildErrorsCount = 0;
        _testStartTime = null;
        _testEndTime = null;
    }

    private void AppendTestRunSummary(ITerminal terminal)
    {
        terminal.AppendLine();

        IEnumerable<IGrouping<bool, LoggerArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);
        if (artifactGroups.Any())
        {
            terminal.AppendLine();
        }

        foreach (IGrouping<bool, LoggerArtifact> artifactGroup in artifactGroups)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(artifactGroup.Key ? PlatformResources.OutOfProcessArtifactsProduced : PlatformResources.InProcessArtifactsProduced);
            foreach (LoggerArtifact artifact in artifactGroup)
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
        terminal.Append($"Test run summary: ");

        if (_wasCancelled)
        {
            terminal.Append(PlatformResources.Aborted);
        }
        else if (notEnoughTests)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.MinimumExpectedTestsPolicyViolation, totalTests, _options.MinimumExpectedTests));
        }
        else if (anyTestFailed)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}! ", PlatformResources.Failed));
        }
        else
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", PlatformResources.Passed));
        }

        terminal.AppendLine();

        if (_options.ShowAssembly && _assemblies.Count > 1)
        {
            foreach (AssemblyRun assemblyRun in _assemblies.Values)
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
        TimeSpan runDuration = _testStartTime != null && _testEndTime != null ? (_testEndTime - _testStartTime).Value : TimeSpan.Zero;

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
        AppendDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
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
                (true, true) => $"failed with {countErrors} error(s) and {countWarnings} warning(s)",
                (true, _) => $"failed with {countErrors} errors(s)",
                (false, true) => $"failed with {countWarnings} warning(s)",
                _ => "failed",
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
        AssemblyRun asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}"];

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

        var update = new TestWorker(asm.PassedTests, asm.FailedTests, asm.SkippedTests, Path.GetFileName(asm.Assembly), asm.TargetFramework, asm.Architecture, asm.Stopwatch, null);
        _consoleWithProgress.UpdateProgress(asm.SlotIndex, update);
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
            _ => throw new NotImplementedException(),
        };
        string outcomeText = outcome switch
        {
            LoggerOutcome.Fail or LoggerOutcome.Error => PlatformResources.FailedLowercase,
            LoggerOutcome.Skipped => PlatformResources.SkippedLowercase,
            LoggerOutcome.Cancelled or LoggerOutcome.Timeout => $"{PlatformResources.FailedLowercase} ({PlatformResources.CancelledLowercase})",
            LoggerOutcome.Passed => PlatformResources.PassedLowercase,
            _ => throw new NotImplementedException(),
        };

        terminal.SetColor(color);
        terminal.Append(outcomeText);
        terminal.ResetColor();
        terminal.Append(' ');
        terminal.Append(displayName);
        terminal.SetColor(TerminalColor.Gray);
        terminal.Append(' ');
        terminal.Append($"({duration.TotalSeconds:F1}s)");
        if (_options.ShowAssembly)
        {
            terminal.AppendLine();
            terminal.Append(SingleIndentation);
            terminal.Append("from ");
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
            terminal.Append("at ");
            terminal.ResetColor();
            terminal.SetColor(TerminalColor.Red);
            terminal.Append(match.Groups["code"].Value);
            terminal.SetColor(TerminalColor.Gray);
            terminal.Append(" in ");
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
            terminal.Append(message.TrimStart(' '));
            return;
        }

        string[] lines = message.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            string trimmedLine = line.TrimStart(' ');
            // Here we could check if the messages are longer than then line, and reflow them so a long line is split into multiple
            // and prepended by the respective indentation.
            // But this does not play nicely with ANSI escape codes. And if you
            // run in narrow terminal and then widen it the text does not reflow correctly. And you also have harder time copying
            // values when the assertion message is longer.
            terminal.Append(indent);
            terminal.Append(trimmedLine);
            terminal.AppendLine();
        }
    }

    internal void AssemblyRunCompleted(string assembly, string? targetFramework, string? architecture)
    {
        AssemblyRun assemblyRun = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        assemblyRun.Stopwatch.Stop();

        _consoleWithProgress.RemoveProgress(assemblyRun.SlotIndex);

        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _consoleWithProgress.WriteToTerminal(terminal => AppendAssemblySummary(assemblyRun, terminal));
        }
    }

    private static void AppendAssemblySummary(AssemblyRun assemblyRun, ITerminal terminal)
    {
        int failedTests = assemblyRun.FailedTests + assemblyRun.CancelledTests + assemblyRun.TimedOutTests;
        int warnings = 0;

        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture);
        terminal.Append(' ');
        AppendAssemblyResult(terminal, assemblyRun.FailedTests == 0, failedTests, warnings);
        terminal.Append(' ');
        AppendDuration(terminal, assemblyRun.Stopwatch.Elapsed);
    }

    private static void AppendDuration(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true, bool colorize = true)
    {
        if (colorize)
        {
            terminal.SetColor(TerminalColor.Gray);
        }

        if (wrapInParentheses)
        {
            terminal.Append('(');
        }

        if (duration.TotalMilliseconds < 1000)
        {
            terminal.Append(duration.TotalMilliseconds.ToString("F0", CultureInfo.CurrentCulture));
            terminal.Append("ms");
        }
        else
        {
            terminal.Append(duration.TotalSeconds.ToString("F1", CultureInfo.CurrentCulture));
            terminal.Append('s');
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
        => _artifacts.Add(new LoggerArtifact(outOfProcess, assembly, targetFramework, architecture, testName, path));

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
        AssemblyRun asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        asm.AddError(text);

        _consoleWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Red);
            terminal.Append(text);
            terminal.ResetColor();
        });
    }

    internal void WriteWarningMessage(string assembly, string? targetFramework, string? architecture, string text)
    {
        AssemblyRun asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture);
        asm.AddWarning(text);
        _consoleWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Yellow);
            terminal.Append(text);
            terminal.ResetColor();
        });
    }

    internal void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, Exception exception) =>
        WriteErrorMessage(assembly, targetFramework, architecture, exception.ToString());

    internal void WriteMessage(string text)
        => _consoleWithProgress.WriteToTerminal(terminal => terminal.Append(text));
}
