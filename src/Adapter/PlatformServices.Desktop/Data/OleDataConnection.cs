// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using System.Collections.Generic;
    using System.Data.OleDb;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    ///      Utility classes to access databases, and to handle quoted strings etc for OLE DB.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1706", Justification = "OleDb instead of Oledb to match System.Data.OleDb")]
    internal sealed class OleDataConnection : TestDataConnectionSql
    {
        private bool isMSSql;

        public OleDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
            : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
        {
            // Need open connection to get Connection.Provider.
            Debug.Assert(this.IsOpen(), "The connection must be open!");

            // Fill m_isMSSql.
            this.isMSSql = this.Connection != null ? IsMSSql(this.Connection.Provider) : false;
        }

        public new OleDbCommandBuilder CommandBuilder
        {
            get { return (OleDbCommandBuilder)base.CommandBuilder; }
        }

        public new OleDbConnection Connection
        {
            get { return (OleDbConnection)base.Connection; }
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
            // Note, in older iterations of the code there seemed to be
            // cases when we also need to look in the "views" table
            // but I do not see that in my test cases
            SchemaMetaData data = new SchemaMetaData()
            {
                SchemaTable = "Tables",
                SchemaColumn = "TABLE_SCHEMA",
                NameColumn = "TABLE_NAME",
                TableTypeColumn = "TABLE_TYPE",
                ValidTableTypes = new string[] { "VIEW", "TABLE" },
                InvalidSchemas = null
            };
            return new SchemaMetaData[] { data };
        }

        protected override string QuoteIdentifier(string identifier)
        {
            Debug.Assert(!string.IsNullOrEmpty(identifier), "identifier");
            return this.CommandBuilder.QuoteIdentifier(identifier, this.Connection);
        }

        protected override string UnquoteIdentifier(string identifier)
        {
            Debug.Assert(!string.IsNullOrEmpty(identifier), "identifier");
            return this.CommandBuilder.UnquoteIdentifier(identifier, this.Connection);
        }

        private static string FixConnectionString(string connectionString, List<string> dataFolders)
        {
            OleDbConnectionStringBuilder oleDbBuilder = new OleDbConnectionStringBuilder(connectionString);

            string fileName = oleDbBuilder.DataSource;

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
                    oleDbBuilder.DataSource = fixedFilePath;
                }

                return oleDbBuilder.ConnectionString;
            }
        }
    }
}
