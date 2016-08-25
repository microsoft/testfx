// ---------------------------------------------------------------------------
// <copyright file="TestCaseStartEventArgs.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     EventArg used for raising testcase start event.
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
    /// EventArg used for raising testcase start event.
    /// </summary>
    public class TestCaseStartEventArgs : EventArgs
    {
        public TestCaseStartEventArgs(TestCase testCase)
        {
            ValidateArg.NotNull<TestCase>(testCase, "testCase");
            TestCase = testCase;
        }

        /// <summary>
        /// Test case
        /// </summary>
        public TestCase TestCase { get; private set; }
    }
}
