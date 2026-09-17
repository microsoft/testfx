// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Hosting;
using Microsoft.Testing.Extensions.Hosting.Resources;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class HotReloadTests
{
    private const string DotnetWatchEnvironmentVariable = "DOTNET_WATCH";
    private const string HotReloadEnabledEnvironmentVariable = "TESTINGPLATFORM_HOTRELOAD_ENABLED";

    private static readonly FieldInfo ShutdownProcessField = typeof(HotReloadHandler)
        .GetField("s_shutdownProcess", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly SemaphoreSlim HotReloadSemaphore = (SemaphoreSlim)typeof(HotReloadHandler)
        .GetField("SemaphoreSlim", BindingFlags.NonPublic | BindingFlags.Static)!
        .GetValue(null)!;

    public HotReloadTests() => ResetHotReloadState();

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void Cleanup() => ResetHotReloadState();

    [TestMethod]
    [DataRow("1", null)]
    [DataRow(null, "1")]
    public async Task Constructor_ActivationEnvironmentVariableIsOne_EnablesHotReloadAndRegistersShutdownFallback(
        string? dotnetWatch,
        string? hotReloadEnabled)
    {
        Mock<IEnvironment> environment = CreateEnvironment(dotnetWatch, hotReloadEnabled);
        var runtimeFeature = new SystemRuntimeFeature();
        var stopPolicies = new Mock<IStopPoliciesService>();
        Func<Task<bool>>? shutdownFallback = null;
        stopPolicies
            .Setup(service => service.RegisterDeadlineStopFallback(It.IsAny<Func<Task<bool>>>()))
            .Callback<Func<Task<bool>>>(callback => shutdownFallback = callback);
        ServiceProvider serviceProvider = CreateServiceProvider(environment.Object, runtimeFeature, stopPolicies.Object);

        _ = new HotReloadTestHostTestFrameworkInvoker(serviceProvider);

        Assert.IsTrue(runtimeFeature.IsHotReloadEnabled);
        Assert.IsNotNull(shutdownFallback);
        Assert.IsTrue(await shutdownFallback());
        Assert.IsTrue(IsShutdownRequested());
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("0", "0")]
    [DataRow("true", null)]
    [DataRow(null, "true")]
    public void Constructor_ActivationEnvironmentVariablesAreNotOne_DoesNotEnableHotReload(
        string? dotnetWatch,
        string? hotReloadEnabled)
    {
        Mock<IEnvironment> environment = CreateEnvironment(dotnetWatch, hotReloadEnabled);
        var runtimeFeature = new SystemRuntimeFeature();
        var stopPolicies = new Mock<IStopPoliciesService>();
        ServiceProvider serviceProvider = CreateServiceProvider(environment.Object, runtimeFeature, stopPolicies.Object);

        _ = new HotReloadTestHostTestFrameworkInvoker(serviceProvider);

        Assert.IsFalse(runtimeFeature.IsHotReloadEnabled);
        stopPolicies.Verify(
            service => service.RegisterDeadlineStopFallback(It.IsAny<Func<Task<bool>>>()),
            Times.Never);
    }

    [TestMethod]
    public void PlatformGuards_CurrentDesktopPlatform_IsSupported()
    {
        Assert.IsFalse(InvokeGuard("IsCancelKeyPressNotSupported"));
#if NET6_0_OR_GREATER
        Assert.IsFalse(InvokeGuard("IsClearNotSupported"));
#endif
    }

#if NET6_0_OR_GREATER
    [TestMethod]
    public async Task ShouldRunAsync_CompletedExecution_DisplaysCompletionClearsConsoleAndDisplaysStart()
    {
        var console = new Mock<IConsole>();
        console.SetupGet(instance => instance.IsOutputRedirected).Returns(false);
        var outputDevice = new Mock<IOutputDevice>();
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>();
        List<string> events = [];
        outputDevice
            .Setup(device => device.DisplayAsync(
                producer,
                It.IsAny<IOutputDeviceData>(),
                CancellationToken.None))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (_, data, _) => events.Add(Assert.IsInstanceOfType<TextOutputDeviceData>(data).Text))
            .Returns(Task.CompletedTask);
        console.Setup(instance => instance.Clear()).Callback(() => events.Add("clear"));
        var handler = new HotReloadHandler(console.Object, outputDevice.Object, producer);

        bool shouldRun = await handler.ShouldRunAsync(Task.CompletedTask, CancellationToken.None);

        Assert.IsTrue(shouldRun);
        Assert.AreSequenceEqual(
            [
                ExtensionResources.HotReloadSessionCompleted,
                "clear",
                ExtensionResources.HotReloadSessionStarted,
            ],
            events);
    }

    [TestMethod]
    public async Task ShouldRunAsync_OutputIsRedirected_DoesNotClearConsole()
    {
        var console = new Mock<IConsole>();
        console.SetupGet(instance => instance.IsOutputRedirected).Returns(true);
        var outputDevice = new Mock<IOutputDevice>();
        List<string> displayedMessages = [];
        outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                CancellationToken.None))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (_, data, _) => displayedMessages.Add(Assert.IsInstanceOfType<TextOutputDeviceData>(data).Text))
            .Returns(Task.CompletedTask);
        var handler = new HotReloadHandler(console.Object, outputDevice.Object, Mock.Of<IOutputDeviceDataProducer>());

        bool shouldRun = await handler.ShouldRunAsync(waitExecutionCompletion: null, CancellationToken.None);

        Assert.IsTrue(shouldRun);
        Assert.AreSequenceEqual([ExtensionResources.HotReloadSessionStarted], displayedMessages);
        console.Verify(instance => instance.Clear(), Times.Never);
    }

    [TestMethod]
    public async Task UpdateApplication_WaiterIsBlocked_ReleasesWaiterForNextRun()
    {
        DrainInitialSignal();
        var console = new Mock<IConsole>();
        console.SetupGet(instance => instance.IsOutputRedirected).Returns(true);
        var outputDevice = new Mock<IOutputDevice>();
        outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                CancellationToken.None))
            .Returns(Task.CompletedTask);
        var handler = new HotReloadHandler(console.Object, outputDevice.Object, Mock.Of<IOutputDeviceDataProducer>());
        Task<bool> shouldRunTask = handler.ShouldRunAsync(waitExecutionCompletion: null, CancellationToken.None);

        Assert.IsFalse(shouldRunTask.IsCompleted);
        HotReloadHandler.UpdateApplication(null);

        Assert.IsTrue(await shouldRunTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RequestShutdown_WaiterIsBlocked_ReleasesWaiterAndStopsNextRun()
    {
        DrainInitialSignal();
        var console = new Mock<IConsole>();
        console.SetupGet(instance => instance.IsOutputRedirected).Returns(true);
        var outputDevice = new Mock<IOutputDevice>();
        outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                CancellationToken.None))
            .Returns(Task.CompletedTask);
        var handler = new HotReloadHandler(console.Object, outputDevice.Object, Mock.Of<IOutputDeviceDataProducer>());
        Task<bool> shouldRunTask = handler.ShouldRunAsync(waitExecutionCompletion: null, CancellationToken.None);

        Assert.IsFalse(shouldRunTask.IsCompleted);
        HotReloadHandler.RequestShutdown();

        Assert.IsFalse(await shouldRunTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.IsFalse(await handler.ShouldRunAsync(waitExecutionCompletion: null, CancellationToken.None));
    }

    [TestMethod]
    public void UpdateApplication_NoWaiter_IsIdempotent()
    {
        HotReloadHandler.UpdateApplication(null);
        HotReloadHandler.UpdateApplication(null);

        Assert.AreEqual(1, HotReloadSemaphore.CurrentCount);
    }

    [TestMethod]
    public void RequestShutdown_NoWaiter_IsIdempotent()
    {
        HotReloadHandler.RequestShutdown();
        HotReloadHandler.RequestShutdown();

        Assert.IsTrue(IsShutdownRequested());
        Assert.AreEqual(1, HotReloadSemaphore.CurrentCount);
    }

    [TestMethod]
    public async Task ShouldRunAsync_CancellationRequested_StopsNextRun()
    {
        var console = new Mock<IConsole>();
        console.SetupGet(instance => instance.IsOutputRedirected).Returns(true);
        var outputDevice = new Mock<IOutputDevice>();
        outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new HotReloadHandler(console.Object, outputDevice.Object, Mock.Of<IOutputDeviceDataProducer>());
        using var cancellationTokenSource = new CancellationTokenSource();
#pragma warning disable VSTHRD103 // CancelAsync is unavailable on .NET Framework.
        cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103

        bool shouldRun = await handler.ShouldRunAsync(waitExecutionCompletion: null, cancellationTokenSource.Token);

        Assert.IsFalse(shouldRun);
    }
