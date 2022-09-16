// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests;

using Microsoft.MSTestV2.CLIAutomation;

public class CustomTestExecutionExtensibilityTests : CLITestBase
{
    private const string TestAssembly = "FxExtensibilityTestProject.dll";

    public void ExecuteCustomTestExtensibilityTests()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly });

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
            TestAssembly,
            true,
            "CustomTestMethod1 - Execution number 3",
            "CustomTestClass1 - Execution number 3");
    }

    public void ExecuteCustomTestExtensibilityWithTestDataTests()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~CustomTestExTests.CustomTestMethod2");

        ValidatePassedTests(
            "CustomTestMethod2 (B)",
            "CustomTestMethod2 (B)",
            "CustomTestMethod2 (B)");
        ValidateFailedTestsCount(6);
        ValidateFailedTestsContain(
            TestAssembly,
            true,
            "CustomTestMethod2 (A)",
            "CustomTestMethod2 (A)",
            "CustomTestMethod2 (A)",
            "CustomTestMethod2 (C)",
            "CustomTestMethod2 (C)",
            "CustomTestMethod2 (C)");
    }
}
