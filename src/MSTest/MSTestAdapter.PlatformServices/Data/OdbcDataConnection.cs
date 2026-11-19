// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Data.Odbc;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///      Utility classes to access databases, and to handle quoted strings etc for ODBC.
/// </summary>
internal sealed class OdbcDataConnection : MSSqlCapableConnection
{
    public OdbcDataConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(invariantProviderName, FixConnectionString(connectionString, dataFolders), dataFolders)
    {
    }

    public new OdbcCommandBuilder CommandBuilder => (OdbcCommandBuilder)base.CommandBuilder;

    public new OdbcConnection Connection => (OdbcConnection)base.Connection;

    protected override string? GetProviderNameForMSSqlDetection() => Connection.Driver;

    protected override SchemaMetaData[] GetSchemaMetaData()
    {
        // The following may fail for Oracle ODBC, need to test that...
        SchemaMetaData data1 = new()
        {
            SchemaTable = "Tables",
            SchemaColumn = "TABLE_SCHEM",
            NameColumn = "TABLE_NAME",
            TableTypeColumn = "TABLE_TYPE",
            ValidTableTypes = ["TABLE", "SYSTEM TABLE"],
            InvalidSchemas = null,
        };
        SchemaMetaData data2 = new()
        {
            SchemaTable = "Views",
            SchemaColumn = "TABLE_SCHEM",
            NameColumn = "TABLE_NAME",
            TableTypeColumn = "TABLE_TYPE",
            ValidTableTypes = ["VIEW"],
            InvalidSchemas = ["sys", "INFORMATION_SCHEMA"],
        };
        return [data1, data2];
    }

    // Need to fix up excel connections
    private static string FixConnectionString(string connectionString, List<string> dataFolders)
    {
        OdbcConnectionStringBuilder builder = [with(connectionString)];

        // only fix this for excel
        return !string.Equals(builder.Dsn, "Excel Files", StringComparison.Ordinal)
            ? connectionString
            : FixConnectionStringFilePath(
                builder,
                connectionString,
                () => builder["dbq"] as string,
                fixedFilePath => builder["dbq"] = fixedFilePath,
                dataFolders);
    }
}
#endif
