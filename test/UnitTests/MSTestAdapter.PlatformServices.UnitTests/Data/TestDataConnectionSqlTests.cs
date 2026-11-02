// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Data;
using System.Data.Common;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

using Moq;
using Moq.Protected;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Data;

public sealed class TestDataConnectionSqlTests : TestContainer
{
    public void ConnectionFileFixupPreservesOriginalForAbsentValuesAndBuilderMutationsForOrdinaryPaths()
    {
        foreach (string? path in new[] { null, string.Empty })
        {
            var builder = new DbConnectionStringBuilder { ["ordinary"] = "mutated" };
            bool setterCalled = false;
            SchemaConnection.FixFile(builder, "original untouched", () => path, _ => setterCalled = true, [])
                .Should().Be("original untouched");
            setterCalled.Should().BeFalse();
        }

        var ordinaryBuilder = new DbConnectionStringBuilder { ["file"] = "ordinary.xml", ["pooling"] = false };
        bool ordinarySetterCalled = false;
        string actual = SchemaConnection.FixFile(ordinaryBuilder, "discarded", () => "ordinary.xml", _ => ordinarySetterCalled = true, []);
        var parsed = new DbConnectionStringBuilder { ConnectionString = actual };
        parsed["file"].Should().Be("ordinary.xml");
        parsed["pooling"].ToString().Should().Be("False");
        ordinarySetterCalled.Should().BeFalse();
    }

