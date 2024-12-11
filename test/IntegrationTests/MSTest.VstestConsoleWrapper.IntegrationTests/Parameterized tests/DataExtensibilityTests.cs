// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class DataExtensibilityTests : CLITestBase
{
    private const string TestAssetName = "FxExtensibilityTestProject";

    public void ExecuteTestDataSourceExtensibilityTests()
    {
        InvokeVsTestForExecution([TestAssetName]);
        ValidatePassedTestsContain("CustomTestDataSourceTestMethod1 (1,2,3)", "CustomTestDataSourceTestMethod1 (4,5,6)");
        ValidateFailedTestsContain(false, "FxExtensibilityTestProject.TestDataSourceExTests.CustomEmptyTestDataSourceTestMethod");
    }

    public void ExecuteDynamicDataExtensibilityTests()
    {
        InvokeVsTestForExecution([TestAssetName]);
        ValidatePassedTestsContain(
            "DynamicDataTestMethod1 (\"string\",2,True)",
            "DynamicDataTestMethod2 (\"string\",4,True)",
            "DynamicDataTestMethod3 (\"string\",2,True)",
            "DynamicDataTestMethod3 (\"string\",4,True)");

        ValidatePassedTestsContain(
            "DynamicDataTestMethod4 (\"string\",2,True)",
            "DynamicDataTestMethod5 (\"string\",4,True)",
            "DynamicDataTestMethod6 (\"string\",2,True)",
            "DynamicDataTestMethod6 (\"string\",4,True)");

        ValidateFailedTestsContain(
            false,
            "FxExtensibilityTestProject.DynamicDataExTests.DynamicEmptyDataTestMethod1",
            "FxExtensibilityTestProject.DynamicDataExTests.DynamicEmptyDataTestMethod2",
            "FxExtensibilityTestProject.DynamicDataExTests.DynamicEmptyDataTestMethod3",
            "FxExtensibilityTestProject.DynamicDataExMoreTests.DynamicEmptyDataTestMethod4",
            "FxExtensibilityTestProject.DynamicDataExMoreTests.DynamicEmptyDataTestMethod5",
            "FxExtensibilityTestProject.DynamicDataExMoreTests.DynamicEmptyDataTestMethod6");
    }

    public void ExecuteCustomTestExtensibilityTests()
    {
        InvokeVsTestForExecution([TestAssetName]);

        ValidatePassedTestsContain(
            "CustomTestMethod1 - Execution number 1",
            "CustomTestMethod1 - Execution number 2",
            "CustomTestMethod1 - Execution number 4",
            "CustomTestMethod1 - Execution number 5",
            "CustomTestClass1 - Execution number 1",
            "CustomTestClass1 - Execution number 2",
            "CustomTestClass1 - Execution number 4",
            "CustomTestClass1 - Execution number 5");
        ValidateFailedTestsContain(
            true,
            "CustomTestMethod1 - Execution number 3",
            "CustomTestClass1 - Execution number 3");
    }

    public void ExecuteCustomTestExtensibilityWithTestDataTests()
    {
        InvokeVsTestForExecution([TestAssetName], testCaseFilter: "FullyQualifiedName~CustomTestExTests.CustomTestMethod2");

        ValidatePassedTests(
            "CustomTestMethod2 (\"B\")",
            "CustomTestMethod2 (\"B\")",
            "CustomTestMethod2 (\"B\")");
        ValidateFailedTestsCount(6);
        ValidateFailedTestsContain(
            true,
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"A\")",
            "CustomTestMethod2 (\"C\")",
            "CustomTestMethod2 (\"C\")",
            "CustomTestMethod2 (\"C\")");
    }
}
