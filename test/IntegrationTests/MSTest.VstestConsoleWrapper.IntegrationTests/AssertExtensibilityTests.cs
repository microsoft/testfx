// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTestAdapter.Smoke.E2ETests;
public class AssertExtensibilityTests : CLITestBase
{
    private const string TestAssembly = "FxExtensibilityTestProject.dll";

    public void ExecuteAssertExtensibilityTests()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly });
        ValidatePassedTestsContain(
            "FxExtensibilityTestProject.AssertExTest.BasicAssertExtensionTest",
            "FxExtensibilityTestProject.AssertExTest.ChainedAssertExtensionTest");

        ValidateFailedTestsContain(
            true,
            "FxExtensibilityTestProject.AssertExTest.BasicFailingAssertExtensionTest",
            "FxExtensibilityTestProject.AssertExTest.ChainedFailingAssertExtensionTest");
    }
}
