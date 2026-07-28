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
    /// <returns>The result of the execution.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<TestResult> ExecuteInternalAsync(object?[]? arguments, CancellationTokenSource? timeoutTokenSource)
    {
        DebugEx.Assert(MethodInfo != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

        var result = new TestResult();

        Exception? testRunnerException = null;
        _isTestCleanupInvoked = false;

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
                    _classInstance = CreateTestClassInstance();
                    setTestContextSucessful = _classInstance != null && SetTestContext(_classInstance, result);
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
                            _classInstance = CreateTestClassInstance();
                            setTestContextSucessful = _classInstance != null && SetTestContext(_classInstance, result);
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
                    // For any failure after this point, we must run TestCleanup
                    _isTestContextSet = true;

                    // Intentionally using ConfigureAwait(true) here to ensure the continuation is posted to the synchronization context.
                    // In case of WinUI's default synchronization context, this will ensure that the test method runs on the UI thread.
                    if (await RunTestInitializeMethodAsync(_classInstance!, result, timeoutTokenSource).ConfigureAwait(true))
                    {
                        if (_executionContext is null)
                        {
                            Task? invokeResult = MethodInfo.GetInvokeResultWithParametersAsync(_classInstance, ParameterTypes, arguments);
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
                                    Task? invokeResult = MethodInfo.GetInvokeResultWithParametersAsync(_classInstance, ParameterTypes, arguments);
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
                    result.TestFailureException ??= HandleMethodException(ex, realException, TestClassName, TestMethodName);
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
        await RunTestCleanupMethodAsync(result, timeoutTokenSource).ConfigureAwait(false);

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
    private object? CreateTestClassInstance()
    {
        object?[]? arguments = Parent.IsParameterlessConstructor ? null : [TestContext];

        // Reflection-free fast path: use the source-generated constructor invoker when available;
        // otherwise fall back to ConstructorInfo.Invoke (reflection mode).
        Func<object?[]?, object>? sourceGeneratedInvoker = PlatformServiceProvider.Instance.ReflectionOperations.GetConstructorInvoker(Parent.ClassType);
        return sourceGeneratedInvoker is not null
            ? sourceGeneratedInvoker(arguments)
            : Parent.Constructor.Invoke(arguments);
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

        if (PlatformServiceProvider.Instance.ThreadOperations.Execute(ExecuteAsyncAction, TimeoutInfo.Timeout, TestContext.Context.CancellationTokenSource.Token))
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
        if (TestContext.Context.CancellationTokenSource.IsCancellationRequested)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName);
        }
        else
        {
            // Cancel the token source as test has timed out
#pragma warning disable VSTHRD103 // Call async methods when in an async method - likely fine in this context. CancelAsync is .NET Core only. We prefer having the same behavior between .NET Core and .NET Framework.
            TestContext.Context.CancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        }

        TestResult timeoutResult = new() { Outcome = UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UnitTestOutcome.Timeout, errorMessage) };

        // Execution context propagation here may still not be accurate.
        // if test init was successfully executed by ExecuteAsyncAction, but then the test itself timed out or cancelled,
        // then at this point we will run the cleanup on an execution context that doesn't have any state set by the test initialize.

        // We don't know when the cancellation happened so it's possible that the cleanup wasn't executed, so we need to run it here.
        // The method already checks if the cleanup was already executed.
        await RunTestCleanupMethodAsync(timeoutResult, null).ConfigureAwait(false);
        return timeoutResult;

        // Local functions
        void ExecuteAsyncAction()
        {
            try
            {
                // Ideally this would avoid blocking.
                // This used to always happen, but now is moved to the code path where there is a Timeout on the test method.
                // The GetAwaiter().GetResult() call here can be a source of deadlocks, especially for UWP/WinUI.
                // When the test method has `await`s with ConfigureAwait(true) (which is the default), the continuation is
                // dispatched back to the SynchronizationContext which offloads the work to the UI thread.
                // However, the GetAwaiter().GetResult() here will block the current thread which is also the UI thread.
                // So, the continuations will not be able, thus this task never completes.
                result = ExecuteInternalAsync(arguments, null).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }
    }
}
