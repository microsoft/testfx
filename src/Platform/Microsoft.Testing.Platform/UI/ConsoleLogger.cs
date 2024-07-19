// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;
#if !NET7_0_OR_GREATER
using System.Reflection;
#endif
using System.Text;
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

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Directory to which all the paths are relative.
    /// </summary>
    private readonly string? _baseDirectory;

    /// <summary>
    /// Tracks the work currently being done by build nodes. Null means the node is not doing any work worth reporting.
    /// </summary>
    /// <remarks>
    /// There is no locking around access to this data structure despite it being accessed concurrently by multiple threads.
    /// However, reads and writes to locations in an array is atomic, so locking is not required.
    /// </remarks>
    private TestWorkerProgress?[] _progress = Array.Empty<TestWorkerProgress>();

    /// <summary>
    /// The thread that performs periodic refresh of the console output.
    /// </summary>
    private Thread? _refresher;

    /// <summary>
    /// What is currently displaying in Nodes section as strings representing per-node console output.
    /// </summary>
    private ProgressFrame _currentFrame = new(Array.Empty<TestWorkerProgress>(), 0, 0);

    /// <summary>
    /// Gets the <see cref="Terminal"/> to write console output to.
    /// </summary>
    private ITerminal Terminal { get; }

    internal Func<StopwatchAbstraction> CreateStopwatch { get; set; } = SystemStopwatch.StartNew;

    /// <summary>
    /// Refresh manually instead of using update thread when testing.
    /// </summary>
    private readonly bool _manualRefresh;

    /// <summary>
    /// The two directory separator characters to be passed to methods like <see cref="string.IndexOfAny(char[])"/>.
    /// </summary>
    private readonly Dictionary<string, AssemblyRun> _assemblies = new();
    private readonly IClock _clock;
    private readonly List<LoggerArtifact> _artifacts = new();

    private readonly ConsoleLoggerOptions _options;

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
    /// Initializes a new instance of the <see cref="ConsoleLogger"/> class.
    /// </summary>
    public ConsoleLogger(IClock clock, ConsoleLoggerOptions options)
        : this(clock, new Terminal(), manualRefresh: false, options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogger"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    internal /* for testing */ ConsoleLogger(IClock clock, ITerminal terminal, bool manualRefresh, ConsoleLoggerOptions options)
    {
        Terminal = terminal;
        _clock = clock;
        _manualRefresh = manualRefresh;
        _options = options;

        // TODO: is this unsafe?
        _baseDirectory = _options.BaseDirectory ?? Directory.GetCurrentDirectory();
    }

    public void TestExecutionStarted(TestRunStartedUpdate testRunStarted)
    {
        _progress = new TestWorkerProgress[testRunStarted.WorkerCount];
        _testStartTime = _clock.UtcNow;
        if (!_manualRefresh)
        {
            _refresher = new Thread(ThreadProc);
            _refresher.Start();
        }

        if (Terminal.SupportsProgressReporting)
        {
            Terminal.Write(AnsiCodes.SetProgressIndeterminate);
        }
    }

    public void AssemblyRunStarted(AssemblyRunStartedUpdate assemblyRunStartedUpdate)
    {
        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Running tests from ");
            AppendAssemblyLinkTargetFrameworkAndArchitecture(stringBuilder, assemblyRunStartedUpdate.Assembly, assemblyRunStartedUpdate.TargetFramework, assemblyRunStartedUpdate.Architecture);
            WriteMessage(stringBuilder.ToString());
        }

        string key = $"{assemblyRunStartedUpdate.Assembly}|{assemblyRunStartedUpdate.TargetFramework}|{assemblyRunStartedUpdate.Architecture}";
        int slotIndex = GetEmptySlot(_progress);
        StopwatchAbstraction sw = CreateStopwatch();
        _assemblies.Add(key, new AssemblyRun(slotIndex, assemblyRunStartedUpdate, sw));
        _progress[slotIndex] = new TestWorkerProgress(assemblyRunStartedUpdate.Tests, 0, 0, 0, Path.GetFileName(assemblyRunStartedUpdate.Assembly), assemblyRunStartedUpdate.TargetFramework, assemblyRunStartedUpdate.Architecture, sw, null);
    }

    private static int GetEmptySlot(TestWorkerProgress?[] progress)
    {
        for (int i = 0; i < progress.Length; i++)
        {
            if (progress[i] == null)
            {
                return i;
            }
        }

        throw new InvalidOperationException("No empty slot found");
    }

    internal void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testEndTime = endTime;
        _cts.Cancel();
        _refresher?.Join();

        Terminal.BeginUpdate();
        try
        {
            EraseNodes();
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine();

            IEnumerable<IGrouping<bool, LoggerArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);

            int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
            int totalFailedTests = _assemblies.Values.Sum(a => a.FailedTests);
            int totalSkippedTests = _assemblies.Values.Sum(a => a.SkippedTests);

            bool notEnoughTests = totalTests < _options.MinimumExpectedTests;
            bool allTestsWereSkipped = totalTests == 0 || totalTests == totalSkippedTests;
            bool anyTestFailed = totalFailedTests > 0;
            bool runFailed = anyTestFailed || notEnoughTests || allTestsWereSkipped || _wasCancelled;
            stringBuilder.Append(SetColor(runFailed ? TerminalColor.Red : TerminalColor.Green));

            stringBuilder.Append($"Test run summary: ");

            if (_wasCancelled)
            {
                stringBuilder.Append(PlatformResources.Aborted);
            }
            else if (notEnoughTests)
            {
                stringBuilder.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.MinimumExpectedTestsPolicyViolation, totalTests, _options.MinimumExpectedTests));
            }
            else if (anyTestFailed)
            {
                stringBuilder.Append(string.Format(CultureInfo.CurrentCulture, "{0}! ", PlatformResources.Failed));
            }
            else
            {
                stringBuilder.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", PlatformResources.Passed));
            }

            stringBuilder.AppendLine();

            if (_options.ShowAssembly && _assemblies.Count > 1)
            {
                foreach (AssemblyRun assemblyRun in _assemblies.Values)
                {
                    stringBuilder.Append(SingleIndentation);
                    AppendAssemblySummary(assemblyRun, stringBuilder);
                }

                stringBuilder.AppendLine();
            }

            foreach (IGrouping<bool, LoggerArtifact> artifactGroup in artifactGroups)
            {
                stringBuilder.AppendLine(artifactGroup.Key ? PlatformResources.OutOfProcessArtifactsProduced : PlatformResources.InProcessArtifactsProduced);
                foreach (LoggerArtifact artifact in artifactGroup)
                {
                    stringBuilder.Append("- ");
                    if (!RoslynString.IsNullOrWhiteSpace(artifact.TestName))
                    {
                        stringBuilder.Append(PlatformResources.ForTest);
                        stringBuilder.Append(" '");
                        stringBuilder.Append(artifact.TestName);
                        stringBuilder.Append("': ");
                    }

                    AppendLink(stringBuilder, artifact.Path, lineNumber: null);
                    stringBuilder.AppendLine();
                }
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

            failedText = colorizeFailed ? AnsiCodes.Colorize(failedText.ToString(), TerminalColor.DarkRed) : failedText;
            passedText = colorizePassed ? AnsiCodes.Colorize(passedText.ToString(), TerminalColor.DarkGreen) : passedText;
            skippedText = colorizeSkipped ? AnsiCodes.Colorize(skippedText.ToString(), TerminalColor.DarkYellow) : skippedText;

            stringBuilder.Append(ResetColor());
            stringBuilder.AppendLine(totalText);
            if (colorizeFailed)
            {
                stringBuilder.Append(SetColor(TerminalColor.Red));
            }

            stringBuilder.AppendLine(failedText);

            if (colorizeFailed)
            {
                stringBuilder.Append(ResetColor());
            }

            if (colorizePassed)
            {
                stringBuilder.Append(SetColor(TerminalColor.Green));
            }

            stringBuilder.AppendLine(passedText);

            if (colorizePassed)
            {
                stringBuilder.Append(ResetColor());
            }

            if (colorizeSkipped)
            {
                stringBuilder.Append(SetColor(TerminalColor.DarkYellow));
            }

            stringBuilder.AppendLine(skippedText);

            if (colorizeSkipped)
            {
                stringBuilder.Append(ResetColor());
            }

            stringBuilder.Append(SetColor(TerminalColor.Gray));
            stringBuilder.Append(durationText);
            AppendDuration(stringBuilder, runDuration);
            stringBuilder.AppendLine();

            Terminal.WriteLine(stringBuilder.ToString());
        }
        finally
        {
            if (Terminal.SupportsProgressReporting)
            {
                Terminal.Write(AnsiCodes.RemoveProgress);
            }

            Terminal.EndUpdate();
        }

        _assemblies.Clear();
        _buildErrorsCount = 0;
        _testStartTime = null;
        _testEndTime = null;
    }

    /// <summary>
    /// The <see cref="_refresher"/> thread proc.
    /// </summary>
    private void ThreadProc()
    {
        // 1_000 / 30 is a poor approx of 30Hz
        while (!_cts.Token.WaitHandle.WaitOne(1_000 / 30))
        {
            lock (_lock)
            {
                DisplayNodes();
            }
        }

        EraseNodes();
    }

    /// <summary>
    /// Render Nodes section.
    /// It shows what all build nodes do.
    /// </summary>
    internal void DisplayNodes()
    {
        ProgressFrame newFrame = new(_progress, width: Terminal.Width, height: Terminal.Height);

        // Do not render delta but clear everything if Terminal width or height have changed.
        if (newFrame.Width != _currentFrame.Width || newFrame.Height != _currentFrame.Height)
        {
            EraseNodes();
        }

        string rendered = newFrame.Render(_currentFrame);

        // Hide the cursor to prevent it from jumping around as we overwrite the live lines.
        Terminal.Write(AnsiCodes.HideCursor);
        try
        {
            Terminal.Write(rendered);
        }
        finally
        {
            Terminal.Write(AnsiCodes.ShowCursor);
        }

        _currentFrame = newFrame;
    }

    /// <summary>
    /// Erases the previously printed live node output.
    /// </summary>
    private void EraseNodes()
    {
        if (_currentFrame.ProgressCount == 0)
        {
            return;
        }

        Terminal.WriteLine($"{AnsiCodes.CSI}{_currentFrame.ProgressCount + 1}{AnsiCodes.MoveUpToLineStart}");
        Terminal.Write($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        _currentFrame.Clear();
    }

    /// <summary>
    /// Print a build result summary to the output.
    /// </summary>
    private static void AppendAssemblyResult(StringBuilder stringBuilder, bool succeeded, int countErrors, int countWarnings)
    {
        if (!succeeded)
        {
            stringBuilder.Append(SetColor(TerminalColor.DarkRed));
            // If the build failed, we print one of three red strings.
            string text = (countErrors > 0, countWarnings > 0) switch
            {
                (true, true) => $"failed with {countErrors} error(s) and {countWarnings} warning(s)",
                (true, _) => $"failed with {countErrors} errors(s)",
                (false, true) => $"failed with {countWarnings} warning(s)",
                _ => "failed",
            };
            stringBuilder.Append(text);
            stringBuilder.Append(ResetColor());
            AnsiCodes.Colorize(text, TerminalColor.DarkRed);
        }
        else if (countWarnings > 0)
        {
            stringBuilder.Append(SetColor(TerminalColor.DarkYellow));
            stringBuilder.Append(CultureInfo.CurrentCulture, $"succeeded with {countWarnings} warning(s)");
            stringBuilder.Append(ResetColor());
        }
        else
        {
            stringBuilder.Append(SetColor(TerminalColor.DarkGreen));
            stringBuilder.Append(PlatformResources.PassedLowercase);
            stringBuilder.Append(ResetColor());
        }
    }

    private void WriteMessage(string message)
    {
        lock (_lock)
        {
            // Calling erase helps to clear the screen before printing the message
            // The immediate output will not overlap with node status reporting
            EraseNodes();
            Terminal.WriteLine(message);
        }
    }

    private void AppendLink(StringBuilder stringBuilder, string? path, int? lineNumber)
    {
        if (RoslynString.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // For non code files, point to the directory, so we don't end up running the
        // exe by clicking at the link.
        bool linkToFile = path.EndsWith(".cs", ignoreCase: true, CultureInfo.CurrentCulture)
            || path.EndsWith(".fs", ignoreCase: true, CultureInfo.CurrentCulture)
            || path.EndsWith(".vb", ignoreCase: true, CultureInfo.CurrentCulture);

        bool knownNonExistingFile = path.StartsWith("/_/", ignoreCase: false, CultureInfo.CurrentCulture);

        string linkPath = path;
        if (!linkToFile)
        {
            try
            {
                linkPath = Path.GetDirectoryName(linkPath) ?? linkPath;
            }
            catch
            {
                // Ignore all GetDirectoryName errors.
            }
        }

        // If the output path is under the initial working directory, make the console output relative to that to save space.
        if (_baseDirectory != null && path.StartsWith(_baseDirectory, FileUtilities.PathComparison))
        {
            if (path.Length > _baseDirectory.Length
                && (path[_baseDirectory.Length] == Path.DirectorySeparatorChar
                    || path[_baseDirectory.Length] == Path.AltDirectorySeparatorChar))
            {
                path = path.Substring(_baseDirectory.Length + 1);
            }
        }

        if (lineNumber != null)
        {
            path += $":{lineNumber}";
        }

        if (knownNonExistingFile)
        {
            stringBuilder.Append(path);
            return;
        }

        // Generates file:// schema url string which is better handled by various Terminal clients than raw folder name.
        string urlString = linkPath.ToString();
        if (Uri.TryCreate(urlString, UriKind.Absolute, out Uri? uri))
        {
            // url.ToString() un-escapes the URL which is needed for our case file://
            urlString = uri.ToString();
        }

        stringBuilder.Append(SetColor(TerminalColor.Gray));
        stringBuilder.Append(AnsiCodes.LinkPrefix);
        stringBuilder.Append(urlString);
        stringBuilder.Append(AnsiCodes.LinkInfix);
        stringBuilder.Append(path);
        stringBuilder.Append(AnsiCodes.LinkSuffix);
        stringBuilder.Append(ResetColor());
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
        string key = $"{assembly}|{targetFramework}|{architecture}";
        if (!_assemblies.TryGetValue(key, out AssemblyRun? asm))
        {
            asm = new AssemblyRun(GetEmptySlot(_progress), new AssemblyRunStartedUpdate(assembly, tests: 0, targetFramework, architecture), CreateStopwatch());
            _assemblies[key] = asm;
        }

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

        lock (_lock)
        {
            Terminal.BeginUpdate();
            try
            {
                EraseNodes();

                var stringBuilder = new StringBuilder();
                RenderTestCompleted(
                    stringBuilder,
                    assembly,
                    targetFramework,
                    architecture,
                    displayName,
                    outcome,
                    duration,
                    errorMessage,
                    errorStackTrace,
                    expected,
                    actual);
                Terminal.Write(stringBuilder.ToString());

                _progress[asm.SlotIndex] = new TestWorkerProgress(asm.AssemblyRunStartedUpdate.Tests, asm.PassedTests, asm.FailedTests, asm.SkippedTests, Path.GetFileName(asm.AssemblyRunStartedUpdate.Assembly), asm.AssemblyRunStartedUpdate.TargetFramework, asm.AssemblyRunStartedUpdate.Architecture, asm.Stopwatch, null);
                DisplayNodes();
            }
            finally
            {
                Terminal.EndUpdate();
            }
        }
    }

    internal /* for testing */ void RenderTestCompleted(
        StringBuilder stringBuilder,
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

        stringBuilder.Append(SetColor(color));
        stringBuilder.Append(outcomeText);
        stringBuilder.Append(ResetColor());
        stringBuilder.Append(' ');
        stringBuilder.Append(displayName);
        stringBuilder.Append(SetColor(TerminalColor.Gray));
        stringBuilder.Append(' ');
        stringBuilder.Append(CultureInfo.CurrentCulture, $"({duration.TotalSeconds:F1}s)");
        if (_options.ShowAssembly)
        {
            stringBuilder.AppendLine();
            stringBuilder.Append(SingleIndentation);
            stringBuilder.Append("from ");
            AppendAssemblyLinkTargetFrameworkAndArchitecture(stringBuilder, assembly, targetFramework, architecture);
        }

        stringBuilder.AppendLine();

        FormatErrorMessage(stringBuilder, errorMessage);
        ConsoleLogger.FormatExpectedAndActual(stringBuilder, expected, actual);
        FormatStackTrace(stringBuilder, errorStackTrace);
    }

    private void AppendAssemblyLinkTargetFrameworkAndArchitecture(StringBuilder stringBuilder, string assembly, string? targetFramework, string? architecture)
    {
        AppendLink(stringBuilder, assembly, lineNumber: null);
        if (!RoslynString.IsNullOrWhiteSpace(architecture) && !RoslynString.IsNullOrWhiteSpace(targetFramework))
        {
            stringBuilder.Append(" (");
            stringBuilder.Append(targetFramework);
            stringBuilder.Append('|');
            stringBuilder.Append(architecture);
            stringBuilder.Append(')');
        }
    }

    private void FormatStackTrace(StringBuilder stringBuilder, string? errorStackTrace)
    {
        if (RoslynString.IsNullOrWhiteSpace(errorStackTrace))
        {
            return;
        }

        stringBuilder.Append(SetColor(TerminalColor.DarkRed));
        stringBuilder.Append(SingleIndentation);
        stringBuilder.Append(PlatformResources.StackTrace);
        stringBuilder.AppendLine(":");

        if (!errorStackTrace.Contains('\n'))
        {
            AppendStackFrame(stringBuilder, errorStackTrace);
            return;
        }

        string[] lines = errorStackTrace.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            AppendStackFrame(stringBuilder, line);
        }

        stringBuilder.Append(ResetColor());
    }

    private void AppendStackFrame(StringBuilder stringBuilder, string stackTraceLine)
    {
        stringBuilder.Append(DoubleIndentation);
        Match match = GetFrameRegex().Match(stackTraceLine);
        if (match.Success)
        {
            stringBuilder.Append(SetColor(TerminalColor.Gray));
            stringBuilder.Append("at ");
            stringBuilder.Append(ResetColor());
            stringBuilder.Append(SetColor(TerminalColor.Red));
            stringBuilder.Append(match.Groups["code"].Value);
            stringBuilder.Append(SetColor(TerminalColor.Gray));
            stringBuilder.Append(" in ");
            int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
            AppendLink(stringBuilder, match.Groups["file"].Value, line);
            stringBuilder.AppendLine();
        }
    }

    private static void FormatExpectedAndActual(StringBuilder stringBuilder, string? expected, string? actual)
    {
        if (RoslynString.IsNullOrWhiteSpace(expected) && RoslynString.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        stringBuilder.Append(SetColor(TerminalColor.Red));
        stringBuilder.Append(SingleIndentation);
        stringBuilder.AppendLine(PlatformResources.Expected);
        AppendIndentedMessage(stringBuilder, expected, DoubleIndentation);
        stringBuilder.AppendLine();
        stringBuilder.Append(SingleIndentation);
        stringBuilder.AppendLine(PlatformResources.Actual);
        AppendIndentedMessage(stringBuilder, actual, DoubleIndentation);
        stringBuilder.Append(ResetColor());
        stringBuilder.AppendLine();
    }

    private static void FormatErrorMessage(StringBuilder stringBuilder, string? errorMessage)
    {
        if (RoslynString.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        stringBuilder.Append(SetColor(TerminalColor.Red));
        AppendIndentedMessage(stringBuilder, errorMessage, SingleIndentation);
        stringBuilder.Append(ResetColor());
        stringBuilder.AppendLine();
    }

    private static string SetColor(TerminalColor color)
        => $"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}";

    private static string ResetColor()
        => AnsiCodes.SetDefaultColor;

    private static void AppendIndentedMessage(StringBuilder stringBuilder, string? message, string indent)
    {
        if (RoslynString.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!message.Contains('\n'))
        {
            stringBuilder.Append(indent);
            stringBuilder.Append(message?.TrimStart(' '));
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
            stringBuilder.Append(indent);
            stringBuilder.Append(trimmedLine);
            stringBuilder.AppendLine();
        }
    }

    internal void AssemblyRunCompleted(string assembly, string? targetFramework, string? architecture)
    {
        string key = $"{assembly}|{targetFramework}|{architecture}";
        AssemblyRun assemblyRun = _assemblies[key];
        assemblyRun.Stopwatch.Stop();

        _progress[assemblyRun.SlotIndex] = null;

        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            lock (_lock)
            {
                Terminal.BeginUpdate();
                var stringBuilder = new StringBuilder();
                try
                {
                    EraseNodes();

                    AppendAssemblySummary(assemblyRun, stringBuilder);
                    Terminal.WriteLine(stringBuilder.ToString());

                    DisplayNodes();
                }
                finally
                {
                    Terminal.EndUpdate();
                }
            }
        }
    }

    private void AppendAssemblySummary(AssemblyRun assemblyRun, StringBuilder stringBuilder)
    {
        int failedTests = assemblyRun.FailedTests + assemblyRun.CancelledTests + assemblyRun.TimedOutTests;
        int warnings = 0;

        AppendAssemblyLinkTargetFrameworkAndArchitecture(stringBuilder, assemblyRun.AssemblyRunStartedUpdate.Assembly, assemblyRun.AssemblyRunStartedUpdate.TargetFramework, assemblyRun.AssemblyRunStartedUpdate.Architecture);
        stringBuilder.Append(' ');
        AppendAssemblyResult(stringBuilder, assemblyRun.FailedTests == 0, failedTests, warnings);
        stringBuilder.Append(' ');
        AppendDuration(stringBuilder, assemblyRun.Stopwatch.Elapsed);
    }

    private static void AppendDuration(StringBuilder stringBuilder, TimeSpan duration)
    {
        stringBuilder.Append(SetColor(TerminalColor.Gray));
        stringBuilder.Append('(');
        if (duration.TotalMilliseconds < 1000)
        {
            stringBuilder.Append(duration.TotalMilliseconds.ToString("F0", CultureInfo.CurrentCulture));
            stringBuilder.Append("ms");
        }
        else
        {
            stringBuilder.Append(duration.TotalSeconds.ToString("F1", CultureInfo.CurrentCulture));
            stringBuilder.Append('s');
        }

        stringBuilder.Append(')');

        stringBuilder.Append(ResetColor());
    }

    public void Dispose() => ((IDisposable)_cts).Dispose();

    public void ArtifactAdded(bool outOfProcess, string? assembly, string? targetFramework, string? architecture, string? testName, string path)
        => _artifacts.Add(new LoggerArtifact(outOfProcess, assembly, targetFramework, architecture, testName, path));

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    internal void StartCancelling()
    {
        _wasCancelled = true;
        WriteMessage(PlatformResources.CancellingTestSession);
    }
}
