// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Defines the strategy for uniquely identifying test data.
/// This is only used when <see cref="ITestDataSourceUnfoldingCapability.UnfoldingStrategy" /> is set to <see cref="TestDataSourceUnfoldingStrategy.Unfold"/>.
/// </summary>
public interface ITestDataIdentifierStrategy
{
    /// <summary>
    /// Gets the strategy used to uniquely identify test data.
    /// </summary>
    /// <remarks>
    /// Altering the strategy may change the test ID generated for each test case, potentially
    /// causing issues if the test is being monitored or tracked over time.
    /// </remarks>
    TestDataIdentifierStrategy IdentifierStrategy { get; }
}

/// <summary>
/// Specifies the available strategies for identifying test data.
/// This is only relevant when <see cref="ITestDataSourceUnfoldingCapability.UnfoldingStrategy" /> is set to <see cref="TestDataSourceUnfoldingStrategy.Unfold"/>.
/// </summary>
public enum TestDataIdentifierStrategy : byte
{
    /// <summary>
    /// Automatically determines the identifier strategy based on the assembly-level attribute
    /// <see cref="TestDataSourceOptionsAttribute" />. If no attribute is specified, the default is
    /// <see cref="DataContractSerialization" />.
    /// </summary>
    Auto,

    /// <summary>
    /// Identifies test data using data contract serialization.
    /// </summary>
    DataContractSerialization,

    /// <summary>
    /// Identifies test data by its index in the data source.
    /// </summary>
    /// <remarks>
    /// Using this strategy will alter the test ID if the data source is reordered, as it depends
    /// on the index of the data. This may affect the ability to track test cases over time.
    /// </remarks>
    DataIndex,
}
