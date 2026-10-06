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
    [DataRow(TestProcessRole.TestHost, 1)]
    [DataRow(TestProcessRole.TestHostController, 0)]
    [DataRow(TestProcessRole.TestHostOrchestrator, 0)]
    public async Task HandleProcessRoleAsync_RegistersOneSharedCallbackOnlyForTestHost(object processRole, int expectedCallbacks)
    {
        TestProcessRole role = Assert.IsInstanceOfType<TestProcessRole>(processRole);
        Mock<IPlatformOutputDevice> originalDevice = new();
        Mock<IStopPoliciesService> policies = new();
        List<Func<int, CancellationToken, Task>> callbacks = [];
        policies.Setup(service => service.RegisterOnMaxFailedTestsCallbackAsync(It.IsAny<Func<int, CancellationToken, Task>>()))
            .Callback<Func<int, CancellationToken, Task>>(callbacks.Add)
            .Returns(Task.CompletedTask);
        using var serverDevice = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverDevice, policies.Object);

        using CancellationTokenSource roleCancellation = new();
        using CancellationTokenSource callbackCancellation = new();
        await proxy.HandleProcessRoleAsync(role, roleCancellation.Token);
        await proxy.HandleProcessRoleAsync(role, CancellationToken.None);

        originalDevice.Verify(device => device.HandleProcessRoleAsync(role, roleCancellation.Token), Times.Once);
        originalDevice.Verify(device => device.HandleProcessRoleAsync(role, CancellationToken.None), Times.Once);
        Assert.HasCount(expectedCallbacks, callbacks);
        if (expectedCallbacks == 1)
        {
            await callbacks[0](42, callbackCancellation.Token);
            originalDevice.Verify(
                device => device.DisplayAsync(proxy, It.IsAny<TextOutputDeviceData>(), callbackCancellation.Token),
                Times.Once);
            Assert.AreEqual(nameof(ProxyOutputDevice), proxy.Uid);
        }
    }

    [TestMethod]
    [DataRow(false, false, 1)]
    [DataRow(true, false, 1)]
    [DataRow(false, true, 0)]
    [DataRow(true, true, 0)]
    public async Task BuildAsync_RegistersMaxFailedTestsMessageExceptForPipeProtocol(bool useServerModeOutputDevice, bool isPipeProtocol, int expectedCallbacks)
    {
        Mock<IStopPoliciesService> policies = new();
        ServiceProvider services = new();
        services.AddService(policies.Object);
        Mock<IPlatformOutputDevice> originalDevice = new();
        originalDevice.Setup(device => device.IsEnabledAsync()).ReturnsAsync(true);
        PlatformOutputDeviceManager manager = new();
        manager.SetPlatformOutputDevice(_ => originalDevice.Object);
        using ProxyOutputDevice proxy = await manager.BuildAsync(services, useServerModeOutputDevice, isPipeProtocol);

        await proxy.HandleProcessRoleAsync(TestProcessRole.TestHost, CancellationToken.None);

        policies.Verify(
            service => service.RegisterOnMaxFailedTestsCallbackAsync(It.IsAny<Func<int, CancellationToken, Task>>()),
            Times.Exactly(expectedCallbacks));
        if (isPipeProtocol)
        {
            Assert.IsInstanceOfType<DotnetTestPassthroughOutputDevice>(proxy.OriginalOutputDevice);
            Assert.IsFalse(proxy.ConfigureRpcOnlyOutput(true));
        }
        else
        {
            Assert.AreSame(originalDevice.Object, proxy.OriginalOutputDevice);
        }
    }

    [TestMethod]
    public async Task DisplayAsync_BeforeHandshake_PreservesUserMessageForOriginalDevice()
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var serverModeDevice = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice, policiesService: null);
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>();

        TextOutputDeviceData message = new("user startup output");
        await proxy.DisplayAsync(producer, message, CancellationToken.None);

        originalDevice.Verify(
            value => value.DisplayAsync(
                producer,
                message,
                CancellationToken.None),
            Times.Once);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisplayAsync_CustomRendererDeclinesAndPreservesAllDuties(bool? requested)
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var serverModeDevice = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice, policiesService: null);
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

        Assert.IsFalse(proxy.ConfigureRpcOnlyOutput(requested));
        foreach (IOutputDeviceData message in messages)
        {
            await proxy.DisplayAsync(producer, message, CancellationToken.None);
            originalDevice.Verify(value => value.DisplayAsync(producer, message, CancellationToken.None), Times.Once());
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
        using var serverModeDevice = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice, policiesService: null);
        Assert.IsFalse(proxy.ConfigureRpcOnlyOutput(requested));

        await proxy.DisplayBannerAsync("banner", CancellationToken.None);
        await proxy.DisplayBeforeSessionStartAsync(CancellationToken.None);
        await proxy.DisplayAfterSessionEndRunAsync(CancellationToken.None);

        var count = Times.Once();
        originalDevice.Verify(value => value.DisplayBannerAsync("banner", CancellationToken.None), count);
        originalDevice.Verify(value => value.DisplayBeforeSessionStartAsync(CancellationToken.None), count);
        originalDevice.Verify(value => value.DisplayAfterSessionEndRunAsync(CancellationToken.None), count);
    }

    [TestMethod]
    public async Task ConfigureRpcOnlyOutput_WithoutJsonRpcDevice_DoesNotSilenceOriginal()
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        using var proxy = new ProxyOutputDevice(originalDevice.Object, null, policiesService: null);
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
        using var serverModeDevice = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice, policiesService: null);
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
        var serverModeDevice = new ServerModePerCallOutputDevice(fileLoggerProvider: null);
        var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice, policiesService: null);

        proxy.Dispose();

        originalDisposable.Verify(x => x.Dispose(), Times.Never);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => serverModeDevice.DisplayAsync(
                Mock.Of<IOutputDeviceDataProducer>(producer => producer.Uid == "producer"),
                new ProgressMessageOutputDeviceData("key", "message"),
                CancellationToken.None));
    }
}
