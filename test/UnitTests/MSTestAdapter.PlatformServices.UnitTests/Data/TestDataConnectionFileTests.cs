// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Data;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Data;

public sealed class TestDataConnectionFileTests : TestContainer
{
    public void CsvTablesUseOnlyFilenameAndReplaceEveryDotWithoutOpeningProvider()
    {
        using var connection = new CsvDataConnection(Path.Combine("folder.with.dots", "rows with space.v2.csv"), []);
        connection.GetDataTablesAndViews().Should().Equal("rows with space#v2#csv");
        connection.Connection.Should().BeNull();
        using var extensionless = new CsvDataConnection("rows", []);
        extensionless.GetDataTablesAndViews().Should().Equal("rows");
    }

    public void SpecializedFactorySelectsXmlAndCsvWithoutOpeningDatabase()
    {
        TestDataConnectionFactory factory = new();
        using TestDataConnection csv = factory.Create("Microsoft.VisualStudio.TestTools.DataSource.CSV", "rows.csv", []);
        using TestDataConnection xml = factory.Create("Microsoft.VisualStudio.TestTools.DataSource.XML", "rows.xml", []);
        csv.Should().BeOfType<CsvDataConnection>();
        csv.GetDataTablesAndViews().Should().Equal("rows#csv");
        xml.Should().BeOfType<XmlDataConnection>();
        xml.GetColumns("missing").Should().BeNull();
    }

    public void XmlSchemaListsTablesAndVisibleColumnsInSchemaOrder()
    {
        using FileDataFixture fixture = new();
        string path = fixture.WriteDataSet();
        fixture.Worker.Tables(path, []).Should().Equal("Rows", "Other");
        fixture.Worker.Columns(path, "Rows", []).Should().Equal("id", "name");
        fixture.Worker.Columns(path, "Other", []).Should().Equal("value");
        fixture.Worker.Columns(path, "absent", []).Should().BeNull();
    }

