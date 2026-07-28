// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Hosts;
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
    public async Task ExecuteRequestAsync_WhenSessionIsCancelled_UsesCancellationTokenNoneForDisplayAfterSessionEndRun()
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

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(testFrameworkInvokerMock.Object);
        serviceProvider.AddService(new TestCoverageResult());

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
            client);

        Assert.IsNotNull(displayAfterToken);
        Assert.IsFalse(displayAfterToken!.Value.CanBeCanceled);

        outputDeviceMock.Verify(x => x.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>()), Times.Once);
        testSessionLifetimeHandlerMock.Verify(x => x.OnTestSessionFinishingAsync(It.IsAny<ITestSessionContext>()), Times.Once);
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

    private sealed class TestableCommonHost(ServiceProvider serviceProvider, bool runTestApplicationLifeCycleCallbacks = false) : CommonHost(serviceProvider)
    {
        protected override string HostType => "TestHost";

        protected override bool RunTestApplicationLifeCycleCallbacks => runTestApplicationLifeCycleCallbacks;

        public static Task DisposeServiceProviderForTestingAsync(ServiceProvider serviceProvider)
            => DisposeServiceProviderAsync(serviceProvider);

        public static Task ExecuteRequestForTestingAsync(
            ProxyOutputDevice outputDevice,
            ITestSessionContext testSessionInfo,
            ServiceProvider serviceProvider,
            BaseMessageBus baseMessageBus,
            ITestFramework testFramework,
            ClientInfo client)
            => ExecuteRequestAsync(outputDevice, testSessionInfo, serviceProvider, baseMessageBus, testFramework, client);

        protected override Task<int> InternalRunAsync(CancellationToken cancellationToken)
            => Task.FromResult(0);
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
