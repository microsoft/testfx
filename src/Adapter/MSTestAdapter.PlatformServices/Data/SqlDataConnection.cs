// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Data.SqlClient;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///      Utility classes to access databases, and to handle quoted strings etc for SQL Server.
/// </summary>
internal sealed class SqlDataConnection : TestDataConnectionSql
{
    public SqlDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
    {
    }

    /// <summary>
    /// Returns default database schema.
    /// this.Connection must be already opened.
    /// </summary>
    /// <returns>The default database schema.</returns>
    public override string? GetDefaultSchema() => GetDefaultSchemaMSSql();

    protected override SchemaMetaData[] GetSchemaMetaData()
    {
        SchemaMetaData data = new()
        {
            SchemaTable = "Tables",
            SchemaColumn = "TABLE_SCHEMA",
            NameColumn = "TABLE_NAME",
            TableTypeColumn = "TABLE_TYPE",
            ValidTableTypes = ["VIEW", "BASE TABLE"],
            InvalidSchemas = null,
        };
        return [data];
    }

    private static string FixConnectionString(string connectionString, List<string> dataFolders)
    {
        SqlConnectionStringBuilder sqlBuilder = new(connectionString);

        string attachedFile = sqlBuilder.AttachDBFilename;

        if (StringEx.IsNullOrEmpty(attachedFile))
        {
            // No file, so no need to rewrite the connection string
            return connectionString;
        }
        else
        {
            // Force pooling off for SQL when there is a file involved
            // Without this, after the connection is closed, an exclusive lock persists
            // for a long time, preventing us from moving files around
            sqlBuilder.Pooling = false;

            // Fix-up magic file paths
            string? fixedFilePath = FixPath(attachedFile, dataFolders);
            if (fixedFilePath != null)
            {
                sqlBuilder.AttachDBFilename = fixedFilePath;
            }

            // Return modified connection string
            return sqlBuilder.ConnectionString;
        }
    }
}

#endif
