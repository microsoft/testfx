// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Internal settings for assertion failure behavior.
/// This class is used by the test adapter to communicate settings to the framework.
/// </summary>
internal static class AssertionFailureSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to launch the debugger when an assertion fails.
    /// </summary>
    public static bool LaunchDebuggerOnFailure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to capture a memory dump when an assertion fails.
    /// </summary>
    public static bool CaptureDumpOnFailure { get; set; }

    /// <summary>
    /// Gets or sets the directory where dumps will be written.
    /// </summary>
    public static string? DumpDirectory { get; set; }
}
