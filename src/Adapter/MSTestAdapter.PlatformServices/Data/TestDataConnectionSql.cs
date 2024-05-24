// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///  Data connections based on direct DB implementations all derive from this one.
/// </summary>
internal class TestDataConnectionSql : TestDataConnection
{
    private readonly DbConnection _connection;
    private string? _quoteSuffix;
    private string? _quotePrefix;

    #region Constructor

    protected internal TestDataConnectionSql(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(dataFolders)
    {
        Factory = DbProviderFactories.GetFactory(invariantProviderName);
        DebugEx.Assert(Factory != null, "factory should not be null.");
        WriteDiagnostics("DbProviderFactory {0}", Factory);

        _connection = Factory.CreateConnection();
        DebugEx.Assert(_connection != null, "connection");
        WriteDiagnostics("DbConnection {0}", _connection);

        CommandBuilder = Factory.CreateCommandBuilder();
        DebugEx.Assert(CommandBuilder != null, "builder");
        WriteDiagnostics("DbCommandBuilder {0}", CommandBuilder);

        if (!StringEx.IsNullOrEmpty(connectionString))
        {
            _connection.ConnectionString = connectionString;
            WriteDiagnostics("Current directory: {0}", Directory.GetCurrentDirectory());
            WriteDiagnostics("Opening connection {0}: {1}", invariantProviderName, connectionString);
            _connection.Open();
        }

        WriteDiagnostics("Connection state is {0}", _connection.State);
    }

    #endregion

    #region Data Properties

    public override DbConnection Connection => _connection;

    protected DbCommandBuilder CommandBuilder { get; }

    protected DbProviderFactory Factory { get; }

    #endregion

    public static TestDataConnectionSql Create(string invariantProviderName, string connectionString, List<string> dataFolders)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(invariantProviderName), "invariantProviderName");

        // unit tests pass a null for connection string, so let it pass. However, not all
        // providers can handle that, an example being ODBC
        WriteDiagnostics("CreateSql {0}, {1}", invariantProviderName, connectionString);

        // For invariant providers we recognize, we have specific sub-classes
        if (string.Equals(invariantProviderName, "System.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlDataConnection(invariantProviderName, connectionString, dataFolders);
        }
        else if (string.Equals(invariantProviderName, "System.Data.OleDb", StringComparison.OrdinalIgnoreCase))
        {
            return new OleDataConnection(invariantProviderName, connectionString, dataFolders);
        }
        else if (string.Equals(invariantProviderName, "System.Data.Odbc", StringComparison.OrdinalIgnoreCase))
        {
            return new OdbcDataConnection(invariantProviderName, connectionString, dataFolders);
        }
        else
        {
            // All other providers handled by my base class
            WriteDiagnostics("Using default SQL implementation for {0}, {1}", invariantProviderName, connectionString);
            return new TestDataConnectionSql(invariantProviderName, connectionString, dataFolders);
        }
    }

    protected virtual SchemaMetaData[] GetSchemaMetaData()
    {
        // A bare minimum set of things that should vaguely work for all databases
        SchemaMetaData data = new()
        {
            SchemaTable = "Tables",
            SchemaColumn = null,
            NameColumn = "TABLE_NAME",
            TableTypeColumn = null,
            ValidTableTypes = null,
            InvalidSchemas = null,
        };
        return [data];
    }

    #region Quotes

#pragma warning disable SA1201 // Elements must appear in the correct order
    [MemberNotNull(nameof(_quotePrefix))]
    public virtual string QuotePrefix
#pragma warning restore SA1201 // Elements must appear in the correct order
    {
        get
        {
            if (StringEx.IsNullOrEmpty(_quotePrefix))
            {
                GetQuoteLiterals();
            }

            return _quotePrefix;
        }

        set => _quotePrefix = value;
    }

    [MemberNotNull(nameof(_quoteSuffix))]
    public virtual string QuoteSuffix
    {
        get
        {
            if (StringEx.IsNullOrEmpty(_quoteSuffix))
            {
                GetQuoteLiterals();
            }

            return _quoteSuffix;
        }

        set => _quoteSuffix = value;
    }

    private char CatalogSeparatorChar
    {
        get
        {
            if (CommandBuilder != null)
            {
                string catalogSeparator = CommandBuilder.CatalogSeparator;
                if (!StringEx.IsNullOrEmpty(catalogSeparator))
                {
                    DebugEx.Assert(catalogSeparator.Length == 1, "catalogSeparator should have 1 element.");
                    return catalogSeparator[0];
                }
            }

            return '.';
        }
    }

