// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using System;
    using System.Collections.Generic;
    using System.Data.Odbc;
    using System.Diagnostics;

    /// <summary>
    ///      Utility classes to access databases, and to handle quoted strings etc for ODBC.
    /// </summary>
    internal sealed class OdbcDataConnection : TestDataConnectionSql
    {
        private bool isMSSql;

        public OdbcDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
            : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
        {
            // Need open connection to get Connection.Driver.
            Debug.Assert(this.IsOpen(), "The connection must be open!");

            this.isMSSql = this.Connection != null ? IsMSSql(this.Connection.Driver) : false;
        }

        public new OdbcCommandBuilder CommandBuilder
        {
            get { return (OdbcCommandBuilder)base.CommandBuilder; }
        }

        public new OdbcConnection Connection
        {
            get { return (OdbcConnection)base.Connection; }
        }

        /// <summary>
        /// This is overridden because we need manually get quote literals, OleDb does not fill those automatically.
        /// </summary>
        public override void GetQuoteLiterals()
        {
            this.GetQuoteLiteralsHelper();
        }

        public override string GetDefaultSchema()
        {
            if (this.isMSSql)
            {
                return this.GetDefaultSchemaMSSql();
            }

            return base.GetDefaultSchema();
        }

        protected override SchemaMetaData[] GetSchemaMetaData()
        {
            // The following may fail for Oracle ODBC, need to test that...
            SchemaMetaData data1 = new SchemaMetaData()
            {
                SchemaTable = "Tables",
                SchemaColumn = "TABLE_SCHEM",
                NameColumn = "TABLE_NAME",
                TableTypeColumn = "TABLE_TYPE",
                ValidTableTypes = new string[] { "TABLE", "SYSTEM TABLE" },
                InvalidSchemas = null
            };
            SchemaMetaData data2 = new SchemaMetaData()
            {
                SchemaTable = "Views",
                SchemaColumn = "TABLE_SCHEM",
                NameColumn = "TABLE_NAME",
                TableTypeColumn = "TABLE_TYPE",
                ValidTableTypes = new string[] { "VIEW" },
                InvalidSchemas = new string[] { "sys", "INFORMATION_SCHEMA" }
            };
            return new SchemaMetaData[] { data1, data2 };
        }

        protected override string QuoteIdentifier(string identifier)
        {
            Debug.Assert(!string.IsNullOrEmpty(identifier), "identifier");
            return this.CommandBuilder.QuoteIdentifier(identifier, this.Connection);  // Must pass connection.
        }

        protected override string UnquoteIdentifier(string identifier)
        {
            Debug.Assert(!string.IsNullOrEmpty(identifier), "identifier");
            return this.CommandBuilder.UnquoteIdentifier(identifier, this.Connection);  // Must pass connection.
        }

        // Need to fix up excel connections
        private static string FixConnectionString(string connectionString, List<string> dataFolders)
        {
            OdbcConnectionStringBuilder builder = new OdbcConnectionStringBuilder(connectionString);

            // only fix this for excel
            if (!string.Equals(builder.Dsn, "Excel Files"))
            {
                return connectionString;
            }

            string fileName = builder["dbq"] as string;

            if (string.IsNullOrEmpty(fileName))
            {
                return connectionString;
            }
            else
            {
                // Fix-up magic file paths
                string fixedFilePath = FixPath(fileName, dataFolders);
                if (fixedFilePath != null)
                {
                    builder["dbq"] = fixedFilePath;
                }

                return builder.ConnectionString;
            }
        }
    }
}
