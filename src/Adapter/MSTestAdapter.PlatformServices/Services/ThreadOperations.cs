// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        if (cancelToken.IsCancellationRequested)
        {
            return false;
        }

        Thread executionThread = new(new ThreadStart(action))
        {
            IsBackground = true,
            Name = "MSTest Test Execution Thread",
        };

        // This is not ideal and should be replaced with a mechanism that use STA thread only for tests that need it
        // (based on user configuration). We are currently relying on testhost to be STA or MTA and will run ALL tests
        // in this mode which is not ideal for different hosting mechanism not for performance.
        executionThread.SetApartmentState(Thread.CurrentThread.GetApartmentState());
        executionThread.Start();

        try
        {
            var executionTask = Task.Run(() => executionThread.Join(timeout), cancelToken);
            if (executionTask.Wait(timeout, cancelToken))
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
#else
        try
        {
            var executionTask = Task.Run(action, cancelToken);
            if (executionTask.Wait(timeout, cancelToken))
            {
                return true;
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
#endif
    }
}
