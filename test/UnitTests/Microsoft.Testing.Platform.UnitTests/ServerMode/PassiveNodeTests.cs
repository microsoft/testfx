// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public async Task ConnectAsync_NegotiatesSupportedProtocolVersion()
    {
        TestMessageHandler handler = new(CreateInitializeRequest([JsonRpcProtocolVersions.Current]));
        using PassiveNode node = CreatePassiveNode(handler);

        Assert.IsTrue(await node.ConnectAsync());

        ResponseMessage response = Assert.IsInstanceOfType<ResponseMessage>(handler.WrittenMessage);
        InitializeResponseArgs result = Assert.IsInstanceOfType<InitializeResponseArgs>(response.Result);
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
