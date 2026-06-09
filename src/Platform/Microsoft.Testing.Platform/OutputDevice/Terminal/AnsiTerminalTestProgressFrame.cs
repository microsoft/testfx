// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Captures <see cref="TestProgressState"/> that was rendered to screen, so we can only partially update the screen on next update.
/// </summary>
internal sealed class AnsiTerminalTestProgressFrame
{
    private const int MaxColumn = 250;

    // Pre-computed ANSI escape sequences for the duration-only update hot path.
    // Re-computing them via AnsiCodes methods on every render tick allocates a new string each time.
    private static readonly string SetCursorHorizontalMaxColumn = AnsiCodes.SetCursorHorizontal(MaxColumn);
    private static readonly string[] MoveCursorBackwardCache = CreateMoveCursorBackwardCache();

    private static string[] CreateMoveCursorBackwardCache()
    {
        // Duration strings are at most ~15 chars ("1d 23h 59m 59s" with parens).
        // A cache of 16 slots (indices 0-15) covers every realistic case with zero allocation.
        const int maxLength = 16;
        string[] cache = new string[maxLength];
        cache[0] = string.Empty;
        for (int i = 1; i < maxLength; i++)
        {
            cache[i] = AnsiCodes.MoveCursorBackward(i);
        }

        return cache;
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public List<RenderedProgressItem>? RenderedLines { get; set; }

    public AnsiTerminalTestProgressFrame(int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;
    }

    /// <summary>
    /// Resets this frame for reuse as the next render target, avoiding a heap allocation per render tick.
    /// </summary>
    internal void Reset(int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;
        RenderedLines?.Clear();
    }

    public void AppendTestWorkerProgress(TestProgressState progress, RenderedProgressItem currentLine, AnsiTerminal terminal)
    {
        string durationString = HumanReadableDurationFormatter.Render(progress.Stopwatch.Elapsed);

        currentLine.RenderedDurationLength = durationString.Length;

        int nonReservedWidth = Width - (durationString.Length + 2);

        int discovered = progress.DiscoveredTests;
        int passed = progress.PassedTests;
        int failed = progress.FailedTests;
        int skipped = progress.SkippedTests;
        int charsTaken = 0;

        if (!progress.IsDiscovery)
        {
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
        }
        else
        {
            string discoveredText = discovered.ToString(CultureInfo.CurrentCulture);
            terminal.Append('[');
            charsTaken++;
            terminal.SetColor(TerminalColor.DarkMagenta);
            terminal.Append('+');
            charsTaken++;
            terminal.Append(discoveredText);
            charsTaken += discoveredText.Length;
            terminal.ResetColor();
            terminal.Append(']');
            charsTaken++;
        }

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
        string durationString = HumanReadableDurationFormatter.Render(detail.Stopwatch?.Elapsed);

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

        // At the end of the terminal we're going to print the live progress.
        // We re-render this progress by moving the cursor to the beginning of the previous progress
        // and then overwriting the lines that have changed.
        // The assumption we do here is that:
        // - Each rendered line is a single line, i.e. a single detail cannot span multiple lines.
        // - Each rendered detail can be tracked via a unique ID and version, so that we can
        //   quickly determine if the detail has changed since the last render.

        // Don't go up if we did not render any lines in previous frame or we already cleared them.
        if (previousFrame.RenderedLines != null && previousFrame.RenderedLines.Count > 0)
        {
            // Move cursor back to 1st line of progress.
            // + 2 because we output and empty line right below.
            terminal.MoveCursorUp(previousFrame.RenderedLines.Count + 2);
        }

        // When there is nothing to render, don't write empty lines, e.g. when we start the test run, and then we kick off build
        // in dotnet test, there is a long pause where we have no assemblies and no test results (yet).
        if (progress.Length > 0)
        {
            terminal.AppendLine();
        }

        int i;
        // Reuse the list if it was already cleared by Reset(); only allocate on first use of each frame object.
        RenderedLines ??= [with(progress.Length * 2)];
        List<object> progresses = GenerateLinesToRender(progress);

        for (i = 0; i < progresses.Count; i++)
        {
            object item = progresses[i];

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
                            // Duration is the same length: rewrite just the duration cell.
                            // Use pre-computed escape sequences to avoid per-tick string allocations.
                            int durLen = durationString.Length;
                            terminal.Append(SetCursorHorizontalMaxColumn);
                            terminal.Append(durLen < MoveCursorBackwardCache.Length ? MoveCursorBackwardCache[durLen] : AnsiCodes.MoveCursorBackward(durLen));
                            terminal.Append(durationString);
                            currentLine.RenderedDurationLength = durLen;
                        }
                        else
                        {
                            // Duration is not the same length (it is longer because time moves only forward), we need to re-render the whole line
                            // to avoid writing the duration over the last portion of text: my.dll (1s) -> my.d (1m 1s)
                            terminal.Append(AnsiCodes.CsiEraseInLine);
                            AppendTestWorkerProgress(progressItem, currentLine, terminal);
                        }
                    }
                    else
                    {
                        // These lines are different or the line was updated. Render the whole line.
                        terminal.Append(AnsiCodes.CsiEraseInLine);
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
                        string durationString = HumanReadableDurationFormatter.Render(detailItem.Stopwatch?.Elapsed);

                        if (previouslyRenderedLine.RenderedDurationLength == durationString.Length)
                        {
                            // Duration is the same length: rewrite just the duration cell.
                            // Use pre-computed escape sequences to avoid per-tick string allocations.
                            int durLen = durationString.Length;
                            terminal.Append(SetCursorHorizontalMaxColumn);
                            terminal.Append(durLen < MoveCursorBackwardCache.Length ? MoveCursorBackwardCache[durLen] : AnsiCodes.MoveCursorBackward(durLen));
                            terminal.Append(durationString);
                            currentLine.RenderedDurationLength = durLen;
                        }
                        else
                        {
                            // Duration is not the same length (it is longer because time moves only forward), we need to re-render the whole line
                            // to avoid writing the duration over the last portion of text: my.dll (1s) -> my.d (1m 1s)
                            terminal.Append(AnsiCodes.CsiEraseInLine);
                            AppendTestWorkerDetail(detailItem, currentLine, terminal);
                        }
                    }
                    else
                    {
                        // These lines are different or the line was updated. Render the whole line.
                        terminal.Append(AnsiCodes.CsiEraseInLine);
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
            terminal.Append(AnsiCodes.CsiEraseInDisplay);
        }
    }

