// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// Base type for errors raised by the MTP server client.
/// </summary>
internal class MtpServerClientException : Exception
{
    public MtpServerClientException()
    {
    }

    public MtpServerClientException(string message)
        : base(message)
    {
    }

    public MtpServerClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when the connection to the test host is closed while a request is still pending, or before the
/// handshake completes.
/// </summary>
internal sealed class MtpServerConnectionClosedException : MtpServerClientException
{
    public MtpServerConnectionClosedException()
        : base("The connection to the test host process was closed unexpectedly.")
    {
    }

    public MtpServerConnectionClosedException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Thrown when the server answers a request with a JSON-RPC error object.
/// </summary>
internal sealed class MtpServerErrorException : MtpServerClientException
{
    public MtpServerErrorException(int errorCode, string message)
        : base(message)
        => ErrorCode = errorCode;

    /// <summary>
    /// Gets the JSON-RPC error code returned by the server.
    /// </summary>
    public int ErrorCode { get; }
}
