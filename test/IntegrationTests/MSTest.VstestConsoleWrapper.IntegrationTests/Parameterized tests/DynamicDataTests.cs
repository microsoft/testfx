// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class DynamicDataTests : CLITestBase
{
    private const string TestAssetName = "DynamicDataTestProject";

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
            "DynamicDataTest_SourceProperty (\"John;Doe\",LibProjectReferencedByDataSourceTest.User)",
            "DynamicDataTest_SourceProperty (\"Jane;Doe\",LibProjectReferencedByDataSourceTest.User)",
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
            "MethodWithOverload (2,\"2\")");

        ValidateFailedTestsCount(0);
    }
}
