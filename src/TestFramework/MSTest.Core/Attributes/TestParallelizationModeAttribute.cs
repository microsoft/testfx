// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// Specification of the parallelization mode.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class TestParallelizationModeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestParallelizationModeAttribute"/> class.
        /// </summary>
        /// <param name="testParallelizationMode">Mode of parallel execution</param>
        public TestParallelizationModeAttribute(TestParallelizationMode testParallelizationMode)
        {
            this.TestParallelizationMode = testParallelizationMode;
        }

        /// <summary>
        /// Gets the mode of parallel execution.
        /// </summary>
        public TestParallelizationMode TestParallelizationMode
        {
            get;
            private set;
        }
    }
}