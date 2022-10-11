// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTestAdapter.Smoke.E2ETests;
public class TestProjectFSharpTests : CLITestBase
{
    private const string TestAssembly = "FSharpTestProject.dll";

    public void ExecuteCustomTestExtensibilityTests()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly });
        ValidatePassedTestsContain("Test method passing with a . in it");
        ValidateFailedTestsCount(0);
        ValidatePassedTestsCount(1);
    }
}
