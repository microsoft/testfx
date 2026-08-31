// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommonHostTests
{
    [TestMethod]
    public async Task RequestGracefulSessionStopAsync_UsesActiveRequestCapabilitiesBeforeApplicationCapability()
    {
        Mock<IGracefulStopTestExecutionCapability> applicationCapability = new();
        Mock<IGracefulStopTestExecutionCapability> requestCapability = new();
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestFrameworkCapabilities(applicationCapability.Object));
        TestableCommonHost host = new(serviceProvider);

        await host.RegisterActiveGracefulStopCapabilityForTestingAsync(requestCapability.Object);
        await host.RequestGracefulSessionStopForTestingAsync();

        requestCapability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
        applicationCapability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);

        host.UnregisterActiveGracefulStopCapabilityForTesting(requestCapability.Object);
        await host.RequestGracefulSessionStopForTestingAsync();

        applicationCapability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RegisterActiveGracefulStopCapabilityAsync_AfterStoppedRequestUnregisters_StopsNextRequest()
    {
        Mock<IGracefulStopTestExecutionCapability> applicationCapability = new();
        Mock<IGracefulStopTestExecutionCapability> completedRequestCapability = new();
        Mock<IGracefulStopTestExecutionCapability> nextRequestCapability = new();
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestFrameworkCapabilities(applicationCapability.Object));
        TestableCommonHost host = new(serviceProvider);

        await host.RegisterActiveGracefulStopCapabilityForTestingAsync(completedRequestCapability.Object);
        await host.RequestGracefulSessionStopForTestingAsync();
        host.UnregisterActiveGracefulStopCapabilityForTesting(completedRequestCapability.Object);
        await host.RegisterActiveGracefulStopCapabilityForTestingAsync(nextRequestCapability.Object);

        completedRequestCapability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
        nextRequestCapability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Once);
        applicationCapability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false, 1)]
    [DataRow(true, 0)]
    public async Task ExecuteRequestAsync_WhenSessionIsCancelled_DisarmsStopPolicyOnlyForRun(bool isDiscoveryRequest, int expectedDisarmCount)
    {
        CancellationToken cancellationToken = new(canceled: true);

        CancellationToken? displayAfterToken = null;

        Mock<IPlatformOutputDevice> outputDeviceMock = new();
        outputDeviceMock.Setup(x => x.DisplayBeforeSessionStartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        outputDeviceMock
            .Setup(x => x.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(token => displayAfterToken = token)
            .Returns(Task.CompletedTask);

        Mock<ITestSessionLifetimeHandler> testSessionLifetimeHandlerMock = outputDeviceMock.As<ITestSessionLifetimeHandler>();
        testSessionLifetimeHandlerMock.Setup(x => x.OnTestSessionStartingAsync(It.IsAny<ITestSessionContext>())).Returns(Task.CompletedTask);
        testSessionLifetimeHandlerMock.Setup(x => x.OnTestSessionFinishingAsync(It.IsAny<ITestSessionContext>())).Returns(Task.CompletedTask);

        ProxyOutputDevice proxyOutputDevice = new(outputDeviceMock.Object, null);

        Mock<ITestSessionContext> sessionContextMock = new();
        sessionContextMock.SetupGet(x => x.SessionUid).Returns(new SessionUid("session"));
        sessionContextMock.SetupGet(x => x.CancellationToken).Returns(cancellationToken);

        Mock<ITestFrameworkInvoker> testFrameworkInvokerMock = new();
        testFrameworkInvokerMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ITestFramework>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellationToken));

        Mock<IStopPoliciesService> policiesServiceMock = new();

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(testFrameworkInvokerMock.Object);
        serviceProvider.AddService(new TestCoverageResult());
        serviceProvider.AddService(policiesServiceMock.Object);

        Mock<BaseMessageBus> baseMessageBusMock = new();
        baseMessageBusMock.Setup(x => x.DrainDataAsync()).Returns(Task.CompletedTask);

        Mock<ITestFramework> testFrameworkMock = new();
        ClientInfo client = new("client", "1.0.0");

        await TestableCommonHost.ExecuteRequestForTestingAsync(
            proxyOutputDevice,
            sessionContextMock.Object,
            serviceProvider,
            baseMessageBusMock.Object,
            testFrameworkMock.Object,
            client,
            isDiscoveryRequest);

        Assert.IsNotNull(displayAfterToken);
        Assert.IsFalse(displayAfterToken!.Value.CanBeCanceled);

        outputDeviceMock.Verify(x => x.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>()), Times.Once);
        testSessionLifetimeHandlerMock.Verify(x => x.OnTestSessionFinishingAsync(It.IsAny<ITestSessionContext>()), Times.Once);

        // Disarming happens in a finally around the invoker, so it must also happen when the invoker threw
        // because the session was canceled. Otherwise a deadline reached while the reporters finalize an
        // already-canceled run would still mark it as truncated.
        policiesServiceMock.Verify(x => x.NotifyTestExecutionCompleted(), Times.Exactly(expectedDisarmCount));
    }

    [TestMethod]
    public async Task ExecuteRequestAsync_WhenSessionStartupFails_DisarmsStopPolicy()
    {
        Mock<IPlatformOutputDevice> outputDeviceMock = new();
        outputDeviceMock
            .Setup(x => x.DisplayBeforeSessionStartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("session startup failure"));

        Mock<ITestSessionContext> sessionContextMock = new();
        sessionContextMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        Mock<IStopPoliciesService> policiesServiceMock = new();
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestCoverageResult());
        serviceProvider.AddService(policiesServiceMock.Object);

        Mock<BaseMessageBus> baseMessageBusMock = new();
        baseMessageBusMock.Setup(x => x.DisableAsync()).Returns(Task.CompletedTask);

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await TestableCommonHost.ExecuteRequestForTestingAsync(
                new ProxyOutputDevice(outputDeviceMock.Object, null),
                sessionContextMock.Object,
                serviceProvider,
                baseMessageBusMock.Object,
                new Mock<ITestFramework>().Object,
                new ClientInfo("client", "1.0.0")));

        Assert.AreEqual("session startup failure", ex.Message);
        policiesServiceMock.Verify(x => x.NotifyTestExecutionCompleted(), Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_WhenTestHostApplicationLifetimeIsAsyncCleanable_CleansUpOnce()
    {
        ServiceProvider serviceProvider = new();
        AsyncCleanableTestHostApplicationLifetime testApplicationLifetime = new();
        serviceProvider.AddService(new TestApplicationCancellationTokenSource());
        serviceProvider.AddService(testApplicationLifetime);

        TestableCommonHost host = new(serviceProvider, runTestApplicationLifeCycleCallbacks: true);

        int exitCode = await host.RunAsync();

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(1, testApplicationLifetime.BeforeRunCount);
        Assert.AreEqual(1, testApplicationLifetime.AfterRunCount);
        Assert.AreEqual(1, testApplicationLifetime.CleanupCount);
    }

    [TestMethod]
    public async Task RunAsync_WhenInternalRunSkipsService_DoesNotDisposeItDuringProcessShutdown()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestApplicationCancellationTokenSource());
        Mock<IDisposable> serviceToSkip = new();
        serviceProvider.AddService(serviceToSkip.Object);
        TestableCommonHost host = new(serviceProvider, serviceToSkip: serviceToSkip.Object);

        int exitCode = await host.RunAsync();

        Assert.AreEqual(0, exitCode);
        serviceToSkip.Verify(x => x.Dispose(), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteRequestAsync_WhenSessionIsCancelled_DisablesTheMessageBus()
    {
        CancellationToken cancellationToken = new(canceled: true);

        Mock<IPlatformOutputDevice> outputDeviceMock = new();
        outputDeviceMock.Setup(x => x.DisplayBeforeSessionStartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        outputDeviceMock.Setup(x => x.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        ProxyOutputDevice proxyOutputDevice = new(outputDeviceMock.Object, null);

        Mock<ITestSessionContext> sessionContextMock = new();
        sessionContextMock.SetupGet(x => x.SessionUid).Returns(new SessionUid("session"));
        sessionContextMock.SetupGet(x => x.CancellationToken).Returns(cancellationToken);

        Mock<ITestFrameworkInvoker> testFrameworkInvokerMock = new();
        testFrameworkInvokerMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ITestFramework>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellationToken));

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(testFrameworkInvokerMock.Object);
        serviceProvider.AddService(new TestCoverageResult());
        serviceProvider.AddService(new Mock<IStopPoliciesService>().Object);

        Mock<BaseMessageBus> baseMessageBusMock = new();
        baseMessageBusMock.Setup(x => x.DrainDataAsync()).Returns(Task.CompletedTask);
        baseMessageBusMock.Setup(x => x.DisableAsync()).Returns(Task.CompletedTask);

        await TestableCommonHost.ExecuteRequestForTestingAsync(
            proxyOutputDevice,
            sessionContextMock.Object,
            serviceProvider,
            baseMessageBusMock.Object,
            new Mock<ITestFramework>().Object,
            new ClientInfo("client", "1.0.0"));

        // The cancellation path skips NotifyTestSessionEndAsync, so without the teardown safety net the bus
        // would never be drained/disabled and consumers could still be running when they get disposed. The
        // absent drain is the mechanism, so pin it: the disable here comes purely from the safety net.
        baseMessageBusMock.Verify(x => x.DrainDataAsync(), Times.Never);
        baseMessageBusMock.Verify(x => x.DisableAsync(), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteRequestAsync_WhenDisablingTheMessageBusFails_DoesNotMaskTheSessionOutcome()
    {
        Mock<IPlatformOutputDevice> outputDeviceMock = new();
        outputDeviceMock.Setup(x => x.DisplayBeforeSessionStartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        outputDeviceMock.Setup(x => x.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        ProxyOutputDevice proxyOutputDevice = new(outputDeviceMock.Object, null);

        Mock<ITestSessionContext> sessionContextMock = new();
        sessionContextMock.SetupGet(x => x.SessionUid).Returns(new SessionUid("session"));
        sessionContextMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        Mock<ITestFrameworkInvoker> testFrameworkInvokerMock = new();
        testFrameworkInvokerMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ITestFramework>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test framework failure"));

        Mock<IStopPoliciesService> policiesServiceMock = new();

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(testFrameworkInvokerMock.Object);
        serviceProvider.AddService(new TestCoverageResult());
        serviceProvider.AddService(policiesServiceMock.Object);

        Mock<BaseMessageBus> baseMessageBusMock = new();
        baseMessageBusMock.Setup(x => x.DrainDataAsync()).Returns(Task.CompletedTask);
        baseMessageBusMock.Setup(x => x.DisableAsync()).ThrowsAsync(new InvalidOperationException("disable failure"));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await TestableCommonHost.ExecuteRequestForTestingAsync(
                proxyOutputDevice,
                sessionContextMock.Object,
                serviceProvider,
                baseMessageBusMock.Object,
                new Mock<ITestFramework>().Object,
                new ClientInfo("client", "1.0.0")));

        // The safety net is best effort: it must never replace the exception that is already propagating.
        Assert.AreEqual("test framework failure", ex.Message);
        baseMessageBusMock.Verify(x => x.DisableAsync(), Times.Once);

        // Disarming sits in a finally around the invoker, so a failing test framework must not leave the
        // deadline armed while the session tears down.
        policiesServiceMock.Verify(x => x.NotifyTestExecutionCompleted(), Times.Once);
    }

    [TestMethod]
    public async Task DisposeServiceProviderAsync_WhenDataConsumerIsAlsoRegisteredAsService_DisposesOnce()
    {
        Mock<IDataConsumer> dataConsumer = new();
        Mock<IDisposable> disposableDataConsumer = dataConsumer.As<IDisposable>();
        Mock<BaseMessageBus> messageBus = new();
        messageBus.SetupGet(x => x.DataConsumerServices).Returns([dataConsumer.Object]);

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(messageBus.Object);
        serviceProvider.AddService(dataConsumer.Object);

        await TestableCommonHost.DisposeServiceProviderForTestingAsync(serviceProvider);

        disposableDataConsumer.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task DisposeServiceProviderAsync_DisablesTheMessageBusBeforeDisposingIt()
    {
        List<string> calls = [];

        Mock<IDataConsumer> dataConsumer = new();
        dataConsumer.As<IDisposable>().Setup(x => x.Dispose()).Callback(() => calls.Add("consumerDispose"));

        Mock<BaseMessageBus> messageBus = new();
        messageBus.SetupGet(x => x.DataConsumerServices).Returns([dataConsumer.Object]);
        messageBus.Setup(x => x.DisableAsync()).Callback(() => calls.Add("disable")).Returns(Task.CompletedTask);
        messageBus.Setup(x => x.Dispose()).Callback(() => calls.Add("busDispose"));

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(messageBus.Object);

        await TestableCommonHost.DisposeServiceProviderForTestingAsync(serviceProvider);

        // Disabling awaits the consumer loops, so it has to happen before the bus and its consumers are
        // disposed. Otherwise a consumer can be disposed while its ConsumeAsync is still executing.
        Assert.AreSequenceEqual(["disable", "busDispose", "consumerDispose"], calls);
    }

    [TestMethod]
    public async Task DisposeServiceProviderAsync_WhenMessageBusIsAlreadyAbandoned_DoesNotDisableItAgain()
    {
        Mock<BaseMessageBus> messageBus = new();
        messageBus.Setup(x => x.DisableAsync()).ThrowsAsync(new InvalidOperationException("disable retried"));

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(messageBus.Object);

        await TestableCommonHost.DisposeServiceProviderForTestingAsync(serviceProvider, [messageBus.Object]);

        messageBus.Verify(x => x.DisableAsync(), Times.Never);
    }

    [TestMethod]
    public async Task DisposeServiceProviderAsync_WhenConsumerIsStillRunning_DoesNotDisposeIt()
    {
        Mock<IDataConsumer> runningConsumer = new();
        runningConsumer.SetupGet(x => x.Uid).Returns("running");
        Mock<IDisposable> disposableRunningConsumer = runningConsumer.As<IDisposable>();

        Mock<IDataConsumer> finishedConsumer = new();
        finishedConsumer.SetupGet(x => x.Uid).Returns("finished");
        Mock<IDisposable> disposableFinishedConsumer = finishedConsumer.As<IDisposable>();

        Mock<BaseMessageBus> messageBus = new();
        messageBus.Setup(x => x.DisableAsync()).Returns(Task.CompletedTask);
        messageBus.SetupGet(x => x.DataConsumerServices).Returns([runningConsumer.Object, finishedConsumer.Object]);
        messageBus.SetupGet(x => x.ConsumersStillRunning).Returns([runningConsumer.Object]);

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(messageBus.Object);

        // A data consumer is normally registered as a plain service too, and the bus is registered first. If
        // the skip were not recorded, the consumer would simply be disposed later in the same pass.
        serviceProvider.AddService(runningConsumer.Object);
        serviceProvider.AddService(finishedConsumer.Object);

        await TestableCommonHost.DisposeServiceProviderForTestingAsync(serviceProvider);

        // The handshake could not complete for this one (a consumer that ignored the cancellation token during
        // an abort). Disposing it now is exactly the race we are fixing, so it has to be left alone.
        disposableRunningConsumer.Verify(x => x.Dispose(), Times.Never);
        disposableFinishedConsumer.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task DisposeServiceProviderAsync_WhenCalledTwice_StillDoesNotDisposeAStillRunningConsumer()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        StuckConsumer consumer = new();

        // Keep the shutdown budget short so the test does not have to wait for the 30s default.
        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("0.5");

        MessageBusProxy proxy = new();
        AsynchronousMessageBus bus = new(
            [consumer],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            environmentMock.Object);
        await bus.InitAsync();
        proxy.SetBuiltMessageBus(bus);

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(proxy);

        // A data consumer is normally registered as a plain service too, and the bus is registered first.
        serviceProvider.AddService(consumer);

        await proxy.PublishAsync(new StuckProducer(), new StuckData());
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        cancellationTokenSource.Cancel();

        // Hosts walk the provider more than once (ConsoleTestHost disposes it, then CommonHost.RunAsync's
        // finally does it again) and each pass starts with a fresh "already disposed" list. The first pass
        // disposes the bus, which drops its processors, so without a latched verdict the second pass would no
        // longer know the consumer is unsafe and would dispose it mid-consumption.
        await TestableCommonHost.DisposeServiceProviderForTestingAsync(serviceProvider);
        await TestableCommonHost.DisposeServiceProviderForTestingAsync(serviceProvider);

        Assert.AreEqual(0, consumer.DisposeCount);

        consumer.AllowConsumeToComplete.SetResult(true);
    }

    private sealed class NopLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NopLogger();
    }

    private sealed class StuckData : IData
    {
        public string DisplayName => nameof(StuckData);

        public string? Description => nameof(StuckData);
    }

    private sealed class StuckProducer : IDataProducer
    {
        public Type[] DataTypesProduced => [typeof(StuckData)];

        public string Uid => nameof(StuckProducer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    /// <summary>
    /// A consumer that never observes the cancellation token, so it is still inside
    /// <see cref="ConsumeAsync"/> when the shutdown budget of the aborted run runs out.
    /// </summary>
    private sealed class StuckConsumer : IDataConsumer, IDisposable
    {
        private int _disposeCount;

        public TaskCompletionSource<bool> ConsumeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowConsumeToComplete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Type[] DataTypesConsumed => [typeof(StuckData)];

        public string Uid => nameof(StuckConsumer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            ConsumeStarted.TrySetResult(true);
            await AllowConsumeToComplete.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    [TestMethod]
    public async Task DisposeServiceProviderAsync_WhenConsumerIsRegisteredBeforeTheBus_StillDoesNotDisposeIt()
    {
        Mock<IDataConsumer> runningConsumer = new();
        runningConsumer.SetupGet(x => x.Uid).Returns("running");
        Mock<IDisposable> disposableRunningConsumer = runningConsumer.As<IDisposable>();

        Mock<BaseMessageBus> messageBus = new();
        messageBus.Setup(x => x.DisableAsync()).Returns(Task.CompletedTask);
        messageBus.SetupGet(x => x.DataConsumerServices).Returns([runningConsumer.Object]);
        messageBus.SetupGet(x => x.ConsumersStillRunning).Returns([runningConsumer.Object]);

        ServiceProvider serviceProvider = new();

        // The output device and the coverage accumulator are registered well before BuildTestFrameworkAsync
        // adds the bus, so a consumer can sit earlier in Services than the bus that owns it. Disabling only
        // when the disposal loop reached the bus would already have disposed this one.
        serviceProvider.AddService(runningConsumer.Object);
        serviceProvider.AddService(messageBus.Object);

        await TestableCommonHost.DisposeServiceProviderForTestingAsync(serviceProvider);

        disposableRunningConsumer.Verify(x => x.Dispose(), Times.Never);
    }

    private sealed class TestableCommonHost(
        ServiceProvider serviceProvider,
        bool runTestApplicationLifeCycleCallbacks = false,
        object? serviceToSkip = null) : CommonHost(serviceProvider)
    {
        protected override string HostType => "TestHost";

        protected override bool RunTestApplicationLifeCycleCallbacks => runTestApplicationLifeCycleCallbacks;

        public static Task DisposeServiceProviderForTestingAsync(ServiceProvider serviceProvider)
            => DisposeServiceProviderAsync(serviceProvider);

        public static Task DisposeServiceProviderForTestingAsync(ServiceProvider serviceProvider, List<object> alreadyDisposed)
            => DisposeServiceProviderAsync(serviceProvider, alreadyDisposed: alreadyDisposed);

        public static Task ExecuteRequestForTestingAsync(
            ProxyOutputDevice outputDevice,
            ITestSessionContext testSessionInfo,
            ServiceProvider serviceProvider,
            BaseMessageBus baseMessageBus,
            ITestFramework testFramework,
            ClientInfo client,
            bool isDiscoveryRequest = false)
            => ExecuteRequestAsync(outputDevice, testSessionInfo, serviceProvider, baseMessageBus, testFramework, client, isDiscoveryRequest);

        public Task RegisterActiveGracefulStopCapabilityForTestingAsync(IGracefulStopTestExecutionCapability capability)
            => RegisterActiveGracefulStopCapabilityAsync(capability);

        public Task RequestGracefulSessionStopForTestingAsync()
            => RequestGracefulSessionStopAsync(CancellationToken.None);

        public void UnregisterActiveGracefulStopCapabilityForTesting(IGracefulStopTestExecutionCapability capability)
            => UnregisterActiveGracefulStopCapability(capability);

        protected override Task<int> InternalRunAsync(CancellationToken cancellationToken, List<object> alreadyDisposed)
        {
            if (serviceToSkip is not null)
            {
                alreadyDisposed.Add(serviceToSkip);
            }

            return Task.FromResult(0);
        }
    }

    private sealed class TestApplicationCancellationTokenSource : ITestApplicationCancellationTokenSource
    {
        public CancellationToken CancellationToken => CancellationToken.None;

        public void Cancel()
        {
        }
    }

    private sealed class AsyncCleanableTestHostApplicationLifetime : ITestHostApplicationLifetime, IAsyncCleanableExtension
    {
        public int BeforeRunCount { get; private set; }

        public int AfterRunCount { get; private set; }

        public int CleanupCount { get; private set; }

        public string Uid => nameof(AsyncCleanableTestHostApplicationLifetime);

        public string Version => "1.0.0";

        public string DisplayName => nameof(AsyncCleanableTestHostApplicationLifetime);

        public string Description => nameof(AsyncCleanableTestHostApplicationLifetime);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task BeforeRunAsync(CancellationToken cancellationToken)
        {
            BeforeRunCount++;
            return Task.CompletedTask;
        }

        public Task AfterRunAsync(int exitCode, CancellationToken cancellationToken)
        {
            AfterRunCount++;
            return Task.CompletedTask;
        }

        public Task CleanupAsync()
        {
            CleanupCount++;
            return Task.CompletedTask;
        }
    }
}
