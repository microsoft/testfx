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

    private void AppendAssemblyLinkTargetFrameworkAndArchitecture(ITerminal terminal)
    {
        terminal.AppendLink(_assembly, lineNumber: null);
        if (_targetFramework == null && _architecture == null)
        {
            return;
        }

        terminal.Append(" (");
        if (_targetFramework != null)
        {
            terminal.Append(_targetFramework);
            if (_architecture != null)
            {
                terminal.Append('|');
            }
        }

        if (_architecture != null)
        {
            terminal.Append(_architecture);
        }

        terminal.Append(')');
    }

    internal /* for testing */ static void AppendStackFrame(ITerminal terminal, string stackTraceLine)
    {
        terminal.Append(DoubleIndentation);
        Match match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
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
}
