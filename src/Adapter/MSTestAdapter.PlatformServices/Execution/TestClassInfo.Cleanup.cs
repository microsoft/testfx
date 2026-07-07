// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - not important to dispose the SemaphoreSlim, we don't access AvailableWaitHandle.
internal sealed partial class TestClassInfo
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    /// <summary>
    /// Execute current and base class cleanups.
    /// </summary>
    /// <remarks>
    /// This is a replacement for RunClassCleanup but as we are on a bug fix version, we do not want to change
    /// the public API, hence this method.
    /// </remarks>
    internal async Task<TestFailedException?> ExecuteClassCleanupAsync(TestContext testContext)
    {
        if ((ClassCleanupMethod is null && BaseClassCleanupMethods.Count == 0)
            || IsClassCleanupExecuted)
        {
            return null;
        }

        MethodInfo? classCleanupMethod = ClassCleanupMethod;

        try
        {
            await _testClassExecuteSyncSemaphore.WaitAsync().ConfigureAwait(false);
            if (IsClassCleanupExecuted
                // If ClassInitialize method has not been executed, then we should not execute ClassCleanup
                // Note that if there is no ClassInitialze method at all, we will still set
                // IsClassInitializeExecuted to true in RunClassInitialize
                // IsClassInitializeExecuted can be false if all tests in the class are ignored.
                || !IsClassInitializeExecuted)
            {
                return null;
            }

            try
            {
                if (classCleanupMethod is not null)
                {
                    if (!classCleanupMethod.DeclaringType!.IsIgnored(out _))
                    {
                        ClassCleanupException = await InvokeCleanupMethodAsync(classCleanupMethod, testContext).ConfigureAwait(false);
                    }
                }

                if (ClassCleanupException is null)
                {
                    for (int i = 0; i < BaseClassCleanupMethods.Count; i++)
                    {
                        classCleanupMethod = BaseClassCleanupMethods[i];
                        if (!classCleanupMethod.DeclaringType!.IsIgnored(out _))
                        {
                            ClassCleanupException = await InvokeCleanupMethodAsync(classCleanupMethod, testContext).ConfigureAwait(false);
                            if (ClassCleanupException is not null)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                ClassCleanupException = exception;
            }
            finally
            {
                IsClassCleanupExecuted = true;
            }
        }
        finally
        {
            _testClassExecuteSyncSemaphore.Release();
        }

        // If ClassCleanup was successful, then don't do anything
        if (ClassCleanupException == null)
        {
            return null;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (ClassCleanupException is TestFailedException classCleanupEx)
        {
            return classCleanupEx;
        }

        Exception realException = ClassCleanupException.GetRealException();

        // special case AssertFailedException to trim off part of the stack trace
        string errorMessage = realException is AssertFailedException or AssertInconclusiveException
            ? realException.Message
            : realException.GetFormattedExceptionMessage();

        StackTraceInformation? exceptionStackTraceInfo = realException.TryGetStackTraceInformation();

        var testFailedException = new TestFailedException(
            UnitTestOutcome.Failed,
            string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_ClassCleanupMethodWasUnsuccesful,
                classCleanupMethod!.DeclaringType!.Name,
                classCleanupMethod.Name,
                errorMessage,
                exceptionStackTraceInfo?.ErrorStackTrace),
            exceptionStackTraceInfo,
            realException);
        ClassCleanupException = testFailedException;

        return testFailedException;
    }

    internal async Task<TestResult?> RunClassCleanupAsync(ITestContext testContext, TestResult[] results)
    {
        if (!HasExecutableCleanupMethod || IsClassCleanupExecuted)
        {
            // DoRun will already do nothing for this condition. So, we gain a bit of performance.
            return null;
        }

        ApartmentState? requestedApartmentState = ClassAttribute is STATestClassAttribute
            ? ApartmentState.STA
            : null;
        return await StaThreadHelper.RunOnApartmentThreadIfNeededAsync<TestResult?>(
            requestedApartmentState,
            "MSTest STATestClass ClassCleanup",
            DoRunAsync,
            () => null,
            entryPointThread =>
            {
                entryPointThread.Join();
                return Task.CompletedTask;
            },
            (thread, ex) =>
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsErrorEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Error(
                        $"Failed to join STA thread '{thread.Name}': {ex}");
                }

                return null;
            },
            () =>
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(Resource.STAIsOnlySupportedOnWindowsWarning);
                }
            }).ConfigureAwait(false);

        // Local functions
        async Task<TestResult?> DoRunAsync()
        {
            TestFailedException? ex = await ExecuteClassCleanupAsync(testContext.Context).ConfigureAwait(false);
            var testContextImpl = testContext as TestContextImplementation;
            if (ex is not null)
            {
                return new TestResult()
                {
                    Outcome = UnitTestOutcome.Failed,
                    DisplayName = $"[{ClassType.FullName} ClassCleanup]",
                    TestFailureException = ex,
                    LogOutput = testContextImpl?.GetAndClearOutput(),
                    LogError = testContextImpl?.GetAndClearError(),
                    DebugTrace = testContextImpl?.GetAndClearTrace(),
                    TestContextMessages = testContext.GetAndClearDiagnosticMessages(),
                };
            }

            if (results.Length > 0)
            {
                TestResult lastResult = results[results.Length - 1];
                lastResult.LogOutput += testContextImpl?.GetAndClearOutput();
                lastResult.LogError += testContextImpl?.GetAndClearError();
                lastResult.DebugTrace += testContextImpl?.GetAndClearTrace();
                lastResult.TestContextMessages += testContext.GetAndClearDiagnosticMessages();
            }

            return null;
        }
    }

    private async Task<TestFailedException?> InvokeCleanupMethodAsync(MethodInfo methodInfo, TestContext testContext)
    {
        TimeoutInfo? timeout = null;
        if (ClassCleanupMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () => methodInfo.InvokeAsFixtureMethodAsync(
                testContext,
                ec => ExecutionContext = ec),
            testContext.CancellationTokenSource,
            timeout,
            methodInfo,
            ExecutionContext ?? Parent.ExecutionContext,
            Resource.ClassCleanupWasCancelled,
            Resource.ClassCleanupTimedOut).ConfigureAwait(false);

        return result;
    }
}
