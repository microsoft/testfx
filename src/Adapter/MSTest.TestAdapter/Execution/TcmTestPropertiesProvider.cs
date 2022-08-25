// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System.Collections.Generic;
    using TestPlatformObjectModel = Microsoft.VisualStudio.TestPlatform.ObjectModel;

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
        public static IDictionary<TestPlatformObjectModel.TestProperty, object> GetTcmProperties(TestPlatformObjectModel.TestCase testCase)
        {
            var tcmProperties = new Dictionary<TestPlatformObjectModel.TestProperty, object>();

            // Return empty properties when testCase is null or when test case id is zero.
            if (testCase == null ||
                testCase.GetPropertyValue<int>(Constants.TestCaseIdProperty, default(int)) == 0)
            {
                return tcmProperties;
            }

            // Step 1: Add common properties.
            tcmProperties[Constants.TestRunIdProperty] = testCase.GetPropertyValue<int>(Constants.TestRunIdProperty, default(int));
            tcmProperties[Constants.TestPlanIdProperty] = testCase.GetPropertyValue<int>(Constants.TestPlanIdProperty, default(int));
            tcmProperties[Constants.BuildConfigurationIdProperty] = testCase.GetPropertyValue<int>(Constants.BuildConfigurationIdProperty, default(int));
            tcmProperties[Constants.BuildDirectoryProperty] = testCase.GetPropertyValue<string>(Constants.BuildDirectoryProperty, default(string));
            tcmProperties[Constants.BuildFlavorProperty] = testCase.GetPropertyValue<string>(Constants.BuildFlavorProperty, default(string));
            tcmProperties[Constants.BuildNumberProperty] = testCase.GetPropertyValue<string>(Constants.BuildNumberProperty, default(string));
            tcmProperties[Constants.BuildPlatformProperty] = testCase.GetPropertyValue<string>(Constants.BuildPlatformProperty, default(string));
            tcmProperties[Constants.BuildUriProperty] = testCase.GetPropertyValue<string>(Constants.BuildUriProperty, default(string));
            tcmProperties[Constants.TfsServerCollectionUrlProperty] = testCase.GetPropertyValue<string>(Constants.TfsServerCollectionUrlProperty, default(string));
            tcmProperties[Constants.TfsTeamProjectProperty] = testCase.GetPropertyValue<string>(Constants.TfsTeamProjectProperty, default(string));
            tcmProperties[Constants.IsInLabEnvironmentProperty] = testCase.GetPropertyValue<bool>(Constants.IsInLabEnvironmentProperty, default(bool));

            // Step 2: Add test case specific properties.
            tcmProperties[Constants.TestCaseIdProperty] = testCase.GetPropertyValue<int>(Constants.TestCaseIdProperty, default(int));
            tcmProperties[Constants.TestConfigurationIdProperty] = testCase.GetPropertyValue<int>(Constants.TestConfigurationIdProperty, default(int));
            tcmProperties[Constants.TestConfigurationNameProperty] = testCase.GetPropertyValue<string>(Constants.TestConfigurationNameProperty, default(string));
            tcmProperties[Constants.TestPointIdProperty] = testCase.GetPropertyValue<int>(Constants.TestPointIdProperty, default(int));

            return tcmProperties;
        }
    }
}
