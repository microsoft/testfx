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
public class ThreadOperations : IThreadOperations
{
    /// <summary>
    /// Execute the given action synchronously on a background thread in the given timeout.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="timeout">Timeout for the specified action in milliseconds.</param>
    /// <param name="cancelToken">Token to cancel the execution.</param>
    /// <returns>Returns true if the action executed before the timeout. returns false otherwise.</returns>
    public bool Execute(Action action, int timeout, CancellationToken cancelToken)
    {
#if NETFRAMEWORK
        return ExecuteWithThread(action, timeout, cancelToken);
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? ExecuteWithThread(action, timeout, cancelToken)
            : ExecuteWithThreadPool(action, timeout, cancelToken);
#endif
    }

#if !NETFRAMEWORK
    private static bool ExecuteWithThreadPool(Action action, int timeout, CancellationToken cancellationToken)
    {
        try
        {
            var executionTask = Task.Run(action, cancellationToken);

            // False means execution timed out.
            return executionTask.Wait(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Task execution canceled.
            return false;
        }
    }
#endif

#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    private static bool ExecuteWithThread(Action action, int timeout, CancellationToken cancellationToken)
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
            var executionTask = Task.Run(() => executionThread.Join(timeout), cancellationToken);
            if (executionTask.Wait(timeout, cancellationToken))
            {
                return executionTask.Result;
            }
            else
            {
                // Timed out.
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            // Task execution canceled.
            return false;
        }
    }
}