    public void ConnectionFileFixupUpdatesOnlyTheResolvedFileAndRetainsOtherBuilderValues()
    {
        string root = Path.Combine(Path.GetTempPath(), "connection-file-helper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string name = Guid.NewGuid().ToString("N") + ".xml";
            string resolved = Path.Combine(root, name);
            File.WriteAllText(resolved, "not opened");
            string magic = TestDataConnection.ConnectionDirectoryKey + name;
            var builder = new DbConnectionStringBuilder { ["file"] = magic, ["pooling"] = false, ["catalog"] = "sample" };
            string actual = SchemaConnection.FixFile(builder, "discarded", () => magic, value => builder["file"] = value, [root]);

            var parsed = new DbConnectionStringBuilder { ConnectionString = actual };
            parsed["file"].Should().Be(resolved);
            parsed["pooling"].ToString().Should().Be("False");
            parsed["catalog"].Should().Be("sample");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void MsSqlCapabilityCachesNonSqlDetectionPerConnectionRatherThanReevaluatingChangedProvider()
    {
        var database = new Mock<DbConnection>();
        database.Setup(value => value.State).Returns(ConnectionState.Open);
        using CapabilityConnection connection = new(database.Object) { ProviderName = "Oracle" };
        connection.GetDefaultSchema().Should().BeNull();
        connection.ProviderName = "SQLNCLI.1";
        connection.GetDefaultSchema().Should().BeNull();
        connection.ProviderReadCount.Should().Be(1, "the first capability result is cached for this connection");

        using CapabilityConnection independent = new(database.Object) { ProviderName = "Access" };
        independent.GetDefaultSchema().Should().BeNull();
        independent.ProviderReadCount.Should().Be(1, "another instance must independently detect its provider");
        independent.GetQuoteLiterals();
        independent.QuotePrefix.Should().Be("[");
        independent.QuoteSuffix.Should().Be("]");
    }

    public void MsSqlCapabilityMissingVirtualConnectionDoesNotQueryProvider()
    {
        var database = new Mock<DbConnection>();
        database.Setup(value => value.State).Returns(ConnectionState.Open);
        using CapabilityConnection connection = new(database.Object) { ReturnMissingConnection = true };
        connection.GetDefaultSchema().Should().BeNull();
        connection.GetDefaultSchema().Should().BeNull();
        connection.ProviderReadCount.Should().Be(0);
    }

    public void SqlAttachedFileNormalizationPreservesNonFileStringsAndDisablesFilePooling()
    {
        const string ordinary = "Data Source=server;Initial Catalog=sample;Pooling=True";
        NormalizeConnectionString(typeof(SqlDataConnection), ordinary, []).Should().Be(ordinary);
        string attached = Path.Combine(Path.GetTempPath(), "not-opened-data.mdf");
        string normalized = NormalizeConnectionString(typeof(SqlDataConnection), $"Data Source=server;AttachDbFilename={attached};Pooling=True", []);
        var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(normalized);
        builder.AttachDBFilename.Should().Be(attached);
        builder.Pooling.Should().BeFalse();
        builder.DataSource.Should().Be("server");
    }

    public void ProviderFileNormalizationUsesExactExcelDsnAndResolvesConfiguredDataFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "provider-fixup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string fileName = Guid.NewGuid().ToString("N") + ".xls";
            string path = Path.Combine(root, fileName);
            File.WriteAllText(path, "not opened by this normalization test");
            string magic = TestDataConnection.ConnectionDirectoryKey + fileName;
            string excel = $"Dsn=Excel Files;dbq={magic}";
            var odbc = new System.Data.Odbc.OdbcConnectionStringBuilder(NormalizeConnectionString(typeof(OdbcDataConnection), excel, [root]));
            ((string)odbc["dbq"]).Should().Be(path);
            odbc.Dsn.Should().Be("Excel Files");
            string differentCase = $"Dsn=excel files;dbq={magic}";
            NormalizeConnectionString(typeof(OdbcDataConnection), differentCase, [root]).Should().Be(differentCase);
            const string noFile = "Dsn=Excel Files;dbq=";
            Action missingDbq = () => _ = NormalizeConnectionString(typeof(OdbcDataConnection), noFile, [root]);
            missingDbq.Should().Throw<TargetInvocationException>().Which.InnerException!
                .Should().BeOfType<ArgumentException>().Which.Message.Should().Contain("dbq");

            var ole = new System.Data.OleDb.OleDbConnectionStringBuilder(
                NormalizeConnectionString(typeof(OleDataConnection), $"Provider=SQLOLEDB;Data Source={magic}", [root]));
            ole.DataSource.Should().Be(path);
            ole.Provider.Should().Be("SQLOLEDB");
            var sql = new System.Data.SqlClient.SqlConnectionStringBuilder(
                NormalizeConnectionString(typeof(SqlDataConnection), $"Data Source=server;AttachDbFilename={magic};Pooling=True", [root]));
            sql.AttachDBFilename.Should().Be(path);
            sql.Pooling.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void ProviderFactoryEmptyStringsCreateTypedClosedConnectionsAndMetadataWithoutOpening()
    {
        foreach ((string provider, string typeName) in new[]
        {
            ("System.Data.SqlClient", "SqlDataConnection"),
            ("System.Data.OleDb", "OleDataConnection"),
            ("System.Data.Odbc", "OdbcDataConnection"),
        })
        {
            using var connection = TestDataConnectionSql.Create(provider, string.Empty, []);
            connection.GetType().Name.Should().Be(typeName);
            connection.Connection.State.Should().Be(ConnectionState.Closed);
            connection.IsOpen().Should().BeFalse();
            object[] metadata = ((Array)connection.GetType()
                .GetMethod("GetSchemaMetaData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(connection, null)!).Cast<object>().ToArray();
            metadata.Length.Should().Be(provider == "System.Data.Odbc" ? 2 : 1);
            string[] tables = metadata.Select(value => (string)value.GetType().GetField("SchemaTable")!.GetValue(value)!).ToArray();
            tables.Should().Equal(provider == "System.Data.Odbc" ? ["Tables", "Views"] : ["Tables"]);
        }
    }

    private static string NormalizeConnectionString(Type provider, string original, List<string> folders)
        => (string)provider.GetMethod("FixConnectionString", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [original, folders])!;

    public void SplitName_UnquotesOneTwoAndThreePartsAndEscapedSuffixes()
    {
        using SchemaConnection connection = new(Mock.Of<DbConnection>());
        connection.SplitName("table").Should().Equal("table");
        connection.SplitName("sales.orders").Should().Equal("sales", "orders");
        connection.SplitName("catalog.sales.orders").Should().Equal("catalog", "sales", "orders");
        connection.SplitName("[sales.region].[a]]b]").Should().Equal("sales.region", "a]b");
        connection.SplitName("[catalog].[sales.region].[a.b]").Should().Equal("catalog", "sales.region", "a.b");
        connection.SplitName("[table with space]").Should().Equal("table with space");
    }

    public void SplitName_RejectsEmptyPartsTrailingSeparatorsAndTooManyParts()
    {
        using SchemaConnection connection = new(Mock.Of<DbConnection>());
        foreach (string malformed in new[] { string.Empty, ".table", "sales.", "sales..table", "a.b.c.d", "[a]x.b" })
        {
            connection.SplitName(malformed).Should().BeNull($"'{malformed}' is not a qualified identifier");
            connection.PrepareNameForSql(malformed).Should().Be(malformed);
        }
    }

    public void PrepareNameForSql_QuotesEveryPartWithoutDoubleQuotingEscapes()
    {
        using SchemaConnection connection = new(Mock.Of<DbConnection>());
        connection.PrepareNameForSql("sales.orders").Should().Be("[sales].[orders]");
        connection.PrepareNameForSql("[sales.region].[a]]b]").Should().Be("[sales.region].[a]]b]");
        connection.PrepareNameForSql("catalog.sales.orders").Should().Be("[catalog].[sales].[orders]");
        connection.PrepareNameForSql("table with space").Should().Be("[table with space]");
    }

    public void JoinAndQuoteName_DistinguishesMinimalAndCompleteQuoting()
    {
        using SchemaConnection connection = new(Mock.Of<DbConnection>());
        connection.JoinAndQuoteName(["sales", "orders"], false).Should().Be("sales.orders");
        connection.JoinAndQuoteName(["sales.region", "a.b"], false).Should().Be("[sales.region].[a.b]");
        connection.JoinAndQuoteName(["catalog", "sales", "orders"], true).Should().Be("[catalog].[sales].[orders]");
        connection.JoinAndQuoteName(["single"], false).Should().Be("single");
        connection.JoinAndQuoteName(["single"], true).Should().Be("[single]");
        connection.DisplayName(null, "a.b").Should().Be("[a.b]");
        connection.DisplayName("sales", "orders").Should().Be("sales.orders");
    }

    public void QuoteLiterals_AreObtainedFromBuilderAndCanBeExplicitlyOverridden()
    {
        using SchemaConnection connection = new(Mock.Of<DbConnection>());
        connection.QuotePrefix.Should().Be("[");
        connection.QuoteSuffix.Should().Be("]");
        connection.GetQuoteLiterals();
        connection.QuotePrefix.Should().Be("[");
        connection.QuoteSuffix.Should().Be("]");
        connection.QuotePrefix = "\"";
        connection.QuoteSuffix = "\"";
        connection.QuotePrefix.Should().Be("\"");
        connection.QuoteSuffix.Should().Be("\"");
        connection.InferQuoteLiterals();
        connection.QuotePrefix.Should().Be("[");
        connection.QuoteSuffix.Should().Be("]");
    }

    public void SplitTableName_UsesDefaultSchemaAndIgnoresCatalog()
    {
        using SchemaConnection connection = new(Mock.Of<DbConnection>());
        connection.SplitTable("orders").Should().Be(("dbo", "orders"));
        connection.SplitTable("[sales].[orders]").Should().Be(("sales", "orders"));
        connection.SplitTable("[catalog].[sales].[order.details]").Should().Be(("sales", "order.details"));
    }

    public void GetTables_FiltersTypesAndSchemasSortsAndMinimallyQuotes()
    {
        using DataTable table = CreateTableMetadata();
        table.Rows.Add("dbo", "zebra", "TABLE");
        table.Rows.Add("sales", "a.b", "VIEW");
        table.Rows.Add("sys", "hidden", "TABLE");
        table.Rows.Add("dbo", "procedure", "PROCEDURE");
        table.Rows.Add(DBNull.Value, "alpha", "TABLE");
        table.Rows.Add("DBO", "middle", DBNull.Value);
        var database = new Mock<DbConnection>();
        database.Setup(value => value.GetSchema("Tables")).Returns(table);
        using SchemaConnection connection = new(database.Object);

        connection.GetDataTablesAndViews().Should().Equal("alpha", "middle", "sales.[a.b]", "zebra");
    }

    public void GetTables_UnsupportedCollectionDoesNotDiscardOtherCollections()
    {
        using DataTable table = CreateTableMetadata();
        table.Rows.Add("dbo", "kept", "TABLE");
        var database = new Mock<DbConnection>();
        database.Setup(value => value.GetSchema("Tables")).Returns(table);
        database.Setup(value => value.GetSchema("Views")).Throws<NotSupportedException>();
        using SchemaConnection connection = new(database.Object) { IncludeViews = true };

        connection.GetDataTablesAndViews().Should().Equal("kept");
    }

    public void GetTables_NullTypeFilterAllowsAllRowsAndUnqualifiedDefaultSchema()
    {
        using DataTable table = CreateTableMetadata();
        table.Rows.Add("dbo", "routine", "PROCEDURE");
        table.Rows.Add("sales", "other", "TABLE");
        var database = new Mock<DbConnection>();
        database.Setup(value => value.GetSchema("Tables")).Returns(table);
        using SchemaConnection connection = new(database.Object) { FilterTypes = false, FilterSchemas = false };

        connection.GetDataTablesAndViews().Should().Equal("routine", "sales.other");
    }

    public void GetTables_UnexpectedMetadataErrorReturnsAlreadyCollectedRows()
    {
        using DataTable table = CreateTableMetadata();
        table.Rows.Add("dbo", "kept", "TABLE");
        using DataTable invalid = new();
        invalid.Columns.Add("unrelated");
        invalid.Rows.Add("broken");
        var database = new Mock<DbConnection>();
        database.Setup(value => value.GetSchema("Tables")).Returns(table);
        database.Setup(value => value.GetSchema("Views")).Returns(invalid);
        using SchemaConnection connection = new(database.Object) { IncludeViews = true };

        connection.GetDataTablesAndViews().Should().Equal("kept");
    }

    public void GetColumns_UsesExactSchemaAndTableRestrictionsAndRetainsColumnOrder()
    {
        using DataTable columns = new();
        columns.Columns.Add("COLUMN_NAME");
        columns.Rows.Add("second");
        columns.Rows.Add("first");
        string?[]? observed = null;
        var database = new Mock<DbConnection>();
        database.Setup(value => value.GetSchema("Columns", It.IsAny<string[]>()))
            .Callback<string, string[]>((_, restrictions) => observed = restrictions)
            .Returns(columns);
        using SchemaConnection connection = new(database.Object);

        connection.GetColumns("[catalog].[sales].[a.b]").Should().Equal("second", "first");
        observed.Should().Equal(null, "sales", "a.b", null);
        connection.GetColumns("orders").Should().Equal("second", "first");
        observed.Should().Equal(null, "dbo", "orders", null);
    }

    public void GetColumns_DistinguishesEmptyCollectionFromUnsupportedAndUnexpectedErrors()
    {
        using DataTable empty = new();
        empty.Columns.Add("COLUMN_NAME");
        var database = new Mock<DbConnection>();
        database.Setup(value => value.GetSchema("Columns", It.IsAny<string[]>())).Returns(empty);
        using SchemaConnection connection = new(database.Object);
        connection.GetColumns("table").Should().NotBeNull().And.BeEmpty();
        database.Setup(value => value.GetSchema("Columns", It.IsAny<string[]>())).Throws<NotSupportedException>();
        connection.GetColumns("table").Should().BeNull();
        database.Setup(value => value.GetSchema("Columns", It.IsAny<string[]>())).Throws<InvalidOperationException>();
        connection.GetColumns("table").Should().BeNull();
    }

    public void GetColumns_InvalidMetadataIsHandledWithoutReturningPartialColumns()
    {
        using DataTable invalid = new();
        invalid.Columns.Add("WRONG_NAME");
        invalid.Rows.Add("value");
        var database = new Mock<DbConnection>();
        database.Setup(value => value.GetSchema("Columns", It.IsAny<string[]>())).Returns(invalid);
        using SchemaConnection connection = new(database.Object);

        connection.GetColumns("orders").Should().BeNull();
    }

    public void ProviderDetection_RecognizesSupportedPrefixesAndRejectsOtherProviders()
    {
        SchemaConnection.IsSqlProvider("SQLOLEDB").Should().BeTrue();
        SchemaConnection.IsSqlProvider("sqloledb.1").Should().BeTrue();
        SchemaConnection.IsSqlProvider("SQLNCLI.1").Should().BeTrue();
        SchemaConnection.IsSqlProvider("sqlsrv32.dll").Should().BeTrue();
        SchemaConnection.IsSqlProvider("sqlsrv32.dll.extra").Should().BeFalse();
        SchemaConnection.IsSqlProvider("Oracle").Should().BeFalse();
        SchemaConnection.IsSqlProvider(string.Empty).Should().BeFalse();
        SchemaConnection.IsSqlProvider(null).Should().BeFalse();
    }

    public void SqlFactory_CreatesClosedProviderAndDisposalDoesNotOpenIt()
    {
        using var connection = TestDataConnectionSql.Create("System.Data.SqlClient", string.Empty, []);
        connection.GetType().Name.Should().Be("SqlDataConnection");
        connection.Connection.State.Should().Be(ConnectionState.Closed);
        connection.IsOpen().Should().BeFalse();
    }

    public void ReadTable_QuotesRequestedColumnsAndPreservesReturnedRowsAndName()
    {
        using DataTable rows = new();
        rows.Columns.Add("value", typeof(int));
        rows.Rows.Add(17);
        var database = new Mock<DbConnection>();
        database.Setup(value => value.State).Returns(ConnectionState.Open);
        using SchemaConnection connection = new(database.Object);
        var command = new Mock<DbCommand>();
        command.SetupProperty(value => value.CommandText);
        command.Protected().SetupGet<DbConnection>("DbConnection").Returns(database.Object);
        command.Protected().Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>())
            .Returns(() => rows.CreateDataReader());
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(value => value.CreateCommand()).Returns(command.Object);
        factory.Setup(value => value.CreateDataAdapter()).Returns(() => new MemoryDataAdapter());
        typeof(TestDataConnectionSql).GetField("<Factory>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(connection, factory.Object);

        using DataTable actual = connection.ReadTable("sales.orders", new[] { "first", "a]b" });
        command.Object.CommandText.Should().Be("select [first],[a]]b] from [sales].[orders]");
        actual.TableName.Should().Be("sales.orders");
        actual.Locale.Should().Be(CultureInfo.InvariantCulture);
        actual.Rows.Count.Should().Be(1);
        actual.Rows[0]["value"].Should().Be(17);
    }

    public void ReadTable_NullAndEmptyColumnsSelectAllAndMalformedNamesRemainLiteral()
    {
        using DataTable rows = new();
        rows.Columns.Add("value", typeof(int));
        var database = new Mock<DbConnection>();
        database.Setup(value => value.State).Returns(ConnectionState.Open);
        using SchemaConnection connection = new(database.Object);
        var command = new Mock<DbCommand>();
        command.SetupProperty(value => value.CommandText);
        command.Protected().SetupGet<DbConnection>("DbConnection").Returns(database.Object);
        command.Protected().Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>())
            .Returns(() => rows.CreateDataReader());
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(value => value.CreateCommand()).Returns(command.Object);
        factory.Setup(value => value.CreateDataAdapter()).Returns(() => new MemoryDataAdapter());
        typeof(TestDataConnectionSql).GetField("<Factory>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(connection, factory.Object);

        using DataTable first = connection.ReadTable("orders", null);
        command.Object.CommandText.Should().Be("select * from [orders]");
        using DataTable second = connection.ReadTable("a..b", Array.Empty<string>());
        command.Object.CommandText.Should().Be("select * from a..b");
        second.Rows.Count.Should().Be(0);
        second.TableName.Should().Be("a..b");
    }

    private static DataTable CreateTableMetadata()
    {
        DataTable table = new();
        table.Columns.Add("schema");
        table.Columns.Add("name");
        table.Columns.Add("type");
        return table;
    }

    private sealed class SchemaConnection(DbConnection connection) : TestDataConnectionSql("System.Data.SqlClient", string.Empty, [])
    {
        public bool IncludeViews { get; init; }

        public bool FilterTypes { get; init; } = true;

        public bool FilterSchemas { get; init; } = true;

        public override DbConnection Connection => connection;

        public override string GetDefaultSchema() => "dbo";

        public (string? Schema, string Table) SplitTable(string value)
        {
            SplitTableName(value, out string? schema, out string table);
            return (schema, table);
        }

        public string DisplayName(string? schema, string name) => FormatTableNameForDisplay(schema, name);

        public void InferQuoteLiterals() => GetQuoteLiteralsHelper();

        public static bool IsSqlProvider(string? value) => IsMSSql(value);

        public static string FixFile(DbConnectionStringBuilder builder, string original, Func<string?> getter, Action<string> setter, List<string> folders)
            => FixConnectionStringFilePath(builder, original, getter, setter, folders);

        protected override SchemaMetaData[] GetSchemaMetaData()
        {
            SchemaMetaData Create(string name) => new()
            {
                SchemaTable = name,
                SchemaColumn = "schema",
                NameColumn = "name",
                TableTypeColumn = FilterTypes ? "type" : null,
                ValidTableTypes = ["TABLE", "VIEW"],
                InvalidSchemas = FilterSchemas ? ["sys"] : null,
            };
            return IncludeViews ? [Create("Tables"), Create("Views")] : [Create("Tables")];
        }
    }

    private sealed class CapabilityConnection : MSSqlCapableConnection
    {
        private readonly DbConnection _database;

        public CapabilityConnection(DbConnection database)
            : base("System.Data.SqlClient", string.Empty, [])
        {
            // The existing virtual provider seam requires an already-open connection. Keep the
            // provider closed and replace only this instance's state dependency; never open a DB.
            base.Connection.Dispose();
            typeof(TestDataConnectionSql).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, database);
            _database = database;
        }

        public string? ProviderName { get; set; }

        public int ProviderReadCount { get; private set; }

        public bool ReturnMissingConnection { get; init; }

        public override DbConnection Connection => ReturnMissingConnection ? null! : _database;

        protected override string? GetProviderNameForMSSqlDetection()
        {
            ProviderReadCount++;
            return ProviderName;
        }
    }

    private sealed class MemoryDataAdapter : DbDataAdapter;
}

#endif
