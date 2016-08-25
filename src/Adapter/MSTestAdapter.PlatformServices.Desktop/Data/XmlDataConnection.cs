// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Security;
    using System.Xml;

    /// <summary>
    ///      Utility classes to access databases, and to handle quoted strings etc for XML data.
    /// </summary>
    internal sealed class XmlDataConnection : TestDataConnection
    {
        private string m_fileName;

        public XmlDataConnection(string fileName, List<string> dataFolders)
            : base(dataFolders)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileName), "fileName");
            m_fileName = fileName;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private DataSet LoadDataSet(bool schemaOnly)
        {
            try
            {
                DataSet dataSet = new DataSet();
                dataSet.Locale = CultureInfo.CurrentCulture;
                string path = FixPath(m_fileName) ?? Path.GetFullPath(m_fileName);
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
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled,securityException.Message + " for XML data source " + m_fileName);
            }
            catch (XmlException xmlException)
            {
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled,xmlException.Message + " for XML data source " + m_fileName);
            }
            catch (Exception exception)
            {
                // Yes, we get other exceptions too!
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled,exception.Message + " for XML data source " + m_fileName);
            }
            return null;
        }

        public override List<string> GetDataTablesAndViews()
        {
            DataSet dataSet = LoadDataSet(true);

            if (dataSet != null)
            {
                List<string> tableNames = new List<string>();

                int tableCount = dataSet.Tables.Count;
                for (int i = 0; i < tableCount; i++)
                {
                    DataTable table = dataSet.Tables[i];
                    tableNames.Add(table.TableName);
                }
                return tableNames;
            }
            else
            {
                return null;
            }
        }

        public override List<string> GetColumns(string tableName)
        {
            DataSet dataSet = LoadDataSet(true);
            if (dataSet != null)
            {
                DataTable table = dataSet.Tables[tableName];
                if (table != null)
                {
                    List<string> columnNames = new List<string>();
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
            }
            return null;
        }

        public override DataTable ReadTable(string tableName, IEnumerable columns)
        {
            // Reading XML is very simple...
            // We do not ask it to just load a specific table, or specific columns
            // so there is inefficiency since we will reload the entire file
            // once for every table in it. Oh well. Reading XML is pretty quick
            // compared to other forms of data source
            DataSet ds = LoadDataSet(false);
            return ds != null ? ds.Tables[tableName] : null;
        }
    }

}
