// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies how to generate test ID.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class TestIdGenerationStrategyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestIdGenerationStrategyAttribute"/> class.
    /// </summary>
    /// <param name="strategy">
    /// The <see cref="TestIdGenerationStrategy"/> to use when generating the test ID.
    /// </param>
    public TestIdGenerationStrategyAttribute(TestIdGenerationStrategy strategy)
    {
        Strategy = strategy;
    }

    /// <summary>
    /// Gets the test ID generation strategy.
    /// </summary>
    public TestIdGenerationStrategy Strategy { get; }
}