    private char SchemaSeparatorChar
    {
        get
        {
            if (CommandBuilder != null)
            {
                string schemaSeparator = CommandBuilder.SchemaSeparator;
                if (!StringEx.IsNullOrEmpty(schemaSeparator))
                {
                    DebugEx.Assert(schemaSeparator.Length == 1, "schemaSeparator should have 1 element.");
                    return schemaSeparator[0];
                }
            }

            return '.';
        }
    }

    /// <summary>
    /// Take a possibly qualified name with at least minimal quoting
    /// and return a fully quoted string
    /// Take care to only convert names that are of a recognized form.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <returns>A fully quoted string.</returns>
    public string PrepareNameForSql(string tableName)
    {
        string[]? parts = SplitName(tableName);

        if (parts is { Length: > 0 })
        {
            // Seems to be well formed, so make sure we end up fully quoted
            return JoinAndQuoteName(parts, true);
        }
        else
        {
            // Just use what they gave us, literally, since we do not really understand the format
            return tableName;
        }
    }

    /// <summary>
    /// Take a possibly qualified name and break it down into an
    /// array of identifiers unquoting any quoted names.
    /// </summary>
    /// <param name="name">A string.</param>
    /// <returns>An array of unquoted parts, or null if the name fails to conform.</returns>
    public string[]? SplitName(string name)
    {
        List<string> parts = [];

        int here = 0;
        int end = name.Length;
        char firstDelimiter = ' '; // initialize since code analysis is not smart enough
        char currentDelimiter;
        char catalogSeparatorChar = CatalogSeparatorChar;
        char schemaSeparatorChar = SchemaSeparatorChar;

        while (here < end)
        {
            int next = FindIdentifierEnd(name, here);
            string identifier = name.Substring(here, next - here);

            if (StringEx.IsNullOrEmpty(identifier))
            {
                // Not well formed, split failed
                return null;
            }

            if (identifier.StartsWith(QuotePrefix, StringComparison.Ordinal))
            {
                identifier = UnquoteIdentifier(identifier);
            }

            parts.Add(identifier);

            if (next < end)
            {
                currentDelimiter = name[next];
                switch (parts.Count)
                {
                    case 1:
                        // We infer there will be at least 2 parts
                        firstDelimiter = currentDelimiter;
                        if (firstDelimiter != catalogSeparatorChar
                            && firstDelimiter != schemaSeparatorChar)
                        {
                            // Not well formed, split failed
                            return null;
                        }

                        break;

                    case 2:
                        // We infer there will be at least 3 parts
                        if (firstDelimiter != catalogSeparatorChar
                            || currentDelimiter != schemaSeparatorChar)
                        {
                            // Not well formed, split failed
                            return null;
                        }

                        break;

                    default:
                        // We infer there will be at least 4 or more parts
                        // so not well formed, split failed
                        return null;
                }

                // Skip delimiter
                here = next + 1;
            }
            else
            {
                // We have found the end
                if (parts.Count == 2 && firstDelimiter != schemaSeparatorChar)
                {
                    // Not well formed, split failed
                    return null;
                }

                return parts.ToArray();
            }
        }

        // Ended in a delimiter, or no parts at all, either is invalid
        return null;
    }

    /// <summary>
    /// Take a list of unquoted name parts and join them into a
    /// qualified name. Either minimally quote (to the extent required
    /// to reliably split the name again) or fully quote, therefore made suitable
    /// for a database query.
    /// </summary>
    /// <param name="parts">Name parts.</param>
    /// <param name="fullyQuote">Should full quote.</param>
    /// <returns>A qualified name.</returns>
    public string JoinAndQuoteName(string[] parts, bool fullyQuote)
    {
        int partCount = parts.Length;
        StringBuilder result = new();

        DebugEx.Assert(partCount is > 0 and < 4, "partCount should be 1,2 or 3.");

        int currentPart = 0;
        if (partCount > 2)
        {
            result.Append(MaybeQuote(parts[currentPart++], fullyQuote));
            result.Append(CommandBuilder.CatalogSeparator);
        }

        if (partCount > 1)
        {
            result.Append(MaybeQuote(parts[currentPart++], fullyQuote));
            result.Append(CommandBuilder.SchemaSeparator);
        }

        result.Append(MaybeQuote(parts[currentPart], fullyQuote));
        return result.ToString();
    }

