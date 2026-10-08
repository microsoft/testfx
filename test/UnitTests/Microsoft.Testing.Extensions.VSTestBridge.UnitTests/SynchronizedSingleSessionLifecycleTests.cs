// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests;

[TestClass]
public sealed class SynchronizedSingleSessionLifecycleTests
{
    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task CreateTestSessionAsync_CanceledCreation_DoesNotReserveSession()
    {
        var fixture = new BridgeTestFixture();
        using var framework = new SessionFramework(fixture);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.ThrowsExactly<OperationCanceledException>(() =>
            framework.CreateTestSessionAsync(new CreateTestSessionContext(new SessionUid("canceled"), cancellation.Token)));
        CreateTestSessionResult created = await framework.CreateTestSessionAsync(
            new CreateTestSessionContext(fixture.Session.SessionUid, CancellationToken.None));
        CloseTestSessionResult closed = await CloseSessionAsync(framework, fixture.Session.SessionUid);

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsTrue(created.IsSuccess);
        Assert.IsTrue(closed.IsSuccess);
    }

    [TestMethod]
    public async Task CreateTestSessionAsync_SecondSession_IsRejectedWithoutReplacingOriginal()
    {
        var fixture = new BridgeTestFixture();
        using var framework = new SessionFramework(fixture);
        await framework.CreateTestSessionAsync(new CreateTestSessionContext(fixture.Session.SessionUid, CancellationToken.None));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            framework.CreateTestSessionAsync(new CreateTestSessionContext(new SessionUid("second"), CancellationToken.None)));

        Assert.Contains(fixture.Session.SessionUid.Value, exception.Message);
        Assert.IsTrue((await CloseSessionAsync(framework, fixture.Session.SessionUid)).IsSuccess);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IsEnabledAsync_DelegatesToUnderlyingExtension(bool enabled)
    {
        var fixture = new BridgeTestFixture();
        var extension = new Mock<IExtension>();
        extension.Setup(x => x.IsEnabledAsync()).ReturnsAsync(enabled);
        using var framework = new SessionFramework(fixture, extension.Object);

        Assert.AreEqual(enabled, await framework.IsEnabledAsync());

        extension.Verify(x => x.IsEnabledAsync(), Times.Once);
    }

