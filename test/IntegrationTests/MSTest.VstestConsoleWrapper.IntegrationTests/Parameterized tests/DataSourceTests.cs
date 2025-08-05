// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

[TestClass]
public class DataSourceTests : CLITestBase
{
    private const string TestAssetName = "DataSourceTestProject";

    [TestMethod]
    [Ignore("This test fails under CI - will be fixed in a future PR (marked as private to ignore the test)")]
    public void ExecuteCsvTestDataSourceTests()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            [TestAssetName],
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
