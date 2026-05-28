// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

/// <summary>
/// Exception used to surface a JSON-RPC error from request handling so that the
/// server message loop can convert it into a properly coded JSON-RPC error
/// response instead of a generic exception payload.
/// </summary>
internal sealed class JsonRpcException : Exception
{
    public JsonRpcException(int errorCode, string message)
        : base(message)
        => ErrorCode = errorCode;

    public int ErrorCode { get; }
}
