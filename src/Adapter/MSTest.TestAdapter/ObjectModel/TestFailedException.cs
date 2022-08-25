// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Internal class to indicate Test Execution failure
    /// </summary>
    [Serializable]
    internal class TestFailedException : Exception
    {
        public TestFailedException(UnitTestOutcome outcome, string errorMessage)
            : this(outcome, errorMessage, null, null)
        {
        }

        public TestFailedException(UnitTestOutcome outcome, string errorMessage, StackTraceInformation stackTraceInformation)
            : this(outcome, errorMessage, stackTraceInformation, null)
        {
        }

        public TestFailedException(UnitTestOutcome outcome, string errorMessage, Exception realException)
            : this(outcome, errorMessage, null, realException)
        {
        }

        public TestFailedException(UnitTestOutcome outcome, string errorMessage, StackTraceInformation stackTraceInformation, Exception realException)
            : base(errorMessage, realException)
        {
            Debug.Assert(!string.IsNullOrEmpty(errorMessage), "ErrorMessage should not be empty");

            this.Outcome = outcome;
            this.StackTraceInformation = stackTraceInformation;
        }

        /// <summary>
        /// Gets stack trace information associated with the test failure
        /// </summary>
        public StackTraceInformation StackTraceInformation { get; private set; }

        /// <summary>
        /// Gets outcome of the test case
        /// </summary>
        public UnitTestOutcome Outcome { get; private set; }
    }
}
