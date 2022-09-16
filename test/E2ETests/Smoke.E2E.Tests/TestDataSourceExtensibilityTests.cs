// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests;

using Microsoft.MSTestV2.CLIAutomation;

public class TestDataSourceExtensibilityTests : CLITestBase
{
    private const string TestAssembly = "FxExtensibilityTestProject.dll";

    public void ExecuteTestDataSourceExtensibilityTests()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly });
        ValidatePassedTestsContain("CustomTestDataSourceTestMethod1 (1,2,3)", "CustomTestDataSourceTestMethod1 (4,5,6)");
    }
}
