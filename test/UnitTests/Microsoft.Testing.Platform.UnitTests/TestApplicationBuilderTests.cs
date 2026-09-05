// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestApplicationBuilderTests
{
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    private readonly ServiceProvider _serviceProvider = new();

    public TestApplicationBuilderTests()
    {
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        AggregatedConfiguration configuration = new([], testApplicationModuleInfo, new SystemFileSystem(), new SystemEnvironment(), new(null, [], []));
        configuration.SetCurrentWorkingDirectory(string.Empty);
        configuration.SetCurrentWorkingDirectory(string.Empty);
        _serviceProvider.AddService(configuration);
    }

    [TestMethod]
    public async Task TestApplicationLifecycleCallbacks_DuplicatedId_ShouldFail()
    {
        TestHostManager testHostManager = new();
        testHostManager.AddTestHostApplicationLifetime(_ => new ApplicationLifecycleCallbacks("duplicatedId"));
        testHostManager.AddTestHostApplicationLifetime(_ => new ApplicationLifecycleCallbacks("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildTestApplicationLifecycleCallbackAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(ApplicationLifecycleCallbacks).ToString()));
    }

    [TestMethod]
    public async Task DataConsumer_DuplicatedId_ShouldFail()
    {
        TestHostManager testHostManager = new();
        testHostManager.AddDataConsumer(_ => new Consumer("duplicatedId"));
        testHostManager.AddDataConsumer(_ => new Consumer("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildDataConsumersAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(Consumer).ToString()));
    }

    [TestMethod]
    public async Task DataConsumer_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostManager testHostManager = new();
        CompositeExtensionFactory<Consumer> compositeExtensionFactory = new(() => new Consumer("duplicatedId"));
        testHostManager.AddDataConsumer(_ => new Consumer("duplicatedId"));
        testHostManager.AddDataConsumer(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildDataConsumersAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(Consumer).ToString()));
    }

    [TestMethod]
    public async Task TestSessionLifetimeHandle_DuplicatedId_ShouldFail()
    {
        TestHostManager testHostManager = new();
        testHostManager.AddTestSessionLifetimeHandler(_ => new TestSessionLifetimeHandler("duplicatedId"));
        testHostManager.AddTestSessionLifetimeHandler(_ => new TestSessionLifetimeHandler("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildTestSessionLifetimeHandleAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestSessionLifetimeHandler).ToString()));
    }

    [TestMethod]
    public async Task TestSessionLifetimeHandle_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostManager testHostManager = new();
        CompositeExtensionFactory<TestSessionLifetimeHandler> compositeExtensionFactory = new(() => new TestSessionLifetimeHandler("duplicatedId"));
        testHostManager.AddTestSessionLifetimeHandler(_ => new TestSessionLifetimeHandler("duplicatedId"));
        testHostManager.AddTestSessionLifetimeHandler(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildTestSessionLifetimeHandleAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestSessionLifetimeHandler).ToString()));
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task TestHost_ComposeFactory_ShouldSucceed(bool withParameter)
    {
        TestHostManager testHostManager = new();
        CompositeExtensionFactory<TestSessionLifetimeHandlerPlusConsumer> compositeExtensionFactory =
            withParameter
            ? new(sp => new TestSessionLifetimeHandlerPlusConsumer(sp))
            : new(() => new TestSessionLifetimeHandlerPlusConsumer());
        testHostManager.AddTestSessionLifetimeHandler(compositeExtensionFactory);
        testHostManager.AddDataConsumer(compositeExtensionFactory);
        List<ICompositeExtensionFactory> compositeExtensions = [];
        IDataConsumer[] consumers = [.. (await testHostManager.BuildDataConsumersAsync(_serviceProvider, compositeExtensions)).Select(x => (IDataConsumer)x.Consumer)];
        ITestSessionLifetimeHandler[] sessionLifetimeHandle = [.. (await testHostManager.BuildTestSessionLifetimeHandleAsync(_serviceProvider, compositeExtensions)).Select(x => (ITestSessionLifetimeHandler)x.TestSessionLifetimeHandler)];
        Assert.HasCount(1, consumers);
        Assert.HasCount(1, sessionLifetimeHandle);
        Assert.AreEqual(compositeExtensions[0].GetInstance(), consumers[0]);
        Assert.AreEqual(compositeExtensions[0].GetInstance(), sessionLifetimeHandle[0]);
    }

    [TestMethod]
    public async Task TestHostControllerEnvironmentVariableProvider_DuplicatedId_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("duplicatedId"));
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostEnvironmentVariableProvider).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerEnvironmentVariableProvider_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        CompositeExtensionFactory<TestHostEnvironmentVariableProvider> compositeExtensionFactory = new(() => new TestHostEnvironmentVariableProvider("duplicatedId"));
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("duplicatedId"));
        testHostControllerManager.AddEnvironmentVariableProvider(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostEnvironmentVariableProvider).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerEnvironmentVariableProvider_InsertFirst_ShouldPreserveOrdering()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("second"));
        testHostControllerManager.AddEnvironmentVariableProviderFirst(_ => new TestHostEnvironmentVariableProvider("first"));

        TestHostControllerConfiguration configuration = await testHostControllerManager.BuildAsync(_serviceProvider);

        Assert.AreEqual("first", configuration.EnvironmentVariableProviders[0].Uid);
        Assert.AreEqual("second", configuration.EnvironmentVariableProviders[1].Uid);
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_DuplicatedId_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler("duplicatedId"));
        testHostControllerManager.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostProcessLifetimeHandler).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        CompositeExtensionFactory<TestHostProcessLifetimeHandler> compositeExtensionFactory = new(() => new TestHostProcessLifetimeHandler("duplicatedId"));
        testHostControllerManager.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler("duplicatedId"));
        testHostControllerManager.AddProcessLifetimeHandler(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostProcessLifetimeHandler).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_FinalizationTokenStartsUncanceled()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        bool? wasCanceled = null;

        bool finalized = await TryRunControllerExtensionAsync(
            token =>
            {
                wasCanceled = token.IsCancellationRequested;
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        Assert.IsTrue(finalized);
        Assert.IsFalse(wasCanceled);
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_FinalizationIsBounded()
    {
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));
        using ManualResetEventSlim releaseFinalization = new(initialState: false);
        try
        {
            bool finalized = await TryRunControllerExtensionAsync(
                _ =>
                {
                    releaseFinalization.Wait(CancellationToken.None);
                    return Task.CompletedTask;
                },
                cancellationTokenSource.Token);

            Assert.IsFalse(finalized);
        }
        finally
        {
            releaseFinalization.Set();
        }
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_WaitIsBounded()
    {
        Mock<IProcess> process = new();
        TaskCompletionSource<bool> neverExits = new(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(neverExits.Task);
        var stopwatch = Stopwatch.StartNew();

        bool exited = await WaitForExitAfterTerminationAsync(process.Object, TimeSpan.FromMilliseconds(100));

        Assert.IsFalse(exited);
        Assert.IsLessThan(5, stopwatch.Elapsed.TotalSeconds);
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_UnresponsiveCustomHandleIsTerminatedAndDeferred()
    {
        TaskCompletionSource<bool> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.HasExited).Returns(() => exited.Task.IsCompleted);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(exited.Task);
        handle.Setup(x => x.Dispose()).Callback(() => disposed.TrySetResult(true));
        var adapter = new TestHostHandleToProcessAdapter(handle.Object);
        bool cancellationRequested = false;

        await TestHostControllersTestHost.HandleCanceledTestHostAsync(
            adapter,
            () => cancellationRequested = true,
            new NopLogger(),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50));

        Assert.IsTrue(cancellationRequested);
        handle.Verify(x => x.Terminate(), Times.Once);

        adapter.Dispose();
        handle.Verify(x => x.Dispose(), Times.Never);

        exited.SetResult(true);
        await disposed.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        handle.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public void TestHostControllerProcessTermination_CooperativeBudgetUsesConfiguredCanceledConsumerBudget()
    {
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("60");
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        using var host = new TestHostControllersTestHost(
            new([], [], [], testHostLauncher: null, requireProcessRestart: false),
            new(),
            passiveNode: null,
            environment.Object,
            loggerFactory.Object,
            Mock.Of<IClock>());
        FieldInfo field = typeof(TestHostControllersTestHost).GetField(
            "_testHostCooperativeShutdownTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost._testHostCooperativeShutdownTimeout.");
        var cooperativeShutdownTimeout = (TimeSpan)field.GetValue(host)!;

        Assert.AreEqual(TimeSpan.FromSeconds(75), cooperativeShutdownTimeout);
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_CustomHandleDisposalWaitsForExit()
    {
        TaskCompletionSource<bool> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.HasExited).Returns(() => exited.Task.IsCompleted);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(exited.Task);
        handle.Setup(x => x.Dispose()).Callback(() => disposed.TrySetResult(true));
        var adapter = new TestHostHandleToProcessAdapter(handle.Object);

        adapter.DeferDisposalUntilExit();
        adapter.Dispose();

        handle.Verify(x => x.Dispose(), Times.Never);

        exited.SetResult(true);
        await disposed.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        handle.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public void TestHostControllerProcessTermination_CustomHandleDisposalIsImmediateByDefault()
    {
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.HasExited).Returns(false);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
        var adapter = new TestHostHandleToProcessAdapter(handle.Object);

        adapter.Dispose();

        handle.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public void TestHostControllerOutputFinalization_AbandonmentTracksProxyAndOriginalDevice()
    {
        Mock<IPlatformOutputDevice> originalOutputDevice = new();
        ProxyOutputDevice proxyOutputDevice = new(originalOutputDevice.Object, serverModeOutputDevice: null);
        List<object> servicesStillRunning = [];

        MarkOutputDeviceStillRunning(servicesStillRunning, proxyOutputDevice);

        Assert.HasCount(2, servicesStillRunning);
        Assert.Contains(proxyOutputDevice, servicesStillRunning);
        Assert.Contains(originalOutputDevice.Object, servicesStillRunning);
    }

    [TestMethod]
    public void TestHostControllerFinalization_LateCancellationArmsSharedBoundedTokenSource()
    {
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS))
            .Returns("0.1");
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        using var host = new TestHostControllersTestHost(
            new([], [], [], testHostLauncher: null, requireProcessRestart: false),
            new(),
            passiveNode: null,
            environment.Object,
            loggerFactory.Object,
            Mock.Of<IClock>());

        CancellationTokenSource first = EnsureControllerFinalizationCancellationTokenSource(host);
        CancellationTokenSource second = EnsureControllerFinalizationCancellationTokenSource(host);

        Assert.IsFalse(first.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(500)));

        ArmControllerFinalizationTimeout(host);

        Assert.AreSame(first, second);
        Assert.IsTrue(first.Token.WaitHandle.WaitOne(TimeoutHelper.DefaultHangTimeSpanTimeout));
    }

    [TestMethod]
    public async Task TestHostControllerFinalization_CancellationDuringCleanupArmsStableToken()
    {
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS))
            .Returns("0.1");
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        using var host = new TestHostControllersTestHost(
            new([], [], [], testHostLauncher: null, requireProcessRestart: false),
            new(),
            passiveNode: null,
            environment.Object,
            loggerFactory.Object,
            Mock.Of<IClock>());
        using CancellationTokenSource applicationCancellationTokenSource = new();
        TaskCompletionSource<bool> cleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EnsureControllerFinalizationCancellationTokenSource(host);
        RegisterControllerFinalizationTransition(host, applicationCancellationTokenSource.Token);

        try
        {
            Task<bool> cleanup = TryRunControllerCleanupAsync(
                host,
                () =>
                {
                    cleanupStarted.TrySetResult(true);
                    return releaseCleanup.Task;
                });
            await cleanupStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

#pragma warning disable VSTHRD103 // CancelAsync is unavailable on net462, which this project also targets.
            applicationCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103

            await cleanup.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            Assert.IsFalse(await cleanup);
        }
        finally
        {
            releaseCleanup.TrySetResult(true);
        }
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task TestHostController_ComposeFactory_ShouldSucceed(bool withParameter)
    {
        TestHostControllersManager testHostControllerManager = new();
        CompositeExtensionFactory<TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider> compositeExtensionFactory =
            withParameter
            ? new(sp => new TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider(sp))
            : new(() => new TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider());
        testHostControllerManager.AddEnvironmentVariableProvider(compositeExtensionFactory);
        testHostControllerManager.AddProcessLifetimeHandler(compositeExtensionFactory);
        TestHostControllerConfiguration configuration = await testHostControllerManager.BuildAsync(_serviceProvider);
        Assert.IsTrue(configuration.RequireProcessRestart);
        Assert.HasCount(1, configuration.LifetimeHandlers);
        Assert.HasCount(1, configuration.EnvironmentVariableProviders);
        Assert.AreEqual((object)configuration.LifetimeHandlers[0], configuration.EnvironmentVariableProviders[0]);
        Assert.AreEqual(((ICompositeExtensionFactory)compositeExtensionFactory).GetInstance(), configuration.LifetimeHandlers[0]);
        Assert.AreEqual(((ICompositeExtensionFactory)compositeExtensionFactory).GetInstance(), configuration.EnvironmentVariableProviders[0]);
    }

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
    [TestMethod]
    public async Task TestHostLauncher_WhenRegistered_ForcesProcessRestartAndIsStored()
    {
        TestHostControllersManager testHostControllerManager = new();
        TestHostLauncher launcher = new("launcher");
        testHostControllerManager.AddTestHostLauncher(_ => launcher);
        TestHostControllerConfiguration configuration = await testHostControllerManager.BuildAsync(_serviceProvider);
        Assert.IsTrue(configuration.RequireProcessRestart);
        Assert.AreEqual((object)launcher, configuration.TestHostLauncher);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainer pipe authorization is Windows-only.")]
    public async Task TestHostLauncher_AuthorizationConsumerResolvesAndStoresIdentity()
    {
        TestHostControllersManager testHostControllerManager = new();
        TestHostLauncher launcher = new("launcher");
        testHostControllerManager.AddTestHostLauncher(_ => launcher);

        ITestHostLauncher? builtLauncher = await testHostControllerManager.BuildTestHostLauncherAsync(_serviceProvider);
        IReadOnlyList<string>? authorizedSecurityIdentities =
            await _serviceProvider.ResolveTestHostControllerAuthorizedSecurityIdentitiesAsync(
                builtLauncher,
                "testhost.exe",
                new NopLogger(),
                CancellationToken.None);

        Assert.AreSequenceEqual([ContosoPackageSid], authorizedSecurityIdentities);
        Assert.AreSame(authorizedSecurityIdentities, _serviceProvider.TestHostControllerAuthorizedSecurityIdentities);
    }

    [TestMethod]
    public async Task TestHostLauncher_MultipleRegistered_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("launcher1"));
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("launcher2"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
    }

    [TestMethod]
    public async Task TestHostLauncher_DuplicatedId_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("duplicatedId"));
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostLauncher).ToString()));
    }