    /// <summary>
    /// Note that for Oledb and Odbc CommandBuilder.QuotePrefix/Suffix is empty.
    /// So we use GetQuoteLiterals for those. For all others we use CommandBuilder.QuotePrefix/Suffix.
    /// </summary>
    [MemberNotNull(nameof(_quotePrefix), nameof(_quoteSuffix))]
    public virtual void GetQuoteLiterals()
    {
        _quotePrefix = CommandBuilder.QuotePrefix;
        _quoteSuffix = CommandBuilder.QuoteSuffix;
    }

    protected virtual string QuoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier should not be null.");
        return CommandBuilder.QuoteIdentifier(identifier);
    }

    protected virtual string UnquoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier should not be null.");
        return CommandBuilder.UnquoteIdentifier(identifier);
    }

    [MemberNotNull(nameof(_quotePrefix), nameof(_quoteSuffix), nameof(QuotePrefix), nameof(QuoteSuffix))]
    protected void GetQuoteLiteralsHelper()
    {
        // Try to get quote chars by hand for those providers that for some reason return empty QuotePrefix/Suffix.
        string s = "abcdefgh";
        string quoted = QuoteIdentifier(s);
        string[] parts = quoted.Split(new string[] { s }, StringSplitOptions.None);

        DebugEx.Assert(parts is { Length: 2 }, "TestDataConnectionSql.GetQuotesLiteralHelper: Failure when trying to quote an identifier!");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(parts[0]), "TestDataConnectionSql.GetQuotesLiteralHelper: Trying to set empty value for QuotePrefix!");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(parts[1]), "TestDataConnectionSql.GetQuotesLiteralHelper: Trying to set empty value for QuoteSuffix!");

        QuotePrefix = parts[0];
        QuoteSuffix = parts[1];
    }

    private string MaybeQuote(string identifier, bool force) => force || FindSeparators(identifier, 0) != -1 ? QuoteIdentifier(identifier) : identifier;

    /// <summary>
    /// Find the first separator in a string.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="from">Index.</param>
    /// <returns>Location of the separator.</returns>
    private int FindSeparators(string text, int from) => text.IndexOfAny([SchemaSeparatorChar, CatalogSeparatorChar], from);

    /// <summary>
    /// Given a string and a position in that string, assumed
    /// to be the start of an identifier, find the end of that
    /// identifier. Take into account quoting rules.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="start">start index.</param>
    /// <returns>Position in string after end of identifier (may be off end of string).</returns>
    private int FindIdentifierEnd(string text, int start)
    {
        // These routine assumes prefixes and suffixes
        // are single characters
        string prefix = QuotePrefix;
        DebugEx.Assert(prefix.Length == 1, "prefix length should be 1.");
        char prefixChar = prefix[0];

        int end = text.Length;
        if (text[start] == prefixChar)
        {
            // Identifier is quoted. Repeatedly look for
            // suffix character, until not found,
            // the character after is end of string,
            // or not another suffix character

            // Skip opening quote
            int here = start + 1;

            string suffix = QuoteSuffix;
            DebugEx.Assert(suffix.Length == 1, "suffix length should be 1.");
            char suffixChar = suffix[0];

            while (here < end)
            {
                here = text.IndexOf(suffixChar, here);
                if (here == -1)
                {
                    // If this happens the string is malformed, since we had an
                    // opening quote without a closing one, but we can survive this
                    break;
                }

                // Skip the quote we just found
                here++;

                // If this the end?
                if (here == end || text[here] != suffixChar)
                {
                    // Well formed end of identifier
                    return here;
                }

                // We have a double quote, skip the second one, then keep looking
                here++;
            }

            // If we fall off end of loop,
            // we didn't find the matching close quote
            // Best thing to do is to just return the whole string
            return end;
        }
        else
        {
            // In the case of an unquoted strings, the processing is much
            // simpler... the end is end of string, or the first
            // of several possible separators.
            int separatorPosition = FindSeparators(text, start);
            return separatorPosition == -1 ? end : separatorPosition;
        }
    }

    #endregion

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

            // TODO: may be find better way to enumerate tables/views.
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
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DataUtil.GetDataTablesAndViews: exception (can be normal case as some providers do not support views): " + ex);
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
                    string? qualifiedTableName = tableName;
                    qualifiedTableName = isDefaultSchema
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

            if (columns != null)
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
            else
            {
                WriteDiagnostics("Column metadata is null");
            }
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
    /// Just a helper method to see if a string is in a string array
    /// Note that the array can be null, this is treated as an empty array.
    /// </summary>
    /// <param name="candidate">The string.</param>
    /// <param name="values">An array of values.</param>
    /// <returns>True if string exists in array.</returns>
    private static bool IsInArray(string? candidate, string[]? values)
    {
        if (values == null)
        {
            return false;
        }

        foreach (string value in values)
        {
            if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Helpers

#pragma warning disable SA1202 // Elements must be ordered by access
    public bool IsOpen() => _connection is { State: ConnectionState.Open };

    /// <summary>
    /// Returns true when given provider (OLEDB or ODBC) is for MSSql.
    /// </summary>
    /// <param name="providerName">OLEDB or ODBC provider.</param>
    /// <returns>True if provider is for MSSql.</returns>
    protected static bool IsMSSql(string providerName) => (!StringEx.IsNullOrEmpty(providerName) &&
            (providerName.StartsWith(KnownOleDbProviderNames.SqlOleDb, StringComparison.OrdinalIgnoreCase) ||
             providerName.StartsWith(KnownOleDbProviderNames.MSSqlNative, StringComparison.OrdinalIgnoreCase))) ||
             string.Equals(providerName, KnownOdbcDrivers.MSSql, StringComparison.OrdinalIgnoreCase);

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
        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info("ReadTable: data driven test: got table name from attribute: {0}", tableName);
            EqtTrace.Info("ReadTable: data driven test: will use table name: {0}", tableName);
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

    [SuppressMessage("Microsoft.Usage", "CA2215:Dispose methods should call base class dispose", Justification = "Un-tested. Just preserving behavior.")]
#pragma warning disable SA1202 // Elements must be ordered by access
    public override void Dispose()
#pragma warning restore SA1202 // Elements must be ordered by access
    {
        // Ensure that we Dispose of all disposables...
        CommandBuilder?.Dispose();
        _connection?.Dispose();

        GC.SuppressFinalize(this);
    }

    #region Types

    /// <summary>
    /// When querying for tables, metadata varies quite a bit from DB to DB
    /// This struct encapsulates those variations.
    /// </summary>
    protected struct SchemaMetaData
    {
        // Name of a table containing tables or views
        public string? SchemaTable;

        // Column that contains schema names, if null, unused
        public string? SchemaColumn;

        // Column that contains the table names
        public string? NameColumn;

        // Column that contains a table "type", if null, type is unchecked
        public string? TableTypeColumn;

        // If table type is available, it is checked to be one of the values on this list
        public string[]? ValidTableTypes;

        // If schema is available, it is checked to not be one of the values on this list
        public string[]? InvalidSchemas;
    }

    /// <summary>
    /// Known OLE DB providers for MS SQL and Oracle.
    /// </summary>
    /// <remarks>
    /// How Data Connection dialog maps to different providers:
    /// SqlOleDb:   Data Source = MS SQL, Provider = OLE DB
    /// SqlOleDb.1: Data Source = Other,  Provider = Microsoft OLE DB Provider for Sql Server
    ///     Provider=SQLOLEDB;Data Source=SqlServer;Integrated Security=SSPI;Initial Catalog=DatabaseName
    /// SQLNCLI.1:  Data Source = Other,  Provider = Sql Native Client
    ///     Provider=SQLNCLI.1;Data Source=SqlServer;Integrated Security=SSPI;Initial Catalog=DatabaseName.
    /// </remarks>
    protected static class KnownOleDbProviderNames
    {
        internal const string SqlOleDb = "SQLOLEDB";
        internal const string MSSqlNative = "SQLNCLI";

        // Note MSDASQL (OLE DB Provider for ODBC) is not supported by .NET.
    }

    /// <summary>
    /// Known ODBC drivers.
    /// </summary>
    /// <remarks>
    /// sqlsrv32.dll: Driver={SQL Server};Server=SqlServer;Database=DatabaseName;Trusted_Connection=yes
    /// msorcl32.dll: Driver={Microsoft ODBC for Oracle};Server=OracleServer;Uid=user;Pwd=password.
    /// </remarks>
    protected static class KnownOdbcDrivers
    {
        internal const string MSSql = "sqlsrv32.dll";
    }

    #endregion
}

#endif
