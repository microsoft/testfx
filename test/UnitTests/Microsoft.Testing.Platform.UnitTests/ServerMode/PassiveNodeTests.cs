// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class PassiveNodeTests
{
    [TestMethod]
    public async Task ConnectAsync_PreCanceledApplicationDoesNotCreateHandler()
    {
        using CancellationTokenSource shutdown = new();
        shutdown.Cancel();
        Mock<IMessageHandlerFactory> factory = new(MockBehavior.Strict);
        using PassiveNode node = CreatePassiveNodeWithShutdown(factory.Object, shutdown.Token);

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => node.ConnectAsync());
        Assert.AreEqual(shutdown.Token, exception.CancellationToken);
        factory.Verify(value => value.CreateMessageHandlerAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ConnectAsync_DisposedNodeDoesNotCreateHandler()
    {
        Mock<IMessageHandlerFactory> factory = new(MockBehavior.Strict);
        using PassiveNode node = CreatePassiveNodeWithShutdown(factory.Object, CancellationToken.None);
        node.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => node.ConnectAsync());
        factory.Verify(value => value.CreateMessageHandlerAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [UnsupportedOSPlatform("browser")]
    public async Task ConnectAsync_ShutdownOrDisposalDuringCreationClosesLateTransport(bool dispose)
    {
        using CancellationTokenSource shutdown = new();
        TaskCompletionSource<IMessageHandler> created = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IMessageHandlerFactory> factory = new();
        factory.Setup(value => value.CreateMessageHandlerAsync(shutdown.Token)).Returns(created.Task);
        using PassiveNode node = CreatePassiveNodeWithShutdown(factory.Object, shutdown.Token);
        Task<bool> connect = node.ConnectAsync();
        Assert.IsFalse(connect.IsCompleted);
        factory.Verify(value => value.CreateMessageHandlerAsync(shutdown.Token), Times.Once);
        if (dispose)
        {
            node.Dispose();
        }
        else
        {
            shutdown.Cancel();
        }

        using TcpClient client = new();
        using MemoryStream output = new();
        using var handler = new TcpMessageHandler(client, CreateInitializeStream(), output, FormatterUtilities.CreateFormatter());
        int closed = 0;
        handler.ConnectionClosedCallback = () => closed++;
        created.SetResult(handler);
        if (dispose)
        {
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connect);
        }
        else
        {
            OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => connect);
            Assert.AreEqual(shutdown.Token, exception.CancellationToken);
        }

        Assert.AreEqual(1, closed);
        Assert.IsFalse(output.CanWrite);
        Assert.IsEmpty(output.ToArray());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => handler.WriteRequestAsync(new NotificationMessage("must/not/write", null), CancellationToken.None));
        shutdown.Cancel();
        node.Dispose();
        Assert.AreEqual(1, closed);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    [UnsupportedOSPlatform("browser")]
    public async Task CommittedInitializationOrAttachmentWrite_ShutdownOrDisposeClosesTransportPromptly(bool attachment, bool dispose)
    {
        using CancellationTokenSource shutdown = new();
        using TcpClient client = new();
        using TcpMessageHandlerTests.BlockingWriteStream output = new(attachment ? 4 : 2);
        using var handler = new TcpMessageHandler(client, CreateInitializeStream(), output, FormatterUtilities.CreateFormatter());
        using PassiveNode node = CreatePassiveNodeWithShutdown(handler, shutdown.Token);
        Task pending;
        if (attachment)
        {
            Assert.IsTrue(await node.ConnectAsync());
            pending = node.SendAttachmentsAsync(new TestsAttachments([]), CancellationToken.None);
        }
        else
        {
            pending = node.ConnectAsync();
        }

        await output.WriteStarted.Task.TimeoutAfterAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.IsFalse(pending.IsCompleted);
        if (dispose)
        {
            node.Dispose();
        }
        else
        {
            shutdown.Cancel();
        }

        if (dispose)
        {
            await Assert.ThrowsExactlyAsync<IOException>(
                () => pending.TimeoutAfterAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => node.SendAttachmentsAsync(new TestsAttachments([]), CancellationToken.None));
        }
        else
        {
            OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => pending.TimeoutAfterAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
            Assert.AreEqual(shutdown.Token, exception.CancellationToken);
            Assert.IsInstanceOfType<IOException>(exception.InnerException);
            OperationCanceledException next = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => node.SendAttachmentsAsync(new TestsAttachments([]), CancellationToken.None));
            Assert.AreEqual(shutdown.Token, next.CancellationToken);
            Assert.IsInstanceOfType<ObjectDisposedException>(next.InnerException);
        }

        Assert.IsTrue(pending.IsCompleted);
        Assert.IsFalse(output.CanWrite);
        node.Dispose();
        shutdown.Cancel();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [UnsupportedOSPlatform("browser")]
    public async Task AttachmentWrite_OperationCancellationDoesNotCloseTransport(bool committed)
    {
        using CancellationTokenSource shutdown = new();
        using CancellationTokenSource operation = new();
        using TcpClient client = new();
        using TcpMessageHandlerTests.BlockingWriteStream output = new(4);
        using var handler = new TcpMessageHandler(client, CreateInitializeStream(), output, FormatterUtilities.CreateFormatter());
        using PassiveNode node = CreatePassiveNodeWithShutdown(handler, shutdown.Token);
        Assert.IsTrue(await node.ConnectAsync());

        if (committed)
        {
            Task send = node.SendAttachmentsAsync(new TestsAttachments([]), operation.Token);
            await output.WriteStarted.Task.TimeoutAfterAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            operation.Cancel();
            Assert.IsFalse(send.IsCompleted);
            output.CompleteWrite();
            await send.TimeoutAfterAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        else
        {
            operation.Cancel();
            OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(
                () => node.SendAttachmentsAsync(new TestsAttachments([]), operation.Token));
            Assert.AreEqual(operation.Token, exception.CancellationToken);
            Assert.IsFalse(output.WriteStarted.Task.IsCompleted);
            output.CompleteWrite();
            await node.SendAttachmentsAsync(new TestsAttachments([]), CancellationToken.None);
        }

        Assert.IsTrue(output.CanWrite);
        Assert.IsFalse(shutdown.IsCancellationRequested);
        await node.SendAttachmentsAsync(new TestsAttachments([]), CancellationToken.None);

        using TcpClient readerClient = new();
        using var reader = new TcpMessageHandler(readerClient, new MemoryStream(output.ToArray()), new MemoryStream(), FormatterUtilities.CreateFormatter());
        Assert.IsInstanceOfType<ResponseMessage>(await reader.ReadAsync(CancellationToken.None));
        for (int i = 0; i < 2; i++)
        {
            NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(await reader.ReadAsync(CancellationToken.None));
            Assert.AreEqual(JsonRpcMethods.TestingTestUpdatesAttachments, notification.Method);
        }

        Assert.IsNull(await reader.ReadAsync(CancellationToken.None));
    }

    private static MemoryStream CreateInitializeStream()
    {
        const string body = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"processId":123,"clientInfo":{"name":"client","version":"1"},"capabilities":{"testing":{"debuggerProvider":false}}}}""";
        return new MemoryStream(Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConnectAsync_AttachmentPeerDoesNotAcknowledgeShowMessageAsApplied(bool? requested)
    {
        RequestMessage request = CreateInitializeRequest([JsonRpcProtocolVersions.Current]);
        InitializeRequestArgs args = Assert.IsInstanceOfType<InitializeRequestArgs>(request.Params);
        request = request with { Params = args with { Capabilities = args.Capabilities with { ShowMessage = requested } } };
        TestMessageHandler handler = new(request);
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsTrue(await node.ConnectAsync());

        ResponseMessage response = Assert.IsInstanceOfType<ResponseMessage>(handler.WrittenMessage);
        InitializeResponseArgs result = Assert.IsInstanceOfType<InitializeResponseArgs>(response.Result);
        Assert.AreEqual(requested.HasValue ? false : null, result.Capabilities.TestingCapabilities.ShowMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_NegotiatesSupportedProtocolVersion()
    {
        RequestMessage request = CreateInitializeRequest([JsonRpcProtocolVersions.Current]) with { StringId = "1" };
        TestMessageHandler handler = new(request);
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsTrue(await node.ConnectAsync());

        ResponseMessage response = Assert.IsInstanceOfType<ResponseMessage>(handler.WrittenMessage);
        InitializeResponseArgs result = Assert.IsInstanceOfType<InitializeResponseArgs>(response.Result);
        Assert.AreEqual("1", response.StringId);
        Assert.AreEqual(JsonRpcProtocolVersions.Current, result.ProtocolVersion);
    }

    [TestMethod]
    public async Task ConnectAsync_RejectsUnsupportedProtocolVersion()
    {
        TestMessageHandler handler = new(CreateInitializeRequest(["2.0.0"]));
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsFalse(await node.ConnectAsync());

        ErrorMessage error = Assert.IsInstanceOfType<ErrorMessage>(handler.WrittenMessage);
        Assert.AreEqual(ErrorCodes.ProtocolVersionNotSupported, error.ErrorCode);
    }

    [TestMethod]
    public async Task ConnectAsync_RejectsInitializeRequestWithInvalidParams()
    {
        RequestMessage request = new(
            1,
            JsonRpcMethods.Initialize,
            new InvalidRequestParamsArgs(ErrorCodes.InvalidParams, "Invalid initialize request params"))
        {
            StringId = "request-1",
        };
        TestMessageHandler handler = new(request);
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsFalse(await node.ConnectAsync());

        ErrorMessage error = Assert.IsInstanceOfType<ErrorMessage>(handler.WrittenMessage);
        Assert.AreEqual(ErrorCodes.InvalidParams, error.ErrorCode);
        Assert.AreEqual("request-1", error.StringId);
    }

    [TestMethod]
    public async Task ConnectAsync_RejectsNonInitializeRequestBeforeInitialization()
    {
        RequestMessage request = new(
            1,
            JsonRpcMethods.TestingDiscoverTests,
            new DiscoverRequestArgs(Guid.NewGuid(), TestNodes: null, GraphFilter: null));
        TestMessageHandler handler = new(request);
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsFalse(await node.ConnectAsync());

        ErrorMessage error = Assert.IsInstanceOfType<ErrorMessage>(handler.WrittenMessage);
        Assert.AreEqual(ErrorCodes.ServerNotInitialized, error.ErrorCode);
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task ConnectAsync_RejectsNonRequestInitialMessage(bool isNotification)
    {
        RpcMessage message = isNotification
            ? new NotificationMessage(JsonRpcMethods.Exit, Params: null)
            : new ResponseMessage(1, Result: null);
        TestMessageHandler handler = new(message);
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsFalse(await node.ConnectAsync());
        Assert.IsNull(handler.WrittenMessage);
    }

    [TestMethod]
    public async Task SendAttachmentsAsync_BeforeConnectAsync_ThrowsInvalidOperationException()
    {
        TestMessageHandler handler = new(CreateInitializeRequest([JsonRpcProtocolVersions.Current]));
        using PassiveNode node = CreatePassiveNode(handler);
        TestsAttachments attachments = new([]);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => node.SendAttachmentsAsync(attachments, CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAttachmentsAsync_AfterSuccessfulConnect_WritesAttachmentsNotification()
    {
        TestMessageHandler handler = new(CreateInitializeRequest([JsonRpcProtocolVersions.Current]));
        using PassiveNode node = CreatePassiveNode(handler);
        RunTestAttachment attachment = new("uri", "producer", "type", "name", "description");
        TestsAttachments attachments = new([attachment]);

        Assert.IsTrue(await node.ConnectAsync());
        await node.SendAttachmentsAsync(attachments, CancellationToken.None);

        NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(handler.WrittenMessage);
        Assert.AreEqual(JsonRpcMethods.TestingTestUpdatesAttachments, notification.Method);
        TestsAttachments notificationAttachments = Assert.IsInstanceOfType<TestsAttachments>(notification.Params);
        Assert.AreSame(attachment, Assert.ContainsSingle(notificationAttachments.Attachments));
    }

    private static PassiveNode CreatePassiveNode(TestMessageHandler handler)
        => CreatePassiveNodeWithShutdown(new TestMessageHandlerFactory(handler), CancellationToken.None);

    private static PassiveNode CreatePassiveNodeWithShutdown(IMessageHandler handler, CancellationToken shutdown)
        => CreatePassiveNodeWithShutdown(new TestMessageHandlerFactory(handler), shutdown);

    private static PassiveNode CreatePassiveNodeWithShutdown(IMessageHandlerFactory factory, CancellationToken shutdown)
    {
        var cancellationTokenSource = new Mock<ITestApplicationCancellationTokenSource>();
        cancellationTokenSource.SetupGet(source => source.CancellationToken).Returns(shutdown);

        var environment = new Mock<IEnvironment>();
        environment.SetupGet(value => value.ProcessId).Returns(42);

        var logger = new Mock<ILogger<PassiveNode>>();
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(false);

        return new PassiveNode(
            factory,
            cancellationTokenSource.Object,
            environment.Object,
            new SystemMonitorAsyncFactory(),
            logger.Object);
    }

    private static RequestMessage CreateInitializeRequest(string[] protocolVersions)
        => new(
            1,
            JsonRpcMethods.Initialize,
            new InitializeRequestArgs(
                123,
                new ClientInfo("test-client", "1.0.0"),
                new ClientCapabilities(DebuggerProvider: false, IsStateful: false))
            {
                ProtocolVersions = protocolVersions,
            });

    private sealed class TestMessageHandlerFactory(IMessageHandler messageHandler) : IMessageHandlerFactory
    {
        public Task<IMessageHandler> CreateMessageHandlerAsync(CancellationToken cancellationToken)
            => Task.FromResult(messageHandler);
    }

    private sealed class TestMessageHandler(RpcMessage message) : IMessageHandler
    {
        private RpcMessage? _message = message;

        public RpcMessage? WrittenMessage { get; private set; }

        public Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
        {
            RpcMessage? message = _message;
            _message = null;
            return Task.FromResult(message);
        }

        public Task WriteRequestAsync(RpcMessage message, CancellationToken cancellationToken)
        {
            WrittenMessage = message;
            return Task.CompletedTask;
        }
    }
}
