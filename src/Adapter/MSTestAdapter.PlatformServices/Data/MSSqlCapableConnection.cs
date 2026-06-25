// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
/// Abstract base class for OLE DB and ODBC data connections that support MS SQL detection.
/// Centralizes the MS SQL detection, the <see cref="GetQuoteLiterals"/> override, and the
/// <see cref="GetDefaultSchema"/> override that are otherwise duplicated between
/// <see cref="OleDataConnection"/> and <see cref="OdbcDataConnection"/>.
/// </summary>
internal abstract class MSSqlCapableConnection : TestDataConnectionSql
{
    private bool? _isMSSql;

    protected MSSqlCapableConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(invariantProviderName, connectionString, dataFolders)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the underlying connection targets MS SQL Server.
    /// Detection is deferred until first use (and then cached) so that the virtual
    /// <see cref="TestDataConnectionSql.GetProviderNameForMSSqlDetection"/> call happens after
    /// the derived type has been fully constructed.
    /// </summary>
    private bool IsMSSqlConnection => _isMSSql ??= ComputeIsMSSql();

    /// <summary>
    /// This is overridden because OleDb and ODBC do not fill quote literals automatically.
    /// </summary>
    public override void GetQuoteLiterals() => GetQuoteLiteralsHelper();

    public override string? GetDefaultSchema() => IsMSSqlConnection ? GetDefaultSchemaMSSql() : base.GetDefaultSchema();

    private bool ComputeIsMSSql()
    {
        // Need open connection to call GetProviderNameForMSSqlDetection().
        DebugEx.Assert(IsOpen(), "The connection must be open!");

        return Connection is not null && IsMSSql(GetProviderNameForMSSqlDetection());
    }
}

#endif
