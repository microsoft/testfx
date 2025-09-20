// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

using UnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class FixtureMethodRunner
{
    internal static TestFailedException? RunWithTimeoutAndCancellation(
        Action action, CancellationTokenSource cancellationTokenSource, TimeoutInfo? timeoutInfo, MethodInfo methodInfo,
        ExecutionContext? executionContext, string methodCanceledMessageFormat, string methodTimedOutMessageFormat,
        // When a test method is marked with [Timeout], this timeout is applied from ctor to destructor, so we need to take
        // that into account when processing the OCE of the action.
        (CancellationTokenSource TokenSource, int Timeout)? testTimeoutInfo = default)
    {
        // HACK: Avoid calling RunWithTimeoutAndCancellationAsync to preserve async locals correctly between test initialize and the test method.
        // If we call the async version, we end up losing the async locals as it's set after we entered an "async" method.
        // So once we exit that "async" method, the async locals are lost.
        // If we explicitly capture the execution context to try to always preserve it, we end up losing the IllogicalCallContext.
        if (timeoutInfo is null)
        {
            try
            {
                ExecutionContextHelpers.RunOnContext(executionContext, action);
                return null;
            }
            catch (Exception ex)
                when (ex.GetRealException().IsOperationCanceledExceptionFromToken(cancellationTokenSource.Token))
            {
                return new(
                    UnitTestOutcome.Timeout,
                    testTimeoutInfo?.TokenSource.Token.IsCancellationRequested == true
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            methodTimedOutMessageFormat,
                            methodInfo.DeclaringType!.FullName,
                            methodInfo.Name,
                            testTimeoutInfo.Value.Timeout)
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            methodCanceledMessageFormat,
                            methodInfo.DeclaringType!.FullName,
                            methodInfo.Name));
            }
        }

        return RunWithTimeoutAndCancellationAsync(
            () =>
            {
                action();
                return Task.CompletedTask;
            }, cancellationTokenSource, timeoutInfo, methodInfo, executionContext, methodCanceledMessageFormat, methodTimedOutMessageFormat, testTimeoutInfo).GetAwaiter().GetResult();
    }

    internal static async Task<TestFailedException?> RunWithTimeoutAndCancellationAsync(
        Func<Task> action, CancellationTokenSource cancellationTokenSource, TimeoutInfo? timeoutInfo, MethodInfo methodInfo,
        ExecutionContext? executionContext, string methodCanceledMessageFormat, string methodTimedOutMessageFormat,
        // When a test method is marked with [Timeout], this timeout is applied from ctor to destructor, so we need to take
        // that into account when processing the OCE of the action.
        (CancellationTokenSource TokenSource, int Timeout)? testTimeoutInfo = default)
    {
        if (cancellationTokenSource.Token.IsCancellationRequested)
        {
            return new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodCanceledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
        }

        // If no timeout is specified, we can run the action directly. This avoids any overhead of creating a task/thread and
        // ensures that the execution context is preserved (as we run the action on the current thread).
        if (timeoutInfo is null)
        {
            try
            {
                await ExecutionContextHelpers.RunOnContextAsync(executionContext, action).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
                when (ex.GetRealException().IsOperationCanceledExceptionFromToken(cancellationTokenSource.Token))
            {
                return new(
                    UnitTestOutcome.Timeout,
                    testTimeoutInfo?.TokenSource.Token.IsCancellationRequested == true
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            methodTimedOutMessageFormat,
                            methodInfo.DeclaringType!.FullName,
                            methodInfo.Name,
                            testTimeoutInfo.Value.Timeout)
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            methodCanceledMessageFormat,
                            methodInfo.DeclaringType!.FullName,
                            methodInfo.Name));
            }
        }

        if (timeoutInfo.Value.CooperativeCancellation)
        {
            return await RunWithCooperativeCancellationAsync(
                action, executionContext, cancellationTokenSource, timeoutInfo.Value.Timeout, methodInfo, methodCanceledMessageFormat, methodTimedOutMessageFormat).ConfigureAwait(false);
        }

        // We need to start a thread to handle "cancellation" and "timeout" scenarios.
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? RunWithTimeoutAndCancellationWithSTAThread(action, executionContext, cancellationTokenSource, timeoutInfo.Value.Timeout, methodInfo, methodCanceledMessageFormat, methodTimedOutMessageFormat)
            : RunWithTimeoutAndCancellationWithThreadPool(action, executionContext, cancellationTokenSource, timeoutInfo.Value.Timeout, methodInfo, methodCanceledMessageFormat, methodTimedOutMessageFormat);
    }

    private static async Task<TestFailedException?> RunWithCooperativeCancellationAsync(Func<Task> action, ExecutionContext? executionContext, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo, string methodCanceledMessageFormat, string methodTimedOutMessageFormat)
    {
        CancellationTokenSource? timeoutTokenSource = null;
        try
        {
            timeoutTokenSource = new(timeout);
            timeoutTokenSource.Token.Register(cancellationTokenSource.Cancel);
            if (timeoutTokenSource.Token.IsCancellationRequested)
            {
                return new(
                    UnitTestOutcome.Timeout,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        methodTimedOutMessageFormat,
                        methodInfo.DeclaringType!.FullName,
                        methodInfo.Name,
                        timeout));
            }

            try
            {
                await ExecutionContextHelpers.RunOnContextAsync(executionContext, action).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex) when (ex.IsOperationCanceledExceptionFromToken(cancellationTokenSource.Token))
            {
                // Ideally we would like to check that the token of the exception matches cancellationTokenSource but TestContext
                // instances are not well defined so we have to handle the exception entirely.
                return new(
                    UnitTestOutcome.Timeout,
                    timeoutTokenSource.Token.IsCancellationRequested
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            methodTimedOutMessageFormat,
                            methodInfo.DeclaringType!.FullName,
                            methodInfo.Name,
                            timeout)
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            methodCanceledMessageFormat,
                            methodInfo.DeclaringType!.FullName,
                            methodInfo.Name));
            }
        }
        finally
        {
            timeoutTokenSource?.Dispose();
        }
    }

    private static TestFailedException? RunWithTimeoutAndCancellationWithThreadPool(
        Func<Task> action, ExecutionContext? executionContext, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo,
        string methodCanceledMessageFormat, string methodTimedOutMessageFormat)
    {
        Exception? realException = null;
        Task? executionTask;
        try
        {
            executionTask = Task.Run(
                () =>
                {
                    try
                    {
                        ExecutionContextHelpers.RunOnContextAsync(executionContext, action).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        realException = ex;
                        throw;
                    }
                }, cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            return new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodCanceledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
        }

        try
        {
            if (executionTask.Wait(timeout, cancellationTokenSource.Token))
            {
                // If we reach this line then the task has completed successfully.
                return null;
            }

            // Timed out. For cancellation, either OCE or AggregateException with TCE will be thrown.
            return new(
                UnitTestOutcome.Timeout,
                string.Format(
                    CultureInfo.InvariantCulture,
                    methodTimedOutMessageFormat,
                    methodInfo.DeclaringType!.FullName,
                    methodInfo.Name,
                    timeout));
        }
        catch (Exception ex) when (ex.IsOperationCanceledExceptionFromToken(cancellationTokenSource.Token))
        {
            return new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodCanceledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
        }
        catch (Exception) when (realException is not null)
        {
            // We throw the real exception to have the original stack trace to elaborate up the chain.
            throw realException;
        }
    }

    [SupportedOSPlatform("windows")]
    private static TestFailedException? RunWithTimeoutAndCancellationWithSTAThread(
        Func<Task> action, ExecutionContext? executionContext, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo,
        string methodCanceledMessageFormat, string methodTimedOutMessageFormat)
    {
        TaskCompletionSource<int> tcs = new();
        Exception? realException = null;
        Thread executionThread = new(() =>
        {
            try
            {
                ExecutionContextHelpers.RunOnContextAsync(executionContext, action).GetAwaiter().GetResult();
                tcs.SetResult(0);
            }
            catch (Exception ex)
            {
                realException = ex;
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "MSTest Fixture Execution Thread",
        };

        executionThread.SetApartmentState(ApartmentState.STA);
        executionThread.Start();

        try
        {
            // If the execution thread completes before the timeout, the task will return true, otherwise false.
            if (tcs.Task.Wait(timeout, cancellationTokenSource.Token))
            {
                return null;
            }

            // Timed out. For cancellation, either OCE or AggregateException with TCE will be thrown.
            return new(
                UnitTestOutcome.Timeout,
                string.Format(
                    CultureInfo.InvariantCulture,
                    methodTimedOutMessageFormat,
                    methodInfo.DeclaringType!.FullName,
                    methodInfo.Name,
                    timeout));
        }
        catch (Exception ex) when (ex.IsOperationCanceledExceptionFromToken(cancellationTokenSource.Token))
        {
            return new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodCanceledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
        }
        catch (Exception) when (realException is not null)
        {
            // We throw the real exception to have the original stack trace to elaborate up the chain.
            throw realException;
        }
        catch (Exception) when (tcs.Task.Exception is not null)
        {
            // The tcs.Task.Exception can be an AggregateException, so we favor the "real exception".
            throw tcs.Task.Exception;
        }
    }
}
