// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Captures <see cref="TestProgressState"/> that was rendered to screen, so we can only partially update the screen on next update.
/// </summary>
internal sealed class AnsiTerminalTestProgressFrame
{
    private const int MaxColumn = 250;

    private readonly (TestProgressState TestProgressState, int DurationLength)[] _progressItems;

    public int Width { get; }

    public int Height { get; }

    public int ProgressCount { get; private set; }

    public AnsiTerminalTestProgressFrame(TestProgressState?[] nodes, int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;

        _progressItems = new (TestProgressState, int)[nodes.Length];

        foreach (TestProgressState? status in nodes)
        {
            if (status is not null)
            {
                _progressItems[ProgressCount++].TestProgressState = status;
            }
        }
    }

    public void AppendTestWorkerProgress(int i, AnsiTerminal terminal)
    {
        TestProgressState p = _progressItems[i].TestProgressState;

        string durationString = HumanReadableDurationFormatter.Render(p.Stopwatch.Elapsed);

        _progressItems[i].DurationLength = durationString.Length;

        int nonReservedWidth = Width - (durationString.Length + 2);

        int passed = p.PassedTests;
        int failed = p.FailedTests;
        int skipped = p.SkippedTests;
        int charsTaken = 0;

        terminal.Append('[');
        charsTaken++;
        terminal.SetColor(TerminalColor.DarkGreen);
        terminal.Append('✓');
        charsTaken++;
        string passedText = passed.ToString(CultureInfo.CurrentCulture);
        terminal.Append(passedText);
        charsTaken += passedText.Length;
        terminal.ResetColor();

        terminal.Append('/');
        charsTaken++;

        terminal.SetColor(TerminalColor.DarkRed);
        terminal.Append('x');
        charsTaken++;
        string failedText = failed.ToString(CultureInfo.CurrentCulture);
        terminal.Append(failedText);
        charsTaken += failedText.Length;
        terminal.ResetColor();

        terminal.Append('/');
        charsTaken++;

        terminal.SetColor(TerminalColor.DarkYellow);
        terminal.Append('↓');
        charsTaken++;
        string skippedText = skipped.ToString(CultureInfo.CurrentCulture);
        terminal.Append(skippedText);
        charsTaken += skippedText.Length;
        terminal.ResetColor();
        terminal.Append(']');
        charsTaken++;

        // -5 because we want to output at least 1 char from the name, and ' ...' followed by duration
        terminal.Append(' ');
        charsTaken++;
        AppendToWidth(terminal, p.AssemblyName, nonReservedWidth, ref charsTaken);

        if (charsTaken < nonReservedWidth && (p.TargetFramework != null || p.Architecture != null))
        {
            int lengthNeeded = 0;

            lengthNeeded++; // for '('
            if (p.TargetFramework != null)
            {
                lengthNeeded += p.TargetFramework.Length;
                if (p.Architecture != null)
                {
                    lengthNeeded++; // for '|'
                }
            }

            if (p.Architecture != null)
            {
                lengthNeeded += p.Architecture.Length;
            }

            lengthNeeded++; // for ')'

            if ((charsTaken + lengthNeeded) < nonReservedWidth)
            {
                terminal.Append(" (");
                if (p.TargetFramework != null)
                {
                    terminal.Append(p.TargetFramework);
                    if (p.Architecture != null)
                    {
                        terminal.Append('|');
                    }
                }

                if (p.Architecture != null)
                {
                    terminal.Append(p.Architecture);
                }

                terminal.Append(')');
            }
        }

        if (!RoslynString.IsNullOrWhiteSpace(p.Detail))
        {
            terminal.Append(" - ");
            terminal.Append(p.Detail);
        }

        terminal.SetCursorHorizontal(Width - durationString.Length);
        terminal.Append(durationString);
    }

    private static void AppendToWidth(AnsiTerminal terminal, string text, int width, ref int charsTaken)
    {
        if (charsTaken + text.Length < width)
        {
            terminal.Append(text);
            charsTaken += text.Length;
        }
        else
        {
            terminal.Append("...");
            charsTaken += 3;
            if (charsTaken < width)
            {
                int charsToTake = width - charsTaken;
                string cutText = text[^charsToTake..];
                terminal.Append(cutText);
                charsTaken += charsToTake;
            }
        }
    }

    /// <summary>
    /// Render VT100 string to update from current to next frame.
    /// </summary>
    public void Render(AnsiTerminalTestProgressFrame previousFrame, AnsiTerminal terminal)
    {
        // Don't go up if we did not render progress in previous frame or we cleared it.
        if (previousFrame.ProgressCount > 0)
        {
            // Move cursor back to 1st line of progress.
            // +2 because we prepend 1 empty line before the progress
            // and new line after the progress indicator.
            terminal.MoveCursorUp(previousFrame.ProgressCount + 2);
        }

        terminal.AppendLine();

        int i = 0;
        for (; i < ProgressCount; i++)
        {
            // Optimize the rendering. When we have previous frame to compare with, we can decide to rewrite only part of the screen,
            // rather than deleting whole line and have the line flicker. Most commonly this will rewrite just the time part of the line.
            if (previousFrame.ProgressCount > i)
            {
                if (previousFrame._progressItems[i].TestProgressState.LastUpdate != _progressItems[i].TestProgressState.LastUpdate)
                {
                    // Same everything except time.
                    string durationString = HumanReadableDurationFormatter.Render(_progressItems[i].TestProgressState.Stopwatch.Elapsed);

                    if (previousFrame._progressItems[i].DurationLength == durationString.Length)
                    {
                        terminal.SetCursorHorizontal(MaxColumn);
                        terminal.Append($"{AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(durationString.Length)}{durationString}");
                        _progressItems[i].DurationLength = durationString.Length;
                    }
                    else
                    {
                        // Render full line.
                        terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                        AppendTestWorkerProgress(i, terminal);
                    }
                }
                else
                {
                    // Render full line.
                    terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                    AppendTestWorkerProgress(i, terminal);
                }
            }
            else
            {
                // From now on we have to simply WriteLine
                AppendTestWorkerProgress(i, terminal);
            }

            // This makes the progress not stick to the last line on the command line, which is
            // not what I would prefer. But also if someone writes to console, the message will
            // start at the beginning of the new line. Not after the progress bar that is kept on screen.
            terminal.AppendLine();
        }

        // clear no longer used lines
        if (i < previousFrame.ProgressCount)
        {
            terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        }
    }

    public void Clear() => ProgressCount = 0;
}
