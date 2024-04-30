// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Collections;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///      Utility classes to access databases, and to handle quoted strings etc for comma separated value files.
/// </summary>
internal sealed class CsvDataConnection : TestDataConnection
{
    // Template used to map from a filename to a DB connection string
    private const string CsvConnectionTemplate = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Persist Security Info=False;Extended Properties=\"text;HDR=YES;FMT=Delimited\"";
    private const string CsvConnectionTemplate64 = "Provider=Microsoft.Ace.OLEDB.12.0;Data Source={0};Persist Security Info=False;Extended Properties=\"text;HDR=YES;FMT=Delimited\"";

    private readonly string _fileName;

    public CsvDataConnection(string fileName, List<string> dataFolders)
        : base(dataFolders)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(fileName), "fileName");
        _fileName = fileName;
    }

    private string TableName =>
            // Only one table based on the name of the file, with dots converted to # signs
            Path.GetFileName(_fileName).Replace('.', '#');

    public override List<string> GetDataTablesAndViews()
    {
        List<string> tableNames = new(1)
        {
            TableName,
        };
        return tableNames;
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public override List<string>? GetColumns(string tableName)
    {
        // Somewhat heavy, this could be improved, right now I simply
        // read the table in then check the columns...
        try
        {
            DataTable table = ReadTable(tableName, null);
            if (table != null)
            {
                List<string> columnNames = [];
                foreach (DataColumn column in table.Columns)
                {
                    columnNames.Add(column.ColumnName);
                }

                return columnNames;
            }
        }
        catch (Exception exception)
        {
            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, exception.Message + " for CSV data source " + _fileName);
        }

        return null;
    }

    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Untested. Leaving as-is.")]
    [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security", Justification = "Not passed in from user.")]
    public DataTable ReadTable(string tableName, IEnumerable? columns, int maxRows)
    {
        // We specifically use OleDb to read a CSV file...
        WriteDiagnostics("ReadTable: {0}", tableName);
        WriteDiagnostics("Current Directory: {0}", Directory.GetCurrentDirectory());

        // We better work with a full path, if nothing else, errors become easier to report
        string fullPath = FixPath(_fileName) ?? Path.GetFullPath(_fileName);

        // We can map simplified CSVs to an OLEDB/Text connection, then proceed as normal
        using OleDbConnection connection = new();
        using OleDbDataAdapter dataAdapter = new();
        using OleDbCommandBuilder commandBuilder = new();
        using OleDbCommand command = new();

        // We have to use the name of the folder which contains the CSV file in the connection string
        // If target platform is x64, then use CsvConnectionTemplate64 connection string.
        connection.ConnectionString = IntPtr.Size == 8
            ? string.Format(CultureInfo.InvariantCulture, CsvConnectionTemplate64, Path.GetDirectoryName(fullPath))
            : string.Format(CultureInfo.InvariantCulture, CsvConnectionTemplate, Path.GetDirectoryName(fullPath));

        WriteDiagnostics("Connection String: {0}", connection.ConnectionString);

        // We have to open the connection now, before we try to quote
        // the table name, otherwise QuoteIdentifier fails (for OleDb, go figure!)
        // The connection will get closed when we dispose of it
        connection.Open();

        string quotedTableName = commandBuilder.QuoteIdentifier(tableName, connection);

        command.Connection = connection;

        string topClause = maxRows >= 0
            ? string.Format(CultureInfo.InvariantCulture, " top {0}", maxRows.ToString(NumberFormatInfo.InvariantInfo))
            : string.Empty;
        string columnsClause;
        if (columns != null)
        {
            StringBuilder builder = new();
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

        DataTable table = new()
        {
            Locale = CultureInfo.InvariantCulture,
        };
        dataAdapter.Fill(table);
        return table;
    }

    public override DataTable ReadTable(string tableName, IEnumerable? columns) => ReadTable(tableName, columns, -1);
}
#endif
