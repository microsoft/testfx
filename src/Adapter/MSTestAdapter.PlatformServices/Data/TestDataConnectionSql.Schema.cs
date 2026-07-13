// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

internal partial class TestDataConnectionSql
{
    #region Schema

#pragma warning disable SA1202 // Elements must be ordered by access

    /// <summary>
    /// Returns default database schema, can be null if there is no default schema like for Excel.
    /// Can throw.
    /// </summary>
    /// <returns>The default database schema.</returns>
    public virtual string? GetDefaultSchema() => null;

#pragma warning restore SA1202 // Elements must be ordered by access

    /// <summary>
    /// Returns list of data tables and views. Sorted.
    /// Any errors, return an empty list.
    /// </summary>
    /// <returns>List of sorted tables and views.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public override List<string> GetDataTablesAndViews()
    {
        WriteDiagnostics("GetDataTablesAndViews");
        List<string> tableNames = [];
        try
        {
            string? defaultSchema = GetDefaultSchema();
            WriteDiagnostics("Default schema is {0}", defaultSchema);

            SchemaMetaData[] metadatas = GetSchemaMetaData();

            // There may be a better way to enumerate tables/views.
            foreach (SchemaMetaData metadata in metadatas)
            {
                DataTable? dataTable = null;
                try
                {
                    WriteDiagnostics("Getting schema table {0}", metadata.SchemaTable);
                    dataTable = Connection.GetSchema(metadata.SchemaTable);
                }
                catch (Exception ex)
                {
                    WriteDiagnostics("Failed to get schema table");

                    // This can be normal case as some providers do not support views.
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Warning("DataUtil.GetDataTablesAndViews: exception (can be normal case as some providers do not support views): " + ex);
                    }

                    continue;
                }

                DebugEx.Assert(dataTable != null, "Failed to get data table that contains metadata about tables!");

                foreach (DataRow row in dataTable.Rows)
                {
                    WriteDiagnostics("Row: {0}", row);
                    string? tableSchema = null;
                    bool isDefaultSchema = false;

                    // Check the table type for validity
                    if (metadata.TableTypeColumn != null)
                    {
                        if (row[metadata.TableTypeColumn] != DBNull.Value)
                        {
                            string? tableType = row[metadata.TableTypeColumn] as string;
                            if (!IsInArray(tableType, metadata.ValidTableTypes))
                            {
                                WriteDiagnostics("Table type {0} is not acceptable", tableType);

                                // Not a valid table type, get the next row
                                continue;
                            }
                        }
                    }

                    // Get the schema name, and filter bad schemas
                    if (row[metadata.SchemaColumn] != DBNull.Value)
                    {
                        tableSchema = row[metadata.SchemaColumn] as string;

                        if (IsInArray(tableSchema, metadata.InvalidSchemas))
                        {
                            WriteDiagnostics("Schema {0} is not acceptable", tableSchema);

                            // A table in a schema we do not want to see, get the next row
                            continue;
                        }

                        isDefaultSchema = string.Equals(tableSchema, defaultSchema, StringComparison.OrdinalIgnoreCase);
                    }

                    string? tableName = row[metadata.NameColumn] as string;
                    WriteDiagnostics("Table {0}{1} found", tableSchema != null ? tableSchema + "." : string.Empty, tableName);

                    // If schema is defined and is not equal to default, prepend table schema in front of the table.
                    string qualifiedTableName = isDefaultSchema
                        ? FormatTableNameForDisplay(null, tableName)
                        : FormatTableNameForDisplay(tableSchema, tableName);

                    WriteDiagnostics("Adding Table {0}", qualifiedTableName);
                    tableNames.Add(qualifiedTableName);
                }

                tableNames.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception e)
        {
            WriteDiagnostics("Failed to fetch tables for {0}, exception: {1}", Connection.ConnectionString, e);

            // OK to fall through and return whatever we do have...
        }

        return tableNames;
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public override List<string>? GetColumns(string tableName)
    {
        WriteDiagnostics("GetColumns for {0}", tableName);
        try
        {
            SplitTableName(tableName, out string? targetSchema, out string? targetName);

            // This lets us specifically query for columns from the appropriate table name
            // but assumes all databases have the same restrictions on all the column
            // schema tables
            string?[] restrictions =
            [
                null,               // Catalog (don't care)
                targetSchema,       // Table schema
                targetName,         // Table name
                null,
            ];             // Column name (don't care)

            DataTable? columns = null;
            try
            {
                columns = Connection.GetSchema("Columns", restrictions);
            }
            catch (NotSupportedException e)
            {
                WriteDiagnostics("GetColumns for {0} failed to get column metadata, exception {1}", tableName, e);
            }

            if (columns is not null)
            {
                List<string> result = [];

                // Add all the columns
                foreach (DataRow columnRow in columns.Rows)
                {
                    WriteDiagnostics("Column info: {0}", columnRow);
                    result.Add(columnRow["COLUMN_NAME"].ToString());
                }

                // Now we are done, since for any particular table or view, all the columns
                // must be found in a single metadata collection
                return result;
            }

            WriteDiagnostics("Column metadata is null");
        }
        catch (Exception e)
        {
            WriteDiagnostics("GetColumns for {0}, failed {1}", tableName, e);
        }

        return null; // Some problem occurred
    }

    /// <summary>
    /// Split a table name into schema and table name, providing default
    /// schema if available.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="schemaName">The schema name output.</param>
    /// <param name="tableName">The table name output.</param>
    protected void SplitTableName(string name, out string? schemaName, out string tableName)
    {
        // Split the name because we need to separately look for
        // tableSchema and tableName
        string[]? parts = SplitName(name);

        DebugEx.Assert(parts?.Length > 0, "parts should have more than one element.");

        // Right now this processing ignores any three part names (where the catalog is specified)
        // We use the default schema if the name does not specify one explicitly
        schemaName = parts.Length > 1 ? parts[parts.Length - 2] : GetDefaultSchema();
        tableName = parts[parts.Length - 1];
    }

    /// <summary>
    /// Returns qualified data table name, formatted for display in Data Table list or use in
    /// code or test files. Note that this may not return a suitable string for SQL.
    /// </summary>
    /// <param name="tableSchema">Schema part of qualified table name. Quoted or not quoted.</param>
    /// <param name="tableName">Table name. Quoted or not quoted.</param>
    /// <returns>Qualified data table name.</returns>
    protected string FormatTableNameForDisplay(string? tableSchema, string? tableName)
    {
        // Note: schema can be null/empty, that is OK
        DebugEx.Assert(!StringEx.IsNullOrEmpty(tableName), "FormatDataTableNameForDisplay should be called only when table name is not empty.");

        return StringEx.IsNullOrEmpty(tableSchema)
            ? JoinAndQuoteName([tableName], false)
            : JoinAndQuoteName([tableSchema, tableName], false);
    }

    /// <summary>
    /// Classify a table schema as being hidden from the user
    /// This helps to hide system tables such as INFORMATION_SCHEMA.COLUMNS.
    /// </summary>
    /// <param name="tableSchema">A candidate table schema.</param>
    /// <returns>True always.</returns>
    protected virtual bool IsUserSchema(string tableSchema) =>
        // Default is to allow all schemas
        true;

    /// <summary>
    /// Returns default database schema. Returns null for error
    /// this.Connection must be already opened.
    /// </summary>
    /// <returns>The default db schema.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    protected string? GetDefaultSchemaMSSql()
    {
        DebugEx.Assert(Connection != null, "Connection should not be null.");

        try
        {
            var oleDbConnection = Connection as OleDbConnection;
            var odbcConnection = Connection as OdbcConnection;
            DebugEx.Assert(
                Connection is SqlConnection ||
                (oleDbConnection != null && IsMSSql(oleDbConnection.Provider)) ||
                (odbcConnection != null && IsMSSql(odbcConnection.Driver)),
                "GetDefaultSchemaMSSql should be called only for MS SQL (either native or Ole Db or Odbc).");

            DebugEx.Assert(IsOpen(), "The connection must already be open!");
            DebugEx.Assert(!StringEx.IsNullOrEmpty(Connection.ServerVersion), "GetDefaultSchema: the ServerVersion is null or empty!");

            int index = Connection.ServerVersion.IndexOf(".", StringComparison.Ordinal);
            DebugEx.Assert(index > 0, "GetDefaultSchema: index should be 0");

            string versionString = Connection.ServerVersion.Substring(0, index);
            DebugEx.Assert(!StringEx.IsNullOrEmpty(versionString), "GetDefaultSchema: version string is not present!");

            int version = int.Parse(versionString, CultureInfo.InvariantCulture);

            // For Yukon (9.0) there are non-default schemas, for MSSql schema is the same as user name.
            string sql = version >= 9 ?
                "select default_schema_name from sys.database_principals where name = user_name()" :
                "select user_name()";

            using DbCommand cmd = Connection.CreateCommand();
            cmd.CommandText = sql;
            string? defaultSchema = cmd.ExecuteScalar() as string;
            return defaultSchema;
        }
        catch (Exception e)
        {
            // Any problems, at least return null, which says there is no default
            WriteDiagnostics("Got an exception trying to determine default schema: {0}", e);
        }

        return null;
    }

    #endregion
}

#endif
