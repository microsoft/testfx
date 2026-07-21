// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System.Runtime.InteropServices.JavaScript;

namespace Microsoft.Testing.Platform.IPC;

/// <summary>
/// Adapts a browser-native <c>WebSocket</c> object (reached through JS interop, following the same
/// <c>[JSImport]</c>/<c>[JSExport]</c> pattern as <see cref="OutputDevice.BrowserOutputDevice"/>) to a duplex
/// <see cref="Stream"/>, so the transport-neutral framing in <see cref="NamedPipeConnectionBase"/> can drive it
/// unchanged - the same way <see cref="ClientWebSocketDuplexStream"/> adapts <see cref="System.Net.WebSockets.ClientWebSocket"/>
/// on non-browser runtimes. <see cref="System.Net.WebSockets.ClientWebSocket"/> is not implemented on
/// <c>browser-wasm</c> (it throws <see cref="PlatformNotSupportedException"/>), so this type talks to the
/// browser's own <c>WebSocket</c> API directly instead.
/// </summary>
/// <remarks>
/// <para>
/// The companion JS module is embedded as a string constant and imported through a <c>data:</c> URL via
/// <see cref="JSHost.ImportAsync(string, string, System.Threading.CancellationToken)"/>. This keeps the browser
/// transport fully self-contained in this file - no changes to the generated wasm bootstrap/entry point or to
/// the published asset set are required, unlike a conventional <c>.js</c> asset shipped alongside the app.
/// </para>
/// <para>
/// Each connection owns one JS <c>WebSocket</c> object (kept alive on the .NET side as a <see cref="JSObject"/>
/// proxy). <c>send</c> writes one binary frame per call, mirroring the one-frame-per-<c>WriteAsync</c> contract
/// the framing layer already relies on for named pipes and the non-browser WebSocket adapter. Frames are
/// exchanged as base64 strings (see the module source for why) rather than raw bytes. <c>receive</c> returns
/// the next queued message, or an empty string once the socket has closed (a real protocol frame is always at
/// least 8 bytes, so this is an unambiguous close sentinel) - messages arriving before the .NET side calls
/// <c>receive</c> are buffered in a JS-side queue so no data is lost while a request/reply round-trip is in
/// flight.
/// </para>
/// </remarks>
[SupportedOSPlatform("browser")]
internal sealed partial class BrowserWebSocketDuplexStream : Stream
{
    private const string ModuleName = "Microsoft.Testing.Platform/DotnetTestWebSocket";

    // language=javascript
    private const string ModuleSource = """
        export function open(url) {
            return new Promise((resolve, reject) => {
                let ws;
                try {
                    ws = new WebSocket(url);
                } catch (e) {
                    reject(e);
                    return;
                }

                ws.binaryType = "arraybuffer";
                ws._queue = [];
                ws._waiter = null;
                ws._closed = false;
                ws._openRejected = false;

                ws.onopen = () => resolve(ws);
                ws.onerror = () => {
                    if (!ws._openRejected) {
                        ws._openRejected = true;
                        reject(new Error("The WebSocket connection failed or was refused by the peer."));
                    }
                };
                ws.onmessage = (ev) => {
                    const data = new Uint8Array(ev.data);
                    if (ws._waiter) {
                        const waiter = ws._waiter;
                        ws._waiter = null;
                        waiter.resolve(toBase64(data));
                    } else {
                        ws._queue.push(data);
                    }
                };
                ws.onclose = () => {
                    ws._closed = true;
                    if (ws._waiter) {
                        const waiter = ws._waiter;
                        ws._waiter = null;
                        waiter.resolve("");
                    }
                };
            });
        }

        // Data is exchanged as base64 strings rather than raw byte arrays: the wasm JS-interop source
        // generator does not support marshalling byte[]/Uint8Array across an async (Task-returning) boundary,
        // but string marshalling is always supported. A real protocol frame is always at least 8 bytes (4-byte
        // length header + 4-byte serializer id), so an empty string is an unambiguous "socket closed" sentinel.
        function toBase64(bytes) {
            // Avoid spreading very large arrays into String.fromCharCode (call-stack argument limits);
            // build the binary string in bounded chunks instead.
            let binary = "";
            const chunkSize = 0x8000;
            for (let i = 0; i < bytes.length; i += chunkSize) {
                binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
            }

            return btoa(binary);
        }

        function fromBase64(base64) {
            const binary = atob(base64);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }

            return bytes;
        }

        export function send(ws, base64Data) {
            ws.send(fromBase64(base64Data));
        }

        export function receive(ws) {
            if (ws._queue.length > 0) {
                return Promise.resolve(toBase64(ws._queue.shift()));
            }

            if (ws._closed) {
                return Promise.resolve("");
            }

            return new Promise((resolve, reject) => {
                ws._waiter = { resolve, reject };
            });
        }

        // Called when the .NET side cancels a pending receive() (see WaitForReceiveAsync). Clears the waiter so
        // that a message arriving afterward is queued for the *next* receive() call instead of being handed to
        // this now-abandoned one and silently lost. Resolving the abandoned promise (rather than leaving it
        // permanently pending) also lets the JS engine release it instead of holding onto its closure forever.
        export function cancelReceive(ws) {
            if (ws._waiter) {
                const waiter = ws._waiter;
                ws._waiter = null;
                waiter.resolve("");
            }
        }

        export function close(ws) {
            try {
                ws.close();
            } catch {
                // Best-effort: the socket may already be closed/closing.
            }
        }
        """;

