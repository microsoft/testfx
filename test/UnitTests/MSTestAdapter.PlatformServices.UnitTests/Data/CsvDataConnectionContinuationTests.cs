// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.OleDb;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Data;

public sealed class CsvDataConnectionContinuationTests : TestContainer
{
    public void CsvProjectionAndTopLimitPreserveExactValuesAndColumnOrder()
    {
        using CsvDirectory fixture = new();
        using CsvDataConnection connection = new(fixture.Path, []);
        connection.GetColumns("rows#csv").Should().Equal("id", "display name", "quantity");

        using DataTable projected = connection.ReadTable("rows#csv", new[] { "display name", "quantity" }, 2);

        projected.Columns.Cast<DataColumn>().Select(column => column.ColumnName).Should().Equal("display name", "quantity");
        projected.Rows.Count.Should().Be(2);
        projected.Rows[0].ItemArray.Should().Equal("first, item", 5);
        projected.Rows[1].ItemArray.Should().Equal("second", 0);
        projected.Locale.Should().Be(CultureInfo.InvariantCulture);

        Action zeroLimit = () => connection.ReadTable("rows#csv", null, 0);
        zeroLimit.Should().ThrowExactly<OleDbException>("the text provider rejects a generated TOP 0 query");
    }

    public void NullAndEmptyProjectionReadAllColumnsIncludingDataDirectoryPaths()
    {
        using CsvDirectory fixture = new();
        string token = TestDataConnection.ConnectionDirectoryKey + System.IO.Path.GetFileName(fixture.Path);
        using CsvDataConnection connection = new(token, [fixture.Root]);

        using DataTable all = connection.ReadTable("rows#csv", null);
        using DataTable emptyProjection = connection.ReadTable("rows#csv", Array.Empty<string>(), -1);

        foreach (DataTable table in new[] { all, emptyProjection })
        {
            table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).Should().Equal("id", "display name", "quantity");
            table.Rows.Cast<DataRow>().Select(row => (int)row["id"]).Should().Equal(31, 17, 99);
            table.Rows[0].ItemArray.Should().Equal(31, "first, item", 5);
            table.Rows[2].ItemArray.Should().Equal(99, "last", -3);
            table.Locale.Should().Be(CultureInfo.InvariantCulture);
        }
    }

    public void MissingCsvTableReturnsNullAndLogsFullFailureWithOriginalPath()
    {
        using CsvDirectory fixture = new();
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        TestablePlatformServiceProvider provider = new();
        provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);
        string? failure = null;
        string? source = null;
        provider.MockTraceLogger.Setup(logger => logger.Error(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) =>
            {
                format.Should().Be("CsvDataConnection.GetColumns: {0} for CSV data source '{1}'.");
                failure = (string)arguments[0]!;
                source = (string)arguments[1]!;
            });
        PlatformServiceProvider.Instance = provider;
        try
        {
            using CsvDataConnection connection = new(fixture.Path, []);
            connection.GetColumns("missing#csv").Should().BeNull();
            source.Should().Be(fixture.Path);
            failure.Should().Contain(typeof(OleDbException).FullName).And.Contain("missing");
            provider.MockTraceLogger.Verify(
                logger => logger.Error("CsvDataConnection.GetColumns: {0} for CSV data source '{1}'.", It.IsAny<string>(), fixture.Path),
                Times.Once);
            using DataTable valid = connection.ReadTable("rows#csv", new[] { "id" }, 1);
            valid.Rows[0]["id"].Should().Be(31, "a metadata failure must not poison later independent reads");
        }
        finally
        {
            PlatformServiceProvider.Instance = previous;
        }
    }

    private sealed class CsvDirectory : IDisposable
    {
        public CsvDirectory()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path, "id,display name,quantity\r\n31,\"first, item\",5\r\n17,second,0\r\n99,last,-3\r\n", new UTF8Encoding(false));
            File.WriteAllText(
                System.IO.Path.Combine(Root, "schema.ini"),
                "[rows.csv]\r\nFormat=CSVDelimited\r\nColNameHeader=True\r\nMaxScanRows=0\r\nCol1=id Long\r\nCol2=\"display name\" Text Width 50\r\nCol3=quantity Long\r\n",
                new UTF8Encoding(false));
        }

        public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "testfx-ad03-csv-" + Guid.NewGuid().ToString("N"));

        public string Path => System.IO.Path.Combine(Root, "rows.csv");

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
#endif
