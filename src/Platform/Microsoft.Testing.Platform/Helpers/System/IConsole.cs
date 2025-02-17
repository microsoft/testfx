// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Wraps the static System.Console to be isolatable in tests.
/// </summary>
internal interface IConsole
{
    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    event ConsoleCancelEventHandler? CancelKeyPress;

    [SupportedOSPlatform("Windows")]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    int BufferHeight { get; }

    [SupportedOSPlatform("Windows")]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    int BufferWidth { get; }

    bool IsOutputRedirected { get; }

    [SupportedOSPlatform("Windows")]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    void SetForegroundColor(ConsoleColor color);

    [SupportedOSPlatform("Windows")]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    ConsoleColor GetForegroundColor();

    void WriteLine();

    void WriteLine(string? value);

    void Write(string? value);

    void Write(char value);

    [SupportedOSPlatform("Windows")]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    void Clear();
}