#pragma warning restore TPEXP

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ComposeFactory_InvalidComposition_ShouldFail(bool withParameter)
    {
        CompositeExtensionFactory<InvalidComposition> compositeExtensionFactory =
            withParameter
            ? new CompositeExtensionFactory<InvalidComposition>(sp => new InvalidComposition(sp))
            : new CompositeExtensionFactory<InvalidComposition>(() => new InvalidComposition());
        InvalidOperationException invalidOperationException = Assert.ThrowsExactly<InvalidOperationException>(() => ((ICompositeExtensionFactory)compositeExtensionFactory).GetInstance());
        Assert.AreEqual(CompositeExtensionFactory<InvalidComposition>.ValidateCompositionErrorMessage, invalidOperationException.Message);
    }

    private static Task<bool> TryRunControllerExtensionAsync(
        Func<CancellationToken, Task> finalization,
        CancellationToken cancellationToken)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "TryRunControllerExtensionAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.TryRunControllerExtensionAsync.");
        return (Task<bool>?)method.Invoke(null, [finalization, cancellationToken])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.TryRunControllerExtensionAsync returned null.");
    }

    private static Task<bool> WaitForExitAfterTerminationAsync(IProcess process, TimeSpan timeout)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "WaitForExitAfterTerminationAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.WaitForExitAfterTerminationAsync.");
        return (Task<bool>?)method.Invoke(null, [process, timeout])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.WaitForExitAfterTerminationAsync returned null.");
    }

    private static void MarkOutputDeviceStillRunning(List<object> servicesStillRunning, ProxyOutputDevice outputDevice)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "MarkOutputDeviceStillRunning",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.MarkOutputDeviceStillRunning.");
        method.Invoke(null, [servicesStillRunning, outputDevice]);
    }

    private static CancellationTokenSource EnsureControllerFinalizationCancellationTokenSource(TestHostControllersTestHost host)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "EnsureControllerFinalizationCancellationTokenSource",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.EnsureControllerFinalizationCancellationTokenSource.");
        return (CancellationTokenSource?)method.Invoke(host, null)
            ?? throw new InvalidOperationException("TestHostControllersTestHost.EnsureControllerFinalizationCancellationTokenSource returned null.");
    }

    private static void ArmControllerFinalizationTimeout(TestHostControllersTestHost host)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "ArmControllerFinalizationTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.ArmControllerFinalizationTimeout.");
        method.Invoke(host, null);
    }

    private static void RegisterControllerFinalizationTransition(TestHostControllersTestHost host, CancellationToken applicationCancellationToken)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "RegisterControllerFinalizationTransition",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.RegisterControllerFinalizationTransition.");
        method.Invoke(host, [applicationCancellationToken]);
    }

    private static Task<bool> TryRunControllerCleanupAsync(TestHostControllersTestHost host, Func<Task> cleanup)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "TryRunControllerCleanupAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.TryRunControllerCleanupAsync.");
        return (Task<bool>?)method.Invoke(host, [cleanup])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.TryRunControllerCleanupAsync returned null.");
    }

    [SuppressMessage("Design", "TA0001:Extension should not implement cross-functional areas", Justification = "Done on purpose for testing error")]
    private sealed class InvalidComposition : ITestHostProcessLifetimeHandler, ITestSessionLifetimeHandler
    {
        public InvalidComposition(IServiceProvider serviceProvider)
        {
        }

        public InvalidComposition()
        {
        }

        public string Uid => nameof(InvalidComposition);

        public string Version => PlatformVersion.Version;

        public string DisplayName => nameof(InvalidComposition);

        public string Description => nameof(InvalidComposition);

        public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => throw new NotImplementedException();

        public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();

        public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();
    }

    private sealed class TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider : ITestHostProcessLifetimeHandler, ITestHostEnvironmentVariableProvider
    {
        public TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider()
        {
        }

        public TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider(IServiceProvider serviceProvider)
        {
        }

        public string Uid => nameof(TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider);

        public string Version => PlatformVersion.Version;

        public string DisplayName => nameof(TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider);

        public string Description => nameof(TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider);

        public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables) => throw new NotImplementedException();

        public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task UpdateAsync(IEnvironmentVariables environmentVariables) => throw new NotImplementedException();
    }

    private sealed class TestHostProcessLifetimeHandler : ITestHostProcessLifetimeHandler
    {
        public TestHostProcessLifetimeHandler(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
    private sealed class TestHostLauncher : ITestHostLauncher, ITestHostControllerConnectionAuthorizer
    {
        public TestHostLauncher(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(TestHostLauncher);

        public string DisplayName => nameof(TestHostLauncher);

        public string Description => nameof(TestHostLauncher);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<string>> GetAuthorizedSecurityIdentitiesAsync(string testHostFileName, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([ContosoPackageSid]);
    }
#pragma warning restore TPEXP

    private sealed class TestHostEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
    {
        public TestHostEnvironmentVariableProvider(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task UpdateAsync(IEnvironmentVariables environmentVariables) => throw new NotImplementedException();

        public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables) => throw new NotImplementedException();
    }

    public sealed class TestSessionLifetimeHandlerPlusConsumer : ITestSessionLifetimeHandler, IDataConsumer
    {
        public TestSessionLifetimeHandlerPlusConsumer()
        {
        }

        public TestSessionLifetimeHandlerPlusConsumer(IServiceProvider serviceProvider)
        {
        }

        public string Uid => nameof(TestSessionLifetimeHandlerPlusConsumer);

        public string Version => PlatformVersion.Version;

        public string DisplayName => nameof(TestSessionLifetimeHandlerPlusConsumer);

        public string Description => nameof(TestSessionLifetimeHandlerPlusConsumer);

        public Type[] DataTypesConsumed => [];

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();

        public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();
    }

    private sealed class TestSessionLifetimeHandler : ITestSessionLifetimeHandler
    {
        public TestSessionLifetimeHandler(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();

        public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();
    }

    private sealed class Consumer : IDataConsumer
    {
        public Consumer(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Type[] DataTypesConsumed => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class ApplicationLifecycleCallbacks : ITestHostApplicationLifetime
    {
        public ApplicationLifecycleCallbacks(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(ApplicationLifecycleCallbacks);

        public string DisplayName => nameof(ApplicationLifecycleCallbacks);

        public string Description => nameof(ApplicationLifecycleCallbacks);

        public Task AfterRunAsync(int exitCode, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
