// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TcpMessageHandlerTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // A string whose UTF-8 byte count is strictly greater than its char count: German umlauts (2 bytes),
    // Japanese (3 bytes) and an emoji (4 bytes, 2 chars). Content-Length is declared in bytes, so a reader
    // that consumes that number of *characters* under-reads the frame and leaves its tail in the stream.
    private const string NonAsciiText = "Grüße 日本語 🎉 Čau";

    // The same content carried in the JSON-RPC method, which both formatters always bind (params binding
    // depends on the method being a registered one, so it is not usable in a transport-level test).
    private const string NonAsciiMethod = "testing/Grüße 日本語 🎉 Čau";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_ConnectionReset_LogsFullExceptionAndReturnsNullWhenLoggerFails()
    {
        SocketException connectionReset = new((int)SocketError.ConnectionReset);
        var logAttempted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) =>
                {
                    Assert.Contains(nameof(SocketException), message);
                    Assert.Contains(connectionReset.Message, message);
                    logAttempted.TrySetResult(true);
                })
            .ThrowsAsync(new IOException("Logging failed."));

        using TcpClient tcpClient = new();
        using var handler = new TcpMessageHandler(
            tcpClient,
            new ConnectionResetStream(connectionReset),
            new MemoryStream(),
            Mock.Of<IMessageFormatter>(),
            logger.Object);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNull(message);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Task completedTask = await Task.WhenAny(logAttempted.Task, timeoutTask);
        Assert.AreSame(logAttempted.Task, completedTask, "Expected the queued reset diagnostic to be attempted.");
        logger.Verify(
            x => x.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter),
            Times.Once);
    }

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
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson(NonAsciiMethod));
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/second"));

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        // Comparing the method verifies every multi-byte character survived, not merely that a frame arrived.
        Assert.AreEqual(NonAsciiMethod, first.Method);
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// The same round trip through <see cref="TcpMessageHandler"/> on both ends, using real server-to-client
    /// notifications.
    /// <para>
    /// Note this test only exercises the byte/char mismatch on the <c>#else</c> (Jsonite) branch, which emits
    /// non-ASCII BMP characters unescaped. On .NET the System.Text.Json formatter escapes every non-ASCII
    /// character to <c>\uXXXX</c>, so the body reaches the wire as pure ASCII and this degrades to an ASCII
    /// round trip — it is NOT cross-TFM coverage of the defect. That coverage comes from
    /// <see cref="ReadAsync_RawUtf8FramesFromPeer_DoesNotDesynchronizeSubsequentFrames"/> and
    /// <see cref="ReadAsync_BodyLargerThanReadBuffer_SpansMultipleRefillsWithoutDesynchronizing"/>, which
    /// frame raw UTF-8 bytes themselves and so bypass formatter escaping on every TFM.
    /// </para>
    /// The second frame uses a different method so a desynchronized read cannot accidentally satisfy the
    /// assertion.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_HandlerWrittenFramesWithNonAscii_DoesNotDesynchronizeSubsequentFrames()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        // A localized log message is exactly how non-ASCII reaches this transport in production.
        await handlers.Writer.WriteRequestAsync(
            new NotificationMessage(
                JsonRpcMethods.ClientLog,
                new LogEventArgs(new ServerLogMessage(LogLevel.Information, NonAsciiText))),
            TestContext.CancellationToken).ConfigureAwait(false);
        await handlers.Writer.WriteRequestAsync(
            new NotificationMessage(
                JsonRpcMethods.TelemetryUpdate,
                new TelemetryEventArgs("ascii-only", new Dictionary<string, object>())),
            TestContext.CancellationToken).ConfigureAwait(false);

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual(JsonRpcMethods.ClientLog, first.Method);
        Assert.AreEqual(JsonRpcMethods.TelemetryUpdate, second.Method);
    }

    /// <summary>
    /// Guards the ASCII fast path: a plain frame followed by another must still round-trip, so the byte-exact
    /// read does not over-consume when bytes and characters happen to coincide.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_AsciiFrames_RoundTripInOrder()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/first"));
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/second"));

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual("testing/first", first.Method);
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// A peer that writes its headers through a preamble-emitting encoder (for example
    /// <c>new StreamWriter(stream, Encoding.UTF8)</c>) prefixes the very first header line with a UTF-8
    /// byte-order mark. The reader must skip it, as the previous StreamReader-based implementation did.
    /// <para>
    /// Deliberately ASCII-only: the byte/char defect is covered elsewhere, and keeping non-ASCII out of this
    /// frame means a failure here can only mean the preamble handling itself broke.
    /// </para>
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_LeadingByteOrderMark_IsSkipped()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        byte[] preamble = Encoding.UTF8.GetPreamble();
        await handlers.WriterStream.WriteAsync(preamble, 0, preamble.Length, TestContext.CancellationToken).ConfigureAwait(false);
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/first"));
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/second"));

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual("testing/first", first.Method);
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// A body larger than the reader's internal buffer forces the refill loop in <c>ReadExactlyAsync</c> to
    /// run several times, so the body spans buffer boundaries. Production traffic hits this routinely: a test
    /// node update carrying a stack trace or captured standard output easily exceeds the buffer size.
    /// A second frame follows to prove the reader stopped on the exact byte boundary.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_BodyLargerThanReadBuffer_SpansMultipleRefillsWithoutDesynchronizing()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        // Comfortably larger than the 4 KB read buffer, and non-ASCII so the byte/char distinction still
        // applies across every refill rather than only on the first. Carried in the method so the assertion
        // below compares every single byte of the oversized frame.
        string largeMethod = "testing/" + string.Concat(Enumerable.Repeat(NonAsciiText, 1000));

        WriteRawFrame(handlers.WriterStream, BuildNotificationJson(largeMethod));
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/after"));

        var large = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var after = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual(largeMethod, large.Method);
        Assert.AreEqual("testing/after", after.Method);
    }

    /// <summary>
    /// A header line longer than the initial header buffer forces the growth path in
    /// <c>ReadHeaderLineAsync</c> to run through several doublings. Headers are unknown-length input from the
    /// peer, so an over-long one must grow the buffer rather than truncate or corrupt the frame.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_HeaderLineLongerThanInitialBuffer_GrowsAndParsesFrame()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        // An unknown header is ignored by the parser but must still be consumed in full. Well past the
        // 256-byte initial header buffer, so the doubling path runs repeatedly.
        string longHeaderValue = new('x', 5000);
        string body = BuildNotificationJson("testing/first");
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        byte[] header = Encoding.ASCII.GetBytes(
            $"X-Padding: {longHeaderValue}\r\nContent-Length: {bodyBytes.Length}\r\nContent-Type: application/testingplatform\r\n\r\n");

        await handlers.WriterStream.WriteAsync(header, 0, header.Length, TestContext.CancellationToken).ConfigureAwait(false);
        await handlers.WriterStream.WriteAsync(bodyBytes, 0, bodyBytes.Length, TestContext.CancellationToken).ConfigureAwait(false);
        WriteRawFrame(handlers.WriterStream, BuildNotificationJson("testing/second"));

        var first = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;
        var second = (NotificationMessage)(await ReadWithTimeoutAsync(handlers).ConfigureAwait(false))!;

        Assert.AreEqual("testing/first", first.Method);
        Assert.AreEqual("testing/second", second.Method);
    }

    /// <summary>
    /// A peer that announces a body and then disconnects before sending all of it must surface as a graceful
    /// disconnect (<see langword="null"/>), not as a hang waiting for bytes that will never arrive, and not as
    /// an exception escaping to the read loop.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_PeerDisconnectsMidBody_ReturnsNull()
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        string body = BuildNotificationJson("testing/first");
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        byte[] header = Encoding.ASCII.GetBytes(
            $"Content-Length: {bodyBytes.Length}\r\nContent-Type: application/testingplatform\r\n\r\n");

        await handlers.WriterStream.WriteAsync(header, 0, header.Length, TestContext.CancellationToken).ConfigureAwait(false);

        // Deliberately short: announce the full length, then send only part of it and hang up.
        await handlers.WriterStream.WriteAsync(bodyBytes, 0, bodyBytes.Length / 2, TestContext.CancellationToken).ConfigureAwait(false);
        await handlers.WriterStream.FlushAsync(TestContext.CancellationToken).ConfigureAwait(false);
        handlers.CloseWriterSide();

        RpcMessage? message = await ReadWithTimeoutAsync(handlers).ConfigureAwait(false);

        Assert.IsNull(message);
    }

    /// <summary>
    /// A Content-Length that cannot frame a message must be reported as a graceful disconnect rather than
    /// escaping as an exception the read loop does not expect.
    /// <para>
    /// A negative length is the sharp case: it reaches <c>ArrayPool.Rent</c> (or <c>new byte[]</c> on net462)
    /// and surfaces as an <see cref="ArgumentOutOfRangeException"/>/<see cref="OverflowException"/>, neither
    /// of which the <c>SocketException</c>/<c>IOException</c> filter in <c>ReadAsync</c> catches.
    /// </para>
    /// <para>
    /// The unparseable cases are the quiet ones: <c>int.TryParse</c> leaves the length at 0 when it fails, so
    /// a malformed header used to be read as a valid empty body and then fail later as a JSON parse error,
    /// pointing at the payload instead of at the header that was actually wrong.
    /// </para>
    /// </summary>
    [TestMethod]
    [DataRow("-5", DisplayName = "negative")]
    [DataRow("-2147483648", DisplayName = "int.MinValue")]
    [DataRow("99999999999999", DisplayName = "larger than Int32")]
    [DataRow("abc", DisplayName = "non-numeric")]
    [DataRow("", DisplayName = "empty")]
    public async Task ReadAsync_MalformedContentLength_ReturnsNull(string contentLength)
    {
        using ConnectedHandlers handlers = await ConnectedHandlers.CreateAsync().ConfigureAwait(false);

        byte[] header = Encoding.ASCII.GetBytes(
            $"Content-Length: {contentLength}\r\nContent-Type: application/testingplatform\r\n\r\n");
        await handlers.WriterStream.WriteAsync(header, 0, header.Length, TestContext.CancellationToken).ConfigureAwait(false);
        await handlers.WriterStream.FlushAsync(TestContext.CancellationToken).ConfigureAwait(false);

        RpcMessage? message = await ReadWithTimeoutAsync(handlers).ConfigureAwait(false);

        Assert.IsNull(message);
    }

    /// <summary>
    /// Builds a JSON-RPC notification body by hand, so the test controls the exact bytes that hit the wire
    /// rather than going through a formatter that may escape non-ASCII characters.
    /// </summary>
    private static string BuildNotificationJson(string method)
        => "{\"jsonrpc\":\"2.0\",\"method\":\"" + method + "\",\"params\":{}}";

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

    private sealed class ConnectionResetStream(SocketException exception) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw exception;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromException<int>(exception);

#if NETCOREAPP
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(exception);
#endif
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

            TcpClient? clientSocket = null;
            TcpClient? serverSocket = null;
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;

                clientSocket = new TcpClient();
                await clientSocket.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                clientSocket.NoDelay = true;

                serverSocket = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                serverSocket.NoDelay = true;

                return new ConnectedHandlers(listener, clientSocket, serverSocket);
            }
            catch
            {
                // Without this the listener and any half-established socket leak for the rest of the run,
                // which on a loopback-heavy suite shows up as port exhaustion rather than as this failure.
                serverSocket?.Dispose();
                clientSocket?.Dispose();
                listener.Stop();
                throw;
            }
        }

        /// <summary>
        /// Half-closes the writer's send direction, which the reader observes as an end of stream.
        /// </summary>
        public void CloseWriterSide() => _serverSocket.Client.Shutdown(SocketShutdown.Send);

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
