// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    public void CustomTestDataSourceTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "CustomTestDataSourceTestMethod1");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(testResults, "CustomTestDataSourceTestMethod1 (1,2,3)", "CustomTestDataSourceTestMethod1 (4,5,6)");
    }

    public void AssertExtensibilityTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FxExtensibilityTestProject.AssertExTest");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(testResults, "BasicAssertExtensionTest", "ChainedAssertExtensionTest");
        VerifyE2E.ContainsTestsFailed(testResults, "BasicFailingAssertExtensionTest", "ChainedFailingAssertExtensionTest");
    }

    public void ExecuteCustomTestExtensibilityTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "(Name~CustomTestMethod1)|(Name~CustomTestClass1)");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

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
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "Name~CustomTestMethod2");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "CustomTestMethod2 (\"B\")",
            "CustomTestMethod2 (\"B\")",
            "CustomTestMethod2 (\"B\")");

        VerifyE2E.TestsFailed(
            testResults,
            "CustomTestMethod2 (\"A)\"",
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"C\")",
            "CustomTestMethod2 (\"C\")",
            "CustomTestMethod2 (\"C\")");
    }
}
