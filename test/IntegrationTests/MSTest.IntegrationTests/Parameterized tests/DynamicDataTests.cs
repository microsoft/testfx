// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class DynamicDataTests : CLITestBase
{
    private const string TestAssetName = "DynamicDataTestProject";

    public void ExecuteDynamicDataTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(
            testResults,
            "DynamicDataTest_SourceProperty (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceMethod (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "UserDynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType with 2 parameters",
            "StackOverflowException_Example (DataSourceTestProject.DynamicDataTests+ExampleTestCase)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceMethodOtherType (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodOtherType (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayNameOtherType with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayName with 2 parameters",
            "DynamicDataTest_SourcePropertyOtherType (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "UserDynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayNameOtherType with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceMethod (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceProperty (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyOtherType (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters");

        VerifyE2E.FailedTestCount(testResults, 0);
    }

    public void ExecuteDynamicDataTestsWithCategoryFilter()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~DynamicDataWithCategory");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(
            testResults,
            "DynamicDataTestWithTestCategory (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)");

        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
