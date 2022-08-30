﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DynamicDataExtensibilityTests : CLITestBase
{
    private const string TestAssembly = "FxExtensibilityTestProject.dll";

    [TestMethod]
    public void ExecuteDynamicDataExtensibilityTests()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly });
        ValidatePassedTestsContain(
            "DynamicDataTestMethod1 (string,2,True)",
            "DynamicDataTestMethod2 (string,4,True)",
            "DynamicDataTestMethod3 (string,2,True)",
            "DynamicDataTestMethod3 (string,4,True)");

        ValidatePassedTestsContain(
            "DynamicDataTestMethod4 (string,2,True)",
            "DynamicDataTestMethod5 (string,4,True)",
            "DynamicDataTestMethod6 (string,2,True)",
            "DynamicDataTestMethod6 (string,4,True)");
    }
}
