// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Specifies the activation mode for command line options that support auto-detection or explicit on/off states.
/// </summary>
internal enum AutoOnOff
{
    /// <summary>
    /// Auto-detect the appropriate setting.
    /// </summary>
    Auto,

    /// <summary>
    /// Force enable the option.
    /// </summary>
    On,

    /// <summary>
    /// Force disable the option.
    /// </summary>
    Off,
}
