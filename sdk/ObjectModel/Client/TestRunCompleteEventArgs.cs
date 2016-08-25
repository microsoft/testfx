// ---------------------------------------------------------------------------
// <copyright file="TestRunCompleteEventArgs.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      Event arguments used when a test run has completed.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// Event arguments used when a test run has completed.
    /// </summary>
    public class TestRunCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="stats">The final stats for the test run. This parameter is only set for communications between vstest.discoveryengine and vstest.discoveryengine clients (like VS)</param>
        /// <param name="isAborted">Specifies whether the test run is aborted.</param>
        /// <param name="isCanceled">Specifies whether the test run is canceled.</param>
        /// <param name="error">Specifies the error which occured during the execution of the test run.</param>
        /// <param name="attachmentSets">Attachment sets associated with the run.</param>
        /// <param name="elapsedTime">Time elapsed in just running tests</param>
        public TestRunCompleteEventArgs(ITestRunStatistics stats, bool isCanceled, bool isAborted, Exception error, Collection<AttachmentSet> attachmentSets, TimeSpan elapsedTime)
        {
            TestRunStatistics = stats;
            IsCanceled = isCanceled;
            IsAborted = isAborted;
            Error = error;
            AttachmentSets = attachmentSets;
            ElapsedTimeInRunningTests = elapsedTime;
        }

        /// <summary>
        /// Error which occured during the execution of the test run. Null if there is no error.
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary>
        /// Specifies whether the test run is canceled or not. 
        /// </summary>
        public bool IsCanceled { get; private set; }


        /// <summary>
        /// Specifies whether the test run is aborted. 
        /// </summary>
        public bool IsAborted { get; private set; }

        /// <summary>
        /// Statistics on the state of the test run.
        /// </summary>
        public ITestRunStatistics TestRunStatistics { get; private set; }

        /// <summary>
        /// Attachment Sets associated with the test run. 
        /// </summary>
        public Collection<AttachmentSet> AttachmentSets
        {
            get;
            private set;
        }

        /// <summary>
        /// Time elapsed in just running the tests.
        /// Value is set tp TimeSpan.Zero incase of any error.
        /// </summary>
        public TimeSpan ElapsedTimeInRunningTests
        {
            get;
            private set;
        }
    }
}
