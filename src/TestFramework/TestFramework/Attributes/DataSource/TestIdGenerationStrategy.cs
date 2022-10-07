// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The supported test ID generation strategies.
/// </summary>
public enum TestIdGenerationStrategy
{
    /// <summary>
    /// Uses legacy test ID generation. <see cref="ITestDataSource"/> tests will not be discovered and instead they will be collapsed into one parent test.
    /// </summary>
    /// <remarks>
    /// This option is incompatible with <see cref="TestDataSourceDiscoveryOption.DuringDiscovery"/> option of <see cref="TestDataSourceDiscoveryAttribute"/> and will be ignored.
    /// This was the default option until version 2.2.3.
    /// </remarks>
    Legacy = 1,

    /// <summary>
    /// Uses the test display name to generate the test ID. 
    /// </summary>
    /// <remarks>
    /// This is the default behavior between versions 2.2.4 and 3.0.0.
    /// </remarks>
    DisplayName = 2,

    /// <summary>
    /// Uses a combination of test display name and test data to generate the test ID.
    /// </summary>
    /// <remarks>
    /// This is the default behavior starting with version 3.0.0.
    /// </remarks>
    Data = 3,
}
