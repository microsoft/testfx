// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///      Utility classes to access databases, and to handle quoted strings etc for XML data.
/// </summary>
internal sealed class XmlDataConnection : TestDataConnection
{
    private readonly string _fileName;

    public XmlDataConnection(string fileName, List<string> dataFolders)
        : base(dataFolders)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(fileName), "fileName");
        _fileName = fileName;
    }

    public override List<string>? GetDataTablesAndViews()
    {
        DataSet? dataSet = LoadDataSet(true);
        if (dataSet == null)
        {
            return null;
        }

        List<string> tableNames = [];

        int tableCount = dataSet.Tables.Count;
        for (int i = 0; i < tableCount; i++)
        {
            DataTable table = dataSet.Tables[i];
            tableNames.Add(table.TableName);
        }

        return tableNames;
    }

    public override List<string>? GetColumns(string tableName)
    {
        DataSet? dataSet = LoadDataSet(true);

        DataTable? table = dataSet?.Tables[tableName];
        if (table == null)
        {
            return null;
        }

        List<string> columnNames = [];
        foreach (DataColumn column in table.Columns)
        {
            // Only show "normal" columns, we try to hide derived columns used as part
            // of the support for relations
            if (column.ColumnMapping != MappingType.Hidden)
            {
                columnNames.Add(column.ColumnName);
            }
        }

        return columnNames;
    }

    public override DataTable? ReadTable(string tableName, IEnumerable? columns)
    {
        // Reading XML is very simple...
        // We do not ask it to just load a specific table, or specific columns
        // so there is inefficiency since we will reload the entire file
        // once for every table in it. Oh well. Reading XML is pretty quick
        // compared to other forms of data source
        DataSet? ds = LoadDataSet(false);
        return ds?.Tables[tableName];
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Un-tested. Preserving behavior.")]
    [SuppressMessage("Security", "CA3075:Insecure DTD processing in XML", Justification = "Not enough tests to understand if we would break")]
    [SuppressMessage("Security", "CA5366:Use XmlReader for 'DataSet.ReadXml()'", Justification = "Not enough tests to understand if we would break")]
    private DataSet? LoadDataSet(bool schemaOnly)
    {
        try
        {
            DataSet dataSet = new()
            {
                Locale = CultureInfo.CurrentCulture,
            };
            string path = FixPath(_fileName) ?? Path.GetFullPath(_fileName);
            if (schemaOnly)
            {
                dataSet.ReadXmlSchema(path);
            }
            else
            {
                dataSet.ReadXml(path);
            }

            return dataSet;
        }
        catch (SecurityException securityException)
        {
            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, securityException.Message + " for XML data source " + _fileName);
        }
        catch (XmlException xmlException)
        {
            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, xmlException.Message + " for XML data source " + _fileName);
        }
        catch (Exception exception)
        {
            // Yes, we get other exceptions too!
            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, exception.Message + " for XML data source " + _fileName);
        }

        return null;
    }
}

#endif
