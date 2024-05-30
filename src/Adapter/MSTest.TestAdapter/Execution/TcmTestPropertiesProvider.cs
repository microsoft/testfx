// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestPlatformObjectModel = Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Reads and parses the TcmTestProperties in order to populate them in TestRunParameters.
/// </summary>
internal static class TcmTestPropertiesProvider
{
    /// <summary>
    /// Gets tcm properties from test case.
    /// </summary>
    /// <param name="testCase">Test case.</param>
    public static void AddTcmProperties(TestPlatformObjectModel.TestCase testCase, IDictionary<string, object?> properties)
    {
        if (testCase == null || testCase.GetPropertyValue<int>(Constants.TestCaseIdProperty, default) == 0)
        {
            return;
        }

        // Step 1: Add common properties.
        properties[Constants.TestRunIdProperty.Id] = testCase.GetPropertyValue<int>(Constants.TestRunIdProperty, default);
        properties[Constants.TestPlanIdProperty.Id] = testCase.GetPropertyValue<int>(Constants.TestPlanIdProperty, default);
        properties[Constants.BuildConfigurationIdProperty.Id] = testCase.GetPropertyValue<int>(Constants.BuildConfigurationIdProperty, default);
        properties[Constants.BuildDirectoryProperty.Id] = testCase.GetPropertyValue<string>(Constants.BuildDirectoryProperty, default);
        properties[Constants.BuildFlavorProperty.Id] = testCase.GetPropertyValue<string>(Constants.BuildFlavorProperty, default);
        properties[Constants.BuildNumberProperty.Id] = testCase.GetPropertyValue<string>(Constants.BuildNumberProperty, default);
        properties[Constants.BuildPlatformProperty.Id] = testCase.GetPropertyValue<string>(Constants.BuildPlatformProperty, default);
        properties[Constants.BuildUriProperty.Id] = testCase.GetPropertyValue<string>(Constants.BuildUriProperty, default);
        properties[Constants.TfsServerCollectionUrlProperty.Id] = testCase.GetPropertyValue<string>(Constants.TfsServerCollectionUrlProperty, default);
        properties[Constants.TfsTeamProjectProperty.Id] = testCase.GetPropertyValue<string>(Constants.TfsTeamProjectProperty, default);
        properties[Constants.IsInLabEnvironmentProperty.Id] = testCase.GetPropertyValue<bool>(Constants.IsInLabEnvironmentProperty, default);

        // Step 2: Add test case specific properties.
        properties[Constants.TestCaseIdProperty.Id] = testCase.GetPropertyValue<int>(Constants.TestCaseIdProperty, default);
        properties[Constants.TestConfigurationIdProperty.Id] = testCase.GetPropertyValue<int>(Constants.TestConfigurationIdProperty, default);
        properties[Constants.TestConfigurationNameProperty.Id] = testCase.GetPropertyValue<string>(Constants.TestConfigurationNameProperty, default);
        properties[Constants.TestPointIdProperty.Id] = testCase.GetPropertyValue<int>(Constants.TestPointIdProperty, default);

        // PERF: initialize the capacity to what we are adding here + what we will add later so we don't have to copy.
        // Please see TestExecutionManager.cs if you are adding properties, so far there are 15. Setting the capacity there
        // helps us to not resize this dictionary later, this is on hot path, it happens for every test.
    }
}
