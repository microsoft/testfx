// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Buffers;
#endif
using System.Net.Sockets;

#if !NETCOREAPP
using Microsoft.Testing.Platform.Helpers;
#endif

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class TcpMessageHandler(
    TcpClient client,
    Stream clientToServerStream,
    Stream serverToClientStream,
    IMessageFormatter formatter) : IMessageHandler, IDisposable
{
    private readonly TcpClient _client = client;
    private readonly StreamReader _reader = new(clientToServerStream);
    private readonly StreamWriter _writer = new(serverToClientStream)
    {
        // We need to force the NewLine because in Windows and nix different char sequence are used
        // https://learn.microsoft.com/dotnet/api/system.io.textwriter.newline?view=net-7.0
        NewLine = "\r\n",
    };

    private readonly IMessageFormatter _formatter = formatter;

    public async Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Reads an RPC message.
            // The message is currently encoded by writing a list of headers
            // and then passing a byte stream with the message.
            // The headers include the size of the byte stream.
            // Content-Length: [content-length]\r\n
            // Content-Type: [mime-type]\r\n
            // \r\n
            // [content]\r\n
            while (true)
            {
                int commandSize = await ReadHeadersAsync(cancellationToken).ConfigureAwait(false);

                // Most probably connection lost
                if (commandSize is -1)
                {
                    return null;
                }

#if NETCOREAPP
                char[] commandCharsBuffer = ArrayPool<char>.Shared.Rent(commandSize);
                try
                {
                    Memory<char> memoryBuffer = new(commandCharsBuffer, 0, commandSize);
                    await _reader.ReadBlockAsync(memoryBuffer, cancellationToken).ConfigureAwait(false);
                    return _formatter.Deserialize<RpcMessage>(memoryBuffer);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(commandCharsBuffer);
                }
#else
                char[] commandChars = new char[commandSize];
                await _reader.ReadBlockAsync(commandChars, 0, commandSize).WithCancellationAsync(cancellationToken).ConfigureAwait(false);
                return _formatter.Deserialize<RpcMessage>(new string(commandChars, 0, commandSize));
#endif
            }
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

    private async Task<int> ReadHeadersAsync(CancellationToken cancellationToken)
    {
        int contentSize = -1;

        while (true)
        {
#if NET7_0_OR_GREATER
            string? line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#elif NET6_0_OR_GREATER
            string? line = await _reader.ReadLineAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
#else
            string? line = await _reader.ReadLineAsync().WithCancellationAsync(cancellationToken).ConfigureAwait(false);
#endif
            if (line is null || (line.Length == 0 && contentSize != -1))
            {
                break;
            }

            const string ContentLengthHeaderName = "Content-Length:";
            // Content type is not mandatory, and we don't use it.
            if (line.StartsWith(ContentLengthHeaderName, StringComparison.OrdinalIgnoreCase))
            {
#if NETCOREAPP
                _ = int.TryParse(line.AsSpan()[ContentLengthHeaderName.Length..].Trim(), out contentSize);
#else
                _ = int.TryParse(line[ContentLengthHeaderName.Length..].Trim(), out contentSize);
#endif
            }
        }

        return contentSize;
    }

    public async Task WriteRequestAsync(RpcMessage message, CancellationToken cancellationToken)
    {
        string messageStr = await _formatter.SerializeAsync(message).ConfigureAwait(false);
        await _writer.WriteLineAsync($"Content-Length: {Encoding.UTF8.GetByteCount(messageStr)}").ConfigureAwait(false);
        await _writer.WriteLineAsync("Content-Type: application/testingplatform").ConfigureAwait(false);
        await _writer.WriteLineAsync().ConfigureAwait(false);
        await _writer.WriteAsync(messageStr).ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _reader.Dispose();

        try
        {
            _writer.Dispose();
        }
        catch (InvalidOperationException)
        {
            // We can exit the server without wait that the streaming activity is completed.
            // In that case we can get an InvalidOperationException
            // (https://learn.microsoft.com/dotnet/api/system.io.streamwriter.writelineasync?view=net-7.0#system-io-streamwriter-writelineasync(system-string)):
            // The stream writer is currently in use by a previous write operation.
        }

#pragma warning disable CA1416 // Validate platform compatibility
        _client.Dispose();
#pragma warning restore CA1416
    }
}
