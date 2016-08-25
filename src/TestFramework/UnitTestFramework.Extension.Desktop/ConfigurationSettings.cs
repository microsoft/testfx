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
        private static TestConfigurationSection m_configurationSection = LoadConfiguration();

        private static TestConfigurationSection LoadConfiguration()
        {
            TestConfigurationSection configurationSection =
                (TestConfigurationSection)ConfigurationManager.GetSection(ConfigurationNames.SectionName);

            // If could not find RTM section, try Beta2 section name.
            if (configurationSection == null)
            {
                configurationSection = (TestConfigurationSection)ConfigurationManager.GetSection(ConfigurationNames.Beta2SectionName);
            }

            if (configurationSection == null)
            {
                return new TestConfigurationSection();
            }
            else
            {
                return configurationSection;
            }
        }

        public static TestConfigurationSection ConfigurationSection
        {
            get
            {
                return m_configurationSection;
            }
        }
    }

    public sealed class TestConfigurationSection : ConfigurationSection
    {
        private static ConfigurationPropertyCollection m_properties;
        private static readonly ConfigurationProperty m_dataSources;

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static TestConfigurationSection()
        {
            m_dataSources = new ConfigurationProperty(ConfigurationNames.DataSourcesSectionName, typeof(DataSourceElementCollection), null);
            m_properties = new ConfigurationPropertyCollection();
            m_properties.Add(m_dataSources);
        }

        public TestConfigurationSection()
        {
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return m_properties; }
        }

        [ConfigurationProperty(ConfigurationNames.DataSourcesSectionName)]
        public DataSourceElementCollection DataSources
        {
            get
            {
                return (DataSourceElementCollection)base[m_dataSources];
            }
        }
    }

    [SuppressMessage("Microsoft.Design", "CA1010:CollectionsShouldImplementGenericInterface")]
    public sealed class DataSourceElementCollection : ConfigurationElementCollection
    {
        public DataSourceElementCollection() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new DataSourceElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            DataSourceElement dataSource = (DataSourceElement)element;
            return dataSource.Key;
        }

        public void Add(DataSourceElement element)
        {
            base.BaseAdd(element, false);
        }

        public void Remove(DataSourceElement element)
        {
            if (base.BaseIndexOf(element) >= 0)
            {
                base.BaseRemove(element.Key);
            }
        }

        public void Remove(string name)
        {
            base.BaseRemove(name);
        }

        public void Clear()
        {
            base.BaseClear();
        }

        public new DataSourceElement this[string name]
        {
            get
            {
                return (DataSourceElement)base.BaseGet(name);
            }
        }

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

        protected override void BaseAdd(ConfigurationElement element)
        {
            base.BaseAdd(element, false);
        }

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

    public sealed class DataSourceElement : ConfigurationElement
    {
        private static ConfigurationPropertyCollection m_properties;
        private static readonly ConfigurationProperty m_name;
        private static readonly ConfigurationProperty m_connectionString;
        private static readonly ConfigurationProperty m_dataTableName;
        private static readonly ConfigurationProperty m_dataAccessMethod;

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static DataSourceElement()
        {
            m_name = new ConfigurationProperty(ConfigurationNames.NameAttributeName, typeof(string), "", ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired);
            m_connectionString = new ConfigurationProperty(ConfigurationNames.ConnectionStringAttributeName, typeof(string), "", ConfigurationPropertyOptions.IsRequired);
            m_dataTableName = new ConfigurationProperty(ConfigurationNames.DataTableAttributeName, typeof(string), "", ConfigurationPropertyOptions.IsRequired);
            m_dataAccessMethod = new ConfigurationProperty(ConfigurationNames.DataAccessMethodAttributeName, typeof(string), "");
            m_properties = new ConfigurationPropertyCollection();
            m_properties.Add(m_name);
            m_properties.Add(m_connectionString);
            m_properties.Add(m_dataAccessMethod);
            m_properties.Add(m_dataTableName);
        }

        public DataSourceElement()
        {
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return m_properties; }
        }

        [ConfigurationProperty(ConfigurationNames.NameAttributeName, IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string)base[m_name]; }
            set { base[m_name] = value; }
        }

        /// <summary>
        /// Refers to ConnectionStringSettings element in &lt;connectionStrings&gt; section in the .config file.
        /// </summary>
        [ConfigurationProperty(ConfigurationNames.ConnectionStringAttributeName, IsRequired = true)]
        public string ConnectionString
        {
            get { return (string)base[m_connectionString]; }
            set { base[m_connectionString] = value; }
        }

        [ConfigurationProperty(ConfigurationNames.DataTableAttributeName, IsRequired = true)]
        public string DataTableName
        {
            get { return (string)base[m_dataTableName]; }
            set { base[m_dataTableName] = value; }
        }

        [ConfigurationProperty(ConfigurationNames.DataAccessMethodAttributeName, DefaultValue = "")]
        public string DataAccessMethod
        {
            get { return (string)base[m_dataAccessMethod]; }
            set { base[m_dataAccessMethod] = value; }
        }

        internal string Key
        {
            get { return Name; }
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
