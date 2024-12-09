// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies how to discover <see cref="ITestDataSource"/> tests.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
#if NET6_0_OR_GREATER
[Obsolete("Attribute is obsolete and will be removed in v4, instead use 'TestDataSourceOptionsAttribute'.", DiagnosticId = "MSTESTOBS")]
#else
[Obsolete("Attribute is obsolete and will be removed in v4, instead use 'TestDataSourceOptionsAttribute'.")]
#endif
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
    /// Gets the discovery option.
    /// </summary>
    public TestDataSourceDiscoveryOption DiscoveryOption { get; }
}
