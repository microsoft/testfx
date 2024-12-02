// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.IntegrationTests;

[TestClass]
public class DataSourceTests : CLITestBase
{
    private const string TestAssetName = "DataSourceTestProject";

    // TODO @haplois | @evangelink: This test fails under CI - will be fixed in a future PR (Marked as private to ignore the test)
#pragma warning disable IDE0051 // Remove unused private members
    private void ExecuteCsvTestDataSourceTests()
#pragma warning restore IDE0051 // Remove unused private members
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "CsvTestMethod");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(
            testResults,
            "CsvTestMethod (Data Row 0)",
            "CsvTestMethod (Data Row 2)");
        VerifyE2E.ContainsTestsFailed(
            testResults,
            "CsvTestMethod (Data Row 1)",
            "CsvTestMethod (Data Row 3)");
    }
}
