// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTestAdapter.Smoke.E2ETests;
public class TimeoutTests : CLITestBase
{
    private const string TimeoutTestAssembly = "TimeoutTestProject.dll";

    public void ValidateTimeoutTests_net462()
    {
        ValidateTimeoutTests("net462");
    }

    public void ValidateTimeoutTests_netcoreapp31()
    {
        ValidateTimeoutTests("netcoreapp3.1");
    }

    private void ValidateTimeoutTests(string targetFramework)
    {
        InvokeVsTestForExecution(new string[] { targetFramework + "\\" + TimeoutTestAssembly }, testCaseFilter: "TimeoutTest|RegularTest");

        ValidateFailedTestsCount(targetFramework == "net462" ? 5 : 4);

        var failedTests = new List<string>
        {
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenUserCancelsTestContextToken_AbortTest",
            "TimeoutTestProject.TimeoutTestClass.RegularTest_WhenUserCancelsTestContextToken_TestContinues",
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenTimeoutReached_CancelsTestContextToken",
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenTimeoutReached_ForcesTestAbort",
        };

        if (targetFramework == "net462")
        {
            failedTests.Add("TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenUserCallsThreadAbort_AbortTest");
        }

        ValidateFailedTestsContain(TimeoutTestAssembly, false, failedTests.ToArray());

        // We should find the <TargetFramework>/TimeoutTestOutput.txt file, as it's our way to validate
        // that when the timeout expires it cancels the test context token.
        File.Exists(GetAssetFullPath(targetFramework + "\\" + "TimeoutTestOutput.txt")).Should().BeTrue();
    }
}
