// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.Diagnostics;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Cancellation token supporting cancellation of a test run.
    /// </summary>
    public class TestRunCancellationToken
    {
        /// <summary>
        /// Stores whether the test run is canceled or not.
        /// </summary>
        private bool canceled;

        /// <summary>
        /// Callback to be invoked when canceled.
        /// </summary>
        private Action registeredCallback;

        /// <summary>
        /// Gets a value indicating whether the test run is canceled.
        /// </summary>
        public bool Canceled
        {
            get
            {
                return this.canceled;
            }

            private set
            {
                this.canceled = value;
                if (this.canceled)
                {
                    this.registeredCallback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Cancels the execution of a test run.
        /// </summary>
        public void Cancel()
        {
            this.Canceled = true;
        }

        /// <summary>
        /// Registers a callback method to be invoked when canceled.
        /// </summary>
        /// <param name="callback">Callback delegate for handling cancellation.</param>
        public void Register(Action callback)
        {
            ValidateArg.NotNull(callback, "callback");

            Debug.Assert(this.registeredCallback == null, "Callback delegate is already registered, use a new cancellationToken");

            this.registeredCallback = callback;
        }

        /// <summary>
        /// Unregister the callback method.
        /// </summary>
        public void Unregister()
        {
            this.registeredCallback = null;
        }
    }
}