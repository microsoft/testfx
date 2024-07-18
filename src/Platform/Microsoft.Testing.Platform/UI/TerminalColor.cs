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
    Red = 31,

    /// <summary>
    /// Green.
    /// </summary>
    Green = 32,

    /// <summary>
    /// Yellow.
    /// </summary>
    Yellow = 33,

    /// <summary>
    /// Blue.
    /// </summary>
    Blue = 34,

    /// <summary>
    /// Magenta.
    /// </summary>
    Magenta = 35,

    /// <summary>
    /// Cyan.
    /// </summary>
    Cyan = 36,

    /// <summary>
    /// White.
    /// </summary>
    White = 37,

    /// <summary>
    /// Default.
    /// </summary>
    Default = 39,

    Gray = 37,
    DarkRed = 31,
}
