// ---------------------------------------------------------------------------
// <copyright file="TestExecutionContext.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Stores information about test execution context.
// </summary>
// <owner>satins</owner> 
// ---------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Diagnostics;

    /// <summary>
    /// Stores information about test execution context.
    /// </summary>
    [DataContract]
    public class TestExecutionContext
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="frequencyOfRunStatsChangeEvent">Frequency of run stats event.</param>
        /// <param name="isDebug">Whether execution is in debug mode</param>
        /// <param name="inIsolation">Wheter execution is out of proc</param>
        /// <param name="keepAlive">Whether executor process should be kept running after test run completion</param>
        /// <param name="appxPath">Tests would be executed in Appx environment.</param>
        public TestExecutionContext(long frequencyOfRunStatsChangeEvent, bool isDebug, bool inIsolation, bool keepAlive, string appxPath)
        {
            Debug.Assert(inIsolation || !keepAlive, "KeepAlive could not be set true for inProc execution of tests");

            this.FrequencyOfRunStatsChangeEvent = frequencyOfRunStatsChangeEvent;
            this.IsDebug = isDebug;
            this.InIsolation = inIsolation;
            this.KeepAlive = keepAlive;
            this.AppxPath = appxPath;
            this.InAppx = !string.IsNullOrEmpty(appxPath);
        }

        /// <summary>
        /// Gets whether execution is in debug mode.
        /// </summary>
        [DataMember]
        public bool IsDebug
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets wheter execution is out of proc.
        /// </summary>
        [DataMember]
        public bool InIsolation
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets whether executor process should be kept running after test run completion.
        /// </summary>
        [DataMember]
        public bool KeepAlive
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets frequency of run stats event.
        /// </summary>
        [DataMember]
        public long FrequencyOfRunStatsChangeEvent
        {
            get;
            private set;
        }

        /// <summary>
        /// Whether tests are executing inside Appx.
        /// </summary>
        [DataMember]
        public bool InAppx
        {
            get;
            private set;
        }

        public string AppxPath
        {
            get;
            private set;
        }
    }
}
