// ---------------------------------------------------------------------------
// <copyright file="ITestDiscoverer.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Interface implemented to provide tests to the test platform.  A class that
//     implements this interface will be available for use if its containing 
//     assembly is either placed in the Extensions folder or is marked as a 'UnitTestExtension' type 
//     in the vsix package.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    /// <summary>
    /// Interface implemented to provide tests to the test platform.  A class that
    //  implements this interface will be available for use if its containing 
    //  assembly is either placed in the Extensions folder or is marked as a 'UnitTestExtension' type 
    //  in the vsix package.
    /// </summary>
    public interface ITestDiscoverer
    {
        /// <summary>
        /// Discovers the tests available from the provided source.
        /// </summary>
        /// <param name="sources">Collection of test containers.</param>
        /// <param name="discoveryContext">Context in which discovery is being performed.</param>
        /// <param name="logger">Logger used to log messages.</param>
        /// <param name="discoverySink">Used to send testcases and discovery related events back to Discoverer manager.</param>
        void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink);
    }
}