    [TestMethod]
    public async Task CloseTestSessionAsync_TwoPendingRequests_WaitsForBothBeforeClosing()
    {
        var fixture = new BridgeTestFixture();
        using var framework = new SessionFramework(fixture);
        await framework.CreateTestSessionAsync(new CreateTestSessionContext(fixture.Session.SessionUid, CancellationToken.None));
        var runStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRun = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDiscovery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        framework.OnRun = (_, _, _) =>
        {
            runStarted.SetResult(true);
            return releaseRun.Task;
        };
        framework.OnDiscover = (_, _, _) =>
        {
            discoveryStarted.SetResult(true);
            return releaseDiscovery.Task;
        };
        Task run = framework.RunDirectAsync(CreateRunRequest(fixture), fixture.MessageBus.Object, CancellationToken.None);
        Task discovery = framework.DiscoverDirectAsync(CreateDiscoverRequest(fixture), fixture.MessageBus.Object, CancellationToken.None);
        Task<CloseTestSessionResult>? close = null;
        using var closeCancellation = new CancellationTokenSource();
        Exception? testFailure = null;
        try
        {
            await VSTestBridgedTestFrameworkBaseTests.AwaitWithinAsync(Task.WhenAll(runStarted.Task, discoveryStarted.Task), RendezvousTimeout);
            close = framework.CloseTestSessionAsync(new CloseTestSessionContext(fixture.Session.SessionUid, closeCancellation.Token));
            Assert.IsFalse(close.IsCompleted);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                framework.CreateTestSessionAsync(new CreateTestSessionContext(new SessionUid("too-early"), CancellationToken.None)));

            releaseRun.SetResult(true);
            await VSTestBridgedTestFrameworkBaseTests.AwaitWithinAsync(run, RendezvousTimeout);
            Assert.IsFalse(close.IsCompleted);

            releaseDiscovery.SetResult(true);
            await VSTestBridgedTestFrameworkBaseTests.AwaitWithinAsync(Task.WhenAll(discovery, close), RendezvousTimeout);
            Assert.IsTrue((await close).IsSuccess);
        }
        catch (Exception exception)
        {
            testFailure = exception;
            throw;
        }
        finally
        {
            releaseRun.TrySetResult(true);
            releaseDiscovery.TrySetResult(true);
            if (testFailure is not null)
            {
                closeCancellation.Cancel();
            }

            try
            {
                await VSTestBridgedTestFrameworkBaseTests.AwaitWithinAsync(
                    close is null ? Task.WhenAll(run, discovery) : Task.WhenAll(run, discovery, close), CleanupTimeout);
            }
            catch (OperationCanceledException) when (testFailure is not null && closeCancellation.IsCancellationRequested)
            {
                // The close wait was canceled for failure cleanup; WhenAll still joined both request tasks.
            }
            catch (Exception cleanupFailure) when (testFailure is not null)
            {
                VSTestBridgedTestFrameworkBaseTests.ReportCleanupFailure(testFailure, cleanupFailure);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RequestGuard_WhenHandlerFails_ReleasesCountSoSessionCanClose(bool discovery)
    {
        var fixture = new BridgeTestFixture();
        using var framework = new SessionFramework(fixture);
        await framework.CreateTestSessionAsync(new CreateTestSessionContext(fixture.Session.SessionUid, CancellationToken.None));
        var failure = new InvalidOperationException("adapter failure");
        framework.OnRun = (_, _, _) => Task.FromException(failure);
        framework.OnDiscover = (_, _, _) => Task.FromException(failure);

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => discovery
            ? framework.DiscoverDirectAsync(CreateDiscoverRequest(fixture), fixture.MessageBus.Object, CancellationToken.None)
            : framework.RunDirectAsync(CreateRunRequest(fixture), fixture.MessageBus.Object, CancellationToken.None));
        CloseTestSessionResult close = await CloseSessionAsync(framework, fixture.Session.SessionUid);

        Assert.AreSame(failure, actual);
        Assert.IsTrue(close.IsSuccess);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RequestGuard_WhenHandlerCancels_ReleasesCountSoSessionCanClose(bool discovery)
    {
        var fixture = new BridgeTestFixture();
        using var framework = new SessionFramework(fixture);
        using var cancellation = new CancellationTokenSource();
        await framework.CreateTestSessionAsync(new CreateTestSessionContext(fixture.Session.SessionUid, CancellationToken.None));
        cancellation.Cancel();
        framework.OnRun = (_, _, token) => Task.FromCanceled(token);
        framework.OnDiscover = (_, _, token) => Task.FromCanceled(token);

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => discovery
            ? framework.DiscoverDirectAsync(CreateDiscoverRequest(fixture), fixture.MessageBus.Object, cancellation.Token)
            : framework.RunDirectAsync(CreateRunRequest(fixture), fixture.MessageBus.Object, cancellation.Token));
        CloseTestSessionResult close = await CloseSessionAsync(framework, fixture.Session.SessionUid);

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsTrue(close.IsSuccess);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteRequestAsync_DiscoveryOrRun_ConvertsAndDispatchesCorrectRequest(bool discovery)
    {
        var fixture = new BridgeTestFixture();
        using var framework = new SessionFramework(fixture);
        await framework.CreateTestSessionAsync(new CreateTestSessionContext(fixture.Session.SessionUid, CancellationToken.None));
        var filter = new TestNodeUidListFilter([new TestNodeUid("Sample.Tests.Test")]);
        using var cancellation = new CancellationTokenSource();
        TestExecutionRequest request = discovery
            ? new DiscoverTestExecutionRequest(fixture.Session, filter)
            : new RunTestExecutionRequest(fixture.Session, filter);
        TestExecutionRequest? captured = null;
        IMessageBus? capturedBus = null;
        CancellationToken capturedToken = default;
        framework.OnRun = (converted, bus, token) =>
        {
            captured = converted;
            capturedBus = bus;
            capturedToken = token;
            return Task.CompletedTask;
        };
        framework.OnDiscover = (converted, bus, token) =>
        {
            captured = converted;
            capturedBus = bus;
            capturedToken = token;
            return Task.CompletedTask;
        };

        await framework.DispatchAsync(request, fixture.MessageBus.Object, cancellation.Token);

        Assert.IsNotNull(captured);
        Assert.AreSame(fixture.Session, captured.Session);
        Assert.AreSame(filter, captured.Filter);
        Assert.AreSame(fixture.MessageBus.Object, capturedBus);
        Assert.AreEqual(cancellation.Token, capturedToken);
        string[] paths = discovery
            ? ((VSTestDiscoverTestExecutionRequest)captured).AssemblyPaths
            : ((VSTestRunTestExecutionRequest)captured).AssemblyPaths;
        Assert.AreSequenceEqual(new[] { typeof(SynchronizedSingleSessionLifecycleTests).Assembly.Location }, paths);
        Assert.IsTrue((await CloseSessionAsync(framework, fixture.Session.SessionUid)).IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteRequestAsync_UnsupportedExecutionRequest_ThrowsAndReleasesGuard()
    {
        var fixture = new BridgeTestFixture();
        using var framework = new SessionFramework(fixture);
        await framework.CreateTestSessionAsync(new CreateTestSessionContext(fixture.Session.SessionUid, CancellationToken.None));
        var request = new UnsupportedExecutionRequest(fixture.Session);

        NotSupportedException exception = await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            framework.DispatchAsync(request, fixture.MessageBus.Object, CancellationToken.None));
        CloseTestSessionResult close = await CloseSessionAsync(framework, fixture.Session.SessionUid);

        Assert.AreEqual($"VSTest Test Adapters do not support requests of type '{request.GetType()}'.", exception.Message);
        Assert.IsTrue(close.IsSuccess);
    }

    private static async Task<CloseTestSessionResult> CloseSessionAsync(SessionFramework framework, SessionUid sessionUid)
    {
        using var cancellation = new CancellationTokenSource();
        Task<CloseTestSessionResult> close = framework.CloseTestSessionAsync(new CloseTestSessionContext(sessionUid, cancellation.Token));
        Exception? testFailure = null;
        try
        {
            await VSTestBridgedTestFrameworkBaseTests.AwaitWithinAsync(close, RendezvousTimeout);
            return await close;
        }
        catch (Exception exception)
        {
            testFailure = exception;
            throw;
        }
        finally
        {
            if (!close.IsCompleted)
            {
                cancellation.Cancel();
                try
                {
                    await VSTestBridgedTestFrameworkBaseTests.AwaitWithinAsync(close, CleanupTimeout);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
                catch (Exception cleanupFailure) when (testFailure is not null)
                {
                    VSTestBridgedTestFrameworkBaseTests.ReportCleanupFailure(testFailure, cleanupFailure);
                }
            }
        }
    }

    private static VSTestRunTestExecutionRequest CreateRunRequest(BridgeTestFixture fixture)
        => new(fixture.Session, new NopFilter(), ["tests.dll"], Mock.Of<IRunContext>(), Mock.Of<IFrameworkHandle>());

    private static VSTestDiscoverTestExecutionRequest CreateDiscoverRequest(BridgeTestFixture fixture)
        => new(fixture.Session, new NopFilter(), ["tests.dll"], Mock.Of<IDiscoveryContext>(),
            Mock.Of<IMessageLogger>(), Mock.Of<ITestCaseDiscoverySink>());

    private sealed class UnsupportedExecutionRequest(TestSessionContext session) : TestExecutionRequest(session, new NopFilter());

    private sealed class SessionFramework(BridgeTestFixture fixture, IExtension? extension = null)
        : SynchronizedSingleSessionVSTestBridgedTestFramework(
            extension ?? new TestExtension(),
            static () => new Assembly[] { typeof(SynchronizedSingleSessionLifecycleTests).Assembly },
            fixture.Framework.ServiceProvider,
            new TestFrameworkCapabilities())
    {
        public Func<VSTestRunTestExecutionRequest, IMessageBus, CancellationToken, Task> OnRun { get; set; } = static (_, _, _) => Task.CompletedTask;

        public Func<VSTestDiscoverTestExecutionRequest, IMessageBus, CancellationToken, Task> OnDiscover { get; set; } = static (_, _, _) => Task.CompletedTask;

        public Task RunDirectAsync(VSTestRunTestExecutionRequest request, IMessageBus bus, CancellationToken token)
            => RunTestsAsync(request, bus, token);

        public Task DiscoverDirectAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus bus, CancellationToken token)
            => DiscoverTestsAsync(request, bus, token);

        public Task DispatchAsync(TestExecutionRequest request, IMessageBus bus, CancellationToken token)
            => ExecuteRequestAsync(request, bus, token);

        protected override Task SynchronizedRunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
            => OnRun(request, messageBus, cancellationToken);

        protected override Task SynchronizedDiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
            => OnDiscover(request, messageBus, cancellationToken);
    }
}
