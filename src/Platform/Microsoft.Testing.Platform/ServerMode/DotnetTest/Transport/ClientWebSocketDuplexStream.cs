// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.WebSockets;

namespace Microsoft.Testing.Platform.IPC;

/// <summary>
/// Adapts a connected <see cref="System.Net.WebSockets.WebSocket"/> (typically a client-side
/// <see cref="System.Net.WebSockets.ClientWebSocket"/>, but any concrete <see cref="WebSocket"/> works - e.g. a
/// server-side socket obtained from an HTTP listener, which is how tests exercise this adapter without a real
/// peer) to a duplex <see cref="Stream"/> so the transport-neutral framing in <see cref="NamedPipeConnectionBase"/>
/// (originally written against <see cref="System.IO.Pipes.PipeStream"/>) can drive it unchanged. Each
/// <c>WriteAsync</c> call from the framing layer writes exactly one already-assembled frame, so it maps 1:1 to
/// one binary WebSocket message (<c>endOfMessage: true</c>). Reads are treated as a plain byte stream:
/// consecutive <see cref="WebSocket.ReceiveAsync(ArraySegment{byte}, CancellationToken)"/> chunks are handed back
/// verbatim (buffering any leftover bytes across calls) without relying on message boundaries, which keeps this
/// adapter correct regardless of how the peer chooses to fragment its writes.
/// </summary>
/// <remarks>
/// This adapter is for non-browser runtimes only: <see cref="System.Net.WebSockets.ClientWebSocket"/> throws
/// <see cref="PlatformNotSupportedException"/> on <c>browser-wasm</c>. See <c>BrowserWebSocketDuplexStream</c>
/// for the browser-specific JS-interop implementation.
/// </remarks>
[UnsupportedOSPlatform("browser")]
internal sealed class ClientWebSocketDuplexStream : Stream
{
    private readonly WebSocket _webSocket;
    private readonly byte[] _receiveBuffer = new byte[8192];
    private int _receiveOffset;
    private int _receiveCount;
    private bool _disposed;

    public ClientWebSocketDuplexStream(WebSocket webSocket) => _webSocket = webSocket;

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // WebSocket sends are not buffered on our side; nothing to flush.
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException($"{nameof(ClientWebSocketDuplexStream)} only supports asynchronous reads.");

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException($"{nameof(ClientWebSocketDuplexStream)} only supports asynchronous writes.");

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_receiveCount == 0)
        {
            WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(_receiveBuffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return 0;
            }

            _receiveOffset = 0;
            _receiveCount = result.Count;

            // A binary frame can legitimately carry zero bytes (e.g. an empty keep-alive); loop rather than
            // reporting a false EOF. The framing layer above always reads a bounded number of bytes so this
            // cannot spin indefinitely against a well-behaved peer.
            if (_receiveCount == 0 && !result.EndOfMessage)
            {
                return await ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
        }

        int toCopy = Math.Min(count, _receiveCount);
        Array.Copy(_receiveBuffer, _receiveOffset, buffer, offset, toCopy);
        _receiveOffset += toCopy;
        _receiveCount -= toCopy;
        return toCopy;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => await _webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken).ConfigureAwait(false);

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _webSocket.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
