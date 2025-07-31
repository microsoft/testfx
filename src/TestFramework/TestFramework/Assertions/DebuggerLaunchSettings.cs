// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Configuration;
#endif

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Settings for controlling debugger launch on test failure.
/// </summary>
internal static class DebuggerLaunchSettings
{
    private static bool? _isEnabled;
    private static string? _testNameFilter;

    /// <summary>
    /// Gets a value indicating whether debugger launch on failure is enabled.
    /// </summary>
    public static bool IsEnabled => _isEnabled ?? ReadFromConfiguration();

    /// <summary>
    /// Gets the test name filter for debugger launch.
    /// </summary>
    public static string? TestNameFilter => _testNameFilter ?? ReadFilterFromConfiguration();

    /// <summary>
    /// Determines if the debugger should be launched for the current test.
    /// </summary>
    /// <returns>True if debugger should be launched, false otherwise.</returns>
    public static bool ShouldLaunchForCurrentTest()
    {
        if (!IsEnabled)
        {
            return false;
        }

        // If no filter is specified, launch for all tests
        if (string.IsNullOrWhiteSpace(TestNameFilter))
        {
            return true;
        }

        // TODO: Implement test name filtering when we have access to current test context
        // For now, return true if any filter is specified
        return true;
    }

    /// <summary>
    /// Allows setting the configuration programmatically (primarily for testing).
    /// </summary>
    /// <param name="enabled">Whether debugger launch is enabled.</param>
    /// <param name="testNameFilter">Optional test name filter.</param>
    internal static void SetConfiguration(bool enabled, string? testNameFilter = null)
    {
        _isEnabled = enabled;
        _testNameFilter = testNameFilter;
    }

    /// <summary>
    /// Resets the cached configuration values and clears thread-local storage.
    /// </summary>
    internal static void Reset()
    {
        _isEnabled = null;
        _testNameFilter = null;
    }

    private static bool ReadFromConfiguration()
    {
        // Fallback to environment variable for backward compatibility
        string? launchDebugger = Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE");
        return launchDebugger == "1";
    }

    private static string? ReadFilterFromConfiguration()
    {
        // Fallback to environment variable for backward compatibility
        return Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER");
    }
}