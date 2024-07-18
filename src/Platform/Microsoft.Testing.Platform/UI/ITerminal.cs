// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

/// <summary>
/// An abstraction of a terminal, built specifically to fit the <see cref="ConsoleLogger"/> needs.
/// </summary>
internal interface ITerminal : IDisposable
{
    /// <summary>
    /// Gets width of the terminal buffer.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets height of the terminal buffer.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets a value indicating whether <see langword="true"/> if the terminal emulator supports progress reporting.
    /// </summary>
    bool SupportsProgressReporting { get; }

    /// <summary>
    /// Starts buffering the text passed via the <c>Write*</c> methods.
    /// </summary>
    /// <remarks>
    /// Upon calling this method, the terminal should be buffering all output internally until <see cref="EndUpdate"/> is called.
    /// </remarks>
    void BeginUpdate();

    /// <summary>
    /// Flushes the text buffered between <see cref="BeginUpdate"/> was called and now into the output.
    /// </summary>
    void EndUpdate();

    /// <summary>
    /// Writes a string to the output. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void Write(string text);

#if NETCOREAPP
    /// <summary>
    /// Writes a string to the output. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void Write(ReadOnlySpan<char> text);
#endif

    /// <summary>
    /// Writes a string to the output. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteLine(string text);

#if NETCOREAPP
    /// <summary>
    /// Writes a string to the output, truncating it if it wouldn't fit on one screen line.
    /// Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteLineFitToWidth(ReadOnlySpan<char> text);
#endif

    /// <summary>
    /// Writes a string to the output using the given color. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteColor(TerminalColor color, string text);

    /// <summary>
    /// Writes a string to the output using the given color. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteColorLine(TerminalColor color, string text);
}
