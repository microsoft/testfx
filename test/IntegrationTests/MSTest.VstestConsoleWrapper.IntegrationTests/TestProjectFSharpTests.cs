﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;
public class TestProjectFSharpTests : CLITestBase
{
    private const string TestAssetName = "FSharpTestProject";

    public void ExecuteCustomTestExtensibilityTests()
    {
        InvokeVsTestForExecution(new string[] { TestAssetName }, targetFramework: "net472");
        ValidatePassedTestsContain("Test method passing with a . in it");
        ValidateFailedTestsCount(0);
        ValidatePassedTestsCount(1);
    }
}
