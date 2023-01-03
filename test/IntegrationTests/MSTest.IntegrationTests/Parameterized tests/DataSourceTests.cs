﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;
public class DataSourceTests : CLITestBase
{
    private const string TestAssetName = "DataSourceTestProject";

    // TODO @haplois | @evangelink: This test fails under CI - will be fixed in a future PR (Marked as private to ignore the test)
    private void ExecuteCsvTestDataSourceTests()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "CsvTestMethod");
        var testResults = RunTests(testCases);

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
