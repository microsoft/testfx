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

        int passed = p.PassedTests;
        int failed = p.FailedTests;
        int skipped = p.SkippedTests;

        string? detail = !RoslynString.IsNullOrWhiteSpace(p.Detail) ? $"- {p.Detail}" : null;
        terminal.Append('[');
        terminal.SetColor(TerminalColor.DarkGreen);
        terminal.Append('✓');
        terminal.Append(passed.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();

        terminal.Append('/');

        terminal.SetColor(TerminalColor.DarkRed);
        terminal.Append('x');
        terminal.Append(failed.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();

        terminal.Append('/');

        terminal.SetColor(TerminalColor.DarkYellow);
        terminal.Append('↓');
        terminal.Append(skipped.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();
        terminal.Append(']');

        terminal.Append(' ');
        terminal.Append(p.AssemblyName);

        AppendTargetFrameworkAndArchitecture(terminal, p);

        if (!RoslynString.IsNullOrWhiteSpace(detail))
        {
            terminal.Append(" - ");
            terminal.Append(detail);
        }

        terminal.SetCursorHorizontal(Width - durationString.Length);
        terminal.Append(durationString);
    }

    private static void AppendTargetFrameworkAndArchitecture(AnsiTerminal terminal, TestProgressState p)
    {
        if (p.TargetFramework != null || p.Architecture != null)
        {
            terminal.Append(" (");
            if (p.TargetFramework != null)
            {
                terminal.Append(p.TargetFramework);
                terminal.Append('|');
            }

            if (p.Architecture != null)
            {
                terminal.Append(p.Architecture);
            }

            terminal.Append(')');
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
                    // Same everything except time, AND same number of digits in time
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
