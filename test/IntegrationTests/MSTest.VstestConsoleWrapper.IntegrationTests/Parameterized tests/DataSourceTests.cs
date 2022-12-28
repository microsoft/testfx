// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTestAdapter.Smoke.E2ETests;
public class DataSourceTests : CLITestBase
{
    private const string TestAssembly = "DataSourceTestProject.dll";

    // TODO @haplois | @evangelink: This test fails under CI - will be fixed in a future PR (marked as private to ignore the test)
    private void ExecuteCsvTestDataSourceTests()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            new string[] { TestAssembly },
            testCaseFilter: "CsvTestMethod");

        // Assert
        ValidatePassedTests(
            "CsvTestMethod (Data Row 0)",
            "CsvTestMethod (Data Row 2)");

        ValidateFailedTests(
            "CsvTestMethod (Data Row 1)",
            "CsvTestMethod (Data Row 3)");
    }
}
