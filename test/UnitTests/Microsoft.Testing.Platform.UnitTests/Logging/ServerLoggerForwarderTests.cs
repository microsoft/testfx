// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

#if NETCOREAPP
using System.Threading.Channels;
#endif

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class ServerLoggerForwarderTests : TestBase
{
    private const string Result1 = "Result1";
    private const string Result2 = "Result2";

    private static readonly Func<string, Exception?, string> Formatter =
        (state, exception) =>
            string.Format(CultureInfo.InvariantCulture, "{0}{1}", state, exception is not null ? $" -- {exception}" : string.Empty);

#if NETCOREAPP
    private readonly Mock<IChannel<ServerLogMessage>> _mockProducerConsumer = new();
#else
    private readonly Mock<IBlockingCollection<ServerLogMessage>> _mockProducerConsumer = new();
#endif
    private readonly Mock<IServerTestHost> _mockServerTestHost = new();
    private readonly Mock<IProducerConsumerFactory<ServerLogMessage>> _mockProducerConsumerFactory = new();

    public ServerLoggerForwarderTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
#if NETCOREAPP
        _mockProducerConsumerFactory.Setup(x => x.Create(It.IsAny<UnboundedChannelOptions>())).Returns(_mockProducerConsumer.Object);
        _mockProducerConsumer.SetupSequence(x => x.WaitToReadAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(true))
            .Returns(new ValueTask<bool>(true))
            .Returns(new ValueTask<bool>(false));
        _mockProducerConsumer.SetupSequence(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ServerLogMessage>(new ServerLogMessage(LogLevel.Information, Result1)))
            .Returns(new ValueTask<ServerLogMessage>(new ServerLogMessage(LogLevel.Information, Result2)));
        _mockProducerConsumer.SetupSequence(x => x.TryWrite(It.IsAny<ServerLogMessage>()))
            .Returns(true)
            .Returns(false);
#else
        _mockProducerConsumerFactory.Setup(x => x.Create()).Returns(_mockProducerConsumer.Object);
        _mockProducerConsumer.Setup(x => x.GetConsumingEnumerable())
            .Returns(new List<ServerLogMessage>()
            {
                new(LogLevel.Information, Result1),
                new(LogLevel.Information, Result2),
            });
        _mockProducerConsumer.Setup(x => x.Add(It.IsAny<ServerLogMessage>()));
#endif

        _mockServerTestHost.Setup(x => x.IsInitialized).Returns(true);
        _mockServerTestHost.Setup(x => x.PushDataAsync(It.IsAny<IData>()));
    }

    public void ServerLoggerForwarder_Log()
    {
        using ServerLoggerForwarder serverLoggerForwarder = (new ServerLoggerForwarderProvider(
                LogLevel.Information,
                new SystemTask(),
                _mockServerTestHost.Object,
                _mockProducerConsumerFactory.Object)
            .CreateLogger("Test") as ServerLoggerForwarder)!;

        serverLoggerForwarder.Log(LogLevel.Trace, Result1, null, Formatter);
#if NETCOREAPP
        _mockProducerConsumer.Verify(x => x.TryWrite(It.IsAny<ServerLogMessage>()), Times.Never);
#else
        _mockProducerConsumer.Verify(x => x.Add(It.IsAny<ServerLogMessage>()), Times.Never);
#endif

        serverLoggerForwarder.Log(LogLevel.Information, Result1, null, Formatter);
#if NETCOREAPP
        _mockProducerConsumer.Verify(x => x.TryWrite(It.IsAny<ServerLogMessage>()), Times.Once);

        Assert.Throws<InvalidOperationException>(() =>
            serverLoggerForwarder.Log(LogLevel.Information, Result2, null, Formatter));
        _mockProducerConsumer.Verify(x => x.TryWrite(It.IsAny<ServerLogMessage>()), Times.Exactly(2));
#else
        _mockProducerConsumer.Verify(x => x.Add(It.IsAny<ServerLogMessage>()), Times.Once);
#endif
    }

    public async Task ServerLoggerForwarder_LogAsync()
    {
        using (ServerLoggerForwarder serverLoggerForwarder = (new ServerLoggerForwarderProvider(
                LogLevel.Information,
                new SystemTask(),
                _mockServerTestHost.Object,
                _mockProducerConsumerFactory.Object)
            .CreateLogger("Test") as ServerLoggerForwarder)!)
        {
            await serverLoggerForwarder.LogAsync(LogLevel.Trace, Result1, null, Formatter);
            await serverLoggerForwarder.LogAsync(LogLevel.Information, Result1, null, Formatter);
        }

        _mockServerTestHost.Verify(x => x.PushDataAsync(It.IsAny<ServerLogMessage>()), Times.Exactly(2 + 1));
    }
}
