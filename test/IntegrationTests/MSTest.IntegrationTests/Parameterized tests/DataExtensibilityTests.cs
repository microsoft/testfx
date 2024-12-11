// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.IntegrationTests;

public class DataExtensibilityTests : CLITestBase
{
    private const string TestAssetName = "FxExtensibilityTestProject";

    /*
        Add tests for:
         - Ignored tests are discovered during discovery
         - Ignored tests are not expanded (DataRow, DataSource, etc)
     */

    public void CustomTestDataSourceTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "CustomTestDataSourceTestMethod1");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(testResults, "CustomTestDataSourceTestMethod1 (1,2,3)", "CustomTestDataSourceTestMethod1 (4,5,6)");
    }

    public void CustomEmptyTestDataSourceTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "CustomEmptyTestDataSourceTestMethod");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsFailed(testResults, new string[] { null });
    }

    public void AssertExtensibilityTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "FxExtensibilityTestProject.AssertExTest");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsFailed(testResults, "BasicFailingAssertExtensionTest", "ChainedFailingAssertExtensionTest");
    }

    public void ExecuteCustomTestExtensibilityTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "(Name~CustomTestMethod1)|(Name~CustomTestClass1)");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

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

    public void ExecuteCustomTestExtensibilityWithTestDataTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "Name~CustomTestMethod2");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "CustomTestMethod2 (\"B\")",
            "CustomTestMethod2 (\"B\")",
            "CustomTestMethod2 (\"B\")");

        VerifyE2E.TestsFailed(
            testResults,
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"C\")",
            "CustomTestMethod2 (\"C\")",
            "CustomTestMethod2 (\"C\")");
    }

    public void WhenUsingCustomITestDataSourceWithExpansionDisabled_RespectSetting()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "CustomDisableExpansionTestDataSourceTestMethod1");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        Verify(testCases.Length == 1);

        VerifyE2E.TestsPassed(
            testResults,
            "CustomDisableExpansionTestDataSourceTestMethod1 (1,2,3)",
            "CustomDisableExpansionTestDataSourceTestMethod1 (4,5,6)");

        VerifyE2E.TestsFailed(testResults);
    }
}
