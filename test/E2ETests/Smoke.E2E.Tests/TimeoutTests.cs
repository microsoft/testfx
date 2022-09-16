// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests;

using System.IO;

using Microsoft.MSTestV2.CLIAutomation;

public class TimeoutTests : CLITestBase
{
    private const string TimeoutTestAssembly = "TimeoutTestProject.dll";
    private const string TimeoutTestAssemblyNetCore = "netcoreapp3.1\\TimeoutTestProjectNetCore.dll";
    private const int TestMethodWaitTimeInMs = 6000;
    private const int OverheadTimeInMs = 2500;
    private const string TimeoutFileToValidateNetCore = "netcoreapp3.1\\TimeoutTestOutputNetCore.txt";
    private const string TimeoutFileToValidate = "TimeoutTestOutput.txt";

    public void ValidateTimeoutTests()
    {
        Validate(TimeoutTestAssembly, TimeoutFileToValidate);
    }

    public void ValidateTimeoutTestsNetCore()
    {
        Validate(TimeoutTestAssemblyNetCore, TimeoutFileToValidateNetCore);
    }

    private void Validate(string testAssembly, string fileToValidate)
    {
        InvokeVsTestForExecution(new string[] { testAssembly });

        ValidateTestRunTime(TestMethodWaitTimeInMs + OverheadTimeInMs);

        ValidateFailedTestsCount(2);

        ValidateFailedTestsContain(
            testAssembly,
            false,
            "TimeoutTestProject.TerminateLongRunningTasksUsingTokenTestClass.TerminateLongRunningTasksUsingToken",
            "TimeoutTestProject.SelfTerminatingTestClass.SelfTerminatingTestMethod");
        // Unable to locate the TimeoutTestOutput.txt file.
        Verify(File.Exists(GetAssetFullPath(fileToValidate)));
    }

    // TODO @haplois | @evangelink: We should add netcoreapp2.1 tests here.
}
