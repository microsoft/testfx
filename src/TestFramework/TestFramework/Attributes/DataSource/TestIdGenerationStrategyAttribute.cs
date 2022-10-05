// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

/// <summary>
/// Specifies how to calculate id of a test.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class TestIdGenerationStrategyAttribute : Attribute
{
    /// <summary>
    /// Gets the default <see cref="TestIdGenerationStrategy"/>.
    /// </summary>
    public const TestIdGenerationStrategy DefaultStrategy = TestIdGenerationStrategy.Legacy;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestIdGenerationStrategyAttribute"/> class.
    /// </summary>
    /// <param name="strategy">
    /// Sets which <see cref="TestIdGenerationStrategy"/> to use when generating the id of a test.
    /// </param>
    public TestIdGenerationStrategyAttribute(TestIdGenerationStrategy strategy)
    {
        Strategy = strategy;
    }

    /// <summary>
    /// Gets specified strategy option.
    /// </summary>
    public TestIdGenerationStrategy Strategy { get; }
}
