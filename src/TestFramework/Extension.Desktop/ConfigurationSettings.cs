// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Configuration;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Support for configuration settings for Tests.
    /// </summary>
    public static class TestConfiguration
    {
        private static TestConfigurationSection configurationSection = LoadConfiguration();

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

        /// <summary>
        /// Gets the configuration section for tests.
        /// </summary>
        public static TestConfigurationSection ConfigurationSection => configurationSection;
    }

    /// <summary>
    /// The configuration section for tests.
    /// </summary>
    public sealed class TestConfigurationSection : ConfigurationSection
    {
        private static ConfigurationPropertyCollection properties;
        private static readonly ConfigurationProperty dataSources;

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static TestConfigurationSection()
        {
            dataSources = new ConfigurationProperty(ConfigurationNames.DataSourcesSectionName, typeof(DataSourceElementCollection), null);
            properties = new ConfigurationPropertyCollection();
            properties.Add(dataSources);
        }

        /// <summary>
        /// Gets the collection of properties.
        /// </summary>
        /// <returns>
        /// The <see cref="T:System.Configuration.ConfigurationPropertyCollection"/> of properties for the element.
        /// </returns>
        protected override ConfigurationPropertyCollection Properties => properties;

        /// <summary>
        /// The data sources for this configuration section.
        /// </summary>
        [ConfigurationProperty(ConfigurationNames.DataSourcesSectionName)]
        public DataSourceElementCollection DataSources => (DataSourceElementCollection)base[dataSources];
    }

    /// <summary>
    /// The Data source element collection.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1010:CollectionsShouldImplementGenericInterface")]
    public sealed class DataSourceElementCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DataSourceElementCollection() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>
        /// Creates a new <see cref="DataSourceElement"/>.
        /// </summary>
        /// <returns>A new <see cref="DataSourceElement"/>.</returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new DataSourceElement();
        }

        /// <summary>
        /// Gets the element key for a specified configuration element.
        /// </summary>
        /// <param name="element">The System.Configuration.ConfigurationElement to return the key for.</param>
        /// <returns>An System.Object that acts as the key for the specified System.Configuration.ConfigurationElement.</returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            DataSourceElement dataSource = (DataSourceElement)element;
            return dataSource.Key;
        }

        /// <summary>
        /// Adds a configuration element to the configuration element collection.
        /// </summary>
        /// <param name="element">The System.Configuration.ConfigurationElement to add.</param>
        public void Add(DataSourceElement element)
        {
            base.BaseAdd(element, false);
        }

        /// <summary>
        /// Removes a System.Configuration.ConfigurationElement from the collection.
        /// </summary>
        /// <param name="element">The <see cref="DataSourceElement"/> .</param>
        public void Remove(DataSourceElement element)
        {
            if (base.BaseIndexOf(element) >= 0)
            {
                base.BaseRemove(element.Key);
            }
        }

        /// <summary>
        /// Removes a System.Configuration.ConfigurationElement from the collection.
        /// </summary>
        /// <param name="name">The key of the System.Configuration.ConfigurationElement to remove.</param>
        public void Remove(string name)
        {
            base.BaseRemove(name);
        }

        /// <summary>
        /// Removes all configuration element objects from the collection.
        /// </summary>
        public void Clear()
        {
            base.BaseClear();
        }

        /// <summary>
        /// Returns the configuration element with the specified key.
        /// </summary>
        /// <param name="name">The key of the element to return.</param>
        /// <returns>The System.Configuration.ConfigurationElement with the specified key; otherwise, null.</returns>
        public new DataSourceElement this[string name]
        {
            get
            {
                return (DataSourceElement)base.BaseGet(name);
            }
        }

        /// <summary>
        /// Gets the configuration element at the specified index location.
        /// </summary>
        /// <param name="index">The index location of the System.Configuration.ConfigurationElement to return.</param>
        public DataSourceElement this[int index]
        {
            get
            {
                return (DataSourceElement)base.BaseGet(index);
            }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                this.BaseAdd(index, value);
            }
        }

        /// <summary>
        /// Adds a configuration element to the configuration element collection.
        /// </summary>
        /// <param name="element">The System.Configuration.ConfigurationElement to add.</param>
        protected override void BaseAdd(ConfigurationElement element)
        {
            base.BaseAdd(element, false);
        }

        /// <summary>
        /// Adds a configuration element to the configuration element collection.
        /// </summary>
        /// <param name="index">The index location at which to add the specified System.Configuration.ConfigurationElement.</param>
        /// <param name="element">The System.Configuration.ConfigurationElement to add.</param>
        protected override void BaseAdd(int index, ConfigurationElement element)
        {
            if (index == -1)
            {
                base.BaseAdd(element, false);
            }
            else
            {
                base.BaseAdd(index, element);
            }
        }
    }

    /// <summary>
    /// The Data Source element.
    /// </summary>
    public sealed class DataSourceElement : ConfigurationElement
    {
        private static ConfigurationPropertyCollection properties;
        private static readonly ConfigurationProperty name;
        private static readonly ConfigurationProperty connectionString;
        private static readonly ConfigurationProperty dataTableName;
        private static readonly ConfigurationProperty dataAccessMethod;

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static DataSourceElement()
        {
            name = new ConfigurationProperty(ConfigurationNames.NameAttributeName, typeof(string), "", ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired);
            connectionString = new ConfigurationProperty(ConfigurationNames.ConnectionStringAttributeName, typeof(string), "", ConfigurationPropertyOptions.IsRequired);
            dataTableName = new ConfigurationProperty(ConfigurationNames.DataTableAttributeName, typeof(string), "", ConfigurationPropertyOptions.IsRequired);
            dataAccessMethod = new ConfigurationProperty(ConfigurationNames.DataAccessMethodAttributeName, typeof(string), "");
            properties = new ConfigurationPropertyCollection();
            properties.Add(name);
            properties.Add(connectionString);
            properties.Add(dataAccessMethod);
            properties.Add(dataTableName);
        }

        /// <summary>
        /// Gets the configuration properties.
        /// </summary>
        protected override ConfigurationPropertyCollection Properties => properties;

        /// <summary>
        /// Gets the name of this configuration.
        /// </summary>
        [ConfigurationProperty(ConfigurationNames.NameAttributeName, IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string)base[name]; }
            set { base[name] = value; }
        }

        /// <summary>
        /// Refers to ConnectionStringSettings element in &lt;connectionStrings&gt; section in the .config file.
        /// </summary>
        [ConfigurationProperty(ConfigurationNames.ConnectionStringAttributeName, IsRequired = true)]
        public string ConnectionString
        {
            get { return (string)base[connectionString]; }
            set { base[connectionString] = value; }
        }

        /// <summary>
        /// Gets the name of the data table.
        /// </summary>
        [ConfigurationProperty(ConfigurationNames.DataTableAttributeName, IsRequired = true)]
        public string DataTableName
        {
            get { return (string)base[dataTableName]; }
            set { base[dataTableName] = value; }
        }

        /// <summary>
        /// Gets the type of data access.
        /// </summary>
        [ConfigurationProperty(ConfigurationNames.DataAccessMethodAttributeName, DefaultValue = "")]
        public string DataAccessMethod
        {
            get { return (string)base[dataAccessMethod]; }
            set { base[dataAccessMethod] = value; }
        }

        internal string Key
        {
            get { return this.Name; }
        }
    }

    /// <summary>
    /// Contains literals for names of sections, properties, attributes.
    /// </summary>
    internal static class ConfigurationNames
    {
        public const string SectionName = "microsoft.visualstudio.testtools";
        public const string Beta2SectionName = "microsoft.visualstudio.qualitytools";   // We keep this for Beta2 compatibility.
        public const string DataSourcesSectionName = "dataSources";
        public const string NameAttributeName = "name";
        public const string ConnectionStringAttributeName = "connectionString";
        public const string DataAccessMethodAttributeName = "dataAccessMethod";
        public const string DataTableAttributeName = "dataTableName";
    }
}
