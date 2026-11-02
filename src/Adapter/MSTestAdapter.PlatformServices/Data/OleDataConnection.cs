// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Data.OleDb;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///      Utility classes to access databases, and to handle quoted strings etc for OLE DB.
/// </summary>
internal sealed class OleDataConnection : MSSqlCapableConnection
{
    public OleDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
    {
    }

    public new OleDbCommandBuilder CommandBuilder => (OleDbCommandBuilder)base.CommandBuilder;

    public new OleDbConnection Connection => (OleDbConnection)base.Connection;

    protected override string? GetProviderNameForMSSqlDetection() => Connection.Provider;

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

    private static string FixConnectionString(string connectionString, List<string> dataFolders)
    {
        OleDbConnectionStringBuilder oleDbBuilder = [with(connectionString)];

        return FixConnectionStringFilePath(
            oleDbBuilder,
            connectionString,
            () => oleDbBuilder.DataSource,
            fixedFilePath => oleDbBuilder.DataSource = fixedFilePath,
            dataFolders);
    }
}

#endif
