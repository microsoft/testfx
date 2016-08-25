// ---------------------------------------------------------------------------
// <copyright file="TestCaseDiscoverySink.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Used by test adapters to send discovered tests and discovery related events back to test manager.
// </summary>
// <owner>dhruvk</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    /// <summary>
    /// TestCaseDiscovery sink is used by discovery extensions to communicate test cases as they are being discovered,
    /// and various discovery related events.
    /// </summary>
    public interface ITestCaseDiscoverySink
    {
        /// <summary>
        /// Callback used by discovery extensions to send back testcases as they are being discovered.
        /// </summary>
        /// <param name="discoveredTests">New test discovered since last invocation.</param>
        void SendTestCase(TestCase discoveredTest);

    }
}
