// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

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
    }
}
