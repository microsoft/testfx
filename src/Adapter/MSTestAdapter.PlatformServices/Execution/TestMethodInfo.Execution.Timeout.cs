// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    /// <summary>
    /// Execute test with a timeout.
    /// </summary>
    /// <param name="arguments">The arguments to be passed.</param>
    /// <returns>The result of execution.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<TestResult> ExecuteInternalWithTimeoutAsync(object?[]? arguments)
    {
        DebugEx.Assert(IsTimeoutSet, "Timeout should be set");

        if (TimeoutInfo.CooperativeCancellation)
        {
            async SynchronizationContextPreservingTask<TestResult> ExecuteWithTimeoutTokenAsync(CancellationTokenSource timeoutTokenSource)
                => await ExecuteInternalAsync(arguments, timeoutTokenSource).ConfigureAwait(false);

            return await CancellationTimeoutHelper.RunWithCooperativeCancellationAsync<TestResult>(
                ExecuteWithTimeoutTokenAsync,
                TestContext.Context.CancellationTokenSource,
                TimeoutInfo.Timeout,
                isTimeout => new()
                {
                    Outcome = UnitTestOutcome.Timeout,
                    TestFailureException = new TestFailedException(
                        UnitTestOutcome.Timeout,
                        isTimeout
                            ? string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout)
                            : string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName)),
                }).ConfigureAwait(false);
        }

        TestResult? result = null;
        Exception? failure = null;
        var invocationState = new TestInvocationState();
        CancellationTokenSource cancellationTokenSource = TestContext.Context.CancellationTokenSource;
        TaskScheduler? synchronizationContextScheduler = SynchronizationContext.Current is null
            ? null
            : TaskScheduler.FromCurrentSynchronizationContext();
        bool executionCompleted;

        if (synchronizationContextScheduler is null)
        {
            executionCompleted = PlatformServiceProvider.Instance.ThreadOperations.Execute(
                ExecuteAsyncAction, TimeoutInfo.Timeout, cancellationTokenSource.Token);
        }
        else
        {
            // Queue the invocation on its original dispatcher without blocking the message pump.
            // A new STA thread cannot access controls owned by this UI thread.
            Task executionTask = Task.Factory.StartNew(
                ExecuteOnSynchronizationContextAsync,
                cancellationTokenSource.Token,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler,
                synchronizationContextScheduler).Unwrap();
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            var timeoutTask = Task.Delay(TimeoutInfo.Timeout, timeoutCancellation.Token);
            Task completedTask = await Task.WhenAny(executionTask, timeoutTask).ConfigureAwait(false);
            executionCompleted = completedTask == executionTask && !executionTask.IsCanceled;
#pragma warning disable VSTHRD103 // CancelAsync is not available on all supported target frameworks.
            timeoutCancellation.Cancel();
#pragma warning restore VSTHRD103 // CancelAsync is not available on all supported target frameworks.
        }

        if (executionCompleted)
        {
            if (failure != null)
            {
                throw failure;
            }

            DebugEx.Assert(result is not null, "result is not null");
            return result;
        }

        // Timed out or canceled
        string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout);
        if (cancellationTokenSource.IsCancellationRequested)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName);
        }
        else
        {
            // Cancel the token source as test has timed out
#pragma warning disable VSTHRD103 // Call async methods when in an async method - likely fine in this context. CancelAsync is .NET Core only. We prefer having the same behavior between .NET Core and .NET Framework.
            cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        }

        TestResult timeoutResult = new() { Outcome = UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UnitTestOutcome.Timeout, errorMessage) };

        // Execution context propagation here may still not be accurate.
        // if test init was successfully executed by ExecuteAsyncAction, but then the test itself timed out or cancelled,
        // then at this point we will run the cleanup on an execution context that doesn't have any state set by the test initialize.
        if (synchronizationContextScheduler is null)
        {
            await RunTestCleanupMethodAsync(timeoutResult, null, invocationState).ConfigureAwait(false);
        }
        else if (!invocationState.IsCleanupStarted
            && invocationState.TryGetCleanupSnapshot(out TestInvocationCleanupSnapshot snapshot))
        {
            if (!HasCleanupsToInvoke(snapshot.ClassInstanceLease))
            {
                // Cancellation bookkeeping does not need a dispatcher blocked by synchronous user code.
                await RunTestCleanupMethodAsync(timeoutResult, null, invocationState).ConfigureAwait(false);
            }
            else
            {
                const int CleanupDispatchTimeoutMilliseconds = 1_000;
                int cleanupDispatchClaimed = 0;
                Task cleanupTask = Task.Factory.StartNew(
                    async () =>
                    {
                        // Either the dispatcher starts cleanup, or the monitor leaves it to the original invocation.
                        if (Interlocked.CompareExchange(ref cleanupDispatchClaimed, 1, 0) == 0)
                        {
                            await RunTestCleanupMethodAsync(timeoutResult, null, invocationState).ConfigureAwait(false);
                        }
                    },
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler,
                    synchronizationContextScheduler).Unwrap();
                using var dispatchTimeoutCancellation = new CancellationTokenSource();
                await Task.WhenAny(
                    cleanupTask,
                    Task.Delay(CleanupDispatchTimeoutMilliseconds, dispatchTimeoutCancellation.Token)).ConfigureAwait(false);
#pragma warning disable VSTHRD103 // CancelAsync is not available on all supported target frameworks.
                dispatchTimeoutCancellation.Cancel();
#pragma warning restore VSTHRD103 // CancelAsync is not available on all supported target frameworks.
                if (Interlocked.CompareExchange(ref cleanupDispatchClaimed, 1, 0) != 0)
                {
                    await cleanupTask.ConfigureAwait(false);
                }
                else if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsVerboseEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Verbose(
                        "MSTest.TestMethodInfo: Cleanup for timed out test '{0}' is deferred because its synchronization context did not become available within {1}ms.",
                        TestMethodName,
                        CleanupDispatchTimeoutMilliseconds);
                }
            }
        }

        return timeoutResult;

        // Local functions
        async Task ExecuteOnSynchronizationContextAsync()
        {
            try
            {
                result = await ExecuteInternalAsync(arguments, null, invocationState).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }

        void ExecuteAsyncAction()
        {
            try
            {
                result = ExecuteInternalAsync(arguments, null, invocationState).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }
    }

    private sealed class TestInvocationState
    {
        private const int TestContextSetupIncomplete = 0;
        private const int TestContextSetupFailed = 1;
        private const int TestContextSetupSucceeded = 2;

        private object? _classInstance;
        private ITestClassInstanceLease? _classInstanceLease;
        private int _testContextSetupState;
        private int _cleanupStarted;

        public bool IsCleanupStarted => Volatile.Read(ref _cleanupStarted) != 0;

        public void PublishActivation(object classInstance, ITestClassInstanceLease? classInstanceLease)
        {
            _classInstance = classInstance;
            _classInstanceLease = classInstanceLease;
        }

        public void CompleteTestContextSetup(bool successful)
            => Volatile.Write(
                ref _testContextSetupState,
                successful ? TestContextSetupSucceeded : TestContextSetupFailed);

        public bool TryGetCleanupSnapshot(out TestInvocationCleanupSnapshot snapshot)
        {
            int testContextSetupState = Volatile.Read(ref _testContextSetupState);
            if (testContextSetupState == TestContextSetupIncomplete)
            {
                snapshot = default;
                return false;
            }

            snapshot = new(
                _classInstance,
                _classInstanceLease,
                testContextSetupState == TestContextSetupSucceeded);
            return true;
        }

        public bool TryBeginCleanup()
            => Interlocked.Exchange(ref _cleanupStarted, 1) == 0;
    }
}
