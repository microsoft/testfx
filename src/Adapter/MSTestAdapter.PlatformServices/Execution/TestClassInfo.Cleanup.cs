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

        bool isSTATestClass = ClassAttribute is STATestClassAttribute;
        bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isSTATestClass
            && isWindowsOS
            && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            TestResult? result = null;
            var entryPointThread = new Thread(() => result = DoRunAsync().GetAwaiter().GetResult())
            {
                Name = "MSTest STATestClass ClassCleanup",
            };

            entryPointThread.SetApartmentState(ApartmentState.STA);
            entryPointThread.Start();

            try
            {
                entryPointThread.Join();
            }
            catch (Exception ex)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsErrorEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Error(ex.ToString());
                }
            }

            return result;
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
            async () =>
            {
                // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                // It's safer to reset it before the capture.
                using (TestContextImplementation.SetCurrentTestContext(testContext))
                {
                    Task? task = methodInfo.GetParameters().Length == 0
                        ? methodInfo.GetInvokeResultAsync(null)
                        : methodInfo.GetInvokeResultAsync(null, testContext);

                    if (task is not null)
                    {
                        await task.ConfigureAwait(false);
                    }
                }

                // **After** we have executed the class cleanup, we save the current context.
                // This context will contain async locals set by the current class cleanup method.
                // This is essential to propagate async locals between multiple class cleanup methods.
                ExecutionContext = ExecutionContext.Capture();
            },
            testContext.CancellationTokenSource,
            timeout,
            methodInfo,
            ExecutionContext ?? Parent.ExecutionContext,
            Resource.ClassCleanupWasCancelled,
            Resource.ClassCleanupTimedOut).ConfigureAwait(false);

        return result;
    }
}
