// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class FixtureMethodRunner
{
    internal static async SynchronizationContextPreservingTask<TestFailedException?> RunWithTimeoutAndCancellationAsync(
        Func<SynchronizationContextPreservingTask> action, CancellationTokenSource cancellationTokenSource, TimeoutInfo? timeoutInfo, MethodInfo methodInfo,
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

    /// <summary>
    /// Runs an action with cooperative cancellation timeout handling.
    /// </summary>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <param name="cancellationTokenSource">The parent cancellation token source to cancel when timeout occurs.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="action">The action to run. Receives the timeout <see cref="CancellationTokenSource"/>.</param>
    /// <param name="onAlreadyCancelled">Called when the timeout token is already cancelled before the action starts.</param>
    /// <param name="onCancelled">
    /// Called when an <see cref="OperationCanceledException"/> is caught.
    /// The <see langword="bool"/> parameter is <see langword="true"/> if the cancellation was caused by a timeout,
    /// <see langword="false"/> if it was caused by user cancellation.
    /// </param>
    internal static async Task<T> RunWithCooperativeCancellationAsync<T>(
        CancellationTokenSource cancellationTokenSource,
        int timeout,
        Func<CancellationTokenSource, Task<T>> action,
        Func<T> onAlreadyCancelled,
        Func<bool, T> onCancelled)
    {
        CancellationTokenSource? timeoutTokenSource = null;
        try
        {
            timeoutTokenSource = new(timeout);
            timeoutTokenSource.Token.Register(cancellationTokenSource.Cancel);
            if (timeoutTokenSource.Token.IsCancellationRequested)
            {
                return onAlreadyCancelled();
            }

            try
            {
                return await action(timeoutTokenSource).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsOperationCanceledExceptionFromToken(cancellationTokenSource.Token)
                || ex.IsOperationCanceledExceptionFromToken(timeoutTokenSource.Token))
            {
                // The action may cooperatively cancel using either the parent cancellation token (cancelled by the
                // timeout registration above) or the timeout token source it receives, so both are treated as a
                // timeout/cancellation here. Any other OperationCanceledException is surfaced as a regular failure.
                return onCancelled(timeoutTokenSource.Token.IsCancellationRequested);
            }
        }
        finally
        {
            timeoutTokenSource?.Dispose();
        }
    }

    private static async SynchronizationContextPreservingTask<TestFailedException?> RunWithCooperativeCancellationAsync(Func<SynchronizationContextPreservingTask> action, ExecutionContext? executionContext, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo, string methodCanceledMessageFormat, string methodTimedOutMessageFormat)
        => await RunWithCooperativeCancellationAsync<TestFailedException?>(
            cancellationTokenSource,
            timeout,
            async _ =>
            {
                await ExecutionContextHelpers.RunOnContextAsync(executionContext, action).ConfigureAwait(false);
                return null;
            },
            () => new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodTimedOutMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name, timeout)),
            isTimeout => isTimeout
                ? new(UnitTestOutcome.Timeout, string.Format(CultureInfo.InvariantCulture, methodTimedOutMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name, timeout))
                : new(UnitTestOutcome.Timeout, string.Format(CultureInfo.InvariantCulture, methodCanceledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name))).ConfigureAwait(false);

    private static TestFailedException? RunWithTimeoutAndCancellationWithThreadPool(
        Func<SynchronizationContextPreservingTask> action, ExecutionContext? executionContext, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo,
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
        Func<SynchronizationContextPreservingTask> action, ExecutionContext? executionContext, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo,
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
