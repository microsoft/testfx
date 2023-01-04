// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class DataExtensibilityTests : CLITestBase
{
    private const string TestAssetName = "FxExtensibilityTestProject";

    /*
        Add tests for:
         - Ignored tests are discovered during discovery
         - Ignored tests are not expanded (DataRow, DataSource, etc)
     */

    public async Task CustomTestDataSourceTests()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "CustomTestDataSourceTestMethod1");
        var testResults = await RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(testResults, "CustomTestDataSourceTestMethod1 (1,2,3)", "CustomTestDataSourceTestMethod1 (4,5,6)");
    }

    public async Task AssertExtensibilityTests()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FxExtensibilityTestProject.AssertExTest");
        var testResults = await RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(testResults, "BasicAssertExtensionTest", "ChainedAssertExtensionTest");
        VerifyE2E.ContainsTestsFailed(testResults, "BasicFailingAssertExtensionTest", "ChainedFailingAssertExtensionTest");
    }

    public async Task ExecuteCustomTestExtensibilityTests()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "(Name~CustomTestMethod1)|(Name~CustomTestClass1)");
        var testResults = await RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(
            testResults,
            "CustomTestMethod1 - Execution number 1",
            "CustomTestMethod1 - Execution number 2",
            "CustomTestMethod1 - Execution number 4",
            "CustomTestMethod1 - Execution number 5",
            "CustomTestClass1 - Execution number 1",
            "CustomTestClass1 - Execution number 2",
            "CustomTestClass1 - Execution number 4",
            "CustomTestClass1 - Execution number 5");

        VerifyE2E.ContainsTestsFailed(
            testResults,
            "CustomTestMethod1 - Execution number 3",
            "CustomTestClass1 - Execution number 3");
    }

    public async Task ExecuteCustomTestExtensibilityWithTestDataTests()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "Name~CustomTestMethod2");
        var testResults = await RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "CustomTestMethod2 (B)",
            "CustomTestMethod2 (B)",
            "CustomTestMethod2 (B)");

        VerifyE2E.TestsFailed(
            testResults,
            "CustomTestMethod2 (A)",
            "CustomTestMethod2 (A)",
            "CustomTestMethod2 (A)",
            "CustomTestMethod2 (C)",
            "CustomTestMethod2 (C)",
            "CustomTestMethod2 (C)");
    }
}
