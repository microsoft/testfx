// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data.Common;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
///  Data connections based on direct DB implementations all derive from this one.
/// </summary>
internal partial class TestDataConnectionSql : TestDataConnection
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
}

#endif
