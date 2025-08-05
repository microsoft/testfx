// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace MSTest.IntegrationTests;

[TestClass]
public class DynamicDataTests : CLITestBase
{
    private const string TestAssetName = "DynamicDataTestProject";

    [TestMethod]
    public async Task ExecuteDynamicDataTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, testCaseFilter: "ClassName~DynamicDataTests");
        ImmutableArray<TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTest_SourcePropertyAutoShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyOtherType (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodOtherType (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "UserDynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceMethodFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayName with 2 parameters",
            "MethodWithOverload (1,\"0\")",
            "DynamicDataTest_SourceFieldAutoDetect (\"test\",4)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayName with 2 parameters",
            "DynamicDataTest_SourcePropertyAuto (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "UserDynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SimpleCollection (2)",
            "DynamicDataTest_SimpleCollection (4)",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceMethodAutoShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAuto (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayName with 2 parameters",
            "DynamicDataTest_SourceMethod (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAuto (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "UserDynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourcePropertyAutoShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceFieldExplicit (\"field\",5)",
            "DynamicDataTest_SourceProperty (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "MethodWithOverload (\"2\",1)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "DynamicDataTest_SourceMethodFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAuto (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodOtherType (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayNameOtherType with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "DynamicDataTest_SourceMethodShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "StackOverflowException_Example (DataSourceTestProject.DynamicDataTests+ExampleTestCase)",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayNameOtherType with 2 parameters",
            "MethodWithOverload (\"1\",1)",
            "DynamicDataTest_SourcePropertyShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceProperty (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceFieldExplicit (\"test\",4)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayName with 2 parameters",
            "DynamicDataTest_SourceFieldAutoDetect (\"field\",5)",
            "DynamicDataTest_SourcePropertyOtherType (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "DynamicDataTest_SourceMethodAutoFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "MethodWithOverload (2,\"2\")",
            "DynamicDataTest_SourceMethodShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethod (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SimpleCollection (0)",
            "UserDynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType with 2 parameters");

        VerifyE2E.FailedTestCount(testResults, 0);
    }

    [TestMethod]
    public async Task ExecuteDynamicDataTestsWithCategoryFilter()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~DynamicDataWithCategory");
        ImmutableArray<TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(
            testResults,
            "DynamicDataTestWithTestCategory (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)");

        VerifyE2E.FailedTestCount(testResults, 0);
    }

    [TestMethod]
    public async Task ExecuteNonExpandableDynamicDataTests()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, testCaseFilter: "ClassName~DisableExpansionTests");
        ImmutableArray<TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        Assert.HasCount(6, testCases);

        VerifyE2E.TestsPassed(
            testResults,
            "TestPropertySourceOnCurrentType (1,\"a\")",
            "TestPropertySourceOnCurrentType (2,\"b\")",
            "TestPropertySourceOnDifferentType (3,\"c\")",
            "TestPropertySourceOnDifferentType (4,\"d\")",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (1,\"a\")",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (2,\"b\")",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (3,\"c\")",
            "TestPropertyWithTwoSourcesAndSecondDisablesExpansion (4,\"d\")",
            "TestMethodSourceOnDifferentType (3,\"c\")",
            "TestMethodSourceOnDifferentType (4,\"d\")",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (1,\"a\")",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (2,\"b\")",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (3,\"c\")",
            "TestPropertyWithTwoSourcesAndFirstDisablesExpansion (4,\"d\")",
            "TestMethodSourceOnCurrentType (1,\"a\")",
            "TestMethodSourceOnCurrentType (2,\"b\")");
        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
