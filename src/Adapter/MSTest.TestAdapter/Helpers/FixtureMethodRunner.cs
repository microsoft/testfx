﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class FixtureMethodRunner
{
    internal static TestFailedException? RunWithTimeoutAndCancellation(
        Action action, CancellationTokenSource cancellationTokenSource, int? timeout, MethodInfo methodInfo,
        IExecutionContextScope executionContextScope, string methodCancelledMessageFormat, string methodTimedOutMessageFormat)
    {
        // If no timeout is specified, we can run the action directly. This avoids any overhead of creating a task/thread and
        // ensures that the execution context is preserved (as we run the action on the current thread).
        if (timeout is null)
        {
            action();
            return null;
        }

        // We need to start a thread to handle "cancellation" and "timeout" scenarios.
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? RunWithTimeoutAndCancellationWithSTAThread(action, cancellationTokenSource, timeout.Value, methodInfo, executionContextScope, methodCancelledMessageFormat, methodTimedOutMessageFormat)
            : RunWithTimeoutAndCancellationWithThreadPool(action, cancellationTokenSource, timeout.Value, methodInfo, executionContextScope, methodCancelledMessageFormat, methodTimedOutMessageFormat);
    }

    private static TestFailedException? RunWithTimeoutAndCancellationWithThreadPool(
        Action action, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo,
        IExecutionContextScope executionContextScope, string methodCancelledMessageFormat, string methodTimedOutMessageFormat)
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
                        ExecutionContextService.RunActionOnContext(action, executionContextScope);
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
                string.Format(CultureInfo.InvariantCulture, methodCancelledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
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
        catch (Exception ex) when
            (ex is OperationCanceledException
            || (ex is AggregateException aggregateEx && aggregateEx.InnerExceptions.OfType<TaskCanceledException>().Any()))
        {
            return new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodCancelledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
        }
        catch (Exception)
        {
            // We throw the real exception to have the original stack trace to elaborate up the chain.
            if (realException is not null)
            {
                throw realException;
            }

            throw;
        }
    }

#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    private static TestFailedException? RunWithTimeoutAndCancellationWithSTAThread(
        Action action, CancellationTokenSource cancellationTokenSource, int timeout, MethodInfo methodInfo,
        IExecutionContextScope executionContextScope, string methodCancelledMessageFormat, string methodTimedOutMessageFormat)
    {
        TaskCompletionSource<int> tcs = new();
        Thread executionThread = new(() =>
        {
            try
            {
                ExecutionContextService.RunActionOnContext(action, executionContextScope);
                tcs.SetResult(0);
            }
            catch (Exception ex)
            {
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
        catch (Exception ex) when

            // This exception occurs when the cancellation happens before the task is actually started.
            ((ex is TaskCanceledException tce && tce.CancellationToken == cancellationTokenSource.Token)
            || (ex is OperationCanceledException oce && oce.CancellationToken == cancellationTokenSource.Token)
            || (ex is AggregateException aggregateEx && aggregateEx.InnerExceptions.OfType<TaskCanceledException>().Any()))
        {
            return new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodCancelledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
        }
        catch (Exception)
        {
            // We throw the real exception to have the original stack trace to elaborate up the chain.
            if (tcs.Task.Exception is not null)
            {
                throw tcs.Task.Exception;
            }

            throw;
        }
    }
}
