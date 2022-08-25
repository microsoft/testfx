// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

    /// <summary>
    /// The test case discovery sink.
    /// </summary>
    internal class TestCaseDiscoverySink : ITestCaseDiscoverySink
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestCaseDiscoverySink"/> class.
        /// </summary>
        public TestCaseDiscoverySink()
        {
            this.Tests = new Collection<TestCase>();
        }

        /// <summary>
        /// Gets the tests.
        /// </summary>
        public ICollection<TestCase> Tests { get; private set; }

        /// <summary>
        /// Sends the test case.
        /// </summary>
        /// <param name="discoveredTest"> The discovered test. </param>
        public void SendTestCase(TestCase discoveredTest)
        {
            if (discoveredTest != null)
            {
                this.Tests.Add(discoveredTest);
            }
        }
    }
}
