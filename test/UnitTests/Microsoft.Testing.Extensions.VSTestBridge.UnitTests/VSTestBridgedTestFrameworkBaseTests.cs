// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests;

[TestClass]
public sealed class VSTestBridgedTestFrameworkBaseTests
{
    private const string AttachDebuggerEnvironmentVariable = "TESTINGPLATFORM_VSTESTBRIDGE_ATTACH_DEBUGGER";
    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void Constructor_NullServiceProvider_RejectsInvalidDependency()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() =>
            new BridgeTestFixture.StubFramework(null!, new TestFrameworkCapabilities()));

        Assert.AreEqual("serviceProvider", exception.ParamName);
    }

    [TestMethod]
    public void IsTrxEnabled_InternalCapability_IsEvaluatedLazilyAndCached()
    {
        var capability = new VSTestBridgeExtensionBaseCapabilities();
        var fixture = new BridgeTestFixture(false, capability);

        ((ITrxReportCapability)capability).Enable();

        Assert.IsTrue(fixture.Framework.IsTrxEnabled);
        Assert.IsTrue(fixture.Framework.IsTrxEnabled);
    }

    [TestMethod]
    public void IsTrxEnabled_InternalCapabilityBeforeEnable_RemainsCachedAsDisabled()
    {
        var capability = new VSTestBridgeExtensionBaseCapabilities();
        var fixture = new BridgeTestFixture(false, capability);
        Assert.IsFalse(fixture.Framework.IsTrxEnabled);

        ((ITrxReportCapability)capability).Enable();

        Assert.IsFalse(fixture.Framework.IsTrxEnabled);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IsTrxEnabled_PublicCapability_UsesSupportAndReadsItOnce(bool supported)
    {
        var capability = new Mock<ITrxReportCapability>();
        capability.SetupGet(x => x.IsSupported).Returns(supported);
        var fixture = new BridgeTestFixture(false, capability.Object);
        capability.VerifyGet(x => x.IsSupported, Times.Never);

        Assert.AreEqual(supported, fixture.Framework.IsTrxEnabled);
        Assert.AreEqual(supported, fixture.Framework.IsTrxEnabled);

        capability.VerifyGet(x => x.IsSupported, Times.Once);
        capability.Verify(x => x.Enable(), Times.Never);
        Assert.IsFalse(new BridgeTestFixture().Framework.IsTrxEnabled);
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public async Task ExecuteRequestAsync_UnknownRequest_CompletesWithoutDispatch()
    {
        string? originalValue = Environment.GetEnvironmentVariable(AttachDebuggerEnvironmentVariable);
        Environment.SetEnvironmentVariable(AttachDebuggerEnvironmentVariable, null);
        try
        {
            var fixture = new BridgeTestFixture();
            var completion = new Mock<IExecuteRequestCompletionNotifier>(MockBehavior.Strict);
            completion.Setup(x => x.Complete());
            fixture.Framework.OnExecute = static (_, _, _) => throw new AssertFailedException("Unknown requests must not be dispatched.");
            var context = new ExecuteRequestContext(
                Mock.Of<IRequest>(), fixture.MessageBus.Object, completion.Object, CancellationToken.None);

            await fixture.Framework.ExecuteRequestAsync(context);

            completion.Verify(x => x.Complete(), Times.Once);
            Assert.IsEmpty(fixture.PublishedMessages);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AttachDebuggerEnvironmentVariable, originalValue);
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public async Task ExecuteRequestAsync_PendingExecution_ForwardsContextAndCompletesOnlyAfterExecution()
    {
        string? originalValue = Environment.GetEnvironmentVariable(AttachDebuggerEnvironmentVariable);
        Environment.SetEnvironmentVariable(AttachDebuggerEnvironmentVariable, null);
        try
        {
            var fixture = new BridgeTestFixture();
            var completion = new Mock<IExecuteRequestCompletionNotifier>();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource();
            var request = new RunTestExecutionRequest(fixture.Session);
            fixture.Framework.OnExecute = (actualRequest, actualBus, actualToken) =>
            {
                Assert.AreSame(request, actualRequest);
                Assert.AreSame(fixture.MessageBus.Object, actualBus);
                Assert.AreEqual(cancellation.Token, actualToken);
                started.SetResult(true);
                return release.Task;
            };
            var context = new ExecuteRequestContext(
                request, fixture.MessageBus.Object, completion.Object, cancellation.Token);
            Task execution = fixture.Framework.ExecuteRequestAsync(context);
            Exception? testFailure = null;
            try
            {
                await AwaitWithinAsync(started.Task, RendezvousTimeout);
                completion.Verify(x => x.Complete(), Times.Never);
                Assert.IsFalse(execution.IsCompleted);

                release.SetResult(true);
                await AwaitWithinAsync(execution, RendezvousTimeout);

                completion.Verify(x => x.Complete(), Times.Once);
            }
            catch (Exception exception)
            {
                testFailure = exception;
                throw;
            }
            finally
            {
                release.TrySetResult(true);
                try
                {
                    await AwaitWithinAsync(execution, CleanupTimeout);
                }
                catch (Exception cleanupFailure) when (testFailure is not null)
                {
                    ReportCleanupFailure(testFailure, cleanupFailure);
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(AttachDebuggerEnvironmentVariable, originalValue);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public async Task ExecuteRequestAsync_FailedOrCanceledExecution_CompletesAndPreservesFailure(bool canceled)
    {
        string? originalValue = Environment.GetEnvironmentVariable(AttachDebuggerEnvironmentVariable);
        Environment.SetEnvironmentVariable(AttachDebuggerEnvironmentVariable, null);
        try
        {
            var fixture = new BridgeTestFixture();
            var completion = new Mock<IExecuteRequestCompletionNotifier>();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Exception expected = canceled
                ? new OperationCanceledException(cancellation.Token)
                : new InvalidOperationException("adapter execution failure");
            fixture.Framework.OnExecute = (_, _, _) => Task.FromException(expected);
            var context = new ExecuteRequestContext(
                new RunTestExecutionRequest(fixture.Session), fixture.MessageBus.Object, completion.Object, cancellation.Token);

            Exception actual = canceled
                ? await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fixture.Framework.ExecuteRequestAsync(context))
                : await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Framework.ExecuteRequestAsync(context));

            Assert.AreSame(expected, actual);
            completion.Verify(x => x.Complete(), Times.Once);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AttachDebuggerEnvironmentVariable, originalValue);
        }
    }

    internal static async Task AwaitWithinAsync(Task task, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource();
        var deadline = Task.Delay(timeout, cancellation.Token);
        try
        {
            Assert.AreSame(task, await Task.WhenAny(task, deadline), "The operation did not terminate within its bound.");
            await task;
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    internal static void ReportCleanupFailure(Exception testFailure, Exception cleanupFailure)
        => throw new AggregateException("Test execution and cleanup both failed.", testFailure, cleanupFailure);
}
