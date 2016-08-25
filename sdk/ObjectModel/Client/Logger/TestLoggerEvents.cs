// ---------------------------------------------------------------------------
// <copyright file="TestLoggerEvents.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      Exposes events that Test Loggers can register for.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// Exposes events that Test Loggers can register for.
    /// </summary>
    public abstract class TestLoggerEvents
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        protected TestLoggerEvents()
        {
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when a test message is received.
        /// </summary>
        public abstract event EventHandler<TestRunMessageEventArgs> TestRunMessage;

        /// <summary>
        /// Raised when a test result is received.
        /// </summary>
        public abstract event EventHandler<TestResultEventArgs> TestResult;

        /// <summary>
        /// Raised when a test run is complete.
        /// </summary>
        public abstract event EventHandler<TestRunCompleteEventArgs> TestRunComplete;

        #endregion
    }
}
