// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System.Configuration;

    /// <summary>
    /// The configuration section for tests.
    /// </summary>
    public sealed class TestConfigurationSection : ConfigurationSection
    {
        private static readonly ConfigurationProperty DataSourcesValue = new(ConfigurationNames.DataSourcesSectionName, typeof(DataSourceElementCollection), null);
        private static ConfigurationPropertyCollection properties;

        static TestConfigurationSection()
        {
            properties = new ConfigurationPropertyCollection();
            properties.Add(DataSourcesValue);
        }

        /// <summary>
        /// Gets the data sources for this configuration section.
        /// </summary>
        [ConfigurationProperty(ConfigurationNames.DataSourcesSectionName)]
        public DataSourceElementCollection DataSources => (DataSourceElementCollection)this[DataSourcesValue];

        /// <summary>
        /// Gets the collection of properties.
        /// </summary>
        /// <returns>
        /// The <see cref="T:System.Configuration.ConfigurationPropertyCollection"/> of properties for the element.
        /// </returns>
        protected override ConfigurationPropertyCollection Properties => properties;
    }
}
