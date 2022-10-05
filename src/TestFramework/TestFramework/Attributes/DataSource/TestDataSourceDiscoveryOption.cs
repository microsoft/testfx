// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Supported TestDataSource discovery modes.
/// </summary>
public enum TestDataSourceDiscoveryOption
{
    /// <summary>
    /// Discover tests during execution.
    /// </summary>
    /// <remarks>
    /// This was the default option on version 2.2.3 and before.
    /// </remarks>
    DuringExecution = 1,

    /// <summary>
    /// Discover and expand ITestDataSource based tests.
    /// </summary>
    /// <remarks>
    /// This is the default behavior after version 2.2.3.
    /// </remarks>
    DuringDiscovery = 2
}
