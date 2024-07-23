// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.Testing.Platform.UI;

/// <summary>
/// Capture states on nodes to be rendered on display.
/// </summary>
internal sealed class ProgressFrame
{
    private const int MaxColumn = 120;

    private readonly (TestWorker TestWorkerProgress, int DurationLength)[] _workers;

    public int Width { get; }

    public int Height { get; }

    public int ProgressCount { get; private set; }

    public ProgressFrame(TestWorker?[] nodes, int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;

        _workers = new (TestWorker, int)[nodes.Length];

        foreach (TestWorker? status in nodes)
        {
            if (status is not null)
            {
                _workers[ProgressCount++].TestWorkerProgress = status;
            }
        }
    }

    public void AppendTestWorkerProgress(int i, AnsiTerminal terminal)
    {
        TestWorker p = _workers[i].TestWorkerProgress;

        string durationString = $" ({p.Stopwatch.Elapsed.TotalSeconds:F1}s)";

        _workers[i].DurationLength = durationString.Length;

        int passed = p.Passed;
        int failed = p.Failed;
        int skipped = p.Skipped;

        string? detail = !RoslynString.IsNullOrWhiteSpace(p.Detail) ? $"- {p.Detail}" : null;
        terminal.Append('[');
        terminal.SetColor(TerminalColor.DarkGreen);
        terminal.Append("✔️");
        terminal.Append(passed.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();

        terminal.Append("/");

        terminal.SetColor(TerminalColor.DarkRed);
        terminal.Append("❌");
        terminal.Append(failed.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();

        terminal.Append("/");

        terminal.SetColor(TerminalColor.DarkYellow);
        terminal.Append("❔");
        terminal.Append(skipped.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();
        terminal.Append(']');

        terminal.Append(' ');
        terminal.Append(p.AssemblyName);
        terminal.Append('(');
        terminal.Append(p.TargetFramework);
        terminal.Append('|');
        terminal.Append(p.Architecture);
        terminal.Append(')');
        if (!RoslynString.IsNullOrWhiteSpace(detail))
        {
            terminal.Append(" - ");
            terminal.Append(detail);
        }

        terminal.SetCursorHorizontal(Width - durationString.Length);
        terminal.Append(durationString);
    }

    /// <summary>
    /// Render VT100 string to update from current to next frame.
    /// </summary>
    public void Render(ProgressFrame previousFrame, AnsiTerminal terminal)
    {
        // Move cursor back to 1st line of progress.
        terminal.MoveCursorUp(previousFrame.ProgressCount + 1);

        int i = 0;
        for (; i < ProgressCount; i++)
        {
            // Do we have previous node string to compare with?
            if (previousFrame.ProgressCount > i)
            {
                if (previousFrame._workers[i] == _workers[i])
                {
                    // Same everything except time, AND same number of digits in time
                    string durationString = $" ({_workers[i].TestWorkerProgress.Stopwatch.Elapsed.TotalSeconds:F1}s)";

                    terminal.SetCursorHorizontal(MaxColumn);
                    terminal.Append($"{AnsiCodes.SetCursorHorizontal(MaxColumn)}{AnsiCodes.MoveCursorBackward(durationString.Length)}{durationString}");
                }
                else
                {
                    // TODO: check components to figure out skips and optimize this
                    terminal.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
                    AppendTestWorkerProgress(i, terminal);
                }
            }
            else
            {
                // From now on we have to simply WriteLine
                AppendTestWorkerProgress(i, terminal);
            }

            // Next line
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
