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
    [Obsolete("This strategy is provided to reduce impact on existing users. It will be removed in a future release.")]
    Legacy = 0,

    /// <summary>
    /// Uses a combination of executor ID, file name, fully qualified name and display name to generate the test ID.
    /// </summary>
    /// <remarks>
    /// This is the default behavior between versions 2.2.4 and 3.0.0.
    /// </remarks>
    [Obsolete("This strategy is provided to reduce impact on existing users. It will be removed in a future release.")]
    DisplayName = 1,

    /// <summary>
    /// Uses a combination of executor ID, file path, assembly name, method fully qualified name and serialized data (values and their fully qualified type) to generate the test ID.
    /// </summary>
    /// <remarks>
    /// This is the default behavior starting with version 3.0.0.
    /// </remarks>
    FullyQualified = 2,
}
