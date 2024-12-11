// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    /// configuration is to unfold.
    /// </summary>
    Auto,

    /// <summary>
    /// Each data row is treated as a separate test case.
    /// </summary>
    Unfold,

    /// <summary>
    /// The parameterized test is not unfolded; all data rows are treated as a single test case.
    /// </summary>
    Fold,
}
