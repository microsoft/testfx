// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Wraps the static System.Console to be isolatable in tests.
/// </summary>
internal interface IConsole
{
    event ConsoleCancelEventHandler? CancelKeyPress;

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public int BufferHeight { get; }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public int BufferWidth { get; }

    public bool IsOutputRedirected { get; }

    void SetForegroundColor(ConsoleColor color);

    ConsoleColor GetForegroundColor();

    void WriteLine();

    void WriteLine(string? value);

    void Write(string? value);

    void Write(char value);

    void Clear();
}
