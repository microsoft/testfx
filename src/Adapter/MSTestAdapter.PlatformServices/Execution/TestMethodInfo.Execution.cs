// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    /// <summary>
    /// Gets the test timeout for the test method.
    /// </summary>
    /// <returns> The timeout value if defined in milliseconds. 0 if not defined. </returns>
    private TimeoutInfo GetTestTimeout()
    {
        DebugEx.Assert(MethodInfo != null, "TestMethod should be non-null");
        TimeoutAttribute? timeoutAttribute = ReflectHelper.Instance.GetFirstAttributeOrDefault<TimeoutAttribute>(MethodInfo);
        if (timeoutAttribute is null)
        {
            return TimeoutInfo.FromTestTimeoutSettings();
        }

        if (!timeoutAttribute.HasCorrectTimeout)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, MethodInfo.DeclaringType!.FullName, MethodInfo.Name);
            throw new TypeInspectionException(message);
        }

        return TimeoutInfo.FromTimeoutAttribute(timeoutAttribute);
    }

    [DoesNotReturn]
    private void ThrowMultipleAttributesException(string attributeName)
    {
        // Note: even if the given attribute has AllowMultiple = false, we can
        // still reach here if a derived attribute authored by the user re-defines AttributeUsage
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_MultipleAttributesOnTestMethod,
            Parent.ClassType.FullName,
            MethodInfo.Name,
            attributeName);
        throw new TypeInspectionException(errorMessage);
    }

    [DoesNotReturn]
    private void ThrowMultipleClassAttributesException(string attributeName)
    {
        // Note: even if the given attribute has AllowMultiple = false, we can
        // still reach here if a derived attribute authored by the user re-defines AttributeUsage
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_MultipleAttributesOnTestClass,
            Parent.ClassType.FullName,
            attributeName);
        throw new TypeInspectionException(errorMessage);
    }

    /// <summary>
    /// Execute test without timeout.
    /// </summary>
    /// <param name="arguments">Arguments to be passed to the method.</param>
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    /// <param name="invocationState">The activation and cleanup state for this invocation.</param>
    /// <returns>The result of the execution.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<TestResult> ExecuteInternalAsync(
        object?[]? arguments,
        CancellationTokenSource? timeoutTokenSource,
        TestInvocationState? invocationState = null)
    {
        DebugEx.Assert(MethodInfo != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

        var result = new TestResult();
        invocationState ??= new();

        Exception? testRunnerException = null;
        object? classInstance = null;

        try
        {
            try
            {
                // We invoke global test initialize methods before creating the test class instance.
                // We consider the test class constructor as a "local" initialization.
                // We want to invoke first the global initializations, then local ones, then test method.
                // After that, we invoke local cleanups (including Dispose) and finally global cleanups at last.
                foreach ((MethodInfo method, TimeoutInfo? timeoutInfo) in Parent.Parent.GlobalTestInitializations)
                {
                    TestFailedException? globalTestInitException = await InvokeGlobalInitializeMethodAsync(method, timeoutInfo, timeoutTokenSource).ConfigureAwait(false);
                    if (globalTestInitException is not null)
                    {
                        // The returned TestFailedException already carries the per-method timeout/cancellation
                        // message produced by FixtureMethodRunner; throwing it lets the outer catch record it
                        // as the test failure (HandleMethodException returns TestFailedException instances as-is).
                        throw globalTestInitException;
                    }
                }

                // This duplicates logic in TestMethodRunner (DRY violation).
                bool setTestContextSucessful = false;
                if (_executionContext is null)
                {
                    classInstance = CreateTestClassInstance(out ITestClassInstanceLease? classInstanceLease);
                    invocationState.PublishActivation(classInstance, classInstanceLease);
                    try
                    {
                        setTestContextSucessful = SetTestContext(classInstance, result);
                    }
                    finally
                    {
                        invocationState.CompleteTestContextSetup(setTestContextSucessful);
                    }
                }
                else
                {
                    // The whole ExecuteInternalAsync method is already running on the execution context we got after class init.
                    // However, after we run global test initialize, it will need to capture the execution context (after it has finished).
                    // This is the case when executionContext is not null (this code path).
                    // In this case, we want to ensure the constructor and setting TestContext are both run on the correct execution context.
                    // Also we re-capture the execution context in case constructor or TestContext setter modifies an async local value.
                    ExecutionContextHelpers.RunOnContext(_executionContext, () =>
                    {
                        try
                        {
                            classInstance = CreateTestClassInstance(out ITestClassInstanceLease? classInstanceLease);
                            invocationState.PublishActivation(classInstance, classInstanceLease);
                            try
                            {
                                setTestContextSucessful = SetTestContext(classInstance, result);
                            }
                            finally
                            {
                                invocationState.CompleteTestContextSetup(setTestContextSucessful);
                            }
                        }
                        finally
                        {
                            _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                            _hostContext = CallContext.HostContext;
#endif
                        }
                    });
                }

                if (setTestContextSucessful)
                {
                    // Intentionally using ConfigureAwait(true) here to ensure the continuation is posted to the synchronization context.
                    // In case of WinUI's default synchronization context, this will ensure that the test method runs on the UI thread.
                    if (await RunTestInitializeMethodAsync(classInstance!, result, timeoutTokenSource).ConfigureAwait(true))
                    {
                        if (_executionContext is null)
                        {
                            Task? invokeResult = MethodInfo.GetInvokeResultWithParametersAsync(classInstance, ParameterTypes, arguments);
                            if (invokeResult is not null)
                            {
                                await invokeResult.ConfigureAwait(true);
                            }
                        }
                        else
                        {
                            var tcs = new TaskCompletionSource<object?>();
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
                            ExecutionContextHelpers.RunOnContext(_executionContext, async () =>
                            {
                                try
                                {
#if NETFRAMEWORK
                                    CallContext.HostContext = _hostContext;
#endif
                                    Task? invokeResult = MethodInfo.GetInvokeResultWithParametersAsync(classInstance, ParameterTypes, arguments);
                                    if (invokeResult is not null)
                                    {
                                        await invokeResult.ConfigureAwait(false);
                                    }
                                }
                                catch (Exception e)
                                {
                                    tcs.SetException(e);
                                }
                                finally
                                {
                                    _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                                    _hostContext = CallContext.HostContext;
#endif
                                    tcs.TrySetResult(null);
                                }
                            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

                            await tcs.Task.ConfigureAwait(false);
                        }

                        result.Outcome = UnitTestOutcome.Passed;
                    }
                }
            }
            catch (Exception ex)
            {
                Exception realException = GetRealException(ex);

                if (realException.IsOperationCanceledExceptionFromToken(TestContext!.Context.CancellationTokenSource.Token))
                {
                    result.Outcome = UnitTestOutcome.Timeout;
                    result.TestFailureException = new TestFailedException(
                        UnitTestOutcome.Timeout,
                        timeoutTokenSource?.Token.IsCancellationRequested == true
                            ? string.Format(
                                CultureInfo.InvariantCulture,
                                Resource.Execution_Test_Timeout,
                                TestMethodName,
                                TimeoutInfo.Timeout)
                            : string.Format(
                                CultureInfo.InvariantCulture,
                                Resource.Execution_Test_Cancelled,
                                TestMethodName));
                }
                else
                {
                    // This block should not throw. If it needs to throw, then handling of
                    // ThreadAbortException will need to be revisited. See comment in RunTestMethod.
                    result.TestFailureException ??= HandleMethodException(
                        ex,
                        realException,
                        TestClassName,
                        TestMethodName,
                        classInstance is not null);
                }

                if (result.TestFailureException is TestFailedException testFailedException)
                {
                    result.Outcome = testFailedException.Outcome;
                }
                else if (result.Outcome != UnitTestOutcome.Passed)
                {
                    result.Outcome = ex is AssertInconclusiveException || ex.InnerException is AssertInconclusiveException
                        ? UnitTestOutcome.Inconclusive
                        : UnitTestOutcome.Failed;
                }
            }
        }
        catch (Exception exception)
        {
            testRunnerException = exception;
        }

        // Update TestContext with outcome and exception so it can be used in the cleanup logic.
        if (TestContext is { } testContext)
        {
            testContext.SetOutcome(result.Outcome);
            // Unwrap the exception if it's a TestFailedException
            Exception? realException = result.TestFailureException is TestFailedException
                ? result.TestFailureException.InnerException
                : result.TestFailureException;
            testContext.SetException(realException);
        }

        // TestCleanup can potentially be a long running operation which shouldn't ideally be in a finally block.
        // Pulling it out so extension writers can abort custom cleanups if need be. Having this in a finally block
        // does not allow a thread abort exception to be raised within the block but throws one after finally is executed
        // crashing the process. This was blocking writing an extension for Dynamic Timeout in VSO.
        await RunTestCleanupMethodAsync(
            result,
            timeoutTokenSource,
            invocationState).ConfigureAwait(false);

        return testRunnerException != null ? throw testRunnerException : result;
    }

    private static Exception GetRealException(Exception ex)
    {
        if (ex is TargetInvocationException)
        {
            DebugEx.Assert(ex.InnerException != null, "Inner exception of TargetInvocationException is null. This should occur because we should have caught this case above.");

            // Our reflected call will typically always get back a TargetInvocationException
            // containing the real exception thrown by the test method as its inner exception
            return ex.InnerException;
        }
        else
        {
            return ex;
        }
    }

    /// <summary>
    /// Creates an instance of TestClass. The TestMethod is invoked on this instance.
    /// </summary>
    /// <returns>
    /// An instance of the TestClass.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private object CreateTestClassInstance(out ITestClassInstanceLease? lease)
    {
        if (TestClassInstanceFactoryProvider.Current is { } factory)
        {
            lease = factory.CreateInstance(Parent.ClassType, TestContext.Context);
            return lease.Instance;
        }

        object?[]? arguments = Parent.IsParameterlessConstructor ? null : [TestContext];

        // Reflection-free fast path: use the source-generated constructor invoker when available;
        // otherwise fall back to ConstructorInfo.Invoke (reflection mode).
        Func<object?[]?, object>? sourceGeneratedInvoker = PlatformServiceProvider.Instance.ReflectionOperations.GetConstructorInvoker(Parent.ClassType);
        object instance = sourceGeneratedInvoker is not null
            ? sourceGeneratedInvoker(arguments)
            : Parent.Constructor.Invoke(arguments);

        bool requiresCleanup = instance is IDisposable;
#if NET6_0_OR_GREATER
        requiresCleanup |= instance is IAsyncDisposable;
#endif
        lease = requiresCleanup ? new DefaultTestClassInstanceLease(instance) : null;
        return instance;
    }

    private sealed class DefaultTestClassInstanceLease(object instance) : ITestClassInstanceLease
    {
        public object Instance { get; } = instance;

        public bool RequiresCleanup => true;

        public async Task DisposeAsync()
        {
#if NET6_0_OR_GREATER
            if (Instance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
#endif
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

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
        var executionCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource cancellationTokenSource = TestContext.Context.CancellationTokenSource;
        CancellationToken? runCancellationToken = PlatformServiceProvider.Instance.TestRunCancellationToken?.CancellationToken;
        CancellationTokenSource timeoutTokenSource = new();
        int timeoutSourceOwners = 2;
        int executionState = 0;
        using CancellationTokenSource monitorCancellationTokenSource = new();
        using CancellationTokenRegistration testCancellationRegistration = cancellationTokenSource.Token.Register(() =>
        {
            // Fixture cancellation must not end the whole-test wait while cleanup is still running.
            if (!invocationState.IsCleanupStarted)
            {
                monitorCancellationTokenSource.Cancel();
            }
        });
        using CancellationTokenRegistration? runCancellationRegistration =
            runCancellationToken?.Register(monitorCancellationTokenSource.Cancel);
        using CancellationTokenRegistration timeoutRegistration =
            timeoutTokenSource.Token.Register(() => TestContext.Context.CancellationTokenSource.Cancel());
        TaskScheduler? synchronizationContextScheduler = SynchronizationContext.Current is null
            ? null
            : TaskScheduler.FromCurrentSynchronizationContext();
        bool executionCompleted;
        var executionTimer = Stopwatch.StartNew();

        try
        {
            if (synchronizationContextScheduler is null)
            {
                executionCompleted = PlatformServiceProvider.Instance.ThreadOperations.Execute(
                    ExecuteAsyncAction, TimeoutInfo.Timeout, monitorCancellationTokenSource.Token);
            }
            else
            {
                // Queue the invocation on its original dispatcher without blocking the message pump.
                // A new STA thread cannot access controls owned by this UI thread.
                Task executionTask = Task.Factory.StartNew(
                    ExecuteOnSynchronizationContextAsync,
                    monitorCancellationTokenSource.Token,
                    TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler,
                    synchronizationContextScheduler).Unwrap();
                using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(monitorCancellationTokenSource.Token);
                var timeoutTask = Task.Delay(TimeoutInfo.Timeout, timeoutCancellation.Token);
                Task completedTask = await Task.WhenAny(executionTask, timeoutTask).ConfigureAwait(false);
                executionCompleted = completedTask == executionTask && !executionTask.IsCanceled;
#pragma warning disable VSTHRD103 // CancelAsync is not available on all supported target frameworks.
                timeoutCancellation.Cancel();
#pragma warning restore VSTHRD103 // CancelAsync is not available on all supported target frameworks.
            }

            if (executionCompleted)
            {
                if (failure is not null)
                {
                    throw failure;
                }

                DebugEx.Assert(result is not null, "result is not null");
                if (result.Outcome == UnitTestOutcome.Passed
                    && (cancellationTokenSource.IsCancellationRequested || TestContext.Context.CancellationTokenSource.IsCancellationRequested))
                {
                    result.Outcome = UnitTestOutcome.Timeout;
                    result.TestFailureException = new TestFailedException(
                        UnitTestOutcome.Timeout,
                        string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName));
                }

                return result;
            }

            int remainingTimeout = (int)Math.Max(0, TimeoutInfo.Timeout - executionTimer.ElapsedMilliseconds);
            bool isTimeout = !monitorCancellationTokenSource.IsCancellationRequested || remainingTimeout == 0;
            string errorMessage = isTimeout
                ? string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout)
                : string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName);
#pragma warning disable VSTHRD103 // Call async methods when in an async method - likely fine in this context. CancelAsync is .NET Core only. We prefer having the same behavior between .NET Core and .NET Framework.
            if (isTimeout)
            {
                timeoutTokenSource.Cancel();
            }
            else
            {
                if (runCancellationToken?.IsCancellationRequested == true)
                {
                    TestContext.Context.CancellationTokenSource.Cancel();
                }

                // A body cancellation may already have been consumed by fresh cleanup; only the original deadline may cancel it again.
                timeoutTokenSource.CancelAfter(remainingTimeout);
            }
#pragma warning restore VSTHRD103 // Call async methods when in an async method

            TestResult timeoutResult = new() { Outcome = UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UnitTestOutcome.Timeout, errorMessage) };
            CancellationTokenSource? cleanupTimeoutSource = isTimeout ? null : timeoutTokenSource;
            bool cleanupCompleted = false;

            // Execution context propagation here may still not be accurate.
            // if test init was successfully executed by ExecuteAsyncAction, but then the test itself timed out or cancelled,
            // then at this point we will run the cleanup on an execution context that doesn't have any state set by the test initialize.
            if (synchronizationContextScheduler is null)
            {
                cleanupCompleted = await RunTestCleanupMethodAsync(timeoutResult, cleanupTimeoutSource, invocationState).ConfigureAwait(false);
            }
            else if (!invocationState.IsCleanupStarted
                && invocationState.TryGetCleanupSnapshot(out TestInvocationCleanupSnapshot snapshot))
            {
                if (!HasCleanupsToInvoke(snapshot.ClassInstanceLease))
                {
                    // Cancellation bookkeeping does not need a dispatcher blocked by synchronous user code.
                    cleanupCompleted = await RunTestCleanupMethodAsync(timeoutResult, cleanupTimeoutSource, invocationState).ConfigureAwait(false);
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
                                cleanupCompleted = await RunTestCleanupMethodAsync(timeoutResult, cleanupTimeoutSource, invocationState).ConfigureAwait(false);
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

            if (!cleanupCompleted && invocationState.IsCleanupStarted && !timeoutTokenSource.IsCancellationRequested)
            {
                using var cleanupWaitCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutTokenSource.Token,
                    runCancellationToken ?? CancellationToken.None);
                await Task.WhenAny(
                    executionCompletion.Task,
                    Task.Delay(Timeout.Infinite, cleanupWaitCancellation.Token)).ConfigureAwait(false);
#pragma warning disable VSTHRD103 // CancelAsync is not available on all supported target frameworks.
                cleanupWaitCancellation.Cancel();
#pragma warning restore VSTHRD103 // CancelAsync is not available on all supported target frameworks.
            }

            return timeoutResult;
        }
        finally
        {
            if (Interlocked.CompareExchange(ref executionState, 2, 0) == 0)
            {
                ReleaseTimeoutSource();
            }

            ReleaseTimeoutSource();
        }

        // Local functions
        async Task ExecuteOnSynchronizationContextAsync()
        {
            if (Interlocked.CompareExchange(ref executionState, 1, 0) != 0)
            {
                return;
            }

            try
            {
                result = await ExecuteInternalAsync(arguments, timeoutTokenSource, invocationState).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                ReleaseTimeoutSource();
                executionCompletion.TrySetResult(true);
            }
        }

        void ExecuteAsyncAction()
        {
            if (Interlocked.CompareExchange(ref executionState, 1, 0) != 0)
            {
                return;
            }

            try
            {
                result = ExecuteInternalAsync(arguments, timeoutTokenSource, invocationState).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                ReleaseTimeoutSource();
                executionCompletion.TrySetResult(true);
            }
        }

        void ReleaseTimeoutSource()
        {
            // A timed-out worker may still be unwinding after the monitor returns.
            if (Interlocked.Decrement(ref timeoutSourceOwners) == 0)
            {
                timeoutTokenSource.Dispose();
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
            if (testContextSetupState == TestContextSetupIncomplete || _classInstance is null)
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

    private readonly record struct TestInvocationCleanupSnapshot(
        object ClassInstance,
        ITestClassInstanceLease? ClassInstanceLease,
        bool IsTestContextSet);
}