    public void XmlReadPreservesDataAndIgnoresRequestedColumnProjection()
    {
        using FileDataFixture fixture = new();
        string path = fixture.WriteDataSet();
        using DataTable table = fixture.Worker.Read(path, "Rows", ["name"], [])!;

        table.TableName.Should().Be("Rows");
        table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).Should().Equal("id", "name", "internal");
        table.Rows.Count.Should().Be(2);
        table.Rows[0]["id"].Should().Be(7);
        table.Rows[0]["name"].Should().Be("first");
        table.Rows[1]["id"].Should().Be(9);
        table.Rows[1]["name"].Should().Be("second");
        table.Rows[1]["internal"].Should().Be(DBNull.Value, "hidden columns are schema-only, not serialized XML data");
        table.Locale.Should().Be(CultureInfo.CurrentCulture);
        fixture.Worker.Read(path, "absent", null, []).Should().BeNull();
    }

    public void XmlMissingMalformedAndEmptyInputsReturnNoInventedTablesOrRows()
    {
        using FileDataFixture fixture = new();
        foreach (string path in new[]
        {
            Path.Combine(fixture.Root, "missing.xml"),
            fixture.Write("malformed.xml", "<dataset><broken>"),
            fixture.Write("empty.xml", string.Empty),
        })
        {
            fixture.Worker.Tables(path, []).Should().BeNull(path);
            fixture.Worker.Columns(path, "Rows", []).Should().BeNull(path);
            fixture.Worker.Read(path, "Rows", null, []).Should().BeNull(path);
        }
    }

    public void XmlDataDirectoryTokenResolvesThroughConfiguredFolders()
    {
        using FileDataFixture fixture = new();
        string path = fixture.WriteDataSet();
        string token = TestDataConnection.ConnectionDirectoryKey + Path.GetFileName(path);
        fixture.Worker.Tables(token, [fixture.Root]).Should().Equal("Rows", "Other");
        using DataTable table = fixture.Worker.Read(token, "Rows", null, [fixture.Root])!;
        table.Rows[0]["id"].Should().Be(7);
        table.Rows[1]["name"].Should().Be("second");
    }

    public void PathFixupIsExactAndUsesFirstExistingFolderThenBasenameFallback()
    {
        using FileDataFixture fixture = new();
        string first = Path.Combine(fixture.Root, "first");
        string second = Path.Combine(fixture.Root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        string fileName = Guid.NewGuid().ToString("N") + ".xml";
        string firstFile = Path.Combine(first, fileName);
        string secondFile = Path.Combine(second, fileName);
        File.WriteAllText(firstFile, "first");
        File.WriteAllText(secondFile, "second");
        string token = TestDataConnection.ConnectionDirectoryKey + fileName;

        TestDataConnection.PathNeedsFixup(token).Should().BeTrue();
        TestDataConnection.GetRelativePart(token).Should().Be(fileName);
        TestDataConnection.FixPath(token, [first, second]).Should().Be(firstFile);
        TestDataConnection.FixPath(token, [second, first]).Should().Be(secondFile);
        TestDataConnection.PathNeedsFixup("|datadirectory|\\" + fileName).Should().BeFalse();
        TestDataConnection.PathNeedsFixup(string.Empty).Should().BeFalse();
        TestDataConnection.FixPath(firstFile, [second]).Should().BeNull();
        string nestedMissing = TestDataConnection.ConnectionDirectoryKey + Path.Combine("absent", fileName);
        TestDataConnection.FixPath(nestedMissing, []).Should().Be(Path.GetFullPath(fileName));
        TestDataConnection.FixPath(nestedMissing, null!).Should().Be(Path.GetFullPath(fileName));
        string absoluteToken = TestDataConnection.ConnectionDirectoryKey + firstFile;
        TestDataConnection.FixPath(absoluteToken, [second]).Should().Be(firstFile, "the directly existing path takes precedence over configured folders");
    }

    private sealed class FileDataFixture : IDisposable
    {
        private AppDomain? _domain;

        public FileDataFixture() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-data-coverage-" + Guid.NewGuid().ToString("N"));

        // Legacy production does not dispose its local XmlTextReader. Isolate those real calls in an
        // owned domain so unloading releases the file handles deterministically, without global GC,
        // retrying cleanup or modifying production behavior.
        public XmlWorker Worker
        {
            get
            {
                if (field is null)
                {
                    _domain = AppDomain.CreateDomain("XML coverage " + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
                    {
                        ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                        ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                    });
                    field = (XmlWorker)_domain.CreateInstanceFromAndUnwrap(typeof(XmlWorker).Assembly.Location, typeof(XmlWorker).FullName);
                }

                return field;
            }
        }

        public string Write(string name, string content)
        {
            string path = Path.Combine(Root, name);
            File.WriteAllText(path, content);
            return path;
        }

        public string WriteDataSet()
        {
            using DataSet dataSet = new("Sample");
            DataTable rows = dataSet.Tables.Add("Rows");
            rows.Columns.Add("id", typeof(int));
            rows.Columns.Add("name", typeof(string));
            rows.Columns.Add("internal", typeof(string)).ColumnMapping = MappingType.Hidden;
            rows.Rows.Add(7, "first", "hidden-first");
            rows.Rows.Add(9, "second", "hidden-second");
            DataTable other = dataSet.Tables.Add("Other");
            other.Columns.Add("value", typeof(int));
            other.Rows.Add(17);
            string path = Path.Combine(Root, "rows.xml");
            dataSet.WriteXml(path, XmlWriteMode.WriteSchema);
            return path;
        }

        public void Dispose()
        {
            if (_domain is not null)
            {
                AppDomain.Unload(_domain);
            }

            Directory.Delete(Root, recursive: true);
        }
    }

    public sealed class XmlWorker : MarshalByRefObject
    {
        public List<string>? Tables(string path, List<string> folders)
        {
            using XmlDataConnection connection = new(path, folders);
            return connection.GetDataTablesAndViews();
        }

        public List<string>? Columns(string path, string table, List<string> folders)
        {
            using XmlDataConnection connection = new(path, folders);
            return connection.GetColumns(table);
        }

        public DataTable? Read(string path, string table, string[]? columns, List<string> folders)
        {
            using XmlDataConnection connection = new(path, folders);
            return connection.ReadTable(table, columns);
        }
    }
}

#endif
