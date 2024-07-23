// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Console;

/// <summary>
/// Non-ANSI terminal that writes text using the standard Console.Foreground color capabilities to stay compatible with
/// standard Windows command line, and other command lines that are not capable of ANSI, or when output is redirected.
/// </summary>
internal class NonAnsiTerminal : ITerminal
{
    private readonly IConsole _console;
    private readonly ConsoleColor _defaultForegroundColor;
    private readonly StringBuilder _stringBuilder = new();
    private bool _isBatching;

    public NonAnsiTerminal(IConsole console)
    {
        _console = console;
        _defaultForegroundColor = _console.GetForegroundColor();
    }

    public int Width => _console.IsOutputRedirected ? int.MaxValue : _console.BufferWidth;

    public int Height => _console.IsOutputRedirected ? int.MaxValue : _console.BufferHeight;

    public void Append(char value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value.ToString());
        }
    }

    public void Append(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    public void AppendLine()
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine();
        }
        else
        {
            _console.WriteLine();
        }
    }

    public void AppendLine(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine(value);
        }
        else
        {
            _console.WriteLine(value);
        }
    }

    public void AppendLink(string path, int? lineNumber)
    {
        Append(path);
        if (lineNumber.HasValue)
        {
            Append($":{lineNumber}");
        }
    }

    public void SetColor(TerminalColor color)
    {
        if (_isBatching)
        {
            _console.Write(_stringBuilder.ToString());
            _stringBuilder.Clear();
        }

        _console.SetForegroundColor(ToConsoleColor(color));
    }

    public void ResetColor()
    {
        if (_isBatching)
        {
            _console.Write(_stringBuilder.ToString());
            _stringBuilder.Clear();
        }

        _console.SetForegroundColor(_defaultForegroundColor);
    }

    public void ShowCursor()
    {
        // nop
    }

    public void HideCursor()
    {
        // nop
    }

    public void StartUpdate()
    {
        if (_isBatching)
        {
            throw new InvalidOperationException("Console is already in batching mode.");
        }

        _stringBuilder.Clear();
        _isBatching = true;
    }

    public void StopUpdate()
    {
        _console.Write(_stringBuilder.ToString());
        _isBatching = false;
    }

    private ConsoleColor ToConsoleColor(TerminalColor color) => color switch
    {
        TerminalColor.Black => ConsoleColor.Black,
        TerminalColor.DarkRed => ConsoleColor.DarkRed,
        TerminalColor.DarkGreen => ConsoleColor.DarkGreen,
        TerminalColor.DarkYellow => ConsoleColor.DarkYellow,
        TerminalColor.DarkBlue => ConsoleColor.DarkBlue,
        TerminalColor.DarkMagenta => ConsoleColor.DarkMagenta,
        TerminalColor.DarkCyan => ConsoleColor.DarkCyan,
        TerminalColor.DarkWhite => ConsoleColor.White,
        TerminalColor.Default => _defaultForegroundColor,
        TerminalColor.Gray => ConsoleColor.Gray,
        TerminalColor.Red => ConsoleColor.Red,
        TerminalColor.Green => ConsoleColor.Green,
        TerminalColor.Yellow => ConsoleColor.Yellow,
        TerminalColor.Blue => ConsoleColor.Blue,
        TerminalColor.Magenta => ConsoleColor.Magenta,
        TerminalColor.Cyan => ConsoleColor.Cyan,
        TerminalColor.White => ConsoleColor.White,
        _ => _defaultForegroundColor,
    };

    public void EraseProgress()
    {
        // nop
    }

    public void RenderProgress(TestWorker?[] progress)
    {
        StartUpdate();
        try
        {
            foreach (TestWorker? p in progress)
            {
                if (p == null)
                {
                    continue;
                }

                string durationString = $" ({p.Stopwatch.Elapsed.TotalSeconds:F1}s)";

                int passed = p.Passed;
                int failed = p.Failed;
                int skipped = p.Skipped;

                string? detail = !RoslynString.IsNullOrWhiteSpace(p.Detail) ? $"- {p.Detail}" : null;
                Append('[');
                SetColor(TerminalColor.DarkGreen);
                Append("✔️");
                Append(passed.ToString(CultureInfo.CurrentCulture));
                ResetColor();

                Append("/");

                SetColor(TerminalColor.DarkRed);
                Append("❌");
                Append(failed.ToString(CultureInfo.CurrentCulture));
                ResetColor();

                Append("/");

                SetColor(TerminalColor.DarkYellow);
                Append("❔");
                Append(skipped.ToString(CultureInfo.CurrentCulture));
                ResetColor();
                Append(']');

                Append(' ');
                Append(p.AssemblyName);
                Append('(');
                Append(p.TargetFramework);
                Append('|');
                Append(p.Architecture);
                Append(')');
                if (!RoslynString.IsNullOrWhiteSpace(detail))
                {
                    Append(" - ");
                    Append(detail);
                }

                Append(durationString);

                AppendLine();
            }

            AppendLine();
        }
        finally
        {
            StopUpdate();
        }
    }

    public void StartBusyIndicator()
    {
        // nop
    }

    public void StopBusyIndicator()
    {
        // nop
    }
}
