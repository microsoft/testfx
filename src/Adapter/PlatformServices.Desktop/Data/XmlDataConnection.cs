// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
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
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    ///      Utility classes to access databases, and to handle quoted strings etc for XML data.
    /// </summary>
    internal sealed class XmlDataConnection : TestDataConnection
    {
        private readonly string fileName;

        public XmlDataConnection(string fileName, List<string> dataFolders)
            : base(dataFolders)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileName), "fileName");
            this.fileName = fileName;
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
            return ds?.Tables[tableName];
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Un-tested. Preserving behavior.")]
        private DataSet LoadDataSet(bool schemaOnly)
        {
            try
            {
                DataSet dataSet = new DataSet();
                dataSet.Locale = CultureInfo.CurrentCulture;
                string path = FixPath(fileName) ?? Path.GetFullPath(fileName);
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
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, securityException.Message + " for XML data source " + fileName);
            }
            catch (XmlException xmlException)
            {
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, xmlException.Message + " for XML data source " + fileName);
            }
            catch (Exception exception)
            {
                // Yes, we get other exceptions too!
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, exception.Message + " for XML data source " + fileName);
            }

            return null;
        }
    }
}
