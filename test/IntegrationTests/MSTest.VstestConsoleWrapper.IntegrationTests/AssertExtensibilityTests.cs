// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

[TestClass]
public class AssertExtensibilityTests : CLITestBase
{
    private const string TestAssetName = "FxExtensibilityTestProject";

    [TestMethod]
    public void ExecuteAssertExtensibilityTests()
    {
        InvokeVsTestForExecution([TestAssetName]);
        ValidatePassedTestsContain(
            "FxExtensibilityTestProject.AssertExTest.BasicAssertExtensionTest",
            "FxExtensibilityTestProject.AssertExTest.ChainedAssertExtensionTest");

        ValidateFailedTestsContain(
            true,
            "FxExtensibilityTestProject.AssertExTest.BasicFailingAssertExtensionTest",
            "FxExtensibilityTestProject.AssertExTest.ChainedFailingAssertExtensionTest");
    }
}