    private List<object> GenerateLinesToRender(TestProgressState?[] progress)
    {
        var linesToRender = new List<object>(progress.Length);

        // Note: We want to render the list of active tests, but this can easily fill up the full screen.
        // As such, we should balance the number of active tests shown per project.
        // We do this by distributing the remaining lines for each projects.

        // Collect non-null progress items without LINQ OfType allocation.
        int itemCount = 0;
        for (int j = 0; j < progress.Length; j++)
        {
            if (progress[j] is not null)
            {
                itemCount++;
            }
        }

        var progressItems = new TestProgressState[itemCount];
        int idx = 0;
        for (int j = 0; j < progress.Length; j++)
        {
            if (progress[j] is not null)
            {
                progressItems[idx++] = progress[j]!;
            }
        }

        int linesToDistribute = (int)(Height * 0.7) - 1 - progressItems.Length;
        var detailItems = new List<TestDetailState>[progressItems.Length];

        // Sort indices by detail count ascending to distribute lines fairly,
        // without LINQ Enumerable.Range + OrderBy allocation.
        int[] sortedItemsIndices = new int[progressItems.Length];
        for (int j = 0; j < progressItems.Length; j++)
        {
            sortedItemsIndices[j] = j;
        }

        Array.Sort(sortedItemsIndices, (a, b) => (progressItems[a].TestNodeResultsState?.Count ?? 0).CompareTo(progressItems[b].TestNodeResultsState?.Count ?? 0));

        foreach (int sortedItemIndex in sortedItemsIndices)
        {
            detailItems[sortedItemIndex] = progressItems[sortedItemIndex].TestNodeResultsState?.GetRunningTasks(
                linesToDistribute / progressItems.Length)
                ?? [];
        }

        for (int progressI = 0; progressI < progressItems.Length; progressI++)
        {
            linesToRender.Add(progressItems[progressI]);
            linesToRender.AddRange(detailItems[progressI]);
        }

        return linesToRender;
    }

    public void Clear() => RenderedLines?.Clear();

    internal sealed class RenderedProgressItem
    {
        public RenderedProgressItem(long id, long version)
        {
            ProgressId = id;
            ProgressVersion = version;
        }

        public long ProgressId { get; }

        public long ProgressVersion { get; }

        public int RenderedDurationLength { get; set; }
    }
}
