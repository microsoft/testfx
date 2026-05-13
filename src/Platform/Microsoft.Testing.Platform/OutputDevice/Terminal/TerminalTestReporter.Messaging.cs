// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    internal void WriteErrorMessage(string text, int? padding)
        => WriteMessage(text, TerminalColor.DarkRed, padding);

    internal void WriteWarningMessage(string text, int? padding)
        => WriteMessage(text, TerminalColor.DarkYellow, padding);

    internal void WriteErrorMessage(Exception exception)
        => WriteErrorMessage(exception.ToString(), padding: null);

    public void WriteMessage(string text, SystemConsoleColor? color = null, int? padding = null)
        => WriteMessage(text, color is not null ? ToTerminalColor(color.ConsoleColor) : null, padding);

    private void WriteMessage(string text, TerminalColor? color = null, int? padding = null)
        => _terminalWithProgress.WriteToTerminal(terminal =>
        {
            if (color.HasValue)
            {
                terminal.SetColor(color.Value);
            }

            if (padding == null)
            {
                terminal.AppendLine(text);
            }
            else
            {
                AppendIndentedLine(terminal, text, new string(' ', padding.Value));
            }

            if (color.HasValue)
            {
                terminal.ResetColor();
            }
        });

    public void TestInProgress(
        string testNodeUid,
        string displayName)
    {
        if (_testProgressState is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        TestProgressState asm = _testProgressState;
        if (_options.ShowActiveTests)
        {
            asm.TestNodeResultsState ??= new(Interlocked.Increment(ref _counter));
            asm.TestNodeResultsState.AddRunningTestNode(
                Interlocked.Increment(ref _counter), testNodeUid, MakeControlCharactersVisible(displayName, true), CreateStopwatch());
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    private static TerminalColor ToTerminalColor(ConsoleColor consoleColor)
        => consoleColor switch
        {
            ConsoleColor.Black => TerminalColor.Black,
            ConsoleColor.DarkBlue => TerminalColor.DarkBlue,
            ConsoleColor.DarkGreen => TerminalColor.DarkGreen,
            ConsoleColor.DarkCyan => TerminalColor.DarkCyan,
            ConsoleColor.DarkRed => TerminalColor.DarkRed,
            ConsoleColor.DarkMagenta => TerminalColor.DarkMagenta,
            ConsoleColor.DarkYellow => TerminalColor.DarkYellow,
            ConsoleColor.DarkGray => TerminalColor.DarkGray,
            ConsoleColor.Gray => TerminalColor.Gray,
            ConsoleColor.Blue => TerminalColor.Blue,
            ConsoleColor.Green => TerminalColor.Green,
            ConsoleColor.Cyan => TerminalColor.Cyan,
            ConsoleColor.Red => TerminalColor.Red,
            ConsoleColor.Magenta => TerminalColor.Magenta,
            ConsoleColor.Yellow => TerminalColor.Yellow,
            ConsoleColor.White => TerminalColor.White,
            _ => TerminalColor.Default,
        };
}
