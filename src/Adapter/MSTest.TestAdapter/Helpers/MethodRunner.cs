// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class MethodRunner
{
    internal static TestFailedException? RunWithTimeoutAndCancellation(
        Action action, CancellationTokenSource cancellationTokenSource, int? timeout, MethodInfo methodInfo,
        string methodCancelledMessageFormat, string methodTimedOutMessageFormat)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? RunWithTimeoutAndCancellationWithCustomThread(action, cancellationTokenSource, timeout, methodInfo, methodCancelledMessageFormat, methodTimedOutMessageFormat)
            : RunWithTimeoutAndCancellationWithThreadPool(action, cancellationTokenSource, timeout, methodInfo, methodCancelledMessageFormat, methodTimedOutMessageFormat);

    private static TestFailedException? RunWithTimeoutAndCancellationWithThreadPool(
        Action action, CancellationTokenSource cancellationTokenSource, int? timeout, MethodInfo methodInfo,
        string methodCancelledMessageFormat, string methodTimedOutMessageFormat)
    {
        Exception? realException = null;
        Task? executionTask = null;
        try
        {
            executionTask = Task.Run(
                () =>
                {
                    try
                    {
                        action();
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
            if (!timeout.HasValue)
            {
                executionTask.Wait(cancellationTokenSource.Token);

                // If we reach this line then the task has completed successfully.
                // If the cancellation was requested, then either OCE or AggregateException with TCE will be thrown.
                return null;
            }

            if (executionTask.Wait(timeout.Value, cancellationTokenSource.Token))
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
                    methodInfo.Name));
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
    private static TestFailedException? RunWithTimeoutAndCancellationWithCustomThread(
        Action action, CancellationTokenSource cancellationTokenSource, int? timeout, MethodInfo methodInfo,
        string methodCancelledMessageFormat, string methodTimedOutMessageFormat)
    {
        Exception? realException = null;
        Thread executionThread = new(new ThreadStart(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                realException = ex;
                throw;
            }
        }))
        {
            IsBackground = true,
            Name = "MSTest Fixture Execution Thread",
        };

        executionThread.SetApartmentState(ApartmentState.STA);
        executionThread.Start();

        try
        {
            // Creates a Task<bool> that represents the results of the execution thread.
            var executionTask = Task.Run(
                () =>
                {
                    if (timeout.HasValue)
                    {
                        return executionThread.Join(timeout.Value);
                    }

                    executionThread.Join();
                    return true;
                },
                cancellationTokenSource.Token);
            executionTask.Wait(cancellationTokenSource.Token);

            // If the execution thread completes before the timeout, the task will return true, otherwise false.
            if (executionTask.Result)
            {
                return realException is not null
                    ? throw realException
                    : null;
            }

            // Timed out. For cancellation, either OCE or AggregateException with TCE will be thrown.
            return new(
                UnitTestOutcome.Timeout,
                string.Format(
                    CultureInfo.InvariantCulture,
                    methodTimedOutMessageFormat,
                    methodInfo.DeclaringType!.FullName,
                    methodInfo.Name));
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
            if (realException is not null)
            {
                throw realException;
            }

            throw;
        }
    }
}
