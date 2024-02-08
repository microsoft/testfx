// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class MethodRunner
{
    internal static TestFailedException? RunWithTimeoutAndCancellation(
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
}
