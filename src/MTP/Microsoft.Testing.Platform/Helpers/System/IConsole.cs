// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Wraps the static System.Console to be isolatable in tests.
/// </summary>
internal interface IConsole
{
    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    event ConsoleCancelEventHandler? CancelKeyPress;

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    int BufferHeight { get; }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    int BufferWidth { get; }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    int WindowHeight { get; }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    int WindowWidth { get; }

    bool IsOutputRedirected { get; }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    void SetForegroundColor(ConsoleColor color);

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    ConsoleColor GetForegroundColor();

    void WriteLine();

    void WriteLine(string? value);

    void Write(string? value);

    /// <summary>
    /// Writes the contents of <paramref name="value"/> without materializing them into a <see cref="string"/> first.
    /// </summary>
    /// <remarks>
    /// This exists so that batched terminal output (which is accumulated in a <see cref="StringBuilder"/> and can grow
    /// to several kilobytes) does not have to allocate a fresh string on every flush.
    /// </remarks>
    void Write(StringBuilder value);

    /// <summary>
    /// Writes the contents of <paramref name="value"/> to the output with a single write call to the underlying stream,
    /// so that output from other writers is not interleaved with it.
    /// </summary>
    /// <remarks>
    /// Used by the scroll-region progress renderer, whose frames save the cursor, draw outside the scroll region,
    /// and restore the cursor. Output from another writer landing between those steps would be drawn at the
    /// wrong position. This is best effort: the operating system can still split a single write call, for example
    /// when a write to a Unix terminal or pipe is only partially accepted and the rest is written by a further call.
    /// </remarks>
    void WriteAtomically(StringBuilder value);

    void Write(char value);

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    void Clear();
}
