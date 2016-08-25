// ---------------------------------------------------------------------------
// <copyright file="DiscoveryCompleteEventArgs.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//    Event arguments used on completion of discovery
// </summary>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    public class DiscoveryCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// Constructor for creating event args object 
        /// </summary>
        /// <param name="totalTests">Total tests which got discovered</param>
        /// <param name="isAborted">Specifies if discovery has been aborted.</param>
        public DiscoveryCompleteEventArgs(long totalTests, bool isAborted)
        {
            Debug.Assert((isAborted ? -1 == totalTests : true), "If discovery request is aborted totalTest should be -1.");
            this.TotalCount = totalTests;
            this.IsAborted = isAborted;            
        }

        /// <summary>
        ///   Indicates the total tests which got discovered in this request.
        /// </summary>
        public long TotalCount { get; private set; }

        /// <summary>
        /// Specifies if discovery has been aborted. If true TotalCount is also set to -1.
        /// </summary>
        public bool IsAborted { get; private set; }
    }
}
