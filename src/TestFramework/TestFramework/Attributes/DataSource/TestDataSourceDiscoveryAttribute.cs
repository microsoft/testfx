// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies how to discover <see cref="ITestDataSource"/> tests.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class TestDataSourceDiscoveryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataSourceDiscoveryAttribute"/> class.
    /// </summary>
    /// <param name="discoveryOption">
    /// The <see cref="TestDataSourceDiscoveryOption"/> to use when discovering <see cref="ITestDataSource"/> tests.
    /// </param>
    public TestDataSourceDiscoveryAttribute(TestDataSourceDiscoveryOption discoveryOption)
        => DiscoveryOption = discoveryOption;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataSourceDiscoveryAttribute"/> class.
    /// Allows to control parameterized tests expansion. When expanded, each data source entry is considered as a
    /// different test. Otherwise, multiple results are associated with the same test.
    /// </summary>
    /// <param name="expandDataSource">Define whether or not to expand data source during discovery.</param>
    /// <remarks>
    /// When a test is expanded, the associated data are serialized using DataContractSerializer which could cause issue
    /// if your data is not serializable.
    /// </remarks>
    public TestDataSourceDiscoveryAttribute(bool expandDataSource)
        => DiscoveryOption = expandDataSource
            ? TestDataSourceDiscoveryOption.DuringDiscovery
            : TestDataSourceDiscoveryOption.DuringExecution;

    /// <summary>
    /// Gets the discovery option.
    /// </summary>
    public TestDataSourceDiscoveryOption DiscoveryOption { get; }
}
