// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

/// <summary>
/// Abstract base class for OLE DB and ODBC data connections that support MS SQL detection.
/// Centralizes the <c>_isMSSql</c> field, the <see cref="GetQuoteLiterals"/> override, and the
/// <see cref="GetDefaultSchema"/> override that are otherwise duplicated between
/// <see cref="OleDataConnection"/> and <see cref="OdbcDataConnection"/>.
/// </summary>
internal abstract class MSSqlCapableConnection : TestDataConnectionSql
{
    private readonly bool _isMSSql;

    protected MSSqlCapableConnection(string invariantProviderName, string connectionString, List<string> dataFolders)
        : base(invariantProviderName, connectionString, dataFolders)
    {
        // Need open connection to call GetProviderNameForMSSqlDetection().
        DebugEx.Assert(IsOpen(), "The connection must be open!");

        _isMSSql = Connection != null && IsMSSql(GetProviderNameForMSSqlDetection());
    }

    /// <summary>
    /// This is overridden because OleDb and ODBC do not fill quote literals automatically.
    /// </summary>
    public override void GetQuoteLiterals() => GetQuoteLiteralsHelper();

    public override string? GetDefaultSchema() => _isMSSql ? GetDefaultSchemaMSSql() : base.GetDefaultSchema();
}

#endif
