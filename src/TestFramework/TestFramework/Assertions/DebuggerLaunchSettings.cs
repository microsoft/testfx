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
    public static bool IsEnabled
    {
        get
        {
            if (_isEnabled.HasValue)
            {
                return _isEnabled.Value;
            }

            // Try to read from configuration
            _isEnabled = ReadFromConfiguration();
            return _isEnabled.Value;
        }
    }

    /// <summary>
    /// Gets the test name filter for debugger launch.
    /// </summary>
    public static string? TestNameFilter
    {
        get
        {
            if (_testNameFilter is not null)
            {
                return _testNameFilter;
            }

            // Initialize by reading the configuration
            _ = IsEnabled; // This will trigger ReadFromConfiguration
            return _testNameFilter;
        }
    }

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
    /// Thread-local storage for TestRunParameters to support runsettings integration.
    /// </summary>
    private static readonly ThreadLocal<Dictionary<string, string>?> ThreadLocalTestRunParameters = new();

    /// <summary>
    /// Registers TestRunParameters from the current TestContext for the current thread.
    /// This should be called by the test framework when a test starts.
    /// </summary>
    /// <param name="testContext">The current test context containing TestRunParameters.</param>
    public static void RegisterTestContext(TestContext testContext)
    {
        if (testContext?.Properties is null)
        {
            return;
        }

        var testRunParameters = new Dictionary<string, string>();
        
        // Extract TestRunParameters from TestContext.Properties
        foreach (System.Collections.DictionaryEntry property in testContext.Properties)
        {
            if (property.Key is string key && property.Value is string value)
            {
                testRunParameters[key] = value;
            }
        }

        ThreadLocalTestRunParameters.Value = testRunParameters;
    }

    /// <summary>
    /// Clears the TestRunParameters for the current thread.
    /// This should be called when a test completes.
    /// </summary>
    public static void UnregisterTestContext()
    {
        ThreadLocalTestRunParameters.Value = null;
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
        ThreadLocalTestRunParameters.Value = null;
    }

    private static bool ReadFromConfiguration()
    {
        bool enabled = false;
        string? filter = null;

#if NETFRAMEWORK
        // Try to read from app.config/web.config
        try
        {
            var section = TestConfiguration.ConfigurationSection;
            if (section is not null)
            {
                enabled = section.LaunchDebuggerOnFailure;
                filter = string.IsNullOrEmpty(section.DebuggerLaunchTestFilter) ? null : section.DebuggerLaunchTestFilter;
            }
        }
        catch
        {
            // Ignore configuration errors
        }
#endif

        // Try to read from TestRunParameters if available
        // This would be set by the test runner when processing .runsettings
        try
        {
            // Look for the setting in TestRunParameters
            // This will be populated by the test execution engine from .runsettings
            var enabledValue = GetTestRunParameter("MSTest.LaunchDebuggerOnFailure");
            if (enabledValue is not null)
            {
                enabled = string.Equals(enabledValue, "true", StringComparison.OrdinalIgnoreCase) ||
                         enabledValue == "1";
            }

            var filterValue = GetTestRunParameter("MSTest.LaunchDebuggerTestFilter");
            if (filterValue is not null)
            {
                filter = filterValue;
            }
        }
        catch
        {
            // Ignore errors when TestRunParameters are not available
        }

        _testNameFilter = filter;
        return enabled;
    }

    private static string? GetTestRunParameter(string parameterName)
    {
        // Try to get from thread-local TestRunParameters first
        var testRunParameters = ThreadLocalTestRunParameters.Value;
        if (testRunParameters is not null && testRunParameters.TryGetValue(parameterName, out string? value))
        {
            return value;
        }

        // Future enhancement: Could also check for other sources of TestRunParameters
        // such as a global registry set by the test runner
        return null;
    }
}