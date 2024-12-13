// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// This service is responsible for any Async operations specific to a platform.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class ThreadOperations : IThreadOperations
{
    /// <summary>
    /// Execute the given action synchronously on a background thread in the given timeout.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="timeout">Timeout for the specified action in milliseconds.</param>
    /// <param name="cancelToken">Token to cancel the execution.</param>
    /// <returns>Returns true if the action executed before the timeout. returns false otherwise.</returns>
    public bool Execute(Action action, int timeout, CancellationToken cancelToken) =>
#if NETFRAMEWORK
        ExecuteWithCustomThread(action, timeout, cancelToken);
#else
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? ExecuteWithCustomThread(action, timeout, cancelToken)
            : ExecuteWithThreadPool(action, timeout, cancelToken);
#endif

#if !NETFRAMEWORK
    private static bool ExecuteWithThreadPool(Action action, int timeout, CancellationToken cancellationToken)
    {
        try
        {
            var executionTask = Task.Run(action, cancellationToken);

            // False means execution timed out.
            return executionTask.Wait(timeout, cancellationToken);
        }
        catch (Exception ex) when
            ((ex is OperationCanceledException oce && oce.CancellationToken == cancellationToken)

            // This exception occurs when the cancellation happens before the task is actually started.
            || (ex is TaskCanceledException tce && tce.CancellationToken == cancellationToken))
        {
            // Task execution canceled.
            return false;
        }
    }
#endif

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool ExecuteWithCustomThread(Action action, int timeout, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        Thread executionThread = new(new ThreadStart(action))
        {
            IsBackground = true,
            Name = "MSTest Test Execution Thread",
        };

        executionThread.SetApartmentState(Thread.CurrentThread.GetApartmentState());
        executionThread.Start();

        try
        {
            // Creates a Task<bool> that represents the results of the execution thread.
            var executionTask = Task.Run(() => executionThread.Join(timeout), cancellationToken);
            executionTask.Wait(cancellationToken);

            // If the execution thread completes before the timeout, the task will return true, otherwise false.
            return executionTask.Result;
        }
        catch (Exception ex) when
            ((ex is OperationCanceledException oce && oce.CancellationToken == cancellationToken)

            // This exception occurs when the cancellation happens before the task is actually started.
            || (ex is TaskCanceledException tce && tce.CancellationToken == cancellationToken))
        {
            // Task execution canceled.
            return false;
        }
    }
}
