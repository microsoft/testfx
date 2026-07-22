// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
#endif

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Tests for the WebSocket transport of the 'dotnet test' pipe protocol (<see cref="DotnetTestWebSocketClient"/>,
/// <see cref="ClientWebSocketDuplexStream"/>). These tests focus on the transport bootstrap itself -
/// authentication, connect, and request/reply framing over a real loopback WebSocket - since the wire protocol
/// carried over the connection is exactly the one the named-pipe transport already uses (see
/// <see cref="IPCTests"/>/<see cref="ProtocolTests"/>).
/// </summary>
[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class DotnetTestWebSocketClientTests
{
    [TestMethod]
    public void BuildAuthenticatedUri_AppendsTokenAsQueryParameter_WhenNoExistingQuery()
    {
        Uri result = DotnetTestWebSocketClient.BuildAuthenticatedUri(new Uri("ws://127.0.0.1:5000/dotnettest"), "abc123");

        Assert.AreEqual("ws://127.0.0.1:5000/dotnettest?dotnetTestToken=abc123", result.AbsoluteUri);
    }

    [TestMethod]
    public void BuildAuthenticatedUri_AppendsTokenWithAmpersand_WhenQueryAlreadyPresent()
    {
        Uri result = DotnetTestWebSocketClient.BuildAuthenticatedUri(new Uri("ws://127.0.0.1:5000/dotnettest?foo=bar"), "abc123");

        Assert.AreEqual("ws://127.0.0.1:5000/dotnettest?foo=bar&dotnetTestToken=abc123", result.AbsoluteUri);
    }

    [TestMethod]
    public void BuildAuthenticatedUri_EscapesSpecialCharactersInToken()
    {
        Uri result = DotnetTestWebSocketClient.BuildAuthenticatedUri(new Uri("ws://127.0.0.1:5000/dotnettest"), "a b&c=d");

        Assert.AreEqual("ws://127.0.0.1:5000/dotnettest?dotnetTestToken=a%20b%26c%3Dd", result.AbsoluteUri);
    }

    [TestMethod]
    public void BuildAuthenticatedUri_InsertsTokenBeforeFragment()
    {
        Uri result = DotnetTestWebSocketClient.BuildAuthenticatedUri(new Uri("ws://127.0.0.1:5000/dotnettest#frag"), "abc123");

        Assert.AreEqual("ws://127.0.0.1:5000/dotnettest?dotnetTestToken=abc123#frag", result.AbsoluteUri);
    }

#if NET
    [TestMethod]
    public async Task ClientWebSocketDuplexStream_ReadAsync_WithZeroCount_ReturnsImmediatelyWithoutReceiving()
    {
        var webSocket = new Mock<WebSocket>();
        using ClientWebSocketDuplexStream stream = new(webSocket.Object);

        int result = await stream.ReadAsync([], 0, 0, CancellationToken.None);

        Assert.AreEqual(0, result);
        webSocket.Verify(
            socket => socket.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_RequestReplyAsync_RoundTripsRealFramingOverLoopbackWebSocket()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(25));
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Task<(HandshakeMessage Received, string RequestLine)> serverTask = AcceptReadAndReplyAsync(listener, new Dictionary<byte, string> { [9] = "negotiated-value" }, cts.Token);

            IEnvironment environment = new Mock<IEnvironment>().Object;
            DotnetTestWebSocketClient client = new(new Uri($"ws://127.0.0.1:{port}/dotnettest"), "s3cr3t-token", environment, exitProcessOnConnectionLoss: false);
            RegisterSerializerViaReflection(client, new HandshakeMessageSerializer(), typeof(HandshakeMessage));

            try
            {
                await client.ConnectAsync(cts.Token);
                Assert.IsTrue(client.IsConnected);

                HandshakeMessage sent = new(new Dictionary<byte, string> { [0] = "1234", [5] = "TestHost" });
                HandshakeMessage response = await client.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(sent, cts.Token);
                Assert.IsNotNull(response.Properties);
                Assert.AreEqual("negotiated-value", response.Properties![9]);

                (HandshakeMessage received, string requestLine) = await serverTask;
                Assert.AreEqual("1234", received.Properties?[0]);
                Assert.AreEqual("TestHost", received.Properties?[5]);

                // The token must have travelled as a query-string parameter (browsers cannot set custom
                // headers on the WebSocket upgrade handshake) - see DotnetTestWebSocketClient.BuildAuthenticatedUri.
                Assert.Contains("dotnetTestToken=s3cr3t-token", requestLine);
            }
            finally
            {
                client.Dispose();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_RequestReplyAsync_RoundTripsLargeMessageSpanningMultipleWebSocketReceives()
    {
        // ClientWebSocketDuplexStream's internal receive buffer is 8192 bytes. A property value comfortably
        // larger than that forces NamedPipeConnectionBase.ReadNextMessageAsync to pull the frame across several
        // WebSocket receives instead of a single one, exercising the offset/count buffering carried across
        // ReadAsync calls (_receiveOffset/_receiveCount) that the single-frame round-trip test above never
        // needs to touch.
        string largeValue = new('x', 50_000);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(25));
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Task<(HandshakeMessage Received, string RequestLine)> serverTask = AcceptReadAndReplyAsync(listener, new Dictionary<byte, string> { [9] = "ok" }, cts.Token);

            IEnvironment environment = new Mock<IEnvironment>().Object;
            DotnetTestWebSocketClient client = new(new Uri($"ws://127.0.0.1:{port}/dotnettest"), "s3cr3t-token", environment, exitProcessOnConnectionLoss: false);
            RegisterSerializerViaReflection(client, new HandshakeMessageSerializer(), typeof(HandshakeMessage));

            try
            {
                await client.ConnectAsync(cts.Token);

                HandshakeMessage sent = new(new Dictionary<byte, string> { [0] = largeValue });
                HandshakeMessage response = await client.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(sent, cts.Token);
                Assert.AreEqual("ok", response.Properties?[9]);

                (HandshakeMessage received, _) = await serverTask;
                Assert.AreEqual(largeValue, received.Properties?[0]);
            }
            finally
            {
                client.Dispose();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task RequestReplyAsync_WhenServerClosesWithoutResponding_ThrowsIOException_WhenExitProcessOnConnectionLossIsFalse()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(25));
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Task serverTask = AcceptReadAndDisconnectWithoutRespondingAsync(listener, cts.Token);

            IEnvironment environment = new Mock<IEnvironment>().Object;

            // exitProcessOnConnectionLoss: false mirrors how DotnetTestConnection opens auxiliary channels: a
            // dropped connection must surface as an exception the caller can handle, not kill the process. This
            // is the WebSocket-transport counterpart of NamedPipeClient's identical behavior for exitProcessOnConnectionLoss: false.
            DotnetTestWebSocketClient client = new(new Uri($"ws://127.0.0.1:{port}/dotnettest"), "s3cr3t-token", environment, exitProcessOnConnectionLoss: false);
            RegisterSerializerViaReflection(client, new HandshakeMessageSerializer(), typeof(HandshakeMessage));

            try
            {
                await client.ConnectAsync(cts.Token);

                HandshakeMessage sent = new(new Dictionary<byte, string> { [0] = "1234" });
                await Assert.ThrowsExactlyAsync<IOException>(() => client.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(sent, cts.Token));

                // IEnvironment.Exit must never have been called: with exitProcessOnConnectionLoss: false a
                // connection loss is the caller's problem to handle, not a reason to terminate the process.
                Mock.Get(environment).Verify(e => e.Exit(It.IsAny<int>()), Times.Never);

                await serverTask;
            }
            finally
            {
                client.Dispose();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task RequestReplyAsync_WhenCancelled_ThrowsOperationCanceledExceptionAndDoesNotHang()
    {
        using CancellationTokenSource testCts = new(TimeSpan.FromSeconds(25));
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            // The server accepts the connection and completes the WebSocket handshake, but then deliberately
            // never reads the request or sends a reply - holding the connection open so RequestReplyAsync would
            // otherwise wait forever. This isolates cancellation of the read from a peer disconnect (already
            // covered by the test above): the connection stays healthy, only the caller's token fires.
            Task<TcpClient> serverAcceptTask = AcceptAndCompleteHandshakeOnlyAsync(listener, testCts.Token);

            IEnvironment environment = new Mock<IEnvironment>().Object;
            DotnetTestWebSocketClient client = new(new Uri($"ws://127.0.0.1:{port}/dotnettest"), "s3cr3t-token", environment, exitProcessOnConnectionLoss: false);
            RegisterSerializerViaReflection(client, new HandshakeMessageSerializer(), typeof(HandshakeMessage));

            try
            {
                await client.ConnectAsync(testCts.Token);

                using CancellationTokenSource requestCts = new(TimeSpan.FromMilliseconds(200));
                HandshakeMessage sent = new(new Dictionary<byte, string> { [0] = "1234" });

                // Not ThrowsExactlyAsync: cancelling a pending Stream read/write typically surfaces as
                // TaskCanceledException (a subclass of OperationCanceledException), not the base type itself -
                // same convention used elsewhere in this test project (e.g. TaskExtensionsTests).
                await Assert.ThrowsAsync<OperationCanceledException>(() => client.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(sent, requestCts.Token));
            }
            finally
            {
                client.Dispose();
                using TcpClient serverClient = await serverAcceptTask;
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ReadAsync_WhenServerSendsEmptyBinaryMessageBeforeReply_IgnoresItInsteadOfTreatingAsEof()
    {
        // Regression test for a real bug found and fixed in ClientWebSocketDuplexStream.ReadAsync: a zero-length,
        // non-Close binary WebSocket message (a legitimate occurrence - e.g. an empty keep-alive, or here, a
        // deliberately injected empty frame) was briefly misreported as end-of-stream/peer-disconnected, because
        // the retry loop only re-read for a non-final *fragment*, not for a complete zero-byte message. If that
        // bug were reintroduced, this test would fail: RequestReplyAsync would observe an EOF where the server's
        // real reply should be and throw IOException (via HandleMissingResponseAsync) instead of returning it.
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(25));
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Task serverTask = AcceptReadSendEmptyMessageThenReplyAsync(listener, new Dictionary<byte, string> { [9] = "ok" }, cts.Token);

            IEnvironment environment = new Mock<IEnvironment>().Object;
            DotnetTestWebSocketClient client = new(new Uri($"ws://127.0.0.1:{port}/dotnettest"), "s3cr3t-token", environment, exitProcessOnConnectionLoss: false);
            RegisterSerializerViaReflection(client, new HandshakeMessageSerializer(), typeof(HandshakeMessage));

            try
            {
                await client.ConnectAsync(cts.Token);

                HandshakeMessage sent = new(new Dictionary<byte, string> { [0] = "1234" });
                HandshakeMessage response = await client.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(sent, cts.Token);
                Assert.AreEqual("ok", response.Properties?[9]);

                await serverTask;
            }
            finally
            {
                client.Dispose();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Task<(HandshakeMessage Result, string RequestLine)> AcceptReadAndReplyAsync(TcpListener listener, Dictionary<byte, string> replyProperties, CancellationToken cancellationToken)
        => AcceptAndHandleAsync(
            listener,
            async (stream, requestPayload, cancellationToken) =>
            {
                int serializerId = BitConverter.ToInt32(requestPayload, 0);
                Assert.AreEqual(HandshakeMessageFieldsId.MessagesSerializerId, serializerId);

                using MemoryStream requestBody = new(requestPayload, 4, requestPayload.Length - 4);
                HandshakeMessageSerializer serializer = new();
                var received = (HandshakeMessage)ProtocolSerializerTestHelper.Deserialize(serializer, requestBody);

                HandshakeMessage reply = new(replyProperties);
                using MemoryStream replyBody = new();
                ProtocolSerializerTestHelper.Serialize(serializer, reply, replyBody);
                await WriteFrameAsync(stream, HandshakeMessageFieldsId.MessagesSerializerId, replyBody.ToArray(), cancellationToken);

                return received;
            },
            cancellationToken);

    private static Task AcceptReadAndDisconnectWithoutRespondingAsync(TcpListener listener, CancellationToken cancellationToken)
        => AcceptAndHandleAsync<object?>(
            listener,
            (_, _, _) => Task.FromResult<object?>(null),
            cancellationToken);

    private static Task AcceptReadSendEmptyMessageThenReplyAsync(TcpListener listener, Dictionary<byte, string> replyProperties, CancellationToken cancellationToken)
        => AcceptAndHandleAsync<object?>(
            listener,
            async (stream, requestPayload, cancellationToken) =>
            {
                using MemoryStream requestBody = new(requestPayload, 4, requestPayload.Length - 4);
                HandshakeMessageSerializer serializer = new();
                ProtocolSerializerTestHelper.Deserialize(serializer, requestBody);

                // Send a genuine zero-length, non-Close binary WebSocket message before the real reply. Writing
                // a 0-byte buffer through the Stream still issues one WebSocket.SendAsync(..., endOfMessage:
                // true, ...) call underneath (see ClientWebSocketDuplexStream.WriteAsync), producing exactly the
                // scenario the regression test above guards against: a conforming reader must skip this and
                // keep reading rather than reporting a false EOF.
                await stream.WriteAsync([], 0, 0, cancellationToken);

                HandshakeMessage reply = new(replyProperties);
                using MemoryStream replyBody = new();
                ProtocolSerializerTestHelper.Serialize(serializer, reply, replyBody);
                await WriteFrameAsync(stream, HandshakeMessageFieldsId.MessagesSerializerId, replyBody.ToArray(), cancellationToken);

                return null;
            },
            cancellationToken);

    // Shared plumbing for the server-side half of a loopback test: accepts one connection, completes the RFC
    // 6455 handshake, reads exactly one request frame, hands it to <paramref name="handleRequestAsync"/> (which
    // may reply, or not, or reply with an extra empty message first - see the callers above), then closes the
    // connection. Factored out so each scenario only needs to express what differs about the server's reaction
    // to the request.
    private static async Task<(T Result, string RequestLine)> AcceptAndHandleAsync<T>(TcpListener listener, Func<Stream, byte[], CancellationToken, Task<T>> handleRequestAsync, CancellationToken cancellationToken)
    {
        using TcpClient tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
        NetworkStream networkStream = tcpClient.GetStream();

        string requestLine = await CompleteServerHandshakeAsync(networkStream, cancellationToken);

        using var serverSocket = WebSocket.CreateFromStream(networkStream, isServer: true, subProtocol: null, keepAliveInterval: Timeout.InfiniteTimeSpan);
        ClientWebSocketDuplexStream stream = new(serverSocket);

        byte[] requestPayload = await ReadFrameAsync(stream, cancellationToken);
        T result = await handleRequestAsync(stream, requestPayload, cancellationToken);
        return (result, requestLine);
    }

    private static async Task<TcpClient> AcceptAndCompleteHandshakeOnlyAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        TcpClient tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
        await CompleteServerHandshakeAsync(tcpClient.GetStream(), cancellationToken);
        return tcpClient;
    }

    // Minimal RFC 6455 server-side opening handshake: read the HTTP request line + headers (up to the blank
    // line terminator), extract Sec-WebSocket-Key, and reply with the computed Sec-WebSocket-Accept. Everything
    // after this point is real WebSocket framing, handled by WebSocket.CreateFromStream.
    private static async Task<string> CompleteServerHandshakeAsync(NetworkStream networkStream, CancellationToken cancellationToken)
    {
        List<byte> buffer = [];
        byte[] singleByte = new byte[1];
        while (true)
        {
            int read = await networkStream.ReadAsync(singleByte, cancellationToken);
            if (read == 0)
            {
                throw new IOException("Connection closed before the WebSocket handshake completed.");
            }

            buffer.Add(singleByte[0]);
            if (buffer.Count >= 4 &&
                buffer[^4] == (byte)'\r' && buffer[^3] == (byte)'\n' &&
                buffer[^2] == (byte)'\r' && buffer[^1] == (byte)'\n')
            {
                break;
            }
        }

        string requestText = Encoding.ASCII.GetString([.. buffer]);
        string key = requestText
            .Split("\r\n")
            .Single(line => line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
            .Split(':', 2)[1]
            .Trim();

        string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        string response = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
        await networkStream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        return requestText;
    }

    // Mirrors the documented wire format (§4 of docs/mstest-runner-protocol/004-protocol-dotnet-test-pipe.md):
    // int32 payloadLength (= 4 + body.Length), int32 serializerId, then the body. Deliberately independent of
    // NamedPipeConnectionBase so this test does not depend on which of its (possibly duplicated, see this
    // project's local IPC/*.cs source links) copies is in scope.
    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[4];
        await ReadExactAsync(stream, header, cancellationToken);
        int payloadLength = BitConverter.ToInt32(header, 0);
        byte[] payload = new byte[payloadLength];
        await ReadExactAsync(stream, payload, cancellationToken);
        return payload;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Unexpected end of stream while reading a frame.");
            }

            offset += read;
        }
    }

    private static async Task WriteFrameAsync(Stream stream, int serializerId, byte[] body, CancellationToken cancellationToken)
    {
        int payloadLength = 4 + body.Length;
        byte[] frame = new byte[4 + payloadLength];
        BitConverter.GetBytes(payloadLength).CopyTo(frame, 0);
        BitConverter.GetBytes(serializerId).CopyTo(frame, 4);
        body.CopyTo(frame, 8);
        await stream.WriteAsync(frame, cancellationToken);
    }

    // NamedPipeBase.RegisterSerializer is public but inherited from a base class that is also (separately)
    // compiled directly into this test project from linked source (see this project's IPC/*.cs Compile Include
    // items). That makes ordinary strongly-typed member lookup on the assembly-referenced DotnetTestWebSocketClient
    // ambiguous with the test project's own same-named local copy, so - exactly like ProtocolSerializerTestHelper
    // does for the serializer Serialize/Deserialize methods - this reaches the real inherited method via
    // reflection instead, which resolves purely at runtime against the actual (unambiguous) object instance.
    private static void RegisterSerializerViaReflection(object client, object serializer, Type messageType)
    {
        MethodInfo method = client.GetType().GetMethod("RegisterSerializer", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RegisterSerializer method not found via reflection.");
        method.Invoke(client, [serializer, messageType]);
    }
#endif
}
