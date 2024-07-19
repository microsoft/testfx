// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

/// <summary>
/// Enumerates the text colors supported by VT100 terminal.
/// </summary>
internal enum TerminalColor
{
    /// <summary>
    /// Black.
    /// </summary>
    Black = 30,

    /// <summary>
    /// Red.
    /// </summary>
    DarkRed = 31,

    /// <summary>
    /// Green.
    /// </summary>
    DarkGreen = 32,

    /// <summary>
    /// Yellow.
    /// </summary>
    DarkYellow = 33,

    /// <summary>
    /// Blue.
    /// </summary>
    DarkBlue = 34,

    /// <summary>
    /// Magenta.
    /// </summary>
    DarkMagenta = 35,

    /// <summary>
    /// Cyan.
    /// </summary>
    DarkCyan = 36,

    /// <summary>
    /// White.
    /// </summary>
    DarkWhite = 37,

    /// <summary>
    /// Default.
    /// </summary>
    Default = 39,

    // Bright black
    Gray = 90,
    Red = 91,
    Green = 92,
    Yellow = 93,
    Blue = 94,
    Magenta = 95,
    Cyan = 96,
    White = 97,
}
