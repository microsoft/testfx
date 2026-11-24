// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class ProxyOutputDeviceTests
{
    [TestMethod]
    public async Task Dispose_DisposesServerModeDeviceButNotOriginalDevice()
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        Mock<IDisposable> originalDisposable = originalDevice.As<IDisposable>();
        var serverModeDevice = new ServerModePerCallOutputDevice(
            fileLoggerProvider: null,
            Mock.Of<IStopPoliciesService>());
        var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice);

        proxy.Dispose();

        originalDisposable.Verify(x => x.Dispose(), Times.Never);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => serverModeDevice.DisplayAsync(
                Mock.Of<IOutputDeviceDataProducer>(producer => producer.Uid == "producer"),
                new ProgressMessageOutputDeviceData("key", "message"),
                CancellationToken.None));
    }

    [TestMethod]
    [UnsupportedOSPlatform("browser")]
    public Task Proxy_BannerBeforeAndAfterSessionForwardToActualServerSink()
        => ServerTestHostTests.WithRunningHostAsync(async (host, next) =>
        {
            using CancellationTokenSource cancellation = new();
            Mock<IPlatformOutputDevice> original = new();
            using ServerModePerCallOutputDevice server = new(null, Mock.Of<IStopPoliciesService>());
            using ProxyOutputDevice proxy = new(original.Object, server);
            await proxy.DisplayBannerAsync("exact banner", cancellation.Token);
            await proxy.DisplayBeforeSessionStartAsync(cancellation.Token);
            await proxy.DisplayAfterSessionEndRunAsync(cancellation.Token);

            await proxy.InitializeAsync(host);

            (Microsoft.Testing.Platform.Logging.LogLevel Level, string Text)[] expected =
            [
                (Microsoft.Testing.Platform.Logging.LogLevel.Debug, "exact banner"),
                (Microsoft.Testing.Platform.Logging.LogLevel.Trace, "Starting test session."),
                (Microsoft.Testing.Platform.Logging.LogLevel.Trace, "Finished test session."),
            ];
            foreach ((Microsoft.Testing.Platform.Logging.LogLevel level, string text) in expected)
            {
                NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(await next());
                Assert.AreEqual("client/log", notification.Method);
                LogEventArgs message = Assert.IsInstanceOfType<LogEventArgs>(notification.Params);
                Assert.AreEqual(level, message.LogMessage.Level);
                Assert.AreEqual(text, message.LogMessage.Message);
            }

            original.Verify(o => o.DisplayBannerAsync("exact banner", cancellation.Token), Times.Once);
            original.Verify(o => o.DisplayBeforeSessionStartAsync(cancellation.Token), Times.Once);
            original.Verify(o => o.DisplayAfterSessionEndRunAsync(cancellation.Token), Times.Once);
        });

    [TestMethod]
    public async Task Proxy_FormattedPayloadKeepsContrastingColorsTextAndPaddingAtDeviceBoundary()
    {
        Mock<IPlatformOutputDevice> sink = new();
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>();
        using CancellationTokenSource cancellation = new();
        List<(string Text, ConsoleColor Foreground, ConsoleColor Background, int? Padding)> observed = [];
        sink.Setup(s => s.DisplayAsync(producer, It.IsAny<IOutputDeviceData>(), cancellation.Token))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) =>
            {
                FormattedTextOutputDeviceData formatted = Assert.IsInstanceOfType<FormattedTextOutputDeviceData>(data);
                observed.Add((
                    formatted.Text,
                    Assert.IsInstanceOfType<SystemConsoleColor>(formatted.ForegroundColor).ConsoleColor,
                    Assert.IsInstanceOfType<SystemConsoleColor>(formatted.BackgroundColor).ConsoleColor,
                    formatted.Padding));
            })
            .Returns(Task.CompletedTask);
        using ProxyOutputDevice proxy = new(sink.Object, null);
        FormattedTextOutputDeviceData payload = new("two contrasting colors")
        {
            ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Blue },
            BackgroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow },
            Padding = 7,
        };

        await proxy.DisplayAsync(producer, payload, cancellation.Token);

        (string Text, ConsoleColor Foreground, ConsoleColor Background, int? Padding) expected =
            ("two contrasting colors", ConsoleColor.Blue, ConsoleColor.DarkYellow, 7);
        Assert.AreEqual(expected, Assert.ContainsSingle(observed));
        sink.Verify(s => s.DisplayAsync(producer, payload, cancellation.Token), Times.Once);
    }
}
