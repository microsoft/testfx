// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Diagnostics;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

    /// <summary>
    /// Wraps calls to ITestExecutionRecorder using types that can be serialized across AppDomains.
    /// </summary>
    internal class TestExecutionRecorderWrapper : MarshalByRefObject
    {
        private readonly ITestExecutionRecorder recorder;

        public TestExecutionRecorderWrapper(ITestExecutionRecorder testExecutionRecorder)
        {
            Debug.Assert(testExecutionRecorder != null, "TestExecutionRecorder should not be null");

            this.recorder = testExecutionRecorder;
        }

        /// <summary>
        /// Returns object to be used for controlling lifetime, null means infinite lifetime.
        /// </summary>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        public override object InitializeLifetimeService()
        {
            return null;
        }

        public void RecordEnd(UnitTestElement test, TestOutcome outcome)
        {
            this.recorder.RecordEnd(test.ToTestCase(), outcome);
        }

        public void RecordStart(UnitTestElement test)
        {
            this.recorder.RecordStart(test.ToTestCase());
        }
    }
}
