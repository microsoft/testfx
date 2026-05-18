// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;

using StringEx = Microsoft.VisualStudio.TestTools.UnitTesting.StringEx;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

internal partial class TestDataConnectionSql
{
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
#pragma warning restore SA1202 // Elements must be ordered by access

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
    /// msorcl32.dll: Driver={Microsoft ODBC for Oracle};Server=OracleServer;Uid=&lt;user&gt;;Pwd=&lt;password&gt;.
    /// </remarks>
    protected static class KnownOdbcDrivers
    {
        internal const string MSSql = "sqlsrv32.dll";
    }

    #endregion
}

#endif
