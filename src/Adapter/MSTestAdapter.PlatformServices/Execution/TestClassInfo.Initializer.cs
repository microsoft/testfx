// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestClassInfo
{
    /// <summary>
    /// Runs the class initialize method.
    /// </summary>
    /// <param name="testContext"> The test context. </param>
    /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
    internal async Task RunClassInitializeAsync(TestContext testContext)
    {
        // If no class initialize and no base class initialize, return
        if (ClassInitializeMethod is null && BaseClassInitMethods.Count == 0)
        {
            DebugEx.Assert(false, "Caller shouldn't call us if nothing to execute");
            IsClassInitializeExecuted = true;
            return;
        }

        MethodInfo? initializeMethod = null;
        string? failedClassInitializeMethodName = string.Empty;

        // If class initialization is not done, then do it.
        DebugEx.Assert(!IsClassInitializeExecuted, "Caller shouldn't call us if it was executed.");
        if (!IsClassInitializeExecuted)
        {
            try
            {
                // We have discovered the methods from bottom (most derived) to top (less derived) but we want to execute
                // from top to bottom.
                for (int i = BaseClassInitMethods.Count - 1; i >= 0; i--)
                {
                    initializeMethod = BaseClassInitMethods[i];
                    ClassInitializationException = await InvokeInitializeMethodAsync(initializeMethod, testContext).ConfigureAwait(false);
                    if (ClassInitializationException is not null)
                    {
                        break;
                    }
                }

                if (ClassInitializationException is null)
                {
                    initializeMethod = ClassInitializeMethod;
                    ClassInitializationException = await InvokeInitializeMethodAsync(ClassInitializeMethod, testContext).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ClassInitializationException = ex;
                failedClassInitializeMethodName = initializeMethod?.Name ?? ClassInitializeMethod?.Name;
            }
            finally
            {
                IsClassInitializeExecuted = true;
            }
        }

        // If classInitialization was successful, then don't do anything
        if (ClassInitializationException == null)
        {
            return;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (ClassInitializationException is TestFailedException)
        {
            throw ClassInitializationException;
        }

        // Fail the current test if it was a failure.
        Exception realException = ClassInitializationException.GetRealException();

        UnitTestOutcome outcome = realException is AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed;

        // Do not use StackTraceHelper.GetFormattedExceptionMessage(realException) as it prefixes the message with the exception type name.
        string exceptionMessage = realException.TryGetMessage();
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ClassInitMethodThrows,
            ClassType.FullName,
            failedClassInitializeMethodName,
            realException.GetType().ToString(),
            exceptionMessage);
        StackTraceInformation? exceptionStackTraceInfo = realException.GetStackTraceInformation();

        var testFailedException = new TestFailedException(outcome, errorMessage, exceptionStackTraceInfo, realException);
        ClassInitializationException = testFailedException;

        throw testFailedException;
    }

    private TestResult? TryGetClonedCachedClassInitializeResult()
    {
        // Historically, we were not caching class initialize result, and were always going through the logic in GetResultOrRunClassInitialize.
        // When caching is introduced, we found out that using the cached instance can change the behavior in some cases. For example,
        // if you have Console.WriteLine in class initialize, those will be present on the TestResult.
        // Before caching was introduced, these logs will be only in the first class initialize result (attached to the first test run in class)
        // By re-using the cached instance, it's now part of all tests.
        // To preserve the original behavior, we clone the cached instance so we keep only the information we are sure should be reused.
        // Volatile.Read pairs with the Volatile.Write performed when the result is published, so
        // that fast-path readers (which bypass _testClassExecuteSyncSemaphore) also observe the
        // PostClassInitProperties snapshot written before the result on the publishing thread.
        TestResult? cached = Volatile.Read(ref _classInitializeResult);
        return cached is null
            ? null
            : new()
            {
                Outcome = cached.Outcome,
                IgnoreReason = cached.IgnoreReason,
                TestFailureException = cached.TestFailureException,
            };
    }

    internal async Task<TestResult> GetResultOrRunClassInitializeAsync(ITestContext testContext, string? initializationLogs, string? initializationErrorLogs, string? initializationTrace, string? initializationTestContextMessages)
    {
        TestResult? clonedInitializeResult = TryGetClonedCachedClassInitializeResult();

        // Optimization: If we already ran before and know the result, return it.
        if (clonedInitializeResult is not null)
        {
            DebugEx.Assert(IsClassInitializeExecuted, "Class initialize result should be available if and only if class initialize was executed");
            return clonedInitializeResult;
        }

        // For optimization purposes, return right away if there is nothing to execute.
        // For STA, this avoids starting a thread when we know it will do nothing.
        // But we still return early even not STA.
        if (ClassInitializeMethod is null && BaseClassInitMethods.Count == 0)
        {
            IsClassInitializeExecuted = true;
            var emptyResult = new TestResult { Outcome = UnitTestOutcome.Passed };
            Volatile.Write(ref _classInitializeResult, emptyResult);
            return emptyResult;
        }

        // At this point, maybe class initialize was executed by another thread such
        // that TryGetClonedCachedClassInitializeResult would return non-null.
        // Now, we need to check again, but under a lock.
        // Note that we are duplicating the logic above.
        // We could keep the logic in lock only and not duplicate, but we don't want to pay
        // the lock cost unnecessarily for a common case.
        // We also need to lock to avoid concurrency issues and guarantee that class init is called only once.
        try
        {
            await _testClassExecuteSyncSemaphore.WaitAsync().ConfigureAwait(false);
            clonedInitializeResult = TryGetClonedCachedClassInitializeResult();

            // Optimization: If we already ran before and know the result, return it.
            if (clonedInitializeResult is not null)
            {
                DebugEx.Assert(IsClassInitializeExecuted, "Class initialize result should be available if and only if class initialize was executed");
                return clonedInitializeResult;
            }

            DebugEx.Assert(!IsClassInitializeExecuted, "If class initialize was executed, we should have been in the previous if were we have a result available.");

            bool isSTATestClass = ClassAttribute is STATestClassAttribute;
            bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isSTATestClass
                && isWindowsOS
                && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                var result = new TestResult
                {
                    Outcome = UnitTestOutcome.Error,
                    IgnoreReason = "MSTest STATestClass ClassInitialize didn't complete",
                };

                Thread entryPointThread = new(() => result = DoRunAsync().GetAwaiter().GetResult())
                {
                    Name = "MSTest STATestClass ClassInitialize",
                };

                entryPointThread.SetApartmentState(ApartmentState.STA);
                entryPointThread.Start();

                try
                {
                    entryPointThread.Join();
                    return result;
                }
                catch (Exception ex)
                {
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsErrorEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Error(ex.ToString());
                    }

                    return new TestResult
                    {
                        TestFailureException = new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()),
                        Outcome = UnitTestOutcome.Error,
                    };
                }
            }
            else
            {
                // If the requested apartment state is STA and the OS is not Windows, then warn the user.
                if (!isWindowsOS && isSTATestClass)
                {
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(Resource.STAIsOnlySupportedOnWindowsWarning);
                    }
                }

                return await DoRunAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _testClassExecuteSyncSemaphore.Release();
        }

        // Local functions
        async Task<TestResult> DoRunAsync()
        {
            var result = new TestResult
            {
                Outcome = UnitTestOutcome.Passed,
            };

            try
            {
                // This runs the ClassInitialize methods only once but saves the
                await RunClassInitializeAsync(testContext.Context).ConfigureAwait(false);

                // Capture a snapshot of TestContext.Properties so that values set during
                // ClassInitialize (and any base-class ClassInitialize methods that ran in the
                // same RunClassInitializeAsync call) flow to subsequent contexts
                // (test execution, class cleanup).
                // The `is` check is defensive: this method is part of an internal but mockable
                // surface, so unit tests can legitimately pass an ITestContext wrapping a mock.
                // Production callers always wrap a TestContextImplementation.
                if (testContext.Context is TestContextImplementation classInitContextImpl)
                {
                    PostClassInitProperties = classInitContextImpl.CaptureLifecycleProperties();
                }
            }
            catch (TestFailedException ex)
            {
                result = new TestResult { TestFailureException = ex, Outcome = ex.Outcome };
            }
            catch (Exception ex)
            {
                result = new TestResult
                {
                    TestFailureException = new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()),
                    Outcome = UnitTestOutcome.Error,
                };
            }
            finally
            {
                // Assembly initialize and class initialize logs are pre-pended to the first result.
                var testContextImpl = testContext as TestContextImplementation;
                result.LogOutput = initializationLogs + testContextImpl?.GetAndClearOutput();
                result.LogError = initializationErrorLogs + testContextImpl?.GetAndClearError();
                result.DebugTrace = initializationTrace + testContextImpl?.GetAndClearTrace();
                result.TestContextMessages = initializationTestContextMessages + testContext.GetAndClearDiagnosticMessages();
            }

            // Publish with Volatile.Write so callers on the cached-result fast path of
            // GetResultOrRunClassInitializeAsync (which bypasses _testClassExecuteSyncSemaphore)
            // safely observe the prior PostClassInitProperties snapshot publication: the
            // release semantics ensure the snapshot write is visible before this assignment
            // becomes observable.
            Volatile.Write(ref _classInitializeResult, result);
            return result;
        }
    }

    private async Task<TestFailedException?> InvokeInitializeMethodAsync(MethodInfo? methodInfo, TestContext testContext)
    {
        if (methodInfo is null)
        {
            return null;
        }

        TimeoutInfo? timeout = null;
        if (ClassInitializeMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
                // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                // It's safer to reset it before the capture.
                using (TestContextImplementation.SetCurrentTestContext(testContext))
                {
                    Task? task = methodInfo.GetInvokeResultAsync(null, testContext);
                    if (task is not null)
                    {
                        await task.ConfigureAwait(false);
                    }
                }

                // **After** we have executed the class initialize, we save the current context.
                // This context will contain async locals set by the class initialize method.
                ExecutionContext = ExecutionContext.Capture();
            },
            testContext.CancellationTokenSource,
            timeout,
            methodInfo,
            ExecutionContext ?? Parent?.ExecutionContext,
            Resource.ClassInitializeWasCancelled,
            Resource.ClassInitializeTimedOut).ConfigureAwait(false);

        return result;
    }
}
