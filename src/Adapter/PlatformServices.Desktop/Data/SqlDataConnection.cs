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

                // Fixup magic file paths
                string fixedFilePath = FixPath(attachedFile, dataFolders);
                if (fixedFilePath != null)
                {
                    sqlBuilder.AttachDBFilename = fixedFilePath;
                }

                // Return modified connection string
                return sqlBuilder.ConnectionString;
            }
        }

        protected override SchemaMetaData[] GetSchemaMetaData()
        {
            SchemaMetaData data = new SchemaMetaData(); ;
            data.schemaTable = "Tables";
            data.schemaColumn = "TABLE_SCHEMA";
            data.nameColumn = "TABLE_NAME";
            data.tableTypeColumn = "TABLE_TYPE";
            data.validTableTypes = new string[] { "VIEW", "BASE TABLE" };
            data.invalidSchemas = null;
            return new SchemaMetaData[] { data };
        }

        /// <summary>
        /// Returns default database schema.
        /// this.Connection must be alredy opened.
        /// </summary>
        public override string GetDefaultSchema()
        {
            return GetDefaultSchemaMSSql();
        }
    }
}
