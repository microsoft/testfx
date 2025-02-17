// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemConsole : IConsole
{
    private const int WriteBufferSize = 256;
    private static readonly StreamWriter CaptureConsoleOutWriter;

    internal static TextWriter ConsoleOut { get; }

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
    /// Gets a value indicating whether output has been redirected from the standard output stream.
    /// </summary>
    public bool IsOutputRedirected => Console.IsOutputRedirected;

    private bool _suppressOutput;

    static SystemConsole()
    {
        // This is the console that the ITerminal will be writing to.
        // So, this is what NonAnsiTerminal need to "lock" on regardless of whether it changed later.
        ConsoleOut = Console.Out;
        // From https://github.com/dotnet/runtime/blob/main/src/libraries/System.Console/src/System/Console.cs#L236
        CaptureConsoleOutWriter = new StreamWriter(
            stream: Console.OpenStandardOutput(),
            encoding: ConsoleOut.Encoding,
            bufferSize: WriteBufferSize,
            leaveOpen: true)
        {
            AutoFlush = true,
        };
    }

    // This is based on the UnsupportedOSPlatformAttribute on the API:
    // https://github.com/dotnet/runtime/blob/8a79637e1454c751c24a984b7fb8af4c4d43394b/src/libraries/System.Console/src/System/Console.cs#L560-L563
    [SupportedOSPlatformGuard("android")]
    [SupportedOSPlatformGuard("ios")]
    [SupportedOSPlatformGuard("tvos")]
    [SupportedOSPlatformGuard("browser")]
    private static bool IsCancelKeyPressNotSupported()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));

    // This is based on the UnsupportedOSPlatformAttribute on the API:
    // https://github.com/dotnet/runtime/blob/d6cfa2039309c1d5cef630e95eb5274a556dc726/src/libraries/System.Console/src/System/Console.cs#L334-L337
    [SupportedOSPlatformGuard("android")]
    [SupportedOSPlatformGuard("ios")]
    [SupportedOSPlatformGuard("tvos")]
    [SupportedOSPlatformGuard("browser")]
    private static bool IsForegroundColorNotSupported()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));

    // This is based on the UnsupportedOSPlatformAttribute on the API:
    // https://github.com/dotnet/runtime/blob/d6cfa2039309c1d5cef630e95eb5274a556dc726/src/libraries/System.Console/src/System/Console.cs#L537-L539
    [SupportedOSPlatformGuard("android")]
    [SupportedOSPlatformGuard("ios")]
    [SupportedOSPlatformGuard("tvos")]
    private static bool IsClearNotSupported()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS"));

    public event ConsoleCancelEventHandler? CancelKeyPress
    {
        add
        {
            if (IsCancelKeyPressNotSupported())
            {
                return;
            }

            Console.CancelKeyPress += value;
        }

        remove
        {
            if (IsCancelKeyPressNotSupported())
            {
                return;
            }

            Console.CancelKeyPress -= value;
        }
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

    public void SetForegroundColor(ConsoleColor color)
    {
        if (IsForegroundColorNotSupported())
        {
            return;
        }

        Console.ForegroundColor = color;
    }

    public ConsoleColor GetForegroundColor()
        => IsForegroundColorNotSupported() ? ConsoleColor.Black : Console.ForegroundColor;

    public void Clear()
    {
        if (IsClearNotSupported())
        {
            return;
        }

        Console.Clear();
    }
}
