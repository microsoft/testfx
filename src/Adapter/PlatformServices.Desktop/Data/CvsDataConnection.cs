// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.OleDb;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    ///      Utility classes to access databases, and to handle quoted strings etc for comma separated value files.
    /// </summary>
    internal sealed class CsvDataConnection : TestDataConnection
    {
        // Template used to map from a filename to a DB connection string
        private const string CsvConnectionTemplate = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Persist Security Info=False;Extended Properties=\"text;HDR=YES;FMT=Delimited\"";

        private string m_fileName;

        public CsvDataConnection(string fileName, List<string> dataFolders)
            : base(dataFolders)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileName), "fileName");
            m_fileName = fileName;
        }

        public override List<string> GetDataTablesAndViews()
        {
            List<string> tableNames = new List<string>(1);
            tableNames.Add(TableName);
            return tableNames;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public override List<string> GetColumns(string tableName)
        {
            // Somewhat heavy, this could be improved, right now I simply
            // read the table in then check the columns...
            try
            {
                DataTable table = ReadTable(tableName, null);
                if (table != null)
                {
                    List<string> columnNames = new List<string>();
                    foreach (DataColumn column in table.Columns)
                    {
                        columnNames.Add(column.ColumnName);
                    }
                    return columnNames;
                }
            }
            catch (Exception exception)
            {
               EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled,exception.Message + " for CSV data source " + m_fileName);
            }

            return null;
        }

        private string TableName
        {
            get
            {
                // Only one table based on the name of the file, with dots converted to # signs
                return Path.GetFileName(m_fileName).Replace('.', '#');
            }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security ")]
        public DataTable ReadTable(string tableName, IEnumerable columns, int maxRows)
        {
            // We specifically use OleDb to read a CSV file...
            WriteDiagnostics("ReadTable: {0}", tableName);
            WriteDiagnostics("Current Directory: {0}", Directory.GetCurrentDirectory());

            // We better work with a full path, if nothing else, errors become easier to report
            string fullPath = FixPath(m_fileName) ?? Path.GetFullPath(m_fileName);

            // We can map simplified CSVs to an OLEDB/Text connection, then proceed as normal
            using (OleDbConnection connection = new OleDbConnection())
            using (OleDbDataAdapter dataAdapter = new OleDbDataAdapter())
            using (OleDbCommandBuilder commandBuilder = new OleDbCommandBuilder())
            using (OleDbCommand command = new OleDbCommand())
            {
                // We have to use the name of the folder which contains the CSV file in the connection string
                connection.ConnectionString = String.Format(CultureInfo.InvariantCulture, CsvConnectionTemplate, Path.GetDirectoryName(fullPath));
                WriteDiagnostics("Connection String: {0}", connection.ConnectionString);

                // We have to open the connection now, before we try to quote
                // the table name, otherwise QuoteIdentifier fails (for OleDb, go figure!)
                // The connection will get closed when we dispose of it
                connection.Open();

                string quotedTableName = commandBuilder.QuoteIdentifier(tableName, connection);

                command.Connection = connection;

                string topClause;
                if (maxRows >= 0)
                {
                    topClause = string.Format(CultureInfo.InvariantCulture, " top {0}", maxRows.ToString(NumberFormatInfo.InvariantInfo));
                }
                else
                {
                    topClause = string.Empty;
                }

                string columnsClause;
                if (columns != null)
                {
                    StringBuilder builder = new StringBuilder();
                    foreach (string columnName in columns)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(',');
                        }
                        builder.Append(commandBuilder.QuoteIdentifier(columnName, connection));
                    }
                    columnsClause = builder.ToString();
                    if (columnsClause.Length == 0)
                    {
                        columnsClause = "*";
                    }
                }
                else
                {
                    columnsClause = "*";
                }

                command.CommandText = string.Format(CultureInfo.InvariantCulture, "select {0} {1} from {2}", topClause, columnsClause, quotedTableName);
                WriteDiagnostics("Query: " + command.CommandText);

                dataAdapter.SelectCommand = command;

                DataTable table = new DataTable();
                table.Locale = CultureInfo.InvariantCulture;
                dataAdapter.Fill(table);
                return table;
            }
        }

        public override DataTable ReadTable(string tableName, IEnumerable columns)
        {
            return ReadTable(tableName, columns, -1);
        }
    }
}
