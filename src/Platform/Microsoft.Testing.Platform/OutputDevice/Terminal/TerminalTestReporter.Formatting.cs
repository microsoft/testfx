// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
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
        terminal.AppendLine(TerminalResources.Expected);
        AppendIndentedLine(terminal, expected, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(TerminalResources.Actual);
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

    private static void FormatStandardAndErrorOutput(ITerminal terminal, string? standardOutput, string? errorOutput)
    {
        bool hasStdOut = !RoslynString.IsNullOrWhiteSpace(standardOutput);
        bool hasStdErr = !RoslynString.IsNullOrWhiteSpace(errorOutput);
        if (!hasStdOut && !hasStdErr)
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);

        if (hasStdOut)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(TerminalResources.StandardOutput);
            string? standardOutputWithoutSpecialChars = MakeControlCharactersVisible(standardOutput, normalizeWhitespaceCharacters: false);
            AppendIndentedLine(terminal, standardOutputWithoutSpecialChars, DoubleIndentation);
        }

        if (hasStdErr)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(TerminalResources.StandardError);
            string? standardErrorWithoutSpecialChars = MakeControlCharactersVisible(errorOutput, normalizeWhitespaceCharacters: false);
            AppendIndentedLine(terminal, standardErrorWithoutSpecialChars, DoubleIndentation);
        }

        terminal.ResetColor();
    }

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(ITerminal terminal, TestProgressState assemblyRun)
        => AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture);

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(ITerminal terminal, string assembly, string? targetFramework, string? architecture)
    {
        terminal.AppendLink(assembly, lineNumber: null);
        if (targetFramework == null && architecture == null)
        {
            return;
        }

        terminal.Append(" (");
        if (targetFramework != null)
        {
            terminal.Append(targetFramework);
            if (architecture != null)
            {
                terminal.Append('|');
            }
        }

        if (architecture != null)
        {
            terminal.Append(architecture);
        }

        terminal.Append(')');
    }

    internal /* for testing */ static void AppendStackFrame(ITerminal terminal, string stackTraceLine)
    {
        terminal.Append(DoubleIndentation);
        Match match;
        try
        {
            match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
        }
        catch (RegexMatchTimeoutException)
        {
            terminal.AppendLine(stackTraceLine);
            return;
        }

        if (!match.Success)
        {
            terminal.AppendLine(stackTraceLine);
            return;
        }

        bool weHaveFilePathAndCodeLine = !RoslynString.IsNullOrWhiteSpace(match.Groups["code"].Value);
        terminal.Append(TerminalResources.StackFrameAt);
        terminal.Append(' ');

        if (weHaveFilePathAndCodeLine)
        {
            terminal.Append(match.Groups["code"].Value);
        }
        else
        {
            terminal.Append(match.Groups["code1"].Value);
        }

        if (!weHaveFilePathAndCodeLine)
        {
            terminal.AppendLine();
            return;
        }

        terminal.Append(' ');
        terminal.Append(TerminalResources.StackFrameIn);
        terminal.Append(' ');
        if (!RoslynString.IsNullOrWhiteSpace(match.Groups["file"].Value))
        {
            int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
            terminal.AppendLink(match.Groups["file"].Value, line);

            // AppendLink finishes by resetting color
            terminal.SetColor(TerminalColor.DarkGray);
        }

        terminal.AppendLine();
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

    /// <summary>
    /// Renders the per-assembly summary line (link + result + compact counts + duration). Used by the
    /// <c>dotnet test</c> orchestrator (<see cref="TerminalTestReporterOptions.ShowAssembly"/>); the in-process
    /// host renders a single run-level summary instead.
    /// </summary>
    private static void AppendAssemblySummary(TestProgressState assemblyRun, ITerminal terminal)
    {
        terminal.ResetColor();

        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyRun);
        terminal.Append(' ');
        AppendAssemblyResult(terminal, assemblyRun);
        terminal.Append(' ');
        AppendAssemblyTestCounts(terminal, assemblyRun);
        terminal.Append(' ');
        AppendLongDuration(terminal, assemblyRun.Stopwatch.Elapsed);
        terminal.AppendLine();
    }

    private static void AppendAssemblyResult(ITerminal terminal, TestProgressState state)
    {
        if (!state.Success)
        {
            terminal.SetColor(TerminalColor.DarkRed);

            // If the assembly failed, we print one of three red strings.
            string text = (state.FailedTests > 0, state.TotalTests == 0) switch
            {
                (true, _) => string.Format(CultureInfo.CurrentCulture, TerminalResources.FailedWithErrors, state.FailedTests),
                (false, true) => TerminalResources.ZeroTestsRan,
                (false, false) => TerminalResources.FailedLowercase,
            };
            terminal.Append(text);
            terminal.ResetColor();
        }
        else
        {
            terminal.SetColor(TerminalColor.DarkGreen);
            terminal.Append(TerminalResources.PassedLowercase);
            terminal.ResetColor();
        }
    }

    /// <summary>
    /// Renders a compact, per-assembly counts block that mirrors the in-progress indicator. Full-ANSI terminals get
    /// "[✓P/xF/↓S]" (with optional "/rR"); simple terminals (NoAnsi / SimpleAnsi / CI) get the ASCII "[+P/xF/?S]"
    /// form so logs stay font- and encoding-friendly.
    /// </summary>
    private static void AppendAssemblyTestCounts(ITerminal terminal, TestProgressState assemblyRun)
    {
        int failed = assemblyRun.FailedTests;
        int passed = assemblyRun.PassedTests;
        int skipped = assemblyRun.SkippedTests;
        int retried = assemblyRun.RetriedFailedTests;

        bool unicode = terminal is AnsiTerminal;
        char passedGlyph = unicode ? '✓' : '+';
        char skippedGlyph = unicode ? '↓' : '?';

        terminal.Append('[');

        AppendGlyphCount(terminal, passedGlyph, passed, TerminalColor.DarkGreen);
        terminal.Append('/');
        AppendGlyphCount(terminal, 'x', failed, TerminalColor.DarkRed);
        terminal.Append('/');
        AppendGlyphCount(terminal, skippedGlyph, skipped, TerminalColor.DarkYellow);

        if (retried > 0)
        {
            terminal.Append('/');
            AppendGlyphCount(terminal, 'r', retried, TerminalColor.Gray);
        }

        terminal.Append(']');
    }

    private static void AppendGlyphCount(ITerminal terminal, char glyph, int count, TerminalColor color)
    {
        terminal.SetColor(color);
        terminal.Append(glyph);
        terminal.Append(count.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();
    }
}