    private static Task? s_moduleReady;

    private readonly JSObject _socket;
    private byte[] _pendingRead = [];
    private int _pendingReadOffset;
    private bool _disposed;

    private BrowserWebSocketDuplexStream(JSObject socket) => _socket = socket;

    /// <summary>
    /// Imports the companion JS module (idempotent - safe to call before every connection attempt) and opens a
    /// new browser WebSocket to <paramref name="uri"/>. The returned stream is only valid once the JS <c>onopen</c>
    /// event has fired; a connection failure (refused, DNS, TLS, ...) surfaces as an awaited exception here rather
    /// than being discovered on the first read/write.
    /// </summary>
    public static async Task<BrowserWebSocketDuplexStream> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        s_moduleReady ??= JSHost.ImportAsync(ModuleName, $"data:text/javascript;charset=utf-8;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(ModuleSource))}", cancellationToken);
        await s_moduleReady.ConfigureAwait(false);

        JSObject socket = await OpenAsync(uri.ToString()).ConfigureAwait(false);
        return new BrowserWebSocketDuplexStream(socket);
    }

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
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException($"{nameof(BrowserWebSocketDuplexStream)} only supports asynchronous reads.");

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException($"{nameof(BrowserWebSocketDuplexStream)} only supports asynchronous writes.");

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_pendingReadOffset >= _pendingRead.Length)
        {
            string next = await WaitForReceiveAsync(cancellationToken).ConfigureAwait(false);

            // A real protocol frame is always at least 8 bytes (4-byte length header + 4-byte serializer id), so
            // an empty result unambiguously means the JS module resolved the close sentinel (see the 'receive'
            // function above) - the peer went away. Surface EOF exactly like a clean named-pipe disconnect so
            // the shared connection-loss handling in DotnetTestWebSocketClient applies uniformly.
            if (next.Length == 0)
            {
                return 0;
            }

            _pendingRead = Convert.FromBase64String(next);
            _pendingReadOffset = 0;
        }

        int toCopy = Math.Min(count, _pendingRead.Length - _pendingReadOffset);
        Array.Copy(_pendingRead, _pendingReadOffset, buffer, offset, toCopy);
        _pendingReadOffset += toCopy;
        return toCopy;
    }

    /// <summary>
    /// Awaits the JS-side <c>receive()</c> promise while honoring <paramref name="cancellationToken"/>, even
    /// though the promise itself has no cancellation hook across the interop boundary.
    /// </summary>
    /// <remarks>
    /// Without this, a caller that cancels a pending read (e.g. <see cref="DotnetTestWebSocketClient.RequestReplyAsync{TRequest, TResponse}"/>
    /// racing a timeout, or a shutdown path) would hang until the peer either sends more data or closes the
    /// socket - on <c>browser-wasm</c>, the very runtime this transport exists for, that would be an unrecoverable
    /// hang. This races the receive against the token and throws <see cref="OperationCanceledException"/> as
    /// soon as it fires.
    /// <para>
    /// On cancellation, <see cref="CancelReceive"/> is called (best-effort) to clear the abandoned JS-side
    /// waiter. This is not just cleanup: without it, the *next* message the peer sends after the cancellation
    /// would resolve the stale, unobserved promise instead of being queued - silently losing that message from
    /// the point of view of whichever call replaces this one. <see cref="CancelReceive"/> ensures a message that
    /// arrives after cancellation is queued normally instead. Closing the socket itself is deliberately not part
    /// of this - a cancelled read must not affect the connection's ability to serve a subsequent read.
    /// </para>
    /// </remarks>
    private async Task<string> WaitForReceiveAsync(CancellationToken cancellationToken)
    {
        Task<string> receiveTask = ReceiveAsync(_socket);
        if (!cancellationToken.CanBeCanceled)
        {
            return await receiveTask.ConfigureAwait(false);
        }

        TaskCompletionSource<bool> cancellationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancellationTcs))
        {
            Task completed = await Task.WhenAny(receiveTask, cancellationTcs.Task).ConfigureAwait(false);
            if (completed == cancellationTcs.Task)
            {
                try
                {
                    CancelReceive(_socket);
                }
                catch (JSException)
                {
                    // Best-effort: the socket may already be gone (closed/disposed racing with this cancellation).
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return await receiveTask.ConfigureAwait(false);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Send() is a synchronous JS call (WebSocket.send buffers the frame internally and returns immediately),
        // so there is no in-flight operation to race against the token the way WaitForReceiveAsync races a
        // receive; the only meaningful cancellation behavior is rejecting an already-cancelled token before
        // doing the (synchronous, uncancellable-once-started) work, exactly like other synchronous-under-the-hood
        // Stream implementations do.
        cancellationToken.ThrowIfCancellationRequested();
        Send(_socket, Convert.ToBase64String(buffer, offset, count));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                Close(_socket);
            }
            catch (JSException)
            {
                // Best-effort close; the socket may already be gone (peer closed first, page navigating away, ...).
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }

    [JSImport("open", ModuleName)]
    private static partial Task<JSObject> OpenAsync(string url);

    [JSImport("send", ModuleName)]
    private static partial void Send(JSObject socket, string base64Data);

    [JSImport("receive", ModuleName)]
    private static partial Task<string> ReceiveAsync(JSObject socket);

    [JSImport("cancelReceive", ModuleName)]
    private static partial void CancelReceive(JSObject socket);

    [JSImport("close", ModuleName)]
    private static partial void Close(JSObject socket);
}

#endif
