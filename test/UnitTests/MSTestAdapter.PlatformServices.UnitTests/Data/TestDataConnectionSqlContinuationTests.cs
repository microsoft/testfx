// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
using System.Data.Odbc;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;
#if !DEBUG
using Moq.Protected;
#endif

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Data;

public sealed class TestDataConnectionSqlContinuationTests : TestContainer
{
    public void OleAndOdbcBuildersDriveEscapedIdentifierParsingWithoutOpeningDatabase()
    {
        using OleDataConnection ole = new("System.Data.OleDb", string.Empty, []);
        ole.CommandBuilder.QuotePrefix = "[";
        ole.CommandBuilder.QuoteSuffix = "]";
        using OdbcDataConnection odbc = new("System.Data.Odbc", string.Empty, []);
        odbc.CommandBuilder.QuotePrefix = "[";
        odbc.CommandBuilder.QuoteSuffix = "]";

        foreach (TestDataConnectionSql connection in new TestDataConnectionSql[] { ole, odbc })
        {
            connection.SplitName("[sales.region].[a]]b]").Should().Equal("sales.region", "a]b");
            connection.PrepareNameForSql("[sales.region].[a]]b]").Should().Be("[sales.region].[a]]b]");
            connection.JoinAndQuoteName(["catalog", "sales.region", "a]b"], true)
                .Should().Be("[catalog].[sales.region].[a]]b]");
            connection.IsOpen().Should().BeFalse();
        }
    }

    public Task OleProviderDetectionDoesNotQuerySqlForAnUnconfiguredProvider()
    {
#if DEBUG
        return DebugAssertionChildProbe.VerifyAsync(DebugAssertionChildProbe.ClosedOleSchemaMode, "The connection must be open!");
#else
        VerifyClosedOleSchema(reportReady: false);
        return Task.CompletedTask;
#endif
    }

    internal static void RunClosedOleSchemaProbe() => VerifyClosedOleSchema(reportReady: true);

    private static void VerifyClosedOleSchema(bool reportReady)
    {
        using OleDataConnection connection = new("System.Data.OleDb", string.Empty, []);
        if (reportReady)
        {
            Console.WriteLine(DebugAssertionChildProbe.ReadyMarker);
        }

        connection.GetDefaultSchema().Should().BeNull();
        connection.GetDefaultSchema().Should().BeNull();
        connection.IsOpen().Should().BeFalse();
        connection.Connection.ConnectionString.Should().BeEmpty();
    }

    public void SuffixInferencePreservesEscapedIdentifierBoundaries()
    {
        using TestDataConnectionSql connection = new("System.Data.SqlClient", string.Empty, [])
        {
            QuotePrefix = "[",
        };
        connection.SplitName("[sales.region].[a]]b]").Should().Equal("sales.region", "a]b");
        connection.PrepareNameForSql("[sales.region].[a]]b]").Should().Be("[sales.region].[a]]b]");
        connection.PrepareNameForSql("sales.orders").Should().Be("[sales].[orders]");
    }

    public void UnrecognizedBuiltInProviderUsesGenericSchemaAndPreservesClosedState()
    {
        using var connection = TestDataConnectionSql.Create("System.Data.OracleClient", string.Empty, []);
        connection.Should().BeOfType<TestDataConnectionSql>();
        connection.QuotePrefix = "[";
        connection.QuoteSuffix = "]";
        connection.GetDataTablesAndViews().Should().BeEmpty();
        connection.GetColumns("orders").Should().BeNull();
        connection.Connection.State.Should().Be(ConnectionState.Closed);
        connection.IsOpen().Should().BeFalse();
    }

    public void MissingLocalOdbcDsnReportsDriverManagerFailureWithoutNetwork()
    {
        string missing = "TestFxAd03Absent" + Guid.NewGuid().ToString("N").Substring(0, 12);
        Action create = () => TestDataConnectionSql.Create("System.Data.Odbc", "Dsn=" + missing, []);

        OdbcException exception = create.Should().ThrowExactly<OdbcException>().Which;

        exception.Errors.Cast<OdbcError>().Select(error => error.SQLState).Should().Contain("IM002");
    }

    public Task DefaultProviderDetectionDoesNotExecuteSql()
    {
#if DEBUG
        return DebugAssertionChildProbe.VerifyAsync(DebugAssertionChildProbe.ClosedDefaultSchemaMode, "The connection must be open!");
#else
        VerifyClosedDefaultSchema(reportReady: false);
        return Task.CompletedTask;
#endif
    }

    internal static void RunClosedDefaultSchemaProbe() => VerifyClosedDefaultSchema(reportReady: true);

    private static void VerifyClosedDefaultSchema(bool reportReady)
    {
        Mock<DbConnection> database = new();
        using DefaultDetectionConnection connection = new(database.Object);
        if (reportReady)
        {
            Console.WriteLine(DebugAssertionChildProbe.ReadyMarker);
        }

        connection.GetDefaultSchema().Should().BeNull();
        connection.GetDefaultSchema().Should().BeNull();

        database.VerifyNoOtherCalls();
    }

