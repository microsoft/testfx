// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    ///  A facade service for options passed to a test method.
    /// </summary>
    internal class TestMethodOptions
    {
        /// <summary>
        /// Gets or sets the timeout specified for a test method.
        /// </summary>
        internal int Timeout { get; set; }

        /// <summary>
        /// Gets or sets the ExpectedException attribute adorned on a test method.
        /// </summary>
        internal ExpectedExceptionBaseAttribute ExpectedException { get; set; }

        /// <summary>
        /// Gets or sets the testcontext passed into the test method.
        /// </summary>
        internal ITestContext TestContext { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug traces should be captured when running the test.
        /// </summary>
        internal bool CaptureDebugTraces { get; set; }

        /// <summary>
        /// Gets or sets the test method executor that invokes the test.
        /// </summary>
        internal TestMethodAttribute Executor { get; set; }
    }
}
