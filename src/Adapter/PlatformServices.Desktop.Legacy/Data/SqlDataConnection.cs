// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using System.Collections.Generic;
    using System.Data.SqlClient;

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
        public override string GetDefaultSchema()
        {
            return this.GetDefaultSchemaMSSql();
        }

        protected override SchemaMetaData[] GetSchemaMetaData()
        {
            SchemaMetaData data = new SchemaMetaData()
            {
                SchemaTable = "Tables",
                SchemaColumn = "TABLE_SCHEMA",
                NameColumn = "TABLE_NAME",
                TableTypeColumn = "TABLE_TYPE",
                ValidTableTypes = new string[] { "VIEW", "BASE TABLE" },
                InvalidSchemas = null
            };
            return new SchemaMetaData[] { data };
        }

        private static string FixConnectionString(string connectionString, List<string> dataFolders)
        {
            SqlConnectionStringBuilder sqlBuilder = new SqlConnectionStringBuilder(connectionString);

            string attachedFile = sqlBuilder.AttachDBFilename;

            if (string.IsNullOrEmpty(attachedFile))
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
                string fixedFilePath = FixPath(attachedFile, dataFolders);
                if (fixedFilePath != null)
                {
                    sqlBuilder.AttachDBFilename = fixedFilePath;
                }

                // Return modified connection string
                return sqlBuilder.ConnectionString;
            }
        }
    }
}
