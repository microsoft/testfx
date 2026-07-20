// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed partial class AnsiTerminalTestProgressFrame
{
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
        AnsiTerminalTextLayoutHelper.AppendToWidth(terminal, progress.AssemblyName, nonReservedWidth, ref charsTaken);

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

        AnsiTerminalTextLayoutHelper.AppendToWidth(terminal, detail.Text, nonReservedWidth, ref charsTaken);

        terminal.SetCursorHorizontal(Width - durationString.Length);
        terminal.Append(durationString);
    }

    private void AppendProgressMessage(TerminalProgressMessageState message, RenderedProgressItem currentLine, AnsiTerminal terminal)
    {
        currentLine.RenderedDurationLength = 0;
        int availableWidth = Math.Max(0, Width - 1);
        if (availableWidth > 0)
        {
            AnsiTerminalTextLayoutHelper.AppendProgressMessageToWidth(terminal, message.Text, availableWidth);
        }
    }
}
