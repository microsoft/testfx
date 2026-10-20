// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
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
                if (Parent.IsFSharpModule)
                {
                    setTestContextSucessful = true;
                    invocationState.CompleteTestContextSetup(true);
                }
                else if (_executionContext is null)
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
                    if (await RunTestInitializeMethodAsync(classInstance, result, timeoutTokenSource).ConfigureAwait(true))
                    {
                        arguments = GetArgumentsWithCancellationToken(arguments);
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
                        classInstance is not null || Parent.IsFSharpModule);
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
}
