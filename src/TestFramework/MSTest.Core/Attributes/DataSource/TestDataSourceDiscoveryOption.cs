// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    /// <summary>
    /// Supported TestDataSource discovery modes
    /// </summary>
    public enum TestDataSourceDiscoveryOption
    {
        /// <summary>
        /// Discover tests during execution.
        /// This was the default option on version 2.2.3 and before.
        /// </summary>
        DuringExecution = 1,

        /// <summary>
        /// Discover and expand ITestDataSource based tests.
        /// This is the default behavior after version 2.2.3.
        /// </summary>
        DuringDiscovery = 2
    }
}
