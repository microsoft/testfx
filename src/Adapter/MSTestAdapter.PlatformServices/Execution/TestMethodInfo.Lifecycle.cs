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
    /// Runs TestCleanup methods of parent TestClass and base classes.
    /// </summary>
    /// <param name="result">Instance of TestResult.</param>
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async SynchronizationContextPreservingTask RunTestCleanupMethodAsync(TestResult result, CancellationTokenSource? timeoutTokenSource)
    {
        DebugEx.Assert(result != null, "result != null");

        if (_classInstance is null || !_isTestContextSet || _isTestCleanupInvoked ||
            // Fast check to see if we can return early.
            // This avoids the code below that allocates CancellationTokenSource
            !HasCleanupsToInvoke())
        {
            return;
        }

        _isTestCleanupInvoked = true;
        MethodInfo? testCleanupMethod = Parent.TestCleanupMethod;
        Exception? testCleanupException;
        try
        {
            try
            {
                // Reset the cancellation token source to avoid cancellation of cleanup methods because of the init or test method cancellation.
                TestContext.Context.CancellationTokenSource = new CancellationTokenSource();

                // If we are running with a method timeout, we need to cancel the cleanup when the overall timeout expires. If it already expired, nothing to do.
                if (timeoutTokenSource is { IsCancellationRequested: false })
                {
                    timeoutTokenSource?.Token.Register(TestContext.Context.CancellationTokenSource.Cancel);
                }

                // Test cleanups are called in the order of discovery
                // Current TestClass -> Parent -> Grandparent
                testCleanupException = testCleanupMethod is not null
                    ? await InvokeCleanupMethodAsync(testCleanupMethod, _classInstance, timeoutTokenSource).ConfigureAwait(false)
                    : null;
                var baseTestCleanupQueue = new Queue<MethodInfo>(Parent.BaseTestCleanupMethodsQueue);
                while (baseTestCleanupQueue.Count > 0 && testCleanupException is null)
                {
                    testCleanupMethod = baseTestCleanupQueue.Dequeue();
                    testCleanupException = await InvokeCleanupMethodAsync(testCleanupMethod, _classInstance, timeoutTokenSource).ConfigureAwait(false);
                }
            }
            finally
            {
#if NET6_0_OR_GREATER
                if (_classInstance is IAsyncDisposable classInstanceAsAsyncDisposable)
                {
                    // If you implement IAsyncDisposable without calling the DisposeAsync this would result a resource leak.
                    await classInstanceAsAsyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
#endif
                if (_classInstance is IDisposable classInstanceAsDisposable)
                {
                    classInstanceAsDisposable.Dispose();
                }

                foreach ((MethodInfo method, TimeoutInfo? timeoutInfo) in Parent.Parent.GlobalTestCleanups)
                {
                    await InvokeGlobalCleanupMethodAsync(method, timeoutInfo, timeoutTokenSource).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            testCleanupException = ex;
        }

        // If testCleanup was successful, then don't do anything
        if (testCleanupException == null)
        {
            return;
        }

        Exception realException = testCleanupException.GetRealException();
        UnitTestOutcome outcomeFromRealException = realException is AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed;
        result.Outcome = result.Outcome.GetMoreImportantOutcome(outcomeFromRealException);

        realException = testCleanupMethod != null
            ? new TestFailedException(
                outcomeFromRealException,
                string.Format(CultureInfo.CurrentCulture, Resource.UTA_CleanupMethodThrows, TestClassName, testCleanupMethod.Name, realException.GetFormattedExceptionMessage()),
                realException.TryGetStackTraceInformation(),
                realException)
            : new TestFailedException(
                outcomeFromRealException,
                string.Format(CultureInfo.CurrentCulture, Resource.UTA_CleanupMethodThrowsGeneralError, TestClassName, realException.GetFormattedExceptionMessage()),
                realException.TryGetStackTraceInformation(),
                realException);

        result.TestFailureException = realException;
    }

    private bool HasCleanupsToInvoke() =>
        Parent.TestCleanupMethod is not null ||
        Parent.BaseTestCleanupMethodsQueue is { Count: > 0 } ||
        _classInstance is IDisposable ||
#if NET6_0_OR_GREATER
        _classInstance is IAsyncDisposable ||
#endif
        Parent.Parent.GlobalTestCleanups is { Count: > 0 };

    /// <summary>
    /// Runs TestInitialize methods of parent TestClass and the base classes.
    /// </summary>
    /// <param name="classInstance">Instance of TestClass.</param>
    /// <param name="result">Instance of TestResult.</param>
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    /// <returns>True if the TestInitialize method(s) did not throw an exception.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async SynchronizationContextPreservingTask<bool> RunTestInitializeMethodAsync(object classInstance, TestResult result, CancellationTokenSource? timeoutTokenSource)
    {
        DebugEx.Assert(classInstance != null, "classInstance != null");
        DebugEx.Assert(result != null, "result != null");

        MethodInfo? testInitializeMethod = null;
        Exception? testInitializeException = null;

        try
        {
            // TestInitialize methods for base classes are called in reverse order of discovery
            // Grandparent -> Parent -> Child TestClass
            var baseTestInitializeStack = new Stack<MethodInfo>(Parent.BaseTestInitializeMethodsQueue);
            while (baseTestInitializeStack.Count > 0)
            {
                testInitializeMethod = baseTestInitializeStack.Pop();
                testInitializeException = testInitializeMethod is not null
                    ? await InvokeInitializeMethodAsync(testInitializeMethod, classInstance, timeoutTokenSource).ConfigureAwait(false)
                    : null;
                if (testInitializeException is not null)
                {
                    break;
                }
            }

            if (testInitializeException == null)
            {
                testInitializeMethod = Parent.TestInitializeMethod;
                testInitializeException = testInitializeMethod is not null
                    ? await InvokeInitializeMethodAsync(testInitializeMethod, classInstance, timeoutTokenSource).ConfigureAwait(false)
                    : null;
            }
        }
        catch (Exception ex)
        {
            testInitializeException = ex;
        }

        // If testInitialization was successful, then don't do anything
        if (testInitializeException == null)
        {
            return true;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (testInitializeException is TestFailedException tfe)
        {
            result.Outcome = tfe.Outcome;
            result.TestFailureException = testInitializeException;
            return false;
        }

        Exception realException = testInitializeException.GetRealException();

        // Prefix the exception message with the exception type name as prefix when exception is not assert exception.
        string exceptionMessage = realException is UnitTestAssertException
            ? realException.TryGetMessage()
            : realException.GetFormattedExceptionMessage();
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_InitMethodThrows,
            TestClassName,
            testInitializeMethod?.Name,
            exceptionMessage);
        StackTraceInformation? stackTrace = realException.GetStackTraceInformation();

        result.Outcome = realException is AssertInconclusiveException
            ? UnitTestOutcome.Inconclusive
            : UnitTestOutcome.Failed;
        result.TestFailureException = new TestFailedException(
            result.Outcome,
            errorMessage,
            stackTrace,
            realException);

        return false;
    }

    private async SynchronizationContextPreservingTask<TestFailedException?> InvokeInitializeMethodAsync(MethodInfo methodInfo, object classInstance, CancellationTokenSource? timeoutTokenSource)
    {
        TimeoutInfo? timeout = null;
        if (Parent.TestInitializeMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(classInstance, null);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                CaptureExecutionContextAfterFixtureIfNeeded(timeout);

#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeout,
            methodInfo,
            _executionContext,
            Resource.TestInitializeWasCancelled,
            Resource.TestInitializeTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

        return result;
    }

    private async SynchronizationContextPreservingTask<TestFailedException?> InvokeGlobalInitializeMethodAsync(MethodInfo methodInfo, TimeoutInfo? timeoutInfo, CancellationTokenSource? timeoutTokenSource)
    {
        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(null, [TestContext]);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                CaptureExecutionContextAfterFixtureIfNeeded(timeoutInfo);

#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeoutInfo: timeoutInfo,
            methodInfo,
            _executionContext,
            Resource.TestInitializeWasCancelled,
            Resource.TestInitializeTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

        return result;
    }

    private async SynchronizationContextPreservingTask<TestFailedException?> InvokeCleanupMethodAsync(MethodInfo methodInfo, object classInstance, CancellationTokenSource? timeoutTokenSource)
    {
        TimeoutInfo? timeout = null;
        if (Parent.TestCleanupMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(classInstance, null);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                CaptureExecutionContextAfterFixtureIfNeeded(timeout);

#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeout,
            methodInfo,
            _executionContext,
            Resource.TestCleanupWasCancelled,
            Resource.TestCleanupTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

        return result;
    }

    private async SynchronizationContextPreservingTask<TestFailedException?> InvokeGlobalCleanupMethodAsync(MethodInfo methodInfo, TimeoutInfo? timeoutInfo, CancellationTokenSource? timeoutTokenSource)
    {
        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(null, [TestContext]);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                CaptureExecutionContextAfterFixtureIfNeeded(timeoutInfo);

#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeoutInfo: timeoutInfo,
            methodInfo,
            _executionContext,
            Resource.TestCleanupWasCancelled,
            Resource.TestCleanupTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

        return result;
    }

    private void CaptureExecutionContextAfterFixtureIfNeeded(TimeoutInfo? timeoutInfo)
    {
        // After we execute a global test initialize, test initialize, test cleanup, or global test cleanup, we
        // might need to capture the execution context.
        // Generally, we do so only if the method has a timeout and that timeout is non-cooperative.
        // For all other cases, we already use a custom task that preserves synchronization and execution contexts.
        // NOTE: it seems that in .NET Framework, the synchronization context is part of the execution context.
        // However, this doesn't appear to be the case in .NET (Core) where the synchronization context is strictly tied to current thread.
        // In addition, if execution context was captured before (due to use of non-cooperative timeout in a previously run fixture), we still capture here again.
        if (timeoutInfo?.CooperativeCancellation == false || _executionContext is not null)
        {
            _executionContext = ExecutionContext.Capture() ?? _executionContext;
        }
    }
}
