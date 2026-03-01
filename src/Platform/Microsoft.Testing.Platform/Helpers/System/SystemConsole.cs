// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemConsole : IConsole
{
    private const int WriteBufferSize = 256;
    private static readonly StreamWriter CaptureConsoleOutWriter;

    /// <summary>
    /// Gets the height of the buffer area.
    /// </summary>
    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public int BufferHeight => Console.BufferHeight;

    /// <summary>
    /// Gets the width of the buffer area.
    /// </summary>
    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public int BufferWidth => Console.BufferWidth;

    /// <summary>
    /// Gets the height of the console window area.
    /// </summary>
    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public int WindowHeight => Console.WindowHeight;

    /// <summary>
    /// Gets the width of the console window area.
    /// </summary>
    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public int WindowWidth => Console.WindowWidth;

    /// <summary>
    /// Gets a value indicating whether output has been redirected from the standard output stream.
    /// </summary>
    public bool IsOutputRedirected => Console.IsOutputRedirected;

    private bool _suppressOutput;

    static SystemConsole()
        // From https://github.com/dotnet/runtime/blob/main/src/libraries/System.Console/src/System/Console.cs#L236
        => CaptureConsoleOutWriter = new StreamWriter(
            stream: Console.OpenStandardOutput(),
            encoding: Console.Out.Encoding,
            bufferSize: WriteBufferSize,
            leaveOpen: true)
        {
            AutoFlush = true,
        };

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("browser")]
    public event ConsoleCancelEventHandler? CancelKeyPress
    {
        add => Console.CancelKeyPress += value;
        remove => Console.CancelKeyPress -= value;
    }

    public void SuppressOutput() => _suppressOutput = true;

    public void WriteLine()
    {
        if (!_suppressOutput)
        {
            CaptureConsoleOutWriter.WriteLine();
        }
    }

    public void WriteLine(string? value)
    {
        if (!_suppressOutput)
        {
            CaptureConsoleOutWriter.WriteLine(value);
        }
    }

    public void Write(string? value)
    {
        if (!_suppressOutput)
        {
            CaptureConsoleOutWriter.Write(value);
        }
    }

    public void Write(char value)
    {
        if (!_suppressOutput)
        {
            CaptureConsoleOutWriter.Write(value);
        }
    }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("browser")]
    public void SetForegroundColor(ConsoleColor color)
        => Console.ForegroundColor = color;

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("browser")]
    public ConsoleColor GetForegroundColor()
        => Console.ForegroundColor;

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public void Clear()
        => Console.Clear();
}
