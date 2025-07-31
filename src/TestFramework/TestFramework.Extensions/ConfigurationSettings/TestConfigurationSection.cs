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
    private static readonly ConfigurationProperty LaunchDebuggerOnFailureValue = new(ConfigurationNames.LaunchDebuggerOnFailureAttributeName, typeof(bool), false);
    private static readonly ConfigurationProperty DebuggerLaunchTestFilterValue = new(ConfigurationNames.DebuggerLaunchTestFilterAttributeName, typeof(string), string.Empty);

    /// <summary>
    /// Gets the data sources for this configuration section.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.DataSourcesSectionName)]
    public DataSourceElementCollection DataSources => (DataSourceElementCollection)this[DataSourcesValue];

    /// <summary>
    /// Gets a value indicating whether debugger should be launched on test failure.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.LaunchDebuggerOnFailureAttributeName, DefaultValue = false)]
    public bool LaunchDebuggerOnFailure => (bool)this[LaunchDebuggerOnFailureValue];

    /// <summary>
    /// Gets the test name filter for debugger launch.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.DebuggerLaunchTestFilterAttributeName, DefaultValue = "")]
    public string DebuggerLaunchTestFilter => (string)this[DebuggerLaunchTestFilterValue];

    /// <summary>
    /// Gets the collection of properties.
    /// </summary>
    /// <returns>
    /// The <see cref="ConfigurationPropertyCollection"/> of properties for the element.
    /// </returns>
    protected override ConfigurationPropertyCollection Properties { get; } = [DataSourcesValue, LaunchDebuggerOnFailureValue, DebuggerLaunchTestFilterValue];
}
#endif
