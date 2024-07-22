// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UI;

/// <summary>
/// An <see cref="ITerminal"/> implementation for ANSI/VT100 terminals.
/// </summary>
internal sealed class Terminal : ITerminal
{
    private const int BigUnknownDimension = int.MaxValue;

    /// <summary>
    /// A string buffer used with <see cref="BeginUpdate"/>/<see cref="EndUpdate"/>.
    /// </summary>
    private readonly StringBuilder _outputBuilder = new();

    private readonly IConsole _console;

    /// <summary>
    /// True if <see cref="BeginUpdate"/> was called and <c>Write*</c> methods are buffering instead of directly printing.
    /// </summary>
    private bool _isBuffering;

    /// <inheritdoc/>
    public int Height
        => _console.IsOutputRedirected ? BigUnknownDimension : _console.BufferHeight;

    /// <inheritdoc/>
    public int Width
        => _console.IsOutputRedirected ? BigUnknownDimension : _console.BufferWidth;

    /// <inheritdoc/>
    /// <remarks>
    /// https://github.com/dotnet/msbuild/issues/8958: iTerm2 treats ;9 code to post a notification instead, so disable progress reporting on Mac.
    /// </remarks>
    public bool SupportsProgressReporting { get; } = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public Terminal(IConsole console)
    {
        _console = console;
    }

    /// <inheritdoc/>
    public void BeginUpdate()
    {
        if (_isBuffering)
        {
            throw new InvalidOperationException();
        }

        _isBuffering = true;
    }

    /// <inheritdoc/>
    public void EndUpdate()
    {
        if (!_isBuffering)
        {
            throw new InvalidOperationException();
        }

        _isBuffering = false;

        _console.Write(_outputBuilder.ToString());
        _outputBuilder.Clear();
    }

    /// <inheritdoc/>
    public void Write(string text)
    {
        if (_isBuffering)
        {
            _outputBuilder.Append(text);
        }
        else
        {
            _console.Write(text);
        }
    }

    /// <inheritdoc/>
    public void WriteLine(string text)
    {
        if (_isBuffering)
        {
            _outputBuilder.AppendLine(text);
        }
        else
        {
            _console.WriteLine(text);
        }
    }

    /// <inheritdoc/>
    public void WriteColor(TerminalColor color, string text)
    {
        if (_isBuffering)
        {
            _outputBuilder
                .Append(AnsiCodes.CSI)
                .Append((int)color)
                .Append(AnsiCodes.SetColor)
                .Append(text)
                .Append(AnsiCodes.SetDefaultColor);
        }
        else
        {
            Write(AnsiCodes.Colorize(text, color));
        }
    }

    /// <inheritdoc/>
    public void WriteColorLine(TerminalColor color, string text)
    {
        if (_isBuffering)
        {
            WriteColor(color, text);
            _outputBuilder.AppendLine();
        }
        else
        {
            WriteLine($"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}{text}{AnsiCodes.SetDefaultColor}");
        }
    }
}
