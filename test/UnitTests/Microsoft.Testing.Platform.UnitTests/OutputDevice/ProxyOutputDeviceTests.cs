// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class ProxyOutputDeviceTests
{
    [TestMethod]
    public async Task DisplayAsync_BeforeHandshake_PreservesConnectionMessageTypeForOriginalDevice()
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var serverModeDevice = new ServerModePerCallOutputDevice(null, Mock.Of<IStopPoliciesService>());
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice);
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>();

        await proxy.DisplayAsync(producer, new ConnectionMessageOutputDeviceData("connecting"), CancellationToken.None);

        originalDevice.Verify(
            value => value.DisplayAsync(
                producer,
                It.Is<IOutputDeviceData>(data => data.GetType() == typeof(TextOutputDeviceData) && ((TextOutputDeviceData)data).Text == "connecting"),
                CancellationToken.None),
            Times.Once);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisplayAsync_RoutesAllMessagesAccordingToNegotiatedMode(bool? requested)
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var serverModeDevice = new ServerModePerCallOutputDevice(null, Mock.Of<IStopPoliciesService>());
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice);
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>(value => value.Uid == "user");
        IOutputDeviceData[] messages =
        [
            new TextOutputDeviceData("plain μ"),
            new FormattedTextOutputDeviceData("formatted"),
            new SessionMessageOutputDeviceData("session"),
            new ProgressMessageOutputDeviceData("key", "progress"),
            new WarningMessageOutputDeviceData("warning"),
            new ErrorMessageOutputDeviceData("error"),
            new ExceptionOutputDeviceData(new InvalidOperationException("exception")),
        ];

        Assert.AreEqual(requested == true, proxy.ConfigureRpcOnlyOutput(requested));
        foreach (IOutputDeviceData message in messages)
        {
            await proxy.DisplayAsync(producer, message, CancellationToken.None);
            originalDevice.Verify(value => value.DisplayAsync(producer, message, CancellationToken.None), requested == true ? Times.Never() : Times.Once());
        }

        var queuedMessages = (ConcurrentQueue<ServerLogMessage>)typeof(ServerModePerCallOutputDevice)
            .GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(serverModeDevice)!;
        Assert.AreSequenceEqual(
            new[] { LogLevel.Information, LogLevel.Information, LogLevel.Information, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Error },
            queuedMessages.Select(message => message.Level));
        Assert.AreSequenceEqual(
            new[] { "plain μ", "formatted", "session", "progress", "warning", "error", "System.InvalidOperationException: exception" },
            queuedMessages.Select(message => message.Message));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LifecycleMessages_UseNegotiatedRoute(bool? requested)
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var serverModeDevice = new ServerModePerCallOutputDevice(null, Mock.Of<IStopPoliciesService>());
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice);
        proxy.ConfigureRpcOnlyOutput(requested);

        await proxy.DisplayBannerAsync("banner", CancellationToken.None);
        await proxy.DisplayBeforeSessionStartAsync(CancellationToken.None);
        await proxy.DisplayAfterSessionEndRunAsync(CancellationToken.None);

        Times count = requested == true ? Times.Never() : Times.Once();
        originalDevice.Verify(value => value.DisplayBannerAsync("banner", CancellationToken.None), count);
        originalDevice.Verify(value => value.DisplayBeforeSessionStartAsync(CancellationToken.None), count);
        originalDevice.Verify(value => value.DisplayAfterSessionEndRunAsync(CancellationToken.None), count);
    }

    [TestMethod]
    public async Task ConfigureRpcOnlyOutput_WithoutJsonRpcDevice_DoesNotSilenceOriginal()
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var proxy = new ProxyOutputDevice(originalDevice.Object, null);
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>();
        TextOutputDeviceData message = new("CLI or pipe output");

        Assert.IsFalse(proxy.ConfigureRpcOnlyOutput(true));
        await proxy.DisplayAsync(producer, message, CancellationToken.None);

        originalDevice.Verify(value => value.DisplayAsync(producer, message, CancellationToken.None), Times.Once);
    }

    [TestMethod]
    public async Task ConfigureRpcOnlyOutput_ResetRestoresOriginalRoute()
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var serverModeDevice = new ServerModePerCallOutputDevice(null, Mock.Of<IStopPoliciesService>());
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice);
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>();
        TextOutputDeviceData message = new("startup failure");
        proxy.ConfigureRpcOnlyOutput(true);
        proxy.ConfigureRpcOnlyOutput(false);

        await proxy.DisplayAsync(producer, message, CancellationToken.None);

        originalDevice.Verify(value => value.DisplayAsync(producer, message, CancellationToken.None), Times.Once);
    }

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
}
