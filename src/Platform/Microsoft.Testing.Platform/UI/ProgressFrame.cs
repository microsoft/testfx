// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace Microsoft.Testing.Platform.UI;

/// <summary>
/// Capture states on nodes to be rendered on display.
/// </summary>
internal sealed class ProgressFrame
{
    private const int MaxColumn = 120;

    private readonly (TestWorkerProgress TestWorkerProgress, int DurationLength)[] _nodes;

    private readonly StringBuilder _renderBuilder = new();

    public int Width { get; }

    public int Height { get; }

    public int ProgressCount { get; private set; }

    public ProgressFrame(TestWorkerProgress?[] nodes, int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;

        _nodes = new (TestWorkerProgress, int)[nodes.Length];

        foreach (TestWorkerProgress? status in nodes)
        {
            if (status is not null)
            {
                _nodes[ProgressCount++].TestWorkerProgress = status;
            }
        }
    }

    internal
#if NETCOREAPP
        ReadOnlySpan<char>
#else
        string
#endif
        RenderTestWorkerProgress(int i)
    {
        TestWorkerProgress p = _nodes[i].TestWorkerProgress;

        string durationString = $"({p.Stopwatch.Elapsed.TotalSeconds:F1}s)";

        _nodes[i].DurationLength = durationString.Length;

        // int buckets = 64;
        // int total = p.Tests;
        int passed = p.Passed;
        int failed = p.Failed;
        int skipped = p.Skipped;

        // Renders progress bar instead of just glyphs and numbers.
        // int remaining = total - passed + failed + skipped;
        // int passedBuckets = AsBucket(total, passed, buckets);
        // int failedBuckets = AsBucket(total, failed, buckets);
        // int skippedBuckets = AsBucket(total, skipped, buckets);
        // int remainingBuckets = buckets - (passedBuckets + failedBuckets + skippedBuckets);
        // string passedRendered = AnsiCodes.Colorize(new string('+', passedBuckets), TerminalColor.Green);
        // string failedRendered = AnsiCodes.Colorize(new string('-', failedBuckets), TerminalColor.Red);
        // string skippedRendered = AnsiCodes.Colorize(new string('?', skippedBuckets), TerminalColor.Yellow);
        // string? remainingRendered = AnsiCodes.Colorize(new string(' ', remainingBuckets), TerminalColor.White);
        string? detail = !RoslynString.IsNullOrWhiteSpace(p.Detail) ? $"- {p.Detail}" : null;
        string passedRendered = AnsiCodes.Colorize("✔️" + passed.ToString(CultureInfo.InvariantCulture), TerminalColor.DarkGreen) + "/";
        string failedRendered = AnsiCodes.Colorize("❌" + failed.ToString(CultureInfo.InvariantCulture), TerminalColor.DarkRed) + "/";
        string skippedRendered = AnsiCodes.Colorize("❔" + skipped.ToString(CultureInfo.InvariantCulture), TerminalColor.DarkYellow);
        string? remainingRendered = null;
        return $"[{passedRendered}{failedRendered}{skippedRendered}{remainingRendered}] {p.AssemblyName} ({p.TargetFramework}|{p.Architecture}){detail}{AnsiCodes.SetCursorHorizontal(Width-durationString.Length)}{durationString}";

        static int AsBucket(int total, int passed, int buckets)
            => (int)Math.Round((decimal)passed / total * buckets, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Render VT100 string to update from current to next frame.
    /// </summary>
    public string Render(ProgressFrame previousFrame)
    {
        StringBuilder sb = _renderBuilder;
        sb.Clear();

        // Move cursor back to 1st line of progress.
        sb.AppendLine(CultureInfo.InvariantCulture, $"{AnsiCodes.CSI}{previousFrame.ProgressCount + 1}{AnsiCodes.MoveUpToLineStart}");

        int i = 0;
        for (; i < ProgressCount; i++)
        {
#if NETCOREAPP
            ReadOnlySpan<char>
#else
            string
#endif
                needed = RenderTestWorkerProgress(i);

            // Do we have previous node string to compare with?
            if (previousFrame.ProgressCount > i)
            {
                if (previousFrame._nodes[i] == _nodes[i])
                {
                    // Same everything except time, AND same number of digits in time
                    string durationString = $"({_nodes[i].TestWorkerProgress.Stopwatch.Elapsed.TotalSeconds:F1}s)";
                    sb.Append(CultureInfo.InvariantCulture, $"{AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(durationString.Length)}{durationString}");
                }
                else
                {
                    // TODO: check components to figure out skips and optimize this
                    sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                    sb.Append(needed);
                }
            }
            else
            {
                // From now on we have to simply WriteLine
                sb.Append(needed);
            }

            // Next line
            sb.AppendLine();
        }

        // clear no longer used lines
        if (i < previousFrame.ProgressCount)
        {
            sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        }

        return sb.ToString();
    }

    public void Clear() => ProgressCount = 0;
}
