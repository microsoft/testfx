// ---------------------------------------------------------------------------
// <copyright file="IMultiTestRunRequest.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//    IMultiTestRunRequest which supports running of multiple test runs. 
// </summary>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    public interface IMultipleTestRunRequest
    {
        /// <summary>
        /// Start the session
        /// </summary>
        void StartSession();

        /// <summary>
        /// Create a test run request for specific tests 
        /// </summary>
        ITestRunRequest CreateTestRunRequest(IEnumerable<TestCase> tests);

        /// <summary>
        /// Create a test run request for specific sources. 
        /// </summary>
        ITestRunRequest CreateTestRunRequest(IEnumerable<string> sources);

        /// <summary>
        /// End the session
        /// </summary>
        Collection<AttachmentSet> EndSession();

        /// <summary>
        /// Raised when the test message is received.
        /// </summary>
        event EventHandler<TestRunMessageEventArgs> TestRunMessage;

        /// <summary>
        /// Raised when data collection message is received.
        /// </summary>
        event EventHandler<DataCollectionMessageEventArgs> DataCollectionMessage;
    }
}