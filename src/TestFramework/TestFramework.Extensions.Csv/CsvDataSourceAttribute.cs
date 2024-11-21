// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Attribute to define dynamic data from a CSV file for a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CsvDataSourceAttribute : Attribute, ITestDataSource
{
    // Template used to map from a filename to a DB connection string
    private const string CsvConnectionTemplate = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Persist Security Info=False;Extended Properties=\"text;HDR=YES;FMT=Delimited\"";
    private const string CsvConnectionTemplate64 = "Provider=Microsoft.Ace.OLEDB.12.0;Data Source={0};Persist Security Info=False;Extended Properties=\"text;HDR=YES;FMT=Delimited\"";

    public CsvDataSourceAttribute(string fileName)
        => FileName = fileName;

    internal string FileName { get; }

    IEnumerable<object?[]> ITestDataSource.GetData(MethodInfo methodInfo)
    {
        // We specifically use OleDb to read a CSV file...
        // We better work with a full path, if nothing else, errors become easier to report
        string fullPath = Path.GetFullPath(FileName);
        if (!File.Exists(fullPath))
        {
            // TODO: Localize.
            throw new FileNotFoundException($"Csv file '{fullPath}' cannot be found.", fullPath);
        }

        string tableName = Path.GetFileName(fullPath).Replace('.', '#');

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

        // We have to open the connection now, before we try to quote
        // the table name, otherwise QuoteIdentifier fails (for OleDb, go figure!)
        // The connection will get closed when we dispose of it
        connection.Open();

        string quotedTableName = commandBuilder.QuoteIdentifier(tableName, connection);

        command.Connection = connection;

        command.CommandText = $"SELECT * FROM {quotedTableName}";

        dataAdapter.SelectCommand = command;

        DataTable table = new()
        {
            Locale = CultureInfo.InvariantCulture,
        };

        dataAdapter.Fill(table);

        object?[][] dataRows = new object?[table.Rows.Count][];
        for (int i = 0; i < dataRows.Length; i++)
        {
            dataRows[i] = [table.Rows[i]];
        }

        return dataRows;
    }

    string? ITestDataSource.GetDisplayName(MethodInfo methodInfo, object?[]? data)
        // TODO
        => null;
}
