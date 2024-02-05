// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Configuration;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The configuration section for tests.
/// </summary>
public sealed class TestConfigurationSection : ConfigurationSection
{
    private static readonly ConfigurationProperty DataSourcesValue = new(ConfigurationNames.DataSourcesSectionName, typeof(DataSourceElementCollection), null);

    /// <summary>
    /// Gets the data sources for this configuration section.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.DataSourcesSectionName)]
    public DataSourceElementCollection DataSources => (DataSourceElementCollection)this[DataSourcesValue];

    /// <summary>
    /// Gets the collection of properties.
    /// </summary>
    /// <returns>
    /// The <see cref="System.Configuration.ConfigurationPropertyCollection"/> of properties for the element.
    /// </returns>
    protected override ConfigurationPropertyCollection Properties { get; } = new() { DataSourcesValue };
}
#endif
