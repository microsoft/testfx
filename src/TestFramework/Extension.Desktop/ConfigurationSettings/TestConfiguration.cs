// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System.Configuration;

    /// <summary>
    /// Support for configuration settings for Tests.
    /// </summary>
    public static class TestConfiguration
    {
        private static readonly TestConfigurationSection ConfigurationSectionValue = LoadConfiguration();

        /// <summary>
        /// Gets the configuration section for tests.
        /// </summary>
        public static TestConfigurationSection ConfigurationSection => ConfigurationSectionValue;

        private static TestConfigurationSection LoadConfiguration()
        {
            TestConfigurationSection configSection =
                (TestConfigurationSection)ConfigurationManager.GetSection(ConfigurationNames.SectionName);

            // If could not find RTM section, try Beta2 section name.
            if (configSection == null)
            {
                configSection = (TestConfigurationSection)ConfigurationManager.GetSection(ConfigurationNames.Beta2SectionName);
            }

            if (configSection == null)
            {
                return new TestConfigurationSection();
            }
            else
            {
                return configSection;
            }
        }
    }
}
