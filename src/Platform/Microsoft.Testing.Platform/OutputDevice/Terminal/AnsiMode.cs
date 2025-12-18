// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Specifies the ANSI escape sequence mode.
/// </summary>
internal enum AnsiMode
{
    /// <summary>
    /// Auto-detect terminal capabilities for ANSI support.
    /// </summary>
    Auto,

    /// <summary>
    /// Force enable ANSI escape sequences.
    /// </summary>
    Enable,

    /// <summary>
    /// Force disable ANSI escape sequences.
    /// </summary>
    Disable,
}
