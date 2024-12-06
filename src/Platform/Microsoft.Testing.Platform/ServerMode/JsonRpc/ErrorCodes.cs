// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.ServerMode;

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Common pattern to use public static readonly fields")]
internal sealed class ErrorCodes
{
    #region JSON-RPC error codes
    // JSON-RPC specific error codes.
    public static readonly int ParseError = -32700;
    public static readonly int InvalidRequest = -32600;
    public static readonly int MethodNotFound = -32601;
    public static readonly int InvalidParams = -32602;
    public static readonly int InternalError = -32603;

    // Error code to be thrown if the server has not yet been initialized.
    public static readonly int ServerNotInitialized = -32002;
    #endregion

    #region LSP error codes
    public static readonly int LspErrorRangeStart = -32899;
    public static readonly int RequestCanceled = -32800;
    public static readonly int LspErrorRangeEnd = -32800;
    #endregion

    #region Testing Platform error codes
    public static readonly int TestingPlatformErrorRangeStart = -31700;
    public static readonly int TestingPlatformErrorRangeEnd = -31000;
    #endregion
}
