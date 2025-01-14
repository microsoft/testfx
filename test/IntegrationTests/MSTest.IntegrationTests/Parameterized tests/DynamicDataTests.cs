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
            "DynamicDataTest_SourcePropertyFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAuto (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceMethod (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAuto (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
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
            "DynamicDataTest_SourceMethodFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAuto (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceProperty (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAuto (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyOtherType (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "MethodWithOverload (\"1\",1)",
            "MethodWithOverload (\"2\",1)",
            "MethodWithOverload (1,\"0\")",
            "MethodWithOverload (2,\"2\")",
            "DynamicDataTest_SimpleCollection (0)",
            "DynamicDataTest_SimpleCollection (2)",
            "DynamicDataTest_SimpleCollection (4)");

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

    public void ExecuteTestsFailingWhenUsingSerialization()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        ImmutableArray<TestCase> testCases = DiscoverTests(assemblyPath, testCaseFilter: "ClassName~IndexBasedDataTests");
        ImmutableArray<TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "Add_ShouldAddTheExpectedValues (System.Collections.ObjectModel.Collection`1[System.String],System.String[],System.Collections.ObjectModel.Collection`1[System.String])",
            "Add_ShouldAddTheExpectedValues (System.Collections.ObjectModel.Collection`1[System.String],System.String[],System.Collections.ObjectModel.Collection`1[System.String])",
            "Add_ShouldAddTheExpectedValues (System.Collections.ObjectModel.Collection`1[System.String],System.String[],System.Collections.ObjectModel.Collection`1[System.String])",
            "Add_ShouldAddTheExpectedValues (System.Collections.ObjectModel.Collection`1[System.String],System.String[],System.Collections.ObjectModel.Collection`1[System.String])",
            "TestReadonlyCollectionData (,DataRowTestProject.IndexBasedDataTests+MyData)",
            "TestUnlimitedNatural (0,*,False)",
            "TestUnlimitedNatural (0,0,True)",
            "ValidateExMessage (DataRowTestProject.IndexBasedDataTests+InvalidUpdateException: Test exception message)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: -0.150 / 2.030,Vector2D: 4.230 / 6.812,13.1935961)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: -1.000 / -2.000,Vector2D: -3.400 / 2.750,-2.1)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: -22.723 / -78.298,Vector2D: -17.433 / -8.196,1037.82079593)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: 0.000 / 0.000,Vector2D: 0.000 / 0.000,0)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: 0.000 / 0.000,Vector2D: 2.000 / 3.000,0)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: 1.000 / 2.000,Vector2D: 0.000 / 0.000,0)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: 1.000 / 3.000,Vector2D: 1.000 / 2.000,7)",
            "Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product (Vector2D: 3.355 / -2.211,Vector2D: 12.430 / -2.754,47.791744)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: -17.433 / -8.196,-0.45,Vector2D: 7.845 / 3.688)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: -3.400 / 2.750,22.415,Vector2D: -76.211 / 61.641)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: 0.000 / 0.000,-3.5,Vector2D: 0.000 / 0.000)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: 0.000 / 0.000,0,Vector2D: 0.000 / 0.000)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: 1.000 / 2.000,-0.73,Vector2D: -0.730 / -1.460)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: 12.430 / -2.754,1023.56,Vector2D: 12722.851 / -2818.884)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: 2.000 / 3.000,2.1,Vector2D: 4.200 / 6.300)",
            "Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector (Vector2D: 4.230 / 6.812,-13.25,Vector2D: -56.048 / -90.257)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (-0.45,Vector2D: -17.433 / -8.196,Vector2D: 7.845 / 3.688)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (-0.73,Vector2D: 1.000 / 2.000,Vector2D: -0.730 / -1.460)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (-13.25,Vector2D: 4.230 / 6.812,Vector2D: -56.048 / -90.257)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (-3.5,Vector2D: 0.000 / 0.000,Vector2D: 0.000 / 0.000)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (0,Vector2D: 0.000 / 0.000,Vector2D: 0.000 / 0.000)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (1023.56,Vector2D: 12.430 / -2.754,Vector2D: 12722.851 / -2818.884)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (2.1,Vector2D: 2.000 / 3.000,Vector2D: 4.200 / 6.300)",
            "Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector (22.415,Vector2D: -3.400 / 2.750,Vector2D: -76.211 / 61.641)");
        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
