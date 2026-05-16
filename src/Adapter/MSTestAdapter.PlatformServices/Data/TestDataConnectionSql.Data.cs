// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using StringEx = Microsoft.VisualStudio.TestTools.UnitTesting.StringEx;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

internal partial class TestDataConnectionSql
{
    #region Data

    /// <summary>
    /// Read a table from the connection, into a DataTable
    /// Code used to be in UnitTestDataManager.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="columns">Columns.</param>
    /// <returns>new DataTable.</returns>
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Un-tested. Leaving behavior as is.")]
    [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security", Justification = "Not passed in from the user.")]
#pragma warning disable SA1202 // Elements must be ordered by access
    public override DataTable ReadTable(string tableName, IEnumerable? columns)
#pragma warning restore SA1202 // Elements must be ordered by access
    {
        using DbDataAdapter dataAdapter = Factory.CreateDataAdapter();
        using DbCommand command = Factory.CreateCommand();

        // We need to escape bad characters in table name like [Sheet1$] in Excel.
        // But if table name is quoted in terms of provider, don't touch it to avoid e.g. [dbo.tables.etc].
        string quotedTableName = PrepareNameForSql(tableName);
        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("ReadTable: data driven test: got table name from attribute: {0}", tableName);
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("ReadTable: data driven test: will use table name: {0}", tableName);
        }

        command.Connection = Connection;
        command.CommandText = string.Format(CultureInfo.InvariantCulture, "select {0} from {1}", GetColumnsSQL(columns), quotedTableName);

        WriteDiagnostics("ReadTable: SQL Query: {0}", command.CommandText);
        dataAdapter.SelectCommand = command;

        DataTable table = new()
        {
            Locale = CultureInfo.InvariantCulture,
        };
        dataAdapter.Fill(table);

        table.TableName = tableName;    // Make table name in the data set the same as original table name.
        return table;
    }

    private string GetColumnsSQL(IEnumerable? columns)
    {
        string? result = null;
        if (columns != null)
        {
            StringBuilder builder = new();
            foreach (string columnName in columns)
            {
                if (builder.Length > 0)
                {
                    builder.Append(',');
                }

                builder.Append(QuoteIdentifier(columnName));
            }

            result = builder.ToString();
        }

        // Return a valid list of columns, or default to * for all columns
        return !StringEx.IsNullOrEmpty(result) ? result : "*";
    }

    #endregion
}

#endif
