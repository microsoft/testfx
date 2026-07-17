// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TcpMessageHandlerTests
{
    [TestMethod]
    public async Task ReadAsync_ConnectionReset_LogsFullExceptionAndReturnsNullWhenLoggerFails()
    {
        SocketException connectionReset = new((int)SocketError.ConnectionReset);
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) =>
                {
                    Assert.Contains(nameof(SocketException), message);
                    Assert.Contains(connectionReset.Message, message);
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
        logger.Verify(
            x => x.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter),
            Times.Once);
    }

    private sealed class ConnectionResetStream(SocketException exception) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw exception;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromException<int>(exception);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(exception);
    }
}
