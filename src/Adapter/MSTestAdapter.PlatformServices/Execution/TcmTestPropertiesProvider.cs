// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestPlatformObjectModel = Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Reads and parses the test-case-management (TCM) properties supplied by a host on a test case, in order to
/// surface them to the running test through <c>TestContext</c>.
/// </summary>
/// <remarks>
/// This is a translation point between the VSTest test-case object model and the neutral execution context bag
/// consumed by the platform services engine. It runs at the adapter boundary (when a host hands the adapter
/// concrete test cases to run) and produces a string-keyed dictionary so the engine never depends on the VSTest
/// <c>TestProperty</c> object model. It is expected to move entirely into the adapter layer once the platform
/// services no longer reference the VSTest object model.
/// </remarks>
internal static class TcmTestPropertiesProvider
{
    /// <summary>
    /// Gets the host execution context properties from a test case, keyed by the host property identifier.
    /// </summary>
    /// <param name="testCase">Test case.</param>
    /// <returns>The properties, or <see langword="null"/> when the test case is null or carries no test case id.</returns>
    public static IReadOnlyDictionary<string, object?>? GetTcmProperties(TestPlatformObjectModel.TestCase? testCase)
    {
        // Return empty properties when testCase is null or when test case id is zero.
        if (testCase == null ||
            testCase.GetPropertyValue<int>(EngineConstants.TestCaseIdProperty, default) == 0)
        {
            return null;
        }

        var tcmProperties = new Dictionary<string, object?>(capacity: 15)
        {
            // Step 1: Add common properties.
            [EngineConstants.TestRunIdProperty.Id] = testCase.GetPropertyValue<int>(EngineConstants.TestRunIdProperty, default),
            [EngineConstants.TestPlanIdProperty.Id] = testCase.GetPropertyValue<int>(EngineConstants.TestPlanIdProperty, default),
            [EngineConstants.BuildConfigurationIdProperty.Id] = testCase.GetPropertyValue<int>(EngineConstants.BuildConfigurationIdProperty, default),
            [EngineConstants.BuildDirectoryProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.BuildDirectoryProperty, default),
            [EngineConstants.BuildFlavorProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.BuildFlavorProperty, default),
            [EngineConstants.BuildNumberProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.BuildNumberProperty, default),
            [EngineConstants.BuildPlatformProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.BuildPlatformProperty, default),
            [EngineConstants.BuildUriProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.BuildUriProperty, default),
            [EngineConstants.TfsServerCollectionUrlProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.TfsServerCollectionUrlProperty, default),
            [EngineConstants.TfsTeamProjectProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.TfsTeamProjectProperty, default),
            [EngineConstants.IsInLabEnvironmentProperty.Id] = testCase.GetPropertyValue<bool>(EngineConstants.IsInLabEnvironmentProperty, default),

            // Step 2: Add test case specific properties.
            [EngineConstants.TestCaseIdProperty.Id] = testCase.GetPropertyValue<int>(EngineConstants.TestCaseIdProperty, default),
            [EngineConstants.TestConfigurationIdProperty.Id] = testCase.GetPropertyValue<int>(EngineConstants.TestConfigurationIdProperty, default),
            [EngineConstants.TestConfigurationNameProperty.Id] = testCase.GetPropertyValue<string>(EngineConstants.TestConfigurationNameProperty, default),
            [EngineConstants.TestPointIdProperty.Id] = testCase.GetPropertyValue<int>(EngineConstants.TestPointIdProperty, default),
        };

        return tcmProperties;
    }
}
