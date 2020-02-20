// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

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
        /// <param name="cancelToken">Token to cancel the execution</param>
        /// <returns>Returns true if the action executed before the timeout. returns false otherwise.</returns>
        public bool Execute(Action action, int timeout, CancellationToken cancelToken)
        {
            var executionTask = Task.Factory.StartNew(action);
            try
            {
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

        /// <summary>
        /// Execute an action with handling for Thread Aborts (if possible) so the main thread of the adapter does not die.
        /// </summary>
        /// <param name="action"> The action to execute. </param>
        public void ExecuteWithAbortSafety(Action action)
        {
            // There is no Thread abort scenarios yet in .Net Core. Once we move the core platform service to support Thread abort related API's
            // then this logic would be similar to the desktop platform service. UWP would then be the only diverging platform service since it does not have Thread APIs exposed.
            action.Invoke();
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
