// ---------------------------------------------------------------------------
// <copyright file="DiscoveredTestsEventArgs.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//    Event arguments used to notify the availability of new tests
// </summary>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// Event arguments used to notify the availability of new tests
    /// </summary>
    public partial class DiscoveredTestsEventArgs : EventArgs
    {
        public DiscoveredTestsEventArgs(IEnumerable<TestCase> discoveredTestCases)
        {
            DiscoveredTestCases = discoveredTestCases;
        }
        /// <summary>
        /// Tests discovered in this discovery request
        /// </summary>
        public IEnumerable<TestCase> DiscoveredTestCases { get; private set; }
    }
}
