// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

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
    /// <returns>Tcm properties.</returns>
    public static IDictionary<TestPlatformObjectModel.TestProperty, object?>? GetTcmProperties(TestPlatformObjectModel.TestCase? testCase)
    {
        // Return empty properties when testCase is null or when test case id is zero.
        if (testCase == null ||
            testCase.GetPropertyValue<int>(EngineConstants.TestCaseIdProperty, default) == 0)
        {
            return null;
        }

        var tcmProperties = new Dictionary<TestPlatformObjectModel.TestProperty, object?>(capacity: 15)
        {
            // Step 1: Add common properties.
            [EngineConstants.TestRunIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestRunIdProperty, default),
            [EngineConstants.TestPlanIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestPlanIdProperty, default),
            [EngineConstants.BuildConfigurationIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.BuildConfigurationIdProperty, default),
            [EngineConstants.BuildDirectoryProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildDirectoryProperty, default),
            [EngineConstants.BuildFlavorProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildFlavorProperty, default),
            [EngineConstants.BuildNumberProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildNumberProperty, default),
            [EngineConstants.BuildPlatformProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildPlatformProperty, default),
            [EngineConstants.BuildUriProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildUriProperty, default),
            [EngineConstants.TfsServerCollectionUrlProperty] = testCase.GetPropertyValue<string>(EngineConstants.TfsServerCollectionUrlProperty, default),
            [EngineConstants.TfsTeamProjectProperty] = testCase.GetPropertyValue<string>(EngineConstants.TfsTeamProjectProperty, default),
            [EngineConstants.IsInLabEnvironmentProperty] = testCase.GetPropertyValue<bool>(EngineConstants.IsInLabEnvironmentProperty, default),

            // Step 2: Add test case specific properties.
            [EngineConstants.TestCaseIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestCaseIdProperty, default),
            [EngineConstants.TestConfigurationIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestConfigurationIdProperty, default),
            [EngineConstants.TestConfigurationNameProperty] = testCase.GetPropertyValue<string>(EngineConstants.TestConfigurationNameProperty, default),
            [EngineConstants.TestPointIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestPointIdProperty, default),
        };

        return tcmProperties;
    }
}
