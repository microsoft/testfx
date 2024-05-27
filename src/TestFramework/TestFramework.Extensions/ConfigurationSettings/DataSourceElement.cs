// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Configuration;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The Data Source element.
/// </summary>
public sealed class DataSourceElement : ConfigurationElement
{
    private static readonly ConfigurationProperty NameValue = new(ConfigurationNames.NameAttributeName, typeof(string), string.Empty, ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired);
    private static readonly ConfigurationProperty ConnectionStringValue = new(ConfigurationNames.ConnectionStringAttributeName, typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);
    private static readonly ConfigurationProperty DataTableNameValue = new(ConfigurationNames.DataTableAttributeName, typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);
    private static readonly ConfigurationProperty DataAccessMethodValue = new(ConfigurationNames.DataAccessMethodAttributeName, typeof(string), string.Empty);

    private static readonly ConfigurationPropertyCollection SharedProperties =
    [
        NameValue,
        ConnectionStringValue,
        DataAccessMethodValue,
        DataTableNameValue
    ];

    /// <summary>
    /// Gets or sets the name of this configuration.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.NameAttributeName, IsKey = true, IsRequired = true)]
    public string Name
    {
        get => (string)this[NameValue];
        set => this[NameValue] = value;
    }

    /// <summary>
    /// Gets or sets the ConnectionStringSettings element in &lt;connectionStrings&gt; section in the .config file.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.ConnectionStringAttributeName, IsRequired = true)]
    public string ConnectionString
    {
        get => (string)this[ConnectionStringValue];
        set => this[ConnectionStringValue] = value;
    }

    /// <summary>
    /// Gets or sets the name of the data table.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.DataTableAttributeName, IsRequired = true)]
    public string DataTableName
    {
        get => (string)this[DataTableNameValue];
        set => this[DataTableNameValue] = value;
    }

    /// <summary>
    /// Gets or sets the type of data access.
    /// </summary>
    [ConfigurationProperty(ConfigurationNames.DataAccessMethodAttributeName, DefaultValue = "")]
    public string DataAccessMethod
    {
        get => (string)this[DataAccessMethodValue];
        set => this[DataAccessMethodValue] = value;
    }

    /// <summary>
    /// Gets the key name.
    /// </summary>
    internal string Key => Name;

    /// <summary>
    /// Gets the configuration properties.
    /// </summary>
    protected override ConfigurationPropertyCollection Properties => SharedProperties;
}
#endif
