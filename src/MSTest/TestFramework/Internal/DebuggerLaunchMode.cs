// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies when the debugger should be launched on test failure.
/// </summary>
internal enum DebuggerLaunchMode
{
    /// <summary>
    /// Never launch the debugger on test failure.
    /// </summary>
    Disabled,

    /// <summary>
    /// Always launch the debugger on test failure.
    /// </summary>
    Enabled,

    /// <summary>
    /// Launch the debugger on test failure only when not running in a CI environment.
    /// </summary>
    EnabledExcludingCI,
}
