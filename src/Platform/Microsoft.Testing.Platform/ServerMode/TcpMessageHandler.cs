// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class TcpMessageHandler : StreamMessageHandler
{
    private readonly TcpClient _client;

    public TcpMessageHandler(
        TcpClient client,
        Stream clientToServerStream,
        Stream serverToClientStream,
        IMessageFormatter formatter)
        : base(clientToServerStream, serverToClientStream, formatter)
    {
        _client = client;
    }

    public override async Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await base.ReadAsync(cancellationToken);
        }

        // Client close the connection in an unexpected way
        catch (Exception ex)
        {
            if (ex is SocketException se)
            {
                if (se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    return null;
                }
            }

            if (ex is IOException iOException && iOException.InnerException is SocketException iose)
            {
                if (iose.SocketErrorCode == SocketError.ConnectionReset)
                {
                    return null;
                }
            }

            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _client.Close();
    }
}
