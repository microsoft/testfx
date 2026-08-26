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
    private const int ReadBufferSize = 4096;
    private const int HeaderLineInitialCapacity = 256;

    private readonly TcpClient _client = client;

    // The read side deliberately does NOT use a StreamReader. Content-Length is declared in UTF-8 *bytes*
    // (see WriteRequestAsync), so the body must be consumed as bytes and decoded afterwards. A StreamReader
    // hands out decoded characters, which for multi-byte UTF-8 content are fewer units than the declared
    // length: the reader under-reads the frame, leaves its tail in the stream, and the framing permanently
    // desynchronizes from the next frame onwards. Reading the headers through a StreamReader and the body
    // from BaseStream would be worse still, because the reader's internal buffer would have already
    // swallowed part of the body. Headers and body are therefore both read through this one byte-level
    // buffer, so nothing can be buffered on the other side of the boundary.
    private readonly Stream _readStream = clientToServerStream;
    private readonly byte[] _readBuffer = new byte[ReadBufferSize];

    private readonly StreamWriter _writer = new(serverToClientStream)
    {
        // We need to force the NewLine because in Windows and nix different char sequence are used
        // https://learn.microsoft.com/dotnet/api/system.io.textwriter.newline?view=net-7.0
        NewLine = "\r\n",
    };

    private readonly IMessageFormatter _formatter = formatter;
    private readonly ILogger _logger = new NopLogger();

    // Reused across header lines so the hot read path (server mode emits a notification per test) does not
    // allocate a fresh buffer per line. Safe to keep as state for the same reason _readBufferOffset/
    // _readBufferCount are: reads are single-threaded by construction, driven by exactly one read loop.
    //
    // It grows to the longest header line seen on the connection and stays there. Unlike a message body,
    // a header line has no legitimate large case, so this is a weaker guarantee than the one that justifies
    // leaving Content-Length uncapped: a peer that never sends a line terminator would grow it without
    // bound. It is left uncapped because server mode is a loopback channel to a test host this process
    // launched, but if a cap is ever wanted, headers are the cheaper and more defensible place to put one.
    private byte[] _headerLineBuffer = new byte[HeaderLineInitialCapacity];

    private int _readBufferOffset;
    private int _readBufferCount;
    private bool _preambleHandled;

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

                // Content-Length counts UTF-8 bytes, so consume exactly that many bytes and hand them to the
                // formatter as bytes. Reading characters here would under-read every multi-byte frame.
                //
                // commandSize is known non-negative (ReadHeadersAsync rejects anything else) but is otherwise
                // taken from the wire without an upper bound. That is deliberate and pre-existing: server mode
                // is a loopback channel to a test host this process launched, and legitimate bodies are
                // unbounded in principle (a test node update can carry an arbitrarily large stack trace or
                // captured stdout), so any cap would be a guess that risks rejecting valid traffic.
#if NETCOREAPP
                byte[] bodyBuffer = ArrayPool<byte>.Shared.Rent(commandSize);
                try
                {
                    if (!await ReadExactlyAsync(bodyBuffer, commandSize, cancellationToken).ConfigureAwait(false))
                    {
                        // The peer went away mid-frame; treat it like any other disconnect.
                        return null;
                    }

                    // The body is already UTF-8, which is what the formatter parses, so it is passed straight
                    // through without transcoding. Note JsonDocument.Parse(ReadOnlyMemory<byte>) does NOT copy
                    // its input: it reads out of this rented buffer until the document is disposed. That is
                    // safe because Deserialize fully materializes the message before disposing the document,
                    // which is the same requirement the previous char-based call already had (the char overload
                    // rents its own byte array internally and returns it on dispose). Keep it that way: no
                    // deserializer may retain a JsonElement past this call.
                    return _formatter.Deserialize<RpcMessage>(new ReadOnlyMemory<byte>(bodyBuffer, 0, commandSize));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bodyBuffer);
                }
