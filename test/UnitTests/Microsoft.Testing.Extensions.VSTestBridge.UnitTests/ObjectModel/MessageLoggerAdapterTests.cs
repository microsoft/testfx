// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public sealed class MessageLoggerAdapterTests
{
    [TestMethod]
    [DataRow(TestMessageLevel.Informational, LogLevel.Information, typeof(TextOutputDeviceData))]
    [DataRow(TestMessageLevel.Warning, LogLevel.Warning, typeof(WarningMessageOutputDeviceData))]
    [DataRow(TestMessageLevel.Error, LogLevel.Error, typeof(ErrorMessageOutputDeviceData))]
    public void SendMessage_ForwardsLogsAndDisplaysMatchingSeverity(TestMessageLevel level, LogLevel expectedLogLevel, Type expectedOutputType)
    {
        const string message = "adapter message";
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var factory = new Mock<ILoggerFactory>();
        factory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var legacy = new Mock<IMessageLogger>();
        var output = new Mock<IOutputDevice>();
        var order = new List<string>();
        legacy.Setup(x => x.SendMessage(level, message)).Callback(() => order.Add("legacy"));
        IOutputDeviceData? displayed = null;
        IOutputDeviceDataProducer? producer = null;
        using var cancellation = new CancellationTokenSource();
        output.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), cancellation.Token))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((actualProducer, data, _) =>
            {
                order.Add("output");
                producer = actualProducer;
                displayed = data;
            })
            .Returns(Task.CompletedTask);
        var adapter = new MessageLoggerAdapter(factory.Object, output.Object, new TestExtension(), legacy.Object, cancellation.Token);

        adapter.SendMessage(level, message);

        Assert.AreSequenceEqual(new[] { "legacy", "output" }, order);
        Assert.IsNotNull(displayed);
        Assert.AreEqual(expectedOutputType, displayed.GetType());
        string actualMessage = displayed switch
        {
            TextOutputDeviceData text => text.Text,
            WarningMessageOutputDeviceData warning => warning.Message,
            ErrorMessageOutputDeviceData error => error.Message,
            _ => throw new AssertFailedException("Unexpected output data."),
        };
        Assert.AreEqual(message, actualMessage);
        Assert.AreSame(adapter, producer);
        legacy.Verify(x => x.SendMessage(level, message), Times.Once);
        Assert.ContainsSingle(logger.Invocations.Where(x => x.Method.Name == nameof(ILogger.Log) && Equals(x.Arguments[0], expectedLogLevel)));
    }

    [TestMethod]
    public void SendMessage_UnsupportedSeverity_ForwardsLegacyCallButDoesNotDisplay()
    {
        var fixture = new BridgeTestFixture();
        var legacy = new Mock<IMessageLogger>();
        var adapter = new MessageLoggerAdapter(fixture.LoggerFactory, fixture.OutputDevice.Object, new TestExtension(), legacy.Object, CancellationToken.None);

        NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(() => adapter.SendMessage((TestMessageLevel)123, "message"));

        Assert.AreEqual("Unsupported logging level '123'.", exception.Message);
        legacy.Verify(x => x.SendMessage((TestMessageLevel)123, "message"), Times.Once);
        fixture.OutputDevice.Verify(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void SendMessage_OutputFailure_PropagatesOriginalException()
    {
        var fixture = new BridgeTestFixture();
        var failure = new InvalidOperationException("output failure");
        fixture.OutputDevice.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(failure));
        var adapter = new MessageLoggerAdapter(fixture.LoggerFactory, fixture.OutputDevice.Object, new TestExtension(), null, CancellationToken.None);

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => adapter.SendMessage(TestMessageLevel.Error, "message"));

        Assert.AreSame(failure, actual);
    }
}