#else
    [TestMethod]
    public async Task ShouldRunAsync_UnsupportedFramework_ThrowsNotSupportedException()
    {
        var handler = new HotReloadHandler(
            Mock.Of<IConsole>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IOutputDeviceDataProducer>());

        NotSupportedException exception = await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => handler.ShouldRunAsync(waitExecutionCompletion: null, CancellationToken.None));

        Assert.AreEqual(ExtensionResources.HotReloadHandlerUnsupportedFrameworkErrorMessage, exception.Message);
    }
#endif

    private static Mock<IEnvironment> CreateEnvironment(string? dotnetWatch, string? hotReloadEnabled)
    {
        var environment = new Mock<IEnvironment>();
        environment
            .Setup(instance => instance.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns((string name) => name switch
            {
                DotnetWatchEnvironmentVariable => dotnetWatch,
                HotReloadEnabledEnvironmentVariable => hotReloadEnabled,
                _ => null,
            });
        return environment;
    }

    private static ServiceProvider CreateServiceProvider(
        IEnvironment environment,
        SystemRuntimeFeature runtimeFeature,
        IStopPoliciesService stopPolicies)
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(environment);
        serviceProvider.AddService(runtimeFeature);
        serviceProvider.AddService(stopPolicies);
        return serviceProvider;
    }

    private static bool InvokeGuard(string methodName)
        => (bool)typeof(HotReloadHandler)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

    private static bool IsShutdownRequested()
        => (bool)ShutdownProcessField.GetValue(null)!;

#if NET6_0_OR_GREATER
    private static void DrainInitialSignal()
        => Assert.IsTrue(HotReloadSemaphore.Wait(0), "The HotReload signal should initially be available.");
#endif

    private static void ResetHotReloadState()
    {
        ShutdownProcessField.SetValue(null, false);
        while (HotReloadSemaphore.Wait(0))
        {
        }

        HotReloadSemaphore.Release();
    }
}