#else
                // System.Buffers is out of framework on netstandard2.0/net462 and this file also ships as
                // source in the dependency-free MTP client package, so allocate rather than pool here.
                byte[] bodyBuffer = new byte[commandSize];
                if (!await ReadExactlyAsync(bodyBuffer, commandSize, cancellationToken).ConfigureAwait(false))
                {
                    // The peer went away mid-frame; treat it like any other disconnect.
                    return null;
                }

                return _formatter.Deserialize<RpcMessage>(Encoding.UTF8.GetString(bodyBuffer, 0, commandSize));
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
            await TryLogDebugBoundedAsync($"TCP connection reset while reading; treating as client disconnect: {ex}").ConfigureAwait(false);
            return null;
        }
    }

    private async Task TryLogDebugBoundedAsync(string message)
    {
        var loggingTask = Task.Run(async () =>
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

        // Give the diagnostic a chance to reach its sink before the host disposes logging, while preventing a
        // custom provider from turning graceful disconnect handling into an unbounded wait.
        await Task.WhenAny(loggingTask, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
    }

    private async Task<int> ReadHeadersAsync(CancellationToken cancellationToken)
    {
        int contentSize = -1;

        while (true)
        {
            string? line = await ReadHeaderLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null || (line.Length == 0 && contentSize != -1))
            {
                break;
            }

            const string ContentLengthHeaderName = "Content-Length:";
            // Content type is not mandatory, and we don't use it.
            if (line.StartsWith(ContentLengthHeaderName, StringComparison.OrdinalIgnoreCase))
            {
#if NETCOREAPP
                bool parsed = int.TryParse(line.AsSpan()[ContentLengthHeaderName.Length..].Trim(), out contentSize);
#else
                // Substring rather than the range operator: string range indexing lowers to
                // System.Range.GetOffsetAndLength, which pulls in System.Range/System.ValueTuple —
                // neither is in-framework on net462, where this file is also compiled as source into
                // the dependency-free MTP client package. Substring is behavior-identical.
                bool parsed = int.TryParse(line.Substring(ContentLengthHeaderName.Length).Trim(), out contentSize);
#endif

                // A Content-Length that does not parse (empty, non-numeric, or larger than Int32) or that is
                // negative cannot frame a message, so report it as a lost connection and let ReadAsync return
                // null. Both cases must be rejected here rather than passed on:
                //
                //  * int.TryParse leaves contentSize at 0 when it fails, which would silently be read as a
                //    valid empty body instead of the malformed header it is;
                //  * a negative length would reach ArrayPool.Rent (or new byte[]) below and surface as an
                //    ArgumentOutOfRangeException (OverflowException on net462) that escapes the
                //    SocketException/IOException filter in ReadAsync and tears down the read loop.
                if (!parsed || contentSize < 0)
                {
                    return -1;
                }
            }
        }

        return contentSize;
    }

    /// <summary>
    /// Reads a single CRLF-terminated header line from the shared byte buffer. Headers are ASCII by protocol,
    /// but they are decoded as UTF-8 (a superset) so a non-conformant peer cannot corrupt the framing.
    /// Returns <see langword="null"/> at end of stream.
    /// </summary>
    /// <remarks>
    /// Only LF terminates a line here. The <see cref="StreamReader"/> this replaced also treated a lone CR as
    /// a terminator; the protocol always sends CRLF, so dropping that is deliberate and keeps a CR inside a
    /// header value from splitting the line.
    /// </remarks>
    private async Task<string?> ReadHeaderLineAsync(CancellationToken cancellationToken)
    {
        int lineLength = 0;
        while (true)
        {
            if (_readBufferOffset == _readBufferCount && !await FillReadBufferAsync(cancellationToken).ConfigureAwait(false))
            {
                // End of stream. A partial line is returned as-is; the caller fails the frame either way.
                return lineLength == 0 ? null : TrimPreamble(Encoding.UTF8.GetString(_headerLineBuffer, 0, lineLength));
            }

            byte current = _readBuffer[_readBufferOffset++];
            if (current == (byte)'\n')
            {
                // Tolerate both CRLF and a bare LF.
                if (lineLength > 0 && _headerLineBuffer[lineLength - 1] == (byte)'\r')
                {
                    lineLength--;
                }

                return TrimPreamble(Encoding.UTF8.GetString(_headerLineBuffer, 0, lineLength));
            }

            if (lineLength == _headerLineBuffer.Length)
            {
                // Plain arrays rather than ArrayPool so this compiles on netstandard2.0/net462, where
                // System.Buffers is out of framework and this file also ships as source in the
                // dependency-free MTP client package.
                byte[] grown = new byte[_headerLineBuffer.Length * 2];
                Array.Copy(_headerLineBuffer, grown, lineLength);
                _headerLineBuffer = grown;
            }

            _headerLineBuffer[lineLength++] = current;
        }
    }

    /// <summary>
    /// Drops a UTF-8 byte-order mark from the very first line of the stream. The previous
    /// <see cref="StreamReader"/>-based reader had byte-order-mark detection enabled by default and silently
    /// swallowed the preamble, so peers that write their headers through a preamble-emitting encoder (for
    /// example <c>new StreamWriter(stream, Encoding.UTF8)</c>) kept working. Preserve that tolerance.
    /// </summary>
    private string TrimPreamble(string line)
    {
        if (_preambleHandled)
        {
            return line;
        }

        _preambleHandled = true;
        return line.Length > 0 && line[0] == '\uFEFF'
            ? line.Substring(1)
            : line;
    }

    /// <summary>
    /// Fills <paramref name="destination"/> with exactly <paramref name="count"/> bytes, draining the shared
    /// read buffer first. Returns <see langword="false"/> if the stream ended before the frame was complete.
    /// </summary>
    private async Task<bool> ReadExactlyAsync(byte[] destination, int count, CancellationToken cancellationToken)
    {
        int written = 0;
        while (written < count)
        {
            if (_readBufferOffset == _readBufferCount && !await FillReadBufferAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            int available = Math.Min(_readBufferCount - _readBufferOffset, count - written);
            Array.Copy(_readBuffer, _readBufferOffset, destination, written, available);
            _readBufferOffset += available;
            written += available;
        }

        return true;
    }

    /// <summary>
    /// Refills the shared read buffer. Returns <see langword="false"/> at end of stream.
    /// </summary>
    private async Task<bool> FillReadBufferAsync(CancellationToken cancellationToken)
    {
#if NETCOREAPP
        int read = await _readStream.ReadAsync(_readBuffer.AsMemory(0, _readBuffer.Length), cancellationToken).ConfigureAwait(false);
#else
        int read = await _readStream.ReadAsync(_readBuffer, 0, _readBuffer.Length, cancellationToken)
            .WithCancellationAsync(cancellationToken).ConfigureAwait(false);
#endif
        _readBufferOffset = 0;
        _readBufferCount = read;
        return read > 0;
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
        _readStream.Dispose();

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

#if IS_MTP_SERVER_MODE_CLIENT
        if (!Microsoft.Testing.Platform.ServerMode.Client.MtpClientOperatingSystem.IsBrowser())
#else
        if (!OperatingSystem.IsBrowser())
#endif
        {
            _client.Dispose();
        }
    }
}
