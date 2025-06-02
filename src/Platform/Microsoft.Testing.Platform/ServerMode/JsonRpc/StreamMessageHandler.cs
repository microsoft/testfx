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

internal class StreamMessageHandler : IMessageHandler, IDisposable
{
    private readonly StreamReader _reader;
    private readonly NetworkStream _serverToClientStream;
    private readonly IMessageFormatter _formatter;
    private bool _isDisposed;

    public StreamMessageHandler(
        NetworkStream clientToServerStream,
        NetworkStream serverToClientStream,
        IMessageFormatter formatter)
    {
        _reader = new StreamReader(clientToServerStream);
        _serverToClientStream = serverToClientStream;
        _formatter = formatter;
    }

    public virtual async Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
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
            int commandSize = await ReadHeadersAsync(cancellationToken);

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
                await _reader.ReadBlockAsync(memoryBuffer, cancellationToken);
                return _formatter.Deserialize<RpcMessage>(memoryBuffer);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(commandCharsBuffer);
            }
#else
            char[] commandChars = new char[commandSize];
            await _reader.ReadBlockAsync(commandChars, 0, commandSize).WithCancellationAsync(cancellationToken);
            return _formatter.Deserialize<RpcMessage>(new string(commandChars, 0, commandSize));
#endif
        }
    }

    private async Task<int> ReadHeadersAsync(CancellationToken cancellationToken)
    {
        int contentSize = -1;

        while (true)
        {
#if NET7_0_OR_GREATER
            string? line = await _reader.ReadLineAsync(cancellationToken);
#elif NET6_0_OR_GREATER
            string? line = await _reader.ReadLineAsync().WaitAsync(cancellationToken);
#else
            string? line = await _reader.ReadLineAsync().WithCancellationAsync(cancellationToken);
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

#if NETCOREAPP
    private static ReadOnlyMemory<byte> ContentLengthPrefix { get; } = "Content-Length: "u8.ToArray();

    private static ReadOnlyMemory<byte> ContentTypeLastHeader { get; } = "\r\nContent-Type: application/testingplatform\r\n\r\n"u8.ToArray();
#else
    private static byte[] ContentLengthPrefix { get; } = [67, 111, 110, 116, 101, 110, 116, 45, 76, 101, 110, 103, 116, 104, 58, 32];

    private static byte[] ContentTypeLastHeader { get; } = [13, 10, 67, 111, 110, 116, 101, 110, 116, 45, 84, 121, 112, 101, 58, 32, 97, 112, 112, 108, 105, 99, 97, 116, 105, 111, 110, 47, 116, 101, 115, 116, 105, 110, 103, 112, 108, 97, 116, 102, 111, 114, 109, 13, 10, 13, 10];
#endif

#pragma warning disable CA1416 // Validate platform compatibility - unsupported on browser
    public async Task WriteRequestAsync(RpcMessage message, CancellationToken cancellationToken)
    {
        // Here is what we want to write:
        // Content-Length: <content-length>
        // Content-Type: application/testingplatform
        //
        // <content>
        string messageStr = await _formatter.SerializeAsync(message);

#if NETCOREAPP
        await _serverToClientStream.WriteAsync(ContentLengthPrefix, cancellationToken);
#else
        await _serverToClientStream.WriteAsync(ContentLengthPrefix, 0, ContentLengthPrefix.Length, cancellationToken);
#endif

        string contentLengthValue = Encoding.UTF8.GetByteCount(messageStr).ToString(CultureInfo.InvariantCulture);
        foreach (char contentLengthDigit in contentLengthValue)
        {
            _serverToClientStream.WriteByte((byte)contentLengthDigit);
        }

#if NETCOREAPP
        await _serverToClientStream.WriteAsync(ContentTypeLastHeader, cancellationToken);
#else
        await _serverToClientStream.WriteAsync(ContentTypeLastHeader, 0, ContentLengthPrefix.Length, cancellationToken);
#endif

        byte[] messageBytes = Encoding.UTF8.GetBytes(messageStr);
#if NETCOREAPP
        await _serverToClientStream.WriteAsync(messageBytes.AsMemory(), cancellationToken);
#else
        await _serverToClientStream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
#endif
    }
#pragma warning restore CA1416 // Validate platform compatibility - unsupported on browser

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                _reader.Dispose();
                try
                {
                    _serverToClientStream.Dispose();
                }
                catch (InvalidOperationException)
                {
                    // We can exit the server without wait that the streaming activity is completed.
                    // In that case we can get an InvalidOperationException
                    // (https://learn.microsoft.com/dotnet/api/system.io.streamwriter.writelineasync?view=net-7.0#system-io-streamwriter-writelineasync(system-string)):
                    // The stream writer is currently in use by a previous write operation.
                }
            }

            _isDisposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
