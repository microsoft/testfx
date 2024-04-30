// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The Data source element collection.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1010:CollectionsShouldImplementGenericInterface", Justification = "Compat")]
public sealed class DataSourceElementCollection : ConfigurationElementCollection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceElementCollection"/> class.
    /// </summary>
    public DataSourceElementCollection()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Returns the configuration element with the specified key.
    /// </summary>
    /// <param name="name">The key of the element to return.</param>
    /// <returns>The System.Configuration.ConfigurationElement with the specified key; otherwise, null.</returns>
    public new DataSourceElement this[string name] => (DataSourceElement)BaseGet(name);

    /// <summary>
    /// Gets the configuration element at the specified index location.
    /// </summary>
    /// <param name="index">The index location of the System.Configuration.ConfigurationElement to return.</param>
    public DataSourceElement this[int index]
    {
        get => (DataSourceElement)BaseGet(index);

        set
        {
            if (BaseGet(index) != null)
            {
                BaseRemoveAt(index);
            }

            BaseAdd(index, value);
        }
    }

    /// <summary>
    /// Adds a configuration element to the configuration element collection.
    /// </summary>
    /// <param name="element">The System.Configuration.ConfigurationElement to add.</param>
    public void Add(DataSourceElement element) => BaseAdd(element, false);

    /// <summary>
    /// Removes a System.Configuration.ConfigurationElement from the collection.
    /// </summary>
    /// <param name="element">The <see cref="DataSourceElement"/> .</param>
    public void Remove(DataSourceElement element)
    {
        if (BaseIndexOf(element) >= 0)
        {
            BaseRemove(element.Key);
        }
    }

    /// <summary>
    /// Removes a System.Configuration.ConfigurationElement from the collection.
    /// </summary>
    /// <param name="name">The key of the System.Configuration.ConfigurationElement to remove.</param>
    public void Remove(string name) => BaseRemove(name);

    /// <summary>
    /// Removes all configuration element objects from the collection.
    /// </summary>
    public void Clear() => BaseClear();

    /// <summary>
    /// Creates a new <see cref="DataSourceElement"/>.
    /// </summary>
    /// <returns>A new <see cref="DataSourceElement"/>.</returns>
    protected override ConfigurationElement CreateNewElement() => new DataSourceElement();

    /// <summary>
    /// Gets the element key for a specified configuration element.
    /// </summary>
    /// <param name="element">The System.Configuration.ConfigurationElement to return the key for.</param>
    /// <returns>An System.Object that acts as the key for the specified System.Configuration.ConfigurationElement.</returns>
    protected override object GetElementKey(ConfigurationElement element)
    {
        var dataSource = (DataSourceElement)element;
        return dataSource.Key;
    }

    /// <summary>
    /// Adds a configuration element to the configuration element collection.
    /// </summary>
    /// <param name="element">The System.Configuration.ConfigurationElement to add.</param>
    protected override void BaseAdd(ConfigurationElement element) => BaseAdd(element, false);

    /// <summary>
    /// Adds a configuration element to the configuration element collection.
    /// </summary>
    /// <param name="index">The index location at which to add the specified System.Configuration.ConfigurationElement.</param>
    /// <param name="element">The System.Configuration.ConfigurationElement to add.</param>
    protected override void BaseAdd(int index, ConfigurationElement element)
    {
        if (index == -1)
        {
            BaseAdd(element, false);
        }
        else
        {
            base.BaseAdd(index, element);
        }
    }
}
#endif
