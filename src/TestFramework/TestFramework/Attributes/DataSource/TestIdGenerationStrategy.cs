// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Supported test id generation modes.
/// </summary>
public enum TestIdGenerationStrategy
{
    /// <summary>
    /// Uses legacy test id generation. <see cref="ITestDataSource"/> tests will not be discovered and instead they will be collapsed into one parent test.
    /// </summary>
    /// <remarks>
    /// This option is incompatible with <see cref="TestDataSourceDiscoveryOption.DuringDiscovery"/> option of <see cref="TestDataSourceDiscoveryAttribute"/>. If you set this, that option will be ignored.
    /// This was the default option on version 2.2.3 and before.
    /// </remarks>
    Legacy = 1,

    /// <summary>
    /// Use the display name of a test to generate its id. 
    /// </summary>
    /// <remarks>
    /// This is the default behavior after version 2.2.3.
    /// </remarks>
    DisplayName = 2,

    /// <summary>
    /// Test id generation is identical to <see cref="DisplayName"/>, but when there is data attached to a test that also affect its id.
    /// </summary>
    Data = 3
}
