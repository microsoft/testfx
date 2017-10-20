// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    /// <summary>
    /// Parallel execution mode.
    /// </summary>
    public enum TestParallelizationMode
    {
        /// <summary>
        /// Test execution is sequential. This is default.
        /// </summary>
        None = 0,

        /// <summary>
        /// Test execution is parallel at class level. Methods of a class can execute in
        /// parallel. Methods of different classes will never execute together.
        /// </summary>
        ClassLevel = 1,

        /// <summary>
        /// Test execution is parallel at method level. All methods can run in parallel.
        /// </summary>
        MethodLevel = 2,
    }
}