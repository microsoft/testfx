// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies options for all <see cref="ITestDataSource"/> of the current assembly.
/// </summary>
/// <remarks>
/// These options can be override by individual <see cref="ITestDataSource"/> attribute.</remarks>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
public sealed class TestDataSourceOptionsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataSourceOptionsAttribute"/> class.
    /// </summary>
    /// <param name="unfoldingStrategy">
    /// The <see cref="UnfoldingStrategy"/> to use when executing parameterized tests.
    /// </param>
    public TestDataSourceOptionsAttribute(TestDataSourceUnfoldingStrategy unfoldingStrategy)
        => UnfoldingStrategy = unfoldingStrategy;

    /// <summary>
    /// Gets the test unfolding strategy.
    /// </summary>
    public TestDataSourceUnfoldingStrategy UnfoldingStrategy { get; }
}
