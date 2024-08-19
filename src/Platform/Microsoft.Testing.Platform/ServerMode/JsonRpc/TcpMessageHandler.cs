// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class TcpMessageHandler(
    TcpClient client,
    Stream clientToServerStream,
    Stream serverToClientStream,
    IMessageFormatter formatter) : StreamMessageHandler(clientToServerStream, serverToClientStream, formatter)
{
    private readonly TcpClient _client = client;

    public override async Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await base.ReadAsync(cancellationToken);
        }

        // Client close the connection in an unexpected way
        catch (Exception ex)
        {
            switch (ex)
            {
                case SocketException { SocketErrorCode: SocketError.ConnectionReset }:
                case IOException { InnerException: SocketException { SocketErrorCode: SocketError.ConnectionReset } }:
                    return null;
                default:
                    throw;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _client.Close();
    }
}
