// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    /// <summary>
    /// Execute test method. Capture failures, handle async and return result.
    /// </summary>
    /// <param name="arguments">
    ///  Arguments to pass to test method. (E.g. For data driven).
    /// </param>
    /// <returns>Result of test method invocation.</returns>
    public virtual async Task<TestResult> InvokeAsync(object?[]? arguments)
    {
        Stopwatch watch = new();
        TestResult? result = null;

        // check if arguments are set for data driven tests
        arguments ??= Arguments;

        watch.Start();

        try
        {
            result = IsTimeoutSet
                ? await ExecuteInternalWithTimeoutAsync(arguments).ConfigureAwait(false)
                : await ExecuteInternalAsync(arguments, null).ConfigureAwait(false);
        }
        finally
        {
            // Handle logs & debug traces.
            watch.Stop();

            if (result != null)
            {
                var testContextImpl = TestContext as TestContextImplementation;
                result.LogOutput = testContextImpl?.GetAndClearOutput();
                result.LogError = testContextImpl?.GetAndClearError();
                result.DebugTrace = testContextImpl?.GetAndClearTrace();
                result.TestContextMessages = TestContext?.GetAndClearDiagnosticMessages();
                result.ResultFiles = TestContext?.GetResultFiles();
                result.Duration = watch.Elapsed;
            }

            _executionContext?.Dispose();
            _executionContext = null;
#if NETFRAMEWORK
            _hostContext = null;
#endif
        }

        return result;
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
                    await InvokeGlobalInitializeMethodAsync(method, timeoutInfo, timeoutTokenSource).ConfigureAwait(false);
                }

                // TODO remove dry violation with TestMethodRunner
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

                    if (await RunTestInitializeMethodAsync(_classInstance!, result, timeoutTokenSource).ConfigureAwait(false))
                    {
                        if (_executionContext is null)
                        {
                            Task? invokeResult = MethodInfo.GetInvokeResultAsync(_classInstance, arguments);
                            if (invokeResult is not null)
                            {
                                await invokeResult.ConfigureAwait(false);
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
                                    Task? invokeResult = MethodInfo.GetInvokeResultAsync(_classInstance, arguments);
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

                if (result.Outcome != UnitTestOutcome.Passed)
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
            // Uwnrap the exception if it's a TestFailedException
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
    /// Handles the exception that is thrown by a test method. The exception can either
    /// be expected or not expected.
    /// </summary>
    /// <param name="ex">Exception that was thrown.</param>
    /// <param name="realException">Real exception thrown by the test method.</param>
    /// <param name="className">The class name.</param>
    /// <param name="methodName">The method name.</param>
    /// <returns>Test framework exception with details.</returns>
    private TestFailedException HandleMethodException(Exception ex, Exception realException, string className, string methodName)
    {
        DebugEx.Assert(ex != null, "exception should not be null.");

        string errorMessage;
        if (ex is TargetInvocationException && ex.InnerException == null)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, className, methodName);
            return new TestFailedException(UnitTestOutcome.Error, errorMessage);
        }

        if (ex is TestFailedException testFailedException)
        {
            return testFailedException;
        }

        // If we are in hot reload context and the exception is a MissingMethodException and the first line of the stack
        // trace contains the method name then it's likely that the current method was removed and the test is failing.
        // For cases where the content of the test would throw a MissingMethodException, the first line of the stack trace
        // would not be the test method name, so we can safely assume this is a proper test failure.
        if (ex is MissingMethodException missingMethodException
            && RuntimeContext.IsHotReloadEnabled
            && missingMethodException.StackTrace?.IndexOf(Environment.NewLine, StringComparison.Ordinal) is { } lineReturnIndex
            && lineReturnIndex >= 0
#pragma warning disable IDE0057 // Use range operator
            && missingMethodException.StackTrace.Substring(0, lineReturnIndex).Contains($"{className}.{methodName}"))
#pragma warning restore IDE0057 // Use range operator
        {
            return new TestFailedException(UnitTestOutcome.NotFound, missingMethodException.Message, missingMethodException);
        }

        // Get the real exception thrown by the test method
        if (realException.TryGetUnitTestAssertException(out UnitTestOutcome outcome, out string? exceptionMessage, out StackTraceInformation? exceptionStackTraceInfo))
        {
            return new TestFailedException(outcome, exceptionMessage, exceptionStackTraceInfo, realException);
        }

        errorMessage = _classInstance is null
            ? string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_InstanceCreationError,
                TestClassName,
                realException.GetFormattedExceptionMessage())
            : string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestMethodThrows,
                className,
                methodName,
                realException.GetFormattedExceptionMessage());

        // Handle special case of UI objects in TestMethod to suggest UITestMethod
        if (realException.HResult == -2147417842)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_WrongThread, errorMessage);
        }

        StackTraceInformation? stackTrace = null;

        // For ThreadAbortException (that can be thrown only by aborting a thread as there's no public constructor)
        // there's no inner exception and exception itself contains reflection-related stack trace
        // (_RuntimeMethodHandle.InvokeMethodFast <- _RuntimeMethodHandle.Invoke <- UnitTestExecuter.RunTestMethod)
        // which has no meaningful info for the user. Thus, we do not show call stack for ThreadAbortException.
        if (realException.GetType().Name != "ThreadAbortException")
        {
            stackTrace = realException.GetStackTraceInformation();
        }

        return new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTrace, realException);
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
            CancellationTokenSource? timeoutTokenSource = null;
            try
            {
                timeoutTokenSource = new(TimeoutInfo.Timeout);
                timeoutTokenSource.Token.Register(TestContext.Context.CancellationTokenSource.Cancel);
                if (timeoutTokenSource.Token.IsCancellationRequested)
                {
                    return new()
                    {
                        Outcome = UnitTestOutcome.Timeout,
                        TestFailureException = new TestFailedException(
                            UnitTestOutcome.Timeout,
                            string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout)),
                    };
                }

                try
                {
                    return await ExecuteInternalAsync(arguments, timeoutTokenSource).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Ideally we would like to check that the token of the exception matches cancellationTokenSource but TestContext
                    // instances are not well defined so we have to handle the exception entirely.
                    return new()
                    {
                        Outcome = UnitTestOutcome.Timeout,
                        TestFailureException = new TestFailedException(
                            UnitTestOutcome.Timeout,
                            timeoutTokenSource.Token.IsCancellationRequested
                                ? string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout)
                                : string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName)),
                    };
                }
            }
            finally
            {
                timeoutTokenSource?.Dispose();
                timeoutTokenSource = null;
            }
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

        // TODO: execution context propagation here may still not be accurate.
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
                // TODO: Avoid blocking.
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
