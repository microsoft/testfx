// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.ServerClient.UnitTests;

/// <summary>
/// Wire-level framing tests for <see cref="TcpMessageHandler"/>, the transport shared by the MTP server and
/// the source-shipped client. The net8.0 leg exercises the <c>#if NETCOREAPP</c> read/write path (System.Text.Json)
/// and the net462 leg the <c>#else</c> path (Jsonite), so both compilation branches are covered.
/// </summary>
[TestClass]
public sealed class TcpMessageHandlerFramingTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // A string whose UTF-8 byte count is strictly greater than its char count: German umlauts (2 bytes),
    // Japanese (3 bytes) and an emoji (4 bytes, 2 chars). Content-Length is declared in bytes, so a reader that
    // consumes that number of *characters* under-reads the frame and leaves its tail in the stream.
    private const string NonAsciiText = "Grüße 日本語 🎉 Čau";

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// A conformant peer (the vstest client, or the source-shipped client running on a different formatter)
    /// emits the JSON body as raw UTF-8 and declares <c>Content-Length</c> in bytes, exactly as
    /// <see cref="TcpMessageHandler.WriteRequestAsync"/> does. The reader must consume that many bytes, so the
    /// next frame starts on a header boundary.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_RawUtf8FramesFromPeer_DoesNotDesynchronizeSubsequentFrames()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        // Two frames: the desynchronization only surfaces on the frame *after* the multi-byte one, because the
        // under-read leaves the first body's tail to be parsed as the second frame's headers.
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/first", NonAsciiText));
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/second", "ascii-only"));

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual("testing/first", first.Method);
        Assert.AreEqual(NonAsciiText, ((IDictionary<string, object?>)first.Params!)["text"]?.ToString());
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// The same round trip through <see cref="TcpMessageHandler"/> on both ends. The Jsonite formatter emits
    /// non-ASCII BMP characters unescaped, so this catches the mismatch on the <c>#else</c> compilation branch
    /// even without a foreign peer.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_HandlerWrittenFramesWithNonAscii_DoesNotDesynchronizeSubsequentFrames()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        await handlers.Writer.WriteRequestAsync(
            new NotificationMessage("testing/first", new Dictionary<string, object?> { ["text"] = NonAsciiText }),
            TestContext.CancellationToken).ConfigureAwait(false);
        await handlers.Writer.WriteRequestAsync(
            new NotificationMessage("testing/second", new Dictionary<string, object?> { ["text"] = "ascii-only" }),
            TestContext.CancellationToken).ConfigureAwait(false);

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual("testing/first", first.Method);
        Assert.AreEqual(NonAsciiText, ((IDictionary<string, object?>)first.Params!)["text"]?.ToString());
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// Guards the ASCII fast path: a plain frame followed by another must still round-trip, so the byte-exact
    /// read does not over-consume when bytes and characters happen to coincide.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_AsciiFrames_RoundTripInOrder()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/first", "plain"));
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/second", "plain"));

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual("testing/first", first.Method);
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// A peer that writes its headers through a preamble-emitting encoder (for example
    /// <c>new StreamWriter(stream, Encoding.UTF8)</c>) prefixes the very first header line with a UTF-8
    /// byte-order mark. The reader must skip it, as the previous StreamReader-based implementation did.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_LeadingByteOrderMark_IsSkipped()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        byte[] preamble = Encoding.UTF8.GetPreamble();
        await handlers.WriterStream.WriteAsync(preamble, 0, preamble.Length, TestContext.CancellationToken).ConfigureAwait(false);
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/first", NonAsciiText));
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/second", "ascii-only"));

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual("testing/first", first.Method);
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// Builds a JSON-RPC notification body by hand, so the test controls the exact bytes that hit the wire
    /// rather than going through a formatter that may escape non-ASCII characters.
    /// </summary>
    private static string BuildNotificationJson(string method, string text)
        => "{\"jsonrpc\":\"2.0\",\"method\":\"" + method + "\",\"params\":{\"text\":\"" + text + "\"}}";

    /// <summary>
    /// Writes a frame the way any conformant peer does: an ASCII header block whose <c>Content-Length</c> is
    /// the UTF-8 byte count, followed by the raw UTF-8 body.
    /// </summary>
    private static void WriteRawFrame(NetworkStream stream, string jsonBody)
    {
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        byte[] header = Encoding.ASCII.GetBytes(
            $"Content-Length: {body.Length}\r\nContent-Type: application/testingplatform\r\n\r\n");

        stream.Write(header, 0, header.Length);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }

    private async Task<RpcMessage?> ReadWithTimeoutAsync(ConnectedHandlers handlers)
    {
        Task<RpcMessage?> readTask = handlers.Reader.ReadAsync(TestContext.CancellationToken);
        Task completed = await Task.WhenAny(readTask, Task.Delay(DefaultTimeout, TestContext.CancellationToken)).ConfigureAwait(false);
        Assert.AreSame(readTask, completed, "Timed out reading a frame; the transport is most likely desynchronized.");
        return await readTask.ConfigureAwait(false);
    }

    /// <summary>
    /// A pair of <see cref="TcpMessageHandler"/> instances joined by a loopback TCP connection, so the bytes
    /// exchanged are exactly what a real MTP server and client put on the wire.
    /// </summary>
    private sealed class ConnectedHandlers : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _clientSocket;
        private readonly TcpClient _serverSocket;

        private ConnectedHandlers(TcpListener listener, TcpClient clientSocket, TcpClient serverSocket)
        {
            _listener = listener;
            _clientSocket = clientSocket;
            _serverSocket = serverSocket;

            WriterStream = serverSocket.GetStream();
            NetworkStream clientStream = clientSocket.GetStream();
            Writer = new TcpMessageHandler(serverSocket, WriterStream, WriterStream, FormatterUtilities.CreateFormatter());
            Reader = new TcpMessageHandler(clientSocket, clientStream, clientStream, FormatterUtilities.CreateFormatter());
        }

        /// <summary>Gets the raw stream behind <see cref="Writer"/>, for tests that frame bytes themselves.</summary>
        public NetworkStream WriterStream { get; }

        public TcpMessageHandler Writer { get; }

        public TcpMessageHandler Reader { get; }

        public static async Task<ConnectedHandlers> CreateAsync()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            TcpClient clientSocket = new();
            await clientSocket.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            clientSocket.NoDelay = true;

            TcpClient serverSocket = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            serverSocket.NoDelay = true;

            return new ConnectedHandlers(listener, clientSocket, serverSocket);
        }

        public void Dispose()
        {
            Writer.Dispose();
            Reader.Dispose();
            _clientSocket.Dispose();
            _serverSocket.Dispose();
            _listener.Stop();
        }
    }
}
