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
        private bool m_isMSSql;

        public OdbcDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
            : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
        {
            // Need open connection to get Connection.Driver.
            Debug.Assert(IsOpen(), "The connection must be open!");

            m_isMSSql = Connection != null ? IsMSSql(Connection.Driver) : false;
        }

        // Need to fix up excel connections
        private static string FixConnectionString(string connectionString, List<string> dataFolders)
        {
            OdbcConnectionStringBuilder builder = new OdbcConnectionStringBuilder(connectionString);

            //only fix this for excel
            if (!String.Equals(builder.Dsn, "Excel Files"))
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
                // Fixup magic file paths
                string fixedFilePath = FixPath(fileName, dataFolders);
                if (fixedFilePath != null)
                {
                    builder["dbq"] = fixedFilePath;
                }
                return builder.ConnectionString;
            }
        }

        protected override SchemaMetaData[] GetSchemaMetaData()
        {
            // The following may fail for Oracle ODBC, need to test that...

            SchemaMetaData data1 = new SchemaMetaData(); ;
            data1.schemaTable = "Tables";
            data1.schemaColumn = "TABLE_SCHEM";
            data1.nameColumn = "TABLE_NAME";
            data1.tableTypeColumn = "TABLE_TYPE";
            data1.validTableTypes = new string[] { "TABLE", "SYSTEM TABLE" };
            data1.invalidSchemas = null;
            SchemaMetaData data2 = new SchemaMetaData(); ;
            data2.schemaTable = "Views";
            data2.schemaColumn = "TABLE_SCHEM";
            data2.nameColumn = "TABLE_NAME";
            data2.tableTypeColumn = "TABLE_TYPE";
            data2.validTableTypes = new string[] { "VIEW" };
            data2.invalidSchemas = new string[] { "sys", "INFORMATION_SCHEMA" };
            return new SchemaMetaData[] { data1, data2 };
        }

        public new OdbcCommandBuilder CommandBuilder
        {
            get { return (OdbcCommandBuilder)base.CommandBuilder; }
        }

        public new OdbcConnection Connection
        {
            get { return (OdbcConnection)base.Connection; }
        }

        protected override string QuoteIdentifier(string identifier)
        {
            Debug.Assert(!string.IsNullOrEmpty(identifier), "identifier");
            return CommandBuilder.QuoteIdentifier(identifier, Connection);  // Must pass connection.
        }

        protected override string UnquoteIdentifier(string identifier)
        {
            Debug.Assert(!string.IsNullOrEmpty(identifier), "identifier");
            return CommandBuilder.UnquoteIdentifier(identifier, Connection);  // Must pass connection.
        }

        /// <summary>
        /// This is overridden because we need manually get quote literals, OleDb does not fill those automatically.
        /// </summary>
        public override void GetQuoteLiterals()
        {
            GetQuoteLiteralsHelper();
        }

        public override string GetDefaultSchema()
        {
            if (m_isMSSql)
            {
                return GetDefaultSchemaMSSql();
            }
            return base.GetDefaultSchema();
        }
    }
}
