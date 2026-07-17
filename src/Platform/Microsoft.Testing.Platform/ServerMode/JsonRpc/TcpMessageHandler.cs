// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Buffers;
#endif
using System.Net.Sockets;

#if !NETCOREAPP
using Microsoft.Testing.Platform.Helpers;
#endif
using Microsoft.Testing.Platform.Logging;

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
    private readonly ILogger _logger = new NopLogger();

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpMessageHandler"/> class with a logger for low-noise
    /// transport diagnostics (e.g. connection resets).
    /// </summary>
    public TcpMessageHandler(
        TcpClient client,
        Stream clientToServerStream,
        Stream serverToClientStream,
        IMessageFormatter formatter,
        ILogger logger)
        : this(client, clientToServerStream, serverToClientStream, formatter)
        => _logger = logger;

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
        catch (Exception ex) when
            (ex is
                 SocketException { SocketErrorCode: SocketError.ConnectionReset } or
                 IOException
                 {
                     InnerException: SocketException { SocketErrorCode: SocketError.ConnectionReset }
                 })
        {
            QueueLogDebug($"TCP connection reset while reading; treating as client disconnect: {ex}");
            return null;
        }
    }

    private void QueueLogDebug(string message)
        => _ = Task.Run(async () =>
        {
            try
            {
                await _logger.LogDebugAsync(message).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A graceful disconnect must remain graceful even when a logging provider fails.
            }
        });

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

        // Encode the message body manually so Content-Length matches the UTF-8 byte count and
        // the body can be written directly to the stream without StreamWriter transcoding.
#if NETCOREAPP
        int byteCount = Encoding.UTF8.GetByteCount(messageStr);
        byte[] rentedBytes = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            Encoding.UTF8.GetBytes(messageStr, rentedBytes);
            await _writer.WriteLineAsync($"Content-Length: {byteCount}").ConfigureAwait(false);
            await _writer.WriteLineAsync("Content-Type: application/testingplatform").ConfigureAwait(false);
            await _writer.WriteLineAsync().ConfigureAwait(false);
            // Flush the StreamWriter's char buffer so the headers reach the underlying NetworkStream
            // before we write the body bytes directly to BaseStream below (otherwise the body would
            // overtake the still-buffered headers). No BaseStream.FlushAsync is needed here or after
            // the body write because the underlying stream is always a NetworkStream (see
            // MessageHandlerFactory) and NetworkStream.Flush/FlushAsync is a no-op.
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _writer.BaseStream.WriteAsync(rentedBytes.AsMemory(0, byteCount), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBytes);
        }
#else
        byte[] messageBytes = Encoding.UTF8.GetBytes(messageStr);
        await _writer.WriteLineAsync($"Content-Length: {messageBytes.Length}").ConfigureAwait(false);
        await _writer.WriteLineAsync("Content-Type: application/testingplatform").ConfigureAwait(false);
        await _writer.WriteLineAsync().ConfigureAwait(false);

        // See the NETCOREAPP branch above for why only StreamWriter.FlushAsync (not
        // BaseStream.FlushAsync) is required here.
        await _writer.FlushAsync().ConfigureAwait(false);
        await _writer.BaseStream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken).ConfigureAwait(false);
#endif
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

        if (!OperatingSystem.IsBrowser())
        {
            _client.Dispose();
        }
    }
}
