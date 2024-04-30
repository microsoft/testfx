// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Data.OleDb;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///      Utility classes to access databases, and to handle quoted strings etc for OLE DB.
/// </summary>
internal sealed class OleDataConnection : TestDataConnectionSql
{
    private readonly bool _isMSSql;

    public OleDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
    {
        // Need open connection to get Connection.Provider.
        DebugEx.Assert(IsOpen(), "The connection must be open!");

        // Fill m_isMSSql.
        _isMSSql = Connection != null && IsMSSql(Connection.Provider);
    }

    public new OleDbCommandBuilder CommandBuilder => (OleDbCommandBuilder)base.CommandBuilder;

    public new OleDbConnection Connection => (OleDbConnection)base.Connection;

    /// <summary>
    /// This is overridden because we need manually get quote literals, OleDb does not fill those automatically.
    /// </summary>
    public override void GetQuoteLiterals() => GetQuoteLiteralsHelper();

    public override string? GetDefaultSchema() => _isMSSql ? GetDefaultSchemaMSSql() : base.GetDefaultSchema();

    protected override SchemaMetaData[] GetSchemaMetaData()
    {
        // Note, in older iterations of the code there seemed to be
        // cases when we also need to look in the "views" table
        // but I do not see that in my test cases
        SchemaMetaData data = new()
        {
            SchemaTable = "Tables",
            SchemaColumn = "TABLE_SCHEMA",
            NameColumn = "TABLE_NAME",
            TableTypeColumn = "TABLE_TYPE",
            ValidTableTypes = ["VIEW", "TABLE"],
            InvalidSchemas = null,
        };
        return [data];
    }

    protected override string QuoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier");
        return CommandBuilder.QuoteIdentifier(identifier, Connection);
    }

    protected override string UnquoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier");
        return CommandBuilder.UnquoteIdentifier(identifier, Connection);
    }

    private static string FixConnectionString(string connectionString, List<string> dataFolders)
    {
        OleDbConnectionStringBuilder oleDbBuilder = new(connectionString);

        string fileName = oleDbBuilder.DataSource;

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
                oleDbBuilder.DataSource = fixedFilePath;
            }

            return oleDbBuilder.ConnectionString;
        }
    }
}

#endif
