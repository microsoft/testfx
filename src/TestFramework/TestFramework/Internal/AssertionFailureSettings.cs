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
    /// Gets or sets a value specifying when to launch the debugger on assertion failure.
    /// </summary>
    public static DebuggerLaunchMode LaunchDebuggerOnAssertionFailure { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked immediately before an assertion failure is reported.
    /// </summary>
    public static Action<string, string?, string?>? CaptureDiagnosticsOnFailure { get; set; }

    /// <summary>
    /// Invokes the configured assertion failure diagnostics callback while the failing caller is still on the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CaptureDiagnostics(string message, string? expected, string? actual)
        => CaptureDiagnosticsOnFailure?.Invoke(message, expected, actual);
}
