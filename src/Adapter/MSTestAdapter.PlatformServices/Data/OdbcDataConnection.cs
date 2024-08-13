// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Data.Odbc;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///      Utility classes to access databases, and to handle quoted strings etc for ODBC.
/// </summary>
internal sealed class OdbcDataConnection : TestDataConnectionSql
{
    private readonly bool _isMSSql;

    public OdbcDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
    {
        // Need open connection to get Connection.Driver.
        DebugEx.Assert(IsOpen(), "The connection must be open!");

        _isMSSql = Connection != null && IsMSSql(Connection.Driver);
    }

    public new OdbcCommandBuilder CommandBuilder => (OdbcCommandBuilder)base.CommandBuilder;

    public new OdbcConnection Connection => (OdbcConnection)base.Connection;

    /// <summary>
    /// This is overridden because we need manually get quote literals, OleDb does not fill those automatically.
    /// </summary>
    public override void GetQuoteLiterals() => GetQuoteLiteralsHelper();

    public override string? GetDefaultSchema() => _isMSSql ? GetDefaultSchemaMSSql() : base.GetDefaultSchema();

    protected override SchemaMetaData[] GetSchemaMetaData()
    {
        // The following may fail for Oracle ODBC, need to test that...
        SchemaMetaData data1 = new()
        {
            SchemaTable = "Tables",
            SchemaColumn = "TABLE_SCHEM",
            NameColumn = "TABLE_NAME",
            TableTypeColumn = "TABLE_TYPE",
            ValidTableTypes = ["TABLE", "SYSTEM TABLE"],
            InvalidSchemas = null,
        };
        SchemaMetaData data2 = new()
        {
            SchemaTable = "Views",
            SchemaColumn = "TABLE_SCHEM",
            NameColumn = "TABLE_NAME",
            TableTypeColumn = "TABLE_TYPE",
            ValidTableTypes = ["VIEW"],
            InvalidSchemas = ["sys", "INFORMATION_SCHEMA"],
        };
        return [data1, data2];
    }

    protected override string QuoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier");
        return CommandBuilder.QuoteIdentifier(identifier, Connection);  // Must pass connection.
    }

    protected override string UnquoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier");
        return CommandBuilder.UnquoteIdentifier(identifier, Connection);  // Must pass connection.
    }

    // Need to fix up excel connections
    private static string FixConnectionString(string connectionString, List<string> dataFolders)
    {
        OdbcConnectionStringBuilder builder = new(connectionString);

        // only fix this for excel
        if (!string.Equals(builder.Dsn, "Excel Files", StringComparison.Ordinal))
        {
            return connectionString;
        }

        string? fileName = builder["dbq"] as string;

        if (StringEx.IsNullOrEmpty(fileName))
        {
            return connectionString;
        }
        else
        {
            // Fix-up magic file paths
            string? fixedFilePath = FixPath(fileName, dataFolders);
            if (fixedFilePath != null)
            {
                builder["dbq"] = fixedFilePath;
            }

            return builder.ConnectionString;
        }
    }
}
#endif
