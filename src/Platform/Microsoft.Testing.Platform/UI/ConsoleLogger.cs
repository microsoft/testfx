// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.UI;

/// <summary>
/// A logger which updates the console output "live" during the build.
/// </summary>
/// <remarks>
/// Uses ANSI/VT100 control codes to erase and overwrite lines as the build is progressing.
/// </remarks>
internal sealed class ConsoleLogger : IDisposable
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
    /// The working directory when the build starts, to trim relative output paths.
    /// </summary>
    private readonly string _initialWorkingDirectory = Environment.CurrentDirectory;

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
    private static readonly char[] PathSeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
    private readonly Dictionary<string, AssemblyRun> _assemblies = new();
    private readonly IEnvironment _environment;
    private readonly IClock _clock;

    /// <summary>
    /// Time of the oldest observed test target start.
    /// </summary>
    private DateTimeOffset? _testStartTime;

    /// <summary>
    /// Time of the most recently observed test target finished.
    /// </summary>
    private DateTime? _testEndTime;

    private int _buildErrorsCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogger"/> class.
    /// </summary>
    public ConsoleLogger(IEnvironment environment, IClock clock)
        : this(environment, clock, new Terminal(), manualRefresh: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogger"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    internal /* for testing */ ConsoleLogger(IEnvironment environment, IClock clock, ITerminal terminal, bool manualRefresh)
    {
        Terminal = terminal;
        _environment = environment;
        _clock = clock;
        _manualRefresh = manualRefresh;
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
        Terminal.WriteLine($"Running tests for {assemblyRunStartedUpdate.Assembly}");
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

    internal void TestExecutionCompleted(TestExecutionCompleted testExecutionCompleted)
    {
        _testEndTime = testExecutionCompleted.Timestamp;
        _cts.Cancel();
        _refresher?.Join();

        Terminal.BeginUpdate();
        try
        {
            Terminal.WriteLine(string.Empty);
            if (_assemblies.Count == 0)
            {
                int total = _assemblies.Values.Sum(t => t.TotalTests);
                int failed = _assemblies.Values.Sum(t => t.FailedTests);
                int passed = _assemblies.Values.Sum(t => t.PassedTests);
                int skipped = _assemblies.Values.Sum(t => t.SkippedTests);
                string testDuration = (_testStartTime != null && _testEndTime != null ? (_testEndTime - _testStartTime).Value.TotalSeconds : 0).ToString("F1", CultureInfo.CurrentCulture);

                bool colorizeFailed = failed > 0;
                bool colorizePassed = passed > 0 && _buildErrorsCount == 0 && failed == 0;
                bool colorizeSkipped = skipped > 0 && skipped == total && _buildErrorsCount == 0 && failed == 0;

                string summaryAndTotalText = $"Test summary: total: {total}";
                string failedText = $"failed: {failed}";
                string passedText = $"succeeded: {passed}";
                string skippedText = $"skipped: {skipped}";
                string durationText = $"duration: {testDuration}s";

                failedText = colorizeFailed ? AnsiCodes.Colorize(failedText.ToString(), TerminalColor.Red) : failedText;
                passedText = colorizePassed ? AnsiCodes.Colorize(passedText.ToString(), TerminalColor.Green) : passedText;
                skippedText = colorizeSkipped ? AnsiCodes.Colorize(skippedText.ToString(), TerminalColor.Yellow) : skippedText;

                Terminal.WriteLine(string.Join(CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ", summaryAndTotalText, failedText, passedText, skippedText, durationText));
            }
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

    #region Refresher thread implementation

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

    #endregion

    #region Helpers

    /// <summary>
    /// Print a build result summary to the output.
    /// </summary>
    /// <param name="succeeded">True if the build completed with success.</param>
    /// <param name="hasError">True if the build has logged at least one error.</param>
    /// <param name="hasWarning">True if the build has logged at least one warning.</param>
    private string RenderBuildResult(bool succeeded, int countErrors, int countWarnings)
    {
        if (!succeeded)
        {
            // If the build failed, we print one of three red strings.
            string text = (countErrors > 0, countWarnings > 0) switch
            {
                (true, true) => $"failed with {countErrors} error(s) and {countWarnings} warning(s)",
                (true, _) => $"failed with {countErrors} errors(s)",
                (false, true) => $"failed with {countWarnings} warning(s)j",
                _ => "failed",
            };
            return AnsiCodes.Colorize(text, TerminalColor.Red);
        }
        else if (countWarnings > 0)
        {
            return AnsiCodes.Colorize($"succeeded with {countWarnings} warning(s)", TerminalColor.Yellow);
        }
        else
        {
            return AnsiCodes.Colorize("succeeded", TerminalColor.Green);
        }
    }

    /// <summary>
    /// Print a build messages to the output that require special customer's attention.
    /// </summary>
    /// <param name="message">Build message needed to be shown immediately.</param>
    private void RenderImmediateMessage(string message)
    {
        lock (_lock)
        {
            // Calling erase helps to clear the screen before printing the message
            // The immediate output will not overlap with node status reporting
            EraseNodes();
            Terminal.WriteLine(message);
        }
    }


    /// <summary>
    /// Colorizes the filename part of the given path.
    /// </summary>
    private static string? HighlightFileName(StringBuilder stringBuilder, string? path)
    {
        if (path == null)
        {
            return null;
        }

        int index = path.LastIndexOfAny(PathSeparators);
        return index >= 0
            ? $"{path.Substring(0, index + 1)}{AnsiCodes.MakeBold(path.Substring(index + 1))}"
            : path;
    }

    //private string FormatWarningMessage(BuildWarningEventArgs e, string indent)
    //{
    //    return FormatEventMessage(
    //            category: AnsiCodes.Colorize("warning", TerminalColor.Yellow),
    //            subcategory: e.Subcategory,
    //            message: e.Message,
    //            code: AnsiCodes.Colorize(e.Code, TerminalColor.Yellow),
    //            file: HighlightFileName(e.File),
    //            lineNumber: e.LineNumber,
    //            endLineNumber: e.EndLineNumber,
    //            columnNumber: e.ColumnNumber,
    //            endColumnNumber: e.EndColumnNumber,
    //            indent);
    //}

    //private string FormatErrorMessage(BuildErrorEventArgs e, string indent)
    //{
    //    return FormatEventMessage(
    //            category: AnsiCodes.Colorize("error", TerminalColor.Red),
    //            subcategory: e.Subcategory,
    //            message: e.Message,
    //            code: AnsiCodes.Colorize(e.Code, TerminalColor.Red),
    //            file: HighlightFileName(e.File),
    //            lineNumber: e.LineNumber,
    //            endLineNumber: e.EndLineNumber,
    //            columnNumber: e.ColumnNumber,
    //            endColumnNumber: e.EndColumnNumber,
    //            indent);
    //}

    private string FormatEventMessage(
            string category,
            string subcategory,
            string? message,
            string code,
            string? file,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            string indent)
    {
        message ??= string.Empty;
        StringBuilder builder = new(128);

        if (string.IsNullOrEmpty(file))
        {
            builder.Append("MSBUILD : ");    // Should not be localized.
        }
        else
        {
            builder.Append(file);

            if (lineNumber == 0)
            {
                builder.Append(" : ");
            }
            else
            {
                if (columnNumber == 0)
                {
                    builder.Append(endLineNumber == 0 ?
                        $"({lineNumber}): " :
                        $"({lineNumber}-{endLineNumber}): ");
                }
                else
                {
                    if (endLineNumber == 0)
                    {
                        builder.Append(endColumnNumber == 0 ?
                            $"({lineNumber},{columnNumber}): " :
                            $"({lineNumber},{columnNumber}-{endColumnNumber}): ");
                    }
                    else
                    {
                        builder.Append(endColumnNumber == 0 ?
                            $"({lineNumber}-{endLineNumber},{columnNumber}): " :
                            $"({lineNumber},{columnNumber},{endLineNumber},{endColumnNumber}): ");
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(subcategory))
        {
            builder.Append(subcategory);
            builder.Append(' ');
        }

        builder.Append($"{category} {code}: ");

        // render multi-line message in a special way
        if (message.IndexOf('\n') >= 0)
        {
            // Place the multiline message under the project in case of minimal and higher verbosity.
            string[] lines = message.Split(NewLineStrings, StringSplitOptions.None);

            foreach (string line in lines)
            {
                if (indent.Length + line.Length > Terminal.Width) // custom wrapping with indentation
                {
                    WrapText(builder, line, Terminal.Width, indent);
                }
                else
                {
                    builder.AppendLine();
                    builder.Append(indent);
                    builder.Append(line);
                }
            }
        }
        else
        {
            builder.Append(message);
        }

        return builder.ToString();
    }

    private static void WrapText(StringBuilder sb, string text, int maxLength, string indent)
    {
        int start = 0;
        while (start < text.Length)
        {
            int length = Math.Min(maxLength - indent.Length, text.Length - start);
            sb.AppendLine();
            sb.Append(indent);
#if NETCOREAPP
            sb.Append(text.AsSpan().Slice(start, length));
#else
            sb.Append(text.Substring(start, length));
#endif

            start += length;
        }
    }

    internal void TestCompleted(
        string assembly,
        string targetFramework,
        string architecture,
        string displayName,
        Outcome outcome,
        TimeSpan duration,
        string? errorMessage,
        string? errorStackTrace,
        string? expected,
        string? actual)
    {
        string key = $"{assembly}|{targetFramework}|{architecture}";
        AssemblyRun asm = _assemblies[key];

        switch (outcome)
        {
            case Outcome.Error:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case Outcome.Fail:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case Outcome.Passed:
                asm.PassedTests++;
                asm.TotalTests++;
                break;
            case Outcome.Skipped:
                asm.SkippedTests++;
                asm.TotalTests++;
                break;
            case Outcome.Timeout:
                asm.TimedOutTests++;
                asm.TotalTests++;
                break;
            case Outcome.Cancelled:
                asm.CancelledTests++;
                asm.TotalTests++;
                break;
            default:
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
        string targetFramework,
        string architecture,
        string displayName,
        Outcome outcome,
        TimeSpan duration,
        string? errorMessage,
        string? errorStackTrace,
        string? expected,
        string? actual)
    {
        if (outcome == Outcome.Passed)
        {
            return;
        }

        TerminalColor color = outcome is Outcome.Error or Outcome.Fail or Outcome.Cancelled or Outcome.Timeout ? TerminalColor.DarkRed : TerminalColor.Yellow;
        string outcomeText = outcome switch
        {
            Outcome.Fail or Outcome.Error => PlatformResources.FailedLowercase,
            Outcome.Skipped => PlatformResources.SkippedLowercase,
            Outcome.Cancelled or Outcome.Timeout => $"{PlatformResources.FailedLowercase} ({PlatformResources.CancelledLowercase})",
            _ => string.Empty,
        };

        stringBuilder.Append(SetColor(color));
        stringBuilder.Append(' ');
        stringBuilder.Append(outcomeText);
        stringBuilder.Append(ResetColor());
        bool anySectionFollows = errorMessage != null || errorStackTrace != null || expected != null || actual != null;
        if (anySectionFollows)
        {
            stringBuilder.AppendLine();
        }

        FormatErrorMessage(stringBuilder, errorMessage);
        FormatExpectedAndActual(stringBuilder, expected, actual);
        FormatStackTrace(stringBuilder, errorStackTrace);
        stringBuilder.AppendLine();
    }

    private void FormatStackTrace(StringBuilder stringBuilder, string? errorStackTrace)
    {
        if (RoslynString.IsNullOrWhiteSpace(errorStackTrace))
        {
            return;
        }

        stringBuilder.Append(SetColor(TerminalColor.DarkRed));
        stringBuilder.AppendLine(PlatformResources.StackTrace);
        AppendIndentedMessage(stringBuilder, errorStackTrace, SingleIndentation);
        stringBuilder.Append(ResetColor());
        stringBuilder.AppendLine();
    }

    private void FormatExpectedAndActual(StringBuilder stringBuilder, string? expected, string? actual)
    {
        if (RoslynString.IsNullOrWhiteSpace(expected) && RoslynString.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        stringBuilder.Append(SetColor(TerminalColor.Red));
        stringBuilder.AppendLine(PlatformResources.Expected);
        AppendIndentedMessage(stringBuilder, expected, SingleIndentation);
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(PlatformResources.Actual);
        AppendIndentedMessage(stringBuilder, actual, SingleIndentation);
        stringBuilder.Append(ResetColor());
        stringBuilder.AppendLine();
    }

    private void FormatErrorMessage(StringBuilder stringBuilder, string? errorMessage)
    {
        if (RoslynString.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        stringBuilder.Append(SetColor(TerminalColor.Red));
        // There is no indentation, so even if there are new lines we don't have to re-format.
        stringBuilder.Append(errorMessage);
        stringBuilder.Append(ResetColor());
        stringBuilder.AppendLine();

    }

    private static string SetColor(TerminalColor color)
        => $"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}";

    private static string ResetColor()
        => AnsiCodes.SetDefaultColor;

    private void AppendIndentedMessage(StringBuilder stringBuilder, string? message, string indent)
    {
        if (RoslynString.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!message.Contains('\n'))
        {
            stringBuilder.Append(indent);
            stringBuilder.Append(message);
            return;
        }

        string[] lines = message.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            // Wrap text but start each new line with the given indentation.
            if (indent.Length + line.Length > Terminal.Width)
            {
                WrapText(stringBuilder, line, Terminal.Width, indent);
            }
            else
            {
                stringBuilder.AppendLine();
                stringBuilder.Append(indent);
                stringBuilder.Append(line);
            }
        }
    }

    internal void AssemblyRunCompleted(AssemblyRunCompletedUpdate assemblyRunCompletedUpdate)
    {
        string key = $"{assemblyRunCompletedUpdate.Assembly}|{assemblyRunCompletedUpdate.TargetFramework}|{assemblyRunCompletedUpdate.Architecture}";
        AssemblyRun assemblyRun = _assemblies[key];
        assemblyRun.Stopwatch.Stop();

        _progress[assemblyRun.SlotIndex] = null;

        lock (_lock)
        {
            Terminal.BeginUpdate();
            try
            {
                EraseNodes();

                string duration = assemblyRun.Stopwatch.ElapsedSeconds.ToString("F1", CultureInfo.CurrentCulture);
#if NETCOREAPP
                ReadOnlyMemory<char>?
#else
                string
#endif
                    outputPath = assemblyRun.Attachments.FirstOrDefault()
#if NETCOREAPP
                    .AsMemory();
#else
                    ;
#endif

                string projectFile = assemblyRun.AssemblyRunStartedUpdate.Assembly is not null ?
                    Path.GetFileNameWithoutExtension(assemblyRun.AssemblyRunStartedUpdate.Assembly) :
                    string.Empty;

                // Build result. One of 'failed', 'succeeded with warnings', or 'succeeded' depending on the build result and diagnostic messages
                // reported during build.
                int countErrors = assemblyRun.Messages?.Count(m => m.Severity == MessageSeverity.Error) ?? 0;
                int countWarnings = assemblyRun.Messages?.Count(m => m.Severity == MessageSeverity.Warning) ?? 0;

                string buildResult = RenderBuildResult(assemblyRun.FailedTests == 0, countErrors, countWarnings);

                bool haveErrors = countErrors > 0;
                bool haveWarnings = countWarnings > 0;

                Terminal.WriteLine($"{SingleIndentation}{projectFile} {buildResult} {duration}");


                // Print the output path as a link if we have it.
                if (outputPath is not null)
                {

#if NETCOREAPP
                    ReadOnlySpan<char> outputPathSpan = outputPath.Value.Span;
                    ReadOnlySpan<char> url = outputPathSpan;
#else
                    string outputPathSpan = outputPath;
                    string url = outputPathSpan;
#endif
                    try
                    {
                        // If possible, make the link point to the containing directory of the output.
                        url = Path.GetDirectoryName(url);
                    }
                    catch
                    {
                        // Ignore any GetDirectoryName exceptions.
                    }

                    // Generates file:// schema url string which is better handled by various Terminal clients than raw folder name.
                    string urlString = url.ToString();
                    if (Uri.TryCreate(urlString, UriKind.Absolute, out Uri? uri))
                    {
                        // url.ToString() un-escapes the URL which is needed for our case file://
                        // but not valid for http://
                        urlString = uri.ToString();
                    }

                    // If the output path is under the initial working directory, make the console output relative to that to save space.
                    if (outputPathSpan.StartsWith(_initialWorkingDirectory
#if NETCOREAPP
                        .AsSpan()
#endif
                        , FileUtilities.PathComparison))
                    {
                        if (outputPathSpan.Length > _initialWorkingDirectory.Length
                            && (outputPathSpan[_initialWorkingDirectory.Length] == Path.DirectorySeparatorChar
                                || outputPathSpan[_initialWorkingDirectory.Length] == Path.AltDirectorySeparatorChar))
                        {
#if NETCOREAPP
                            outputPathSpan = outputPathSpan.Slice(_initialWorkingDirectory.Length + 1);
#else
                            outputPathSpan = outputPathSpan.Substring(_initialWorkingDirectory.Length + 1);
#endif
                        }
                    }
                }
                else
                {
                    Terminal.WriteLine(string.Empty);
                }


                if (assemblyRun.Messages is not null)
                {
                    foreach (IMessage buildMessage in assemblyRun.Messages)
                    {
                        Terminal.WriteLine($"{DoubleIndentation}{buildMessage.Message}");
                    }
                }

                _buildErrorsCount += countErrors;

                DisplayNodes();
            }
            finally
            {
                Terminal.EndUpdate();
            }
        }
    }

    public void Dispose() => ((IDisposable)_cts).Dispose();

    public void ArtifactAdded(string filePath, string displayName, string? description)
    {

    }

#endregion
}

internal sealed class TestExecutionCompleted
{
    public DateTime Timestamp { get; internal set; }
}

internal enum Outcome
{
    Error,
    Fail,
    Passed,
    Skipped,
    Timeout,
    Cancelled
}