    public Task DefaultSchemaSelectsVersionSpecificSqlAndDisposesCommands()
    {
#if DEBUG
        return VerifyUnsupportedSqlMockAsync();
#else
        foreach ((string version, string sql) in new[]
        {
            ("8.00.123", "select user_name()"),
            ("9.0.456", "select default_schema_name from sys.database_principals where name = user_name()"),
            ("16.0.1000", "select default_schema_name from sys.database_principals where name = user_name()"),
        })
        {
            Mock<DbCommand> command = new();
            command.SetupProperty(value => value.CommandText);
            command.Setup(value => value.ExecuteScalar()).Returns("sales");
            Mock<DbConnection> database = new();
            database.SetupGet(value => value.ServerVersion).Returns(version);
            database.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
            using SqlSchemaConnection connection = new(database.Object);

            connection.ReadDefaultSchema().Should().Be("sales");

            command.Object.CommandText.Should().Be(sql);
            command.Verify(value => value.ExecuteScalar(), Times.Once);
            command.Protected().Verify("Dispose", Times.Once(), new object[] { true });
            database.Protected().Verify("CreateDbCommand", Times.Once());
        }

        return Task.CompletedTask;
#endif
    }

    public Task DefaultSchemaMalformedVersionsAndCommandFailuresReturnNull()
    {
#if DEBUG
        return VerifyUnsupportedSqlMockAsync();
#else
        foreach (string version in new[] { string.Empty, "broken", "x.0", "999999999999.0" })
        {
            Mock<DbConnection> database = new();
            database.SetupGet(value => value.ServerVersion).Returns(version);
            using SqlSchemaConnection connection = new(database.Object);
            connection.ReadDefaultSchema().Should().BeNull();
            database.Protected().Verify("CreateDbCommand", Times.Never());
        }

        foreach (object scalar in new object[] { DBNull.Value, 42 })
        {
            Mock<DbCommand> command = new();
            command.SetupProperty(value => value.CommandText);
            command.Setup(value => value.ExecuteScalar()).Returns(scalar);
            Mock<DbConnection> database = new();
            database.SetupGet(value => value.ServerVersion).Returns("12.0");
            database.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
            using SqlSchemaConnection connection = new(database.Object);
            connection.ReadDefaultSchema().Should().BeNull();
            command.Verify(value => value.ExecuteScalar(), Times.Once);
            command.Protected().Verify("Dispose", Times.Once(), new object[] { true });
        }

        Mock<DbCommand> failingCommand = new();
        failingCommand.Setup(value => value.ExecuteScalar()).Throws(new DataException("schema query rejected"));
        Mock<DbConnection> failingDatabase = new();
        failingDatabase.SetupGet(value => value.ServerVersion).Returns("12.0");
        failingDatabase.Protected().Setup<DbCommand>("CreateDbCommand").Returns(failingCommand.Object);
        using SqlSchemaConnection failing = new(failingDatabase.Object);
        failing.ReadDefaultSchema().Should().BeNull();
        failingCommand.Verify(value => value.ExecuteScalar(), Times.Once);
        failingCommand.Protected().Verify("Dispose", Times.Once(), new object[] { true });
        return Task.CompletedTask;
#endif
    }

#if DEBUG
    private static Task VerifyUnsupportedSqlMockAsync()
        => DebugAssertionChildProbe.VerifyAsync(
            DebugAssertionChildProbe.UnsupportedSqlMockMode,
            "GetDefaultSchemaMSSql should be called only for MS SQL (either native or Ole Db or Odbc).");
#endif

    internal static void RunUnsupportedSqlMockProbe()
    {
        Mock<DbConnection> database = new();
        database.SetupGet(value => value.State).Returns(ConnectionState.Open);
        database.SetupGet(value => value.ServerVersion).Returns("16.0.1000");
        using SqlSchemaConnection connection = new(database.Object);
        Console.WriteLine(DebugAssertionChildProbe.ReadyMarker);
        connection.ReadDefaultSchema().Should().BeNull();
    }

    public void UnsupportedMetadataLogsWarningAndReturnsAnEmptyTableList()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        TestablePlatformServiceProvider provider = new();
        provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        PlatformServiceProvider.Instance = provider;
        try
        {
            NotSupportedException unsupported = new("this provider has no Tables collection");
            Mock<DbConnection> database = new();
            database.Setup(value => value.GetSchema("Tables")).Throws(unsupported);
            using SqlSchemaConnection connection = new(database.Object);

            connection.GetDataTablesAndViews().Should().BeEmpty();

            provider.MockTraceLogger.Verify(
                logger => logger.Warning("DataUtil.GetDataTablesAndViews: exception (can be normal case as some providers do not support views): " + unsupported),
                Times.Once);
            database.Verify(value => value.GetSchema("Tables"), Times.Once);
        }
        finally
        {
            PlatformServiceProvider.Instance = previous;
        }
    }

    private sealed class SqlSchemaConnection(DbConnection database) : TestDataConnectionSql("System.Data.SqlClient", string.Empty, [])
    {
        public override DbConnection Connection => database;

        public string? ReadDefaultSchema() => GetDefaultSchemaMSSql();
    }

    private sealed class DefaultDetectionConnection(DbConnection database) : MSSqlCapableConnection("System.Data.SqlClient", string.Empty, [])
    {
        public override DbConnection Connection => database;
    }
}
#endif
