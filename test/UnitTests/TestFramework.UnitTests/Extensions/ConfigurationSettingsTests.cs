// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Configuration;
using System.Reflection;
using System.Xml;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class ConfigurationSettingsTests : TestContainer
{
    public void ElementSchemaAndValuesPreserveRequiredKeysAndDefaults()
    {
        var element = new DataSourceElement();
        element.Name.Should().BeEmpty();
        element.ConnectionString.Should().BeEmpty();
        element.DataTableName.Should().BeEmpty();
        element.DataAccessMethod.Should().BeEmpty();
        element.Name = "orders";
        element.ConnectionString = "ordersDb";
        element.DataTableName = "Orders";
        element.DataAccessMethod = "Sequential";
        element.Name.Should().Be("orders");
        element.ConnectionString.Should().Be("ordersDb");
        element.DataTableName.Should().Be("Orders");
        element.DataAccessMethod.Should().Be("Sequential");
        var schema = (ConfigurationPropertyCollection)typeof(DataSourceElement).GetProperty("Properties", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(element)!;
        schema.Count.Should().Be(4);
        schema["name"].IsKey.Should().BeTrue();
        schema["name"].IsRequired.Should().BeTrue();
        schema["connectionString"].IsRequired.Should().BeTrue();
        schema["dataTableName"].IsRequired.Should().BeTrue();
        schema["dataAccessMethod"].IsRequired.Should().BeFalse();
    }

    public void CollectionMaintainsCaseInsensitiveKeysAndOrder()
    {
        var collection = new DataSourceElementCollection();
        DataSourceElement first = Element("orders", "one");
        DataSourceElement second = Element("customers", "two");
        collection.Add(first);
        collection.Add(second);
        collection["ORDERS"].Should().BeSameAs(first);
        collection[0].Should().BeSameAs(first);
        collection["absent"].Should().BeNull();
        collection.Add(Element("ORDERS", "replacement"));
        collection.Count.Should().Be(2);
        collection[0].ConnectionString.Should().Be("replacement");
        collection[1].Should().BeSameAs(second);
        collection[0] = Element("invoices", "three");
        collection["orders"].Should().BeNull();
        collection[0].Name.Should().Be("invoices");
        collection[1].Name.Should().Be("customers");
        collection.Remove(Element("absent", "ignored"));
        collection.Count.Should().Be(2);
        collection.Remove(collection[0]);
        collection[0].Should().BeSameAs(second);
        collection.Remove("CUSTOMERS");
        collection.Count.Should().Be(0);
        collection.Add(first);
        collection.Clear();
        collection.Count.Should().Be(0);

        MethodInfo addAt = typeof(DataSourceElementCollection).GetMethod("BaseAdd", BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(int), typeof(ConfigurationElement)], null)!;
        addAt.Invoke(collection, [-1, first]);
        collection[0].Should().BeSameAs(first);
    }

    public void DeserializeSectionsCreatesElementsAndRejectsInvalidSchema()
    {
        TestConfigurationSection section = ReadSection("""
            <microsoft.visualstudio.testtools>
              <dataSources>
                <add name="orders" connectionString="ordersDb" dataTableName="Orders" dataAccessMethod="Random" />
                <add name="customers" connectionString="customersDb" dataTableName="Customers" />
              </dataSources>
            </microsoft.visualstudio.testtools>
            """);
        section.DataSources.Count.Should().Be(2);
        section.DataSources["ORDERS"].ConnectionString.Should().Be("ordersDb");
        section.DataSources[0].DataTableName.Should().Be("Orders");
        section.DataSources[0].DataAccessMethod.Should().Be("Random");
        section.DataSources[1].DataAccessMethod.Should().BeEmpty();
        new TestConfigurationSection().DataSources.Count.Should().Be(0);
        Action missingRequired = () => ReadSection("""
            <microsoft.visualstudio.testtools><dataSources><add name="missing" /></dataSources></microsoft.visualstudio.testtools>
            """);
        missingRequired.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<ConfigurationErrorsException>();
        Action unknown = () => ReadSection("""
            <microsoft.visualstudio.testtools unknown="value" />
            """);
        unknown.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<ConfigurationErrorsException>();
    }

    public void ConfigurationSelectionUsesRtmThenBetaThenDefaultInIsolatedDomains()
    {
        string type = typeof(TestConfigurationSection).AssemblyQualifiedName!;
        string rtm = Section("microsoft.visualstudio.testtools", "rtm");
        string beta = Section("microsoft.visualstudio.qualitytools", "beta");
        string declarations = $"""
            <configSections>
              <section name="microsoft.visualstudio.testtools" type="{type}" />
              <section name="microsoft.visualstudio.qualitytools" type="{type}" />
            </configSections>
            """;
        ReadInDomain($"<configuration>{declarations}{rtm}{beta}</configuration>").Should().Be("rtm");
        // A declared but absent RTM section is materialized as an empty section by System.Configuration.
        ReadInDomain($"<configuration>{declarations}{beta}</configuration>").Should().Be("empty");
        string betaDeclaration = $"""<configSections><section name="microsoft.visualstudio.qualitytools" type="{type}" /></configSections>""";
        ReadInDomain($"<configuration>{betaDeclaration}{beta}</configuration>").Should().Be("beta");
        ReadInDomain("<configuration />").Should().Be("empty");
    }

    private static DataSourceElement Element(string name, string connection) => new()
    {
        Name = name,
        ConnectionString = connection,
        DataTableName = "Table",
    };

    private static TestConfigurationSection ReadSection(string xml)
    {
        var section = new TestConfigurationSection();
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text);
        typeof(ConfigurationSection).GetMethod("DeserializeSection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(section, [reader]);
        return section;
    }

    private static string Section(string name, string source) => $"""
        <{name}><dataSources><add name="{source}" connectionString="db" dataTableName="Table" /></dataSources></{name}>
        """;

    private static string ReadInDomain(string xml)
    {
        string directory = Path.Combine(Path.GetTempPath(), "TestFxConfig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        AppDomain? domain = null;
        try
        {
            string config = Path.Combine(directory, "tests.config");
            File.WriteAllText(config, xml);
            domain = AppDomain.CreateDomain("TestFxConfig_" + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                ConfigurationFile = config,
            });
            var probe = (ConfigurationProbe)domain.CreateInstanceAndUnwrap(typeof(ConfigurationProbe).Assembly.FullName!, typeof(ConfigurationProbe).FullName!);
            return probe.Read();
        }
        finally
        {
            if (domain is not null)
            {
                AppDomain.Unload(domain);
            }

            Directory.Delete(directory, true);
        }
    }

    public sealed class ConfigurationProbe : MarshalByRefObject
    {
        public string Read()
        {
            TestConfigurationSection first = TestConfiguration.ConfigurationSection;
            _ = ReferenceEquals(first, TestConfiguration.ConfigurationSection)
                ? true : throw new InvalidOperationException("Configuration is not cached.");

            return first.DataSources.Count == 0 ? "empty" : first.DataSources[0].Name;
        }
    }
}
#endif
