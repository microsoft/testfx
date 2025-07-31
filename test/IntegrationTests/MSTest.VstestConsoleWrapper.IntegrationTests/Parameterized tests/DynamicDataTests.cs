// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

[TestClass]
public class DynamicDataTests : CLITestBase
{
    private const string TestAssetName = "DynamicDataTestProject";

    [TestMethod]
    public void ExecuteDynamicDataTests()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            [TestAssetName],
            testCaseFilter: "ClassName=DataSourceTestProject.DynamicDataTests");

        // Assert
        ValidatePassedTests(
            "DynamicDataTest_SourceMethod (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethod (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAuto (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAuto (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodAutoShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceProperty (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceProperty (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAuto (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAuto (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoFromBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoFromBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoShadowingBase (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyAutoShadowingBase (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayNameOtherType with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethod_CustomDisplayNameOtherType with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayNameOtherType with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceProperty_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTest_SourceMethodOtherType (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceMethodOtherType (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyOtherType (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourcePropertyOtherType (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "Custom DynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayName with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType with 2 parameters",
            "UserDynamicDataTestMethod DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType with 2 parameters",
            "DynamicDataTestWithTestCategory (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTestWithTestCategory (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "StackOverflowException_Example (DataSourceTestProject.DynamicDataTests+ExampleTestCase)",
            "MethodWithOverload (\"1\",1)",
            "MethodWithOverload (\"2\",1)",
            "MethodWithOverload (1,\"0\")",
            "MethodWithOverload (2,\"2\")",
            "DynamicDataTest_SimpleCollection (0)",
            "DynamicDataTest_SimpleCollection (2)",
            "DynamicDataTest_SimpleCollection (4)",
            "DynamicDataTest_SourceFieldExplicit (\"field\",5)",
            "DynamicDataTest_SourceFieldExplicit (\"test\",4)",
            "DynamicDataTest_SourceFieldAutoDetect (\"field\",5)",
            "DynamicDataTest_SourceFieldAutoDetect (\"test\",4)");

        ValidateFailedTestsCount(0);
    }
}
