// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.IntegrationTests;

public class DynamicDataTests : CLITestBase
{
    private const string TestAssetName = "DynamicDataTestProject";

    public void ExecuteDynamicDataTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, testCaseFilter: "ClassName~DynamicDataTests");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
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
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "MethodWithOverload (\"1\",1)",
            "MethodWithOverload (\"2\",1)",
            "MethodWithOverload (1,\"0\")",
            "MethodWithOverload (2,\"2\")");

        VerifyE2E.FailedTestCount(testResults, 0);
    }

    public void ExecuteDynamicDataTestsWithCategoryFilter()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~DynamicDataWithCategory");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(
            testResults,
            "DynamicDataTestWithTestCategory (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)");

        VerifyE2E.FailedTestCount(testResults, 0);
    }

    public void ExecuteNonExpandableDynamicDataTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, testCaseFilter: "ClassName~DisableExpansionTests");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        Verify(testCases.Length == 6);

        VerifyE2E.TestsPassed(
            testResults,
            "TestPropertySourceOnCurrentType (1,a)",
            "TestPropertySourceOnCurrentType (2,b)",
            "TestPropertySourceOnDifferentType (3,c)",
            "TestPropertySourceOnDifferentType (4,d)",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (1,a)",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (2,b)",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (3,c)",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (4,d)",
            "TestMethodSourceOnDifferentType (3,c)",
            "TestMethodSourceOnDifferentType (4,d)",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (1,a)",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (2,b)",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (3,c)",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (4,d)",
            "TestMethodSourceOnCurrentType (1,a)",
            "TestMethodSourceOnCurrentType (2,b)");
        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
