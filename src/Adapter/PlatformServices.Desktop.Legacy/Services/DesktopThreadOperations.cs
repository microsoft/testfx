// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Reflection;
    using System.Threading;

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
            bool executionAborted = false;
            Thread executionThread = new Thread(new ThreadStart(action))
            {
                IsBackground = true,
                Name = "MSTestAdapter Thread"
            };

            executionThread.SetApartmentState(Thread.CurrentThread.GetApartmentState());
            executionThread.Start();
            cancelToken.Register(() =>
            {
                executionAborted = true;
                AbortThread(executionThread);
            });

            if (JoinThread(timeout, executionThread))
            {
                if (executionAborted)
                {
                    return false;
                }

                // Successfully completed
                return true;
            }
            else if (executionAborted)
            {
                // Execution aborted due to user choice
                return false;
            }
            else
            {
                // Timed out
                AbortThread(executionThread);
                return false;
            }
        }

        /// <summary>
        /// Execute an action with handling for Thread Aborts (if possible) so the main thread of the adapter does not die.
        /// </summary>
        /// <param name="action"> The action to execute. </param>
        public void ExecuteWithAbortSafety(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (ThreadAbortException exception)
            {
                Thread.ResetAbort();

                // Throwing an exception so that the test is marked as failed.
                // This is a TargetInvocation exception because we want just the ThreadAbort exception to be shown to the user and not something we create here.
                // TargetInvocation exceptions are stripped off by the test failure handler surfacing the actual exception.
                throw new TargetInvocationException(exception);
            }
        }

        private static bool JoinThread(int timeout, Thread executionThread)
        {
            try
            {
                return executionThread.Join(timeout);
            }
            catch (ThreadStateException)
            {
                // Join was called on a thread not started
            }

            return false;
        }

        private static void AbortThread(Thread executionThread)
        {
            try
            {
                // Abort test thread after timeout.
                executionThread.Abort();
            }
            catch (ThreadStateException)
            {
                // Catch and discard ThreadStateException. If Abort is called on a thread that has been suspended,
                // a ThreadStateException is thrown in the thread that called Abort,
                // and AbortRequested is added to the ThreadState property of the thread being aborted.
                // A ThreadAbortException is not thrown in the suspended thread until Resume is called.
            }
        }
    }
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
