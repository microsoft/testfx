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
    public static IDictionary<TestPlatformObjectModel.TestProperty, object?> GetTcmProperties(TestPlatformObjectModel.TestCase? testCase)
    {
        var tcmProperties = new Dictionary<TestPlatformObjectModel.TestProperty, object?>();

        // Return empty properties when testCase is null or when test case id is zero.
        if (testCase == null ||
            testCase.GetPropertyValue<int>(EngineConstants.TestCaseIdProperty, default) == 0)
        {
            return tcmProperties;
        }

        // Step 1: Add common properties.
        tcmProperties[EngineConstants.TestRunIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestRunIdProperty, default);
        tcmProperties[EngineConstants.TestPlanIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestPlanIdProperty, default);
        tcmProperties[EngineConstants.BuildConfigurationIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.BuildConfigurationIdProperty, default);
        tcmProperties[EngineConstants.BuildDirectoryProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildDirectoryProperty, default);
        tcmProperties[EngineConstants.BuildFlavorProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildFlavorProperty, default);
        tcmProperties[EngineConstants.BuildNumberProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildNumberProperty, default);
        tcmProperties[EngineConstants.BuildPlatformProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildPlatformProperty, default);
        tcmProperties[EngineConstants.BuildUriProperty] = testCase.GetPropertyValue<string>(EngineConstants.BuildUriProperty, default);
        tcmProperties[EngineConstants.TfsServerCollectionUrlProperty] = testCase.GetPropertyValue<string>(EngineConstants.TfsServerCollectionUrlProperty, default);
        tcmProperties[EngineConstants.TfsTeamProjectProperty] = testCase.GetPropertyValue<string>(EngineConstants.TfsTeamProjectProperty, default);
        tcmProperties[EngineConstants.IsInLabEnvironmentProperty] = testCase.GetPropertyValue<bool>(EngineConstants.IsInLabEnvironmentProperty, default);

        // Step 2: Add test case specific properties.
        tcmProperties[EngineConstants.TestCaseIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestCaseIdProperty, default);
        tcmProperties[EngineConstants.TestConfigurationIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestConfigurationIdProperty, default);
        tcmProperties[EngineConstants.TestConfigurationNameProperty] = testCase.GetPropertyValue<string>(EngineConstants.TestConfigurationNameProperty, default);
        tcmProperties[EngineConstants.TestPointIdProperty] = testCase.GetPropertyValue<int>(EngineConstants.TestPointIdProperty, default);

        return tcmProperties;
    }
}
