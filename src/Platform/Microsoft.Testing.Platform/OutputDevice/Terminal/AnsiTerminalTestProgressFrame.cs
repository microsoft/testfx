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

    public int Width { get; }

    public int Height { get; }

    public List<RenderedProgressItem>? RenderedLines { get; set; }

    public AnsiTerminalTestProgressFrame(int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;
    }

    public void AppendTestWorkerProgress(TestProgressState progress, RenderedProgressItem currentLine, AnsiTerminal terminal)
    {
        string durationString = HumanReadableDurationFormatter.Render(progress.Stopwatch.Elapsed);

        currentLine.RenderedDurationLength = durationString.Length;

        int nonReservedWidth = Width - (durationString.Length + 2);

        int passed = progress.PassedTests;
        int failed = progress.FailedTests;
        int skipped = progress.SkippedTests;
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

        terminal.Append(' ');
        charsTaken++;
        AppendToWidth(terminal, progress.AssemblyName, nonReservedWidth, ref charsTaken);

        if (charsTaken < nonReservedWidth && (progress.TargetFramework != null || progress.Architecture != null))
        {
            int lengthNeeded = 0;

            lengthNeeded++; // for '('
            if (progress.TargetFramework != null)
            {
                lengthNeeded += progress.TargetFramework.Length;
                if (progress.Architecture != null)
                {
                    lengthNeeded++; // for '|'
                }
            }

            if (progress.Architecture != null)
            {
                lengthNeeded += progress.Architecture.Length;
            }

            lengthNeeded++; // for ')'

            if ((charsTaken + lengthNeeded) < nonReservedWidth)
            {
                terminal.Append(" (");
                if (progress.TargetFramework != null)
                {
                    terminal.Append(progress.TargetFramework);
                    if (progress.Architecture != null)
                    {
                        terminal.Append('|');
                    }
                }

                if (progress.Architecture != null)
                {
                    terminal.Append(progress.Architecture);
                }

                terminal.Append(')');
            }
        }

        terminal.SetCursorHorizontal(Width - durationString.Length);
        terminal.Append(durationString);
    }

    public void AppendTestWorkerDetail(TestDetailState detail, RenderedProgressItem currentLine, AnsiTerminal terminal)
    {
        string durationString = HumanReadableDurationFormatter.Render(detail.Stopwatch.Elapsed);

        currentLine.RenderedDurationLength = durationString.Length;

        int nonReservedWidth = Width - (durationString.Length + 2);
        int charsTaken = 0;

        terminal.Append("  ");
        charsTaken += 2;

        AppendToWidth(terminal, detail.Text, nonReservedWidth, ref charsTaken);

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
    public void Render(AnsiTerminalTestProgressFrame previousFrame, TestProgressState?[] progress, AnsiTerminal terminal)
    {
        // Clear everything if Terminal width or height have changed.
        if (Width != previousFrame.Width || Height != previousFrame.Height)
        {
            terminal.EraseProgress();
        }

        // Don't go up if we did not render any lines in previous frame or we already cleared them.
        if (previousFrame.RenderedLines != null && previousFrame.RenderedLines.Count > 0)
        {
            // Move cursor back to 1st line of progress.
            // + 2 because we output and empty line right below.
            terminal.MoveCursorUp(previousFrame.RenderedLines.Count + 2);
        }

        terminal.AppendLine();

        int i = 0;
        RenderedLines = new List<RenderedProgressItem>(progress.Length * 2);
        var progresses = new List<object>(progress.Length);

        foreach (TestProgressState? progressItem in progress)
        {
            if (progressItem == null)
            {
                continue;
            }

            progresses.Add(progressItem);
            if (progressItem.Detail != null)
            {
                progresses.Add(progressItem.Detail);
            }
        }

        foreach (object item in progresses)
        {
            if (previousFrame.RenderedLines != null && previousFrame.RenderedLines.Count > i)
            {
                if (item is TestProgressState progressItem)
                {
                    var currentLine = new RenderedProgressItem(progressItem.Id, progressItem.Version);
                    RenderedLines.Add(currentLine);

                    // We have a line that was rendered previously, compare it and decide how to render.
                    RenderedProgressItem previouslyRenderedLine = previousFrame.RenderedLines[i];
                    if (previouslyRenderedLine.ProgressId == progressItem.Id && previouslyRenderedLine.ProgressVersion == progressItem.Version)
                    {
                        // This is the same progress item and it was not updated since we rendered it, only update the timestamp if possible to avoid flicker.
                        string durationString = HumanReadableDurationFormatter.Render(progressItem.Stopwatch.Elapsed);

                        if (previouslyRenderedLine.RenderedDurationLength == durationString.Length)
                        {
                            // Duration is the same length rewrite just it.
                            terminal.SetCursorHorizontal(MaxColumn);
                            terminal.Append($"{AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(durationString.Length)}{durationString}");
                            currentLine.RenderedDurationLength = durationString.Length;
                        }
                        else
                        {
                            // Duration is not the same length (it is longer because time moves only forward), we need to re-render the whole line
                            // to avoid writing the duration over the last portion of text: my.dll (1s) -> my.d (1m 1s)
                            terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                            AppendTestWorkerProgress(progressItem, currentLine, terminal);
                        }
                    }
                    else
                    {
                        // These lines are different or the line was updated. Render the whole line.
                        terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                        AppendTestWorkerProgress(progressItem, currentLine, terminal);
                    }
                }

                if (item is TestDetailState detailItem)
                {
                    var currentLine = new RenderedProgressItem(detailItem.Id, detailItem.Version);
                    RenderedLines.Add(currentLine);

                    // We have a line that was rendered previously, compare it and decide how to render.
                    RenderedProgressItem previouslyRenderedLine = previousFrame.RenderedLines[i];
                    if (previouslyRenderedLine.ProgressId == detailItem.Id && previouslyRenderedLine.ProgressVersion == detailItem.Version)
                    {
                        // This is the same progress item and it was not updated since we rendered it, only update the timestamp if possible to avoid flicker.
                        string durationString = HumanReadableDurationFormatter.Render(detailItem.Stopwatch.Elapsed);

                        if (previouslyRenderedLine.RenderedDurationLength == durationString.Length)
                        {
                            // Duration is the same length rewrite just it.
                            terminal.SetCursorHorizontal(MaxColumn);
                            terminal.Append($"{AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(durationString.Length)}{durationString}");
                            currentLine.RenderedDurationLength = durationString.Length;
                        }
                        else
                        {
                            // Duration is not the same length (it is longer because time moves only forward), we need to re-render the whole line
                            // to avoid writing the duration over the last portion of text: my.dll (1s) -> my.d (1m 1s)
                            terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                            AppendTestWorkerDetail(detailItem, currentLine, terminal);
                        }
                    }
                    else
                    {
                        // These lines are different or the line was updated. Render the whole line.
                        terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                        AppendTestWorkerDetail(detailItem, currentLine, terminal);
                    }
                }
            }
            else
            {
                // We are rendering more lines than we rendered in previous frame
                if (item is TestProgressState progressItem)
                {
                    var currentLine = new RenderedProgressItem(progressItem.Id, progressItem.Version);
                    RenderedLines.Add(currentLine);
                    AppendTestWorkerProgress(progressItem, currentLine, terminal);
                }

                if (item is TestDetailState detailItem)
                {
                    var currentLine = new RenderedProgressItem(detailItem.Id, detailItem.Version);
                    RenderedLines.Add(currentLine);
                    AppendTestWorkerDetail(detailItem, currentLine, terminal);
                }
            }

            // This makes the progress not stick to the last line on the command line, which is
            // not what I would prefer. But also if someone writes to console, the message will
            // start at the beginning of the new line. Not after the progress bar that is kept on screen.
            terminal.AppendLine();
        }

        // We rendered more lines in previous frame. Clear them.
        if (previousFrame.RenderedLines != null && i < previousFrame.RenderedLines.Count)
        {
            terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        }
    }

    public void Clear() => RenderedLines?.Clear();

    internal class RenderedProgressItem
    {
        public RenderedProgressItem(long id, long version)
        {
            ProgressId = id;
            ProgressVersion = version;
        }

        public long ProgressId { get; }

        public long ProgressVersion { get; }

        public int RenderedHeight { get; set; }

        public int RenderedDurationLength { get; set; }
    }
}
