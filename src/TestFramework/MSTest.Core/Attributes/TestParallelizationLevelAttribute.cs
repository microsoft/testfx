// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// Specification for parallelization level for a test run.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class TestParallelizationLevelAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestParallelizationLevelAttribute"/> class.
        /// </summary>
        /// <param name="level">Number of parallel executions.</param>
        public TestParallelizationLevelAttribute(int level)
        {
            this.ParallelizationLevel = level;
        }

        /// <summary>
        /// Gets the number of parallel executions.
        /// </summary>
        public int ParallelizationLevel
        {
            get;
            private set;
        }
    }
}