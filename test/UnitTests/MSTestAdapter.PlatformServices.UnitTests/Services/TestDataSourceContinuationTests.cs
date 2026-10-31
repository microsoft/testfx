// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Xml.Linq;
#endif

using AwesomeAssertions;

#if NETFRAMEWORK && DEBUG
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
#endif

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestDataSourceContinuationTests : TestContainer
{
#if !NETFRAMEWORK
    public void NonFrameworkDataSourceDoesNotReadMethodOrMutateContext()
    {
        Mock<ITestMethod> method = new(MockBehavior.Strict);
        Mock<ITestContext> context = new(MockBehavior.Strict);
        var source = (Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestDataSource)new TestDataSource();

        source.GetData(method.Object, context.Object).Should().BeNull();

        method.VerifyNoOtherCalls();
        context.VerifyNoOtherCalls();
    }
#else
    public void SequentialAndUnknownAccessPreserveSourceRowOrder()
    {
        using SourceDirectory fixture = new();
        foreach (DataAccessMethod access in new[] { DataAccessMethod.Sequential, (DataAccessMethod)123 })
        {
            RowObservation rows = fixture.Worker.ReadDirect(SourceDirectory.XmlProvider, fixture.Data, "Rows", access);
            rows.Ids.Should().Equal(31, 17, 99);
            rows.Labels.Should().Equal("alpha", "beta", "gamma");
            rows.ContextCalls.Should().Be(1);
            rows.ConnectionWasNull.Should().BeTrue("the XML data source has no DbConnection");
#if DEBUG
            string?[] expectedAssertions = access == (DataAccessMethod)123 ? ["Unknown DataAccessMethod: 123"] : [];
            rows.Assertions.Should().Equal(expectedAssertions);
#else
            rows.Assertions.Should().BeEmpty();
#endif
        }
    }

    public void RandomAccessReturnsEveryRowExactlyOnceAndSetsConnection()
    {
        using SourceDirectory fixture = new();

        RowObservation rows = fixture.Worker.ReadDirect(SourceDirectory.XmlProvider, fixture.Data, "Rows", DataAccessMethod.Random);

        rows.Ids.Should().BeEquivalentTo([31, 17, 99]).And.OnlyHaveUniqueItems();
        rows.Ids!.Zip(rows.Labels!, (id, label) => (id, label)).Should().BeEquivalentTo([(31, "alpha"), (17, "beta"), (99, "gamma")]);
        rows.ContextCalls.Should().Be(1);
        rows.ConnectionWasNull.Should().BeTrue();
    }

    public void EmptyTablesDoNotSetAContextConnection()
    {
        using SourceDirectory fixture = new(empty: true);

        RowObservation rows = fixture.Worker.ReadDirect(SourceDirectory.XmlProvider, fixture.Data, "Rows", DataAccessMethod.Sequential);

        rows.Ids.Should().BeNull();
        rows.Labels.Should().BeNull();
        rows.ContextCalls.Should().Be(0);
    }

    public void ConnectionFailuresPreserveInnerCauseAndDoNotSetContext()
    {
        using SourceDirectory fixture = new();
        Action read = () => fixture.Worker.ReadDirect("TestFx.Ad03.InvalidProvider", fixture.Data, "Rows", DataAccessMethod.Sequential);

        Exception exception = read.Should().ThrowExactly<Exception>().Which;

        ArgumentException inner = exception.InnerException.Should().BeOfType<ArgumentException>().Which;
        exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorDataConnectionFailed, inner.Message));
        fixture.Worker.ContextCallCount().Should().Be(0);
    }

    public void ConfiguredDataSourceResolvesConnectionTableAndAccessMode()
    {
        using SourceDirectory fixture = new(configured: true);

        RowObservation rows = fixture.Worker.ReadConfigured("Ad03Rows");

        rows.Ids.Should().Equal(31, 17, 99);
        rows.Labels.Should().Equal("alpha", "beta", "gamma");
        rows.ContextCalls.Should().Be(1);
        rows.ConnectionWasNull.Should().BeTrue();
    }

    public void MissingConfiguredSourceReportsTheSettingWithoutConnectionWrapping()
    {
        using SourceDirectory fixture = new(configured: true);
        Action read = () => fixture.Worker.ReadConfigured("AbsentAd03");

        Exception exception = read.Should().ThrowExactly<Exception>().Which;

        exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_DataSourceConfigurationSectionMissing, "AbsentAd03"));
        exception.InnerException.Should().BeNull();
        fixture.Worker.ContextCallCount().Should().Be(0);
    }

    [Serializable]
    public sealed class RowObservation
    {
        public int[]? Ids { get; set; }

        public string[]? Labels { get; set; }

        public int ContextCalls { get; set; }

        public bool ConnectionWasNull { get; set; }

        public string?[] Assertions { get; set; } = [];
    }

    public sealed class DataSourceWorker : MarshalByRefObject
    {
        private int _contextCalls;

        public void Anchor()
        {
        }

        public int ContextCallCount() => _contextCalls;

        public RowObservation ReadDirect(string provider, string connection, string table, DataAccessMethod access)
        {
#if DEBUG
            var assertions = new List<string?>();
            using DiagnosticAssertionObserver? observer = access == (DataAccessMethod)123 ? new DiagnosticAssertionObserver(assertions.Add) : null;
            RowObservation result = Read(new DataSourceAttribute(provider, connection, table, access));
            result.Assertions = assertions.ToArray();
            return result;
#else
            return Read(new DataSourceAttribute(provider, connection, table, access));
#endif
        }

        public RowObservation ReadConfigured(string name) => Read(new DataSourceAttribute(name));

        private RowObservation Read(DataSourceAttribute attribute)
        {
            _contextCalls = 0;
            bool connectionWasNull = false;
            Mock<ITestMethod> method = new();
            method.SetupGet(value => value.MethodInfo).Returns(typeof(DataSourceWorker).GetMethod(nameof(Anchor))!);
            method.Setup(value => value.GetAttributes<DataSourceAttribute>()).Returns([attribute]);
            Mock<ITestContext> context = new();
            context.Setup(value => value.SetDataConnection(It.IsAny<object>()))
                .Callback<object?>(value =>
                {
                    _contextCalls++;
                    connectionWasNull = value is null;
                });
            IEnumerable<object>? data = new TestDataSource().GetData(method.Object, context.Object);
            DataRow[]? rows = data?.Cast<DataRow>().ToArray();
            return new RowObservation
            {
                Ids = rows?.Select(row => (int)row["id"]).ToArray(),
                Labels = rows?.Select(row => (string)row["label"]).ToArray(),
                ContextCalls = _contextCalls,
                ConnectionWasNull = connectionWasNull,
            };
        }
    }

    private sealed class SourceDirectory : IDisposable
    {
        internal const string XmlProvider = "Microsoft.VisualStudio.TestTools.DataSource.XML";
        private readonly AppDomain _domain;

        public SourceDirectory(bool empty = false, bool configured = false)
        {
            Directory.CreateDirectory(Root);
            using (DataSet data = new("Fixture"))
            {
                DataTable rows = data.Tables.Add("Rows");
                rows.Columns.Add("id", typeof(int));
                rows.Columns.Add("label", typeof(string));
                if (!empty)
                {
                    rows.Rows.Add(31, "alpha");
                    rows.Rows.Add(17, "beta");
                    rows.Rows.Add(99, "gamma");
                }

                data.WriteXml(Data, XmlWriteMode.WriteSchema);
            }

            string configuration = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (configured)
            {
                var document = XDocument.Load(configuration);
                XElement root = document.Root!;
                XElement? sections = root.Element("configSections");
                if (sections is null)
                {
                    sections = new XElement("configSections");
                    root.AddFirst(sections);
                }

                const string sectionName = "microsoft.visualstudio.testtools";
                sections.Elements("section").Where(section => (string?)section.Attribute("name") == sectionName).Remove();
                sections.Add(new XElement("section", new XAttribute("name", sectionName), new XAttribute("type", typeof(TestConfigurationSection).AssemblyQualifiedName!)));
                root.Element(sectionName)?.Remove();
                root.Add(new XElement(
                    sectionName,
                    new XElement(
                        "dataSources",
                        new XElement(
                            "add",
                            new XAttribute("name", "Ad03Rows"),
                            new XAttribute("connectionString", "Ad03Connection"),
                            new XAttribute("dataTableName", "Rows"),
                            new XAttribute("dataAccessMethod", "Sequential")))));
                XElement? strings = root.Element("connectionStrings");
                if (strings is null)
                {
                    strings = new XElement("connectionStrings");
                    root.Add(strings);
                }

                strings.Add(new XElement("add", new XAttribute("name", "Ad03Connection"), new XAttribute("providerName", XmlProvider), new XAttribute("connectionString", Data)));
                configuration = Path.Combine(Root, "fixture.config");
                document.Save(configuration);
            }

            _domain = AppDomain.CreateDomain("Data source fixture " + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                ConfigurationFile = configuration,
            });
            Worker = (DataSourceWorker)_domain.CreateInstanceFromAndUnwrap(typeof(DataSourceWorker).Assembly.Location, typeof(DataSourceWorker).FullName);
        }

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-ad03-source-" + Guid.NewGuid().ToString("N"));

        public string Data => Path.Combine(Root, "rows.xml");

        public DataSourceWorker Worker { get; }

        public void Dispose()
        {
            AppDomain.Unload(_domain);
            Directory.Delete(Root, recursive: true);
        }
    }
#endif
}
