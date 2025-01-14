// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies the capability of a test data source to define how parameterized tests should be executed, either as
/// individual test cases for each data row or as a single test case. This affects the test results and the UI
/// representation of the tests.
/// </summary>
public interface ITestDataSourceUnfoldingCapability
{
    TestDataSourceUnfoldingStrategy UnfoldingStrategy { get; }
}

/// <summary>
/// Specifies how parameterized tests should be executed, either as individual test cases for each data row or as a
/// single test case. This affects the test results and the UI representation of the tests.
/// </summary>
public enum TestDataSourceUnfoldingStrategy : byte
{
    /// <summary>
    /// MSTest will decide whether to unfold the parameterized test based on value from the assembly level attribute
    /// <see cref="TestDataSourceOptionsAttribute" />. If no assembly level attribute is specified, then the default
    /// configuration is to unfold using <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/>.
    /// </summary>
    Auto,

    /// <inheritdoc cref="UnfoldUsingDataContractJsonSerializer"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use 'UnfoldUsingDataContractJsonSerializer' instead")]
    Unfold,

    /// <summary>
    /// The parameterized test is not unfolded; all data rows are treated as a single test case.
    /// </summary>
    Fold,

    /// <summary>
    /// Each data row is treated as a separate test case, and the data is unfolded using
    /// <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/>.
    /// </summary>
    UnfoldUsingDataContractJsonSerializer,

    /// <summary>
    /// Each data row is treated as a separate test case, and the data is unfolded using the data
    /// source index and data index.
    /// </summary>
    /// <remarks>
    /// Using this strategy will alter the test ID if the data source is reordered, as it depends
    /// on the index of the data. This may affect the ability to track test cases over time.
    /// </remarks>
    UnfoldUsingDataIndex,
}
