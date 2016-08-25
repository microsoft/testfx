// ---------------------------------------------------------------------------
// <copyright file="TestRunStartedEventArgs.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Event arguments used for raising TestRunStarted events.
// </summary>
// <owner>todking</owner> 
// ---------------------------------------------------------------------------
using System;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging
{
    /// <summary>
    /// The process Id of the test execution process running the tests.
    /// </summary>
    public class TestRunStartedEventArgs : EventArgs
    {
        public int ProcessId { get; private set; }

        /// <param name="processId">The process Id of the test execution process running the tests.</param>
        public TestRunStartedEventArgs(int processId)
        {
            ProcessId = processId;
        }

        public override string ToString()
        {
            return "ProcessId = " + ProcessId;
        }
    }
}
