// ---------------------------------------------------------------------------
// <copyright file="TestCaseEndEventArgs.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     EventArg used for raising testcase end event.
// </summary>
// <owner>dhruvk</owner> 
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging
{
    /// <summary>
    /// EventArg used for raising testcase end event.
    /// </summary>
    public class TestCaseEndEventArgs : EventArgs
    {
        public TestCaseEndEventArgs(TestCase testCase, TestOutcome outcome)
        {
            ValidateArg.NotNull<TestCase>(testCase, "testCase");
            TestCase = testCase;
            Outcome = outcome;
        }

        /// <summary>
        /// Test case
        /// </summary>
        public TestCase TestCase { get; private set; }

        /// <summary>
        /// Test outcome
        /// </summary>
        public TestOutcome Outcome { get; private set; }
    }
}
