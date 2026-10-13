// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class PassiveNodeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DataConsumer_ActualArtifactsProduceExactNotificationAndCallerToken(bool sessionArtifact)
    {
        TestMessageHandler handler = new(CreateInitializeRequest([JsonRpcProtocolVersions.Current]));
        using PassiveNode node = CreatePassiveNode(handler);
        using PassiveNodeDataConsumer consumer = new(node);
        var producer = new Mock<IDataProducer>();
        producer.SetupGet(value => value.Uid).Returns("actual.producer");
        string path = Path.Combine(Path.GetTempPath(), "passive-file-not-opened.txt");
        IData artifact = sessionArtifact
            ? new SessionFileArtifact(new Microsoft.Testing.Platform.TestHost.SessionUid("session"), new FileInfo(path), "artifact display", "artifact description")
            : new FileArtifact(new FileInfo(path), "artifact display", "artifact description");
        Assert.IsTrue(await node.ConnectAsync());
        Assert.IsTrue(await consumer.IsEnabledAsync());

        await consumer.ConsumeAsync(producer.Object, artifact, TestContext.CancellationToken);

        NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(handler.WrittenMessage);
        Assert.AreEqual(JsonRpcMethods.TestingTestUpdatesAttachments, notification.Method);
        TestsAttachments attachments = Assert.IsInstanceOfType<TestsAttachments>(notification.Params);
        RunTestAttachment actual = Assert.ContainsSingle(attachments.Attachments);
        Assert.AreEqual(new RunTestAttachment(path, "actual.producer", "file", "artifact display", "artifact description"), actual);
        Assert.AreEqual(TestContext.CancellationToken, handler.WrittenToken);
        await consumer.ConsumeAsync(producer.Object, Mock.Of<IData>(), TestContext.CancellationToken);
        Assert.AreSame(notification, handler.WrittenMessage, "Unsupported data must not emit another message.");

        consumer.Dispose();
        Assert.IsTrue(handler.IsDisposed, "The consumer owns and disposes its passive node's initialized handler.");
    }

    [TestMethod]
    public async Task DataConsumer_AbsentNodeIsDisabledAndDoesNotRequireArtifactTypeOrProducer()
    {
        using PassiveNodeDataConsumer consumer = new(null);
        Assert.IsFalse(await consumer.IsEnabledAsync());
        await consumer.ConsumeAsync(null!, Mock.Of<IData>(), TestContext.CancellationToken);
        Assert.AreSequenceEqual(new[] { typeof(SessionFileArtifact), typeof(FileArtifact) }, consumer.DataTypesConsumed);
        Assert.AreEqual(nameof(PassiveNodeDataConsumer), consumer.Uid);
        Assert.AreEqual("Push information as passive node", consumer.Description);
    }

    [TestMethod]
    public async Task DataConsumer_CanceledArtifactDoesNotReplaceInitializationResponse()
    {
        TestMessageHandler handler = new(CreateInitializeRequest([JsonRpcProtocolVersions.Current]));
        using PassiveNode node = CreatePassiveNode(handler);
        using PassiveNodeDataConsumer consumer = new(node);
        Assert.IsTrue(await node.ConnectAsync());
        RpcMessage? original = handler.WrittenMessage;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        FileArtifact artifact = new(new FileInfo(Path.Combine(Path.GetTempPath(), "not-opened.txt")), "display", "description");

        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(
            () => consumer.ConsumeAsync(Mock.Of<IDataProducer>(), artifact, cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreSame(original, handler.WrittenMessage);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConnectAsync_AttachmentPeerDoesNotAcknowledgeShowMessageAsApplied(bool? requested)
    {
        InitializeRequestArgs arguments = Assert.IsInstanceOfType<InitializeRequestArgs>(
            CreateInitializeRequest([JsonRpcProtocolVersions.Current]).Params);
        TestMessageHandler handler = new(new RequestMessage(
            1, JsonRpcMethods.Initialize, arguments with { Capabilities = arguments.Capabilities with { ShowMessage = requested } }));
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsTrue(await node.ConnectAsync());
        InitializeResponseArgs response = Assert.IsInstanceOfType<InitializeResponseArgs>(
            Assert.IsInstanceOfType<ResponseMessage>(handler.WrittenMessage).Result);
        Assert.AreEqual(requested.HasValue ? false : null, response.Capabilities.TestingCapabilities.ShowMessage);
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
    {
        var cancellationTokenSource = new Mock<ITestApplicationCancellationTokenSource>();
        cancellationTokenSource.SetupGet(source => source.CancellationToken).Returns(CancellationToken.None);

        var environment = new Mock<IEnvironment>();
        environment.SetupGet(value => value.ProcessId).Returns(42);

        var logger = new Mock<ILogger<PassiveNode>>();
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(false);

        return new PassiveNode(
            new TestMessageHandlerFactory(handler),
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

    private sealed class TestMessageHandler(RpcMessage message) : IMessageHandler, IDisposable
    {
        private RpcMessage? _message = message;

        public RpcMessage? WrittenMessage { get; private set; }

        public CancellationToken WrittenToken { get; private set; }

        public bool IsDisposed { get; private set; }

        public Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
        {
            RpcMessage? message = _message;
            _message = null;
            return Task.FromResult(message);
        }

        public Task WriteRequestAsync(RpcMessage message, CancellationToken cancellationToken)
        {
            WrittenMessage = message;
            WrittenToken = cancellationToken;
            return Task.CompletedTask;
        }

        public void Dispose() => IsDisposed = true;
    }
}
