// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class TimeoutTests : CLITestBase
{
    private const string TestAssetName = "TimeoutTestProject";

    public void ValidateTimeoutTests_net462() => ValidateTimeoutTests("net462");

    public void ValidateTimeoutTests_netcoreapp31() => ValidateTimeoutTests("netcoreapp3.1");

    private void ValidateTimeoutTests(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetName], testCaseFilter: "TimeoutTest|RegularTest", targetFramework: targetFramework);

        ValidateFailedTestsCount(targetFramework == "net462" ? 5 : 4);

        var failedTests = new List<string>
        {
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenUserCancelsTestContextToken_AbortTest",
            "TimeoutTestProject.TimeoutTestClass.RegularTest_WhenUserCancelsTestContextToken_AbortTest",
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenTimeoutReached_CancelsTestContextToken",
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenTimeoutReached_ForcesTestAbort",
        };

        if (targetFramework == "net462")
        {
            failedTests.Add("TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenUserCallsThreadAbort_AbortTest");
        }

        ValidateFailedTestsContain(false, failedTests.ToArray());

        // We should find the <TargetFramework>/TimeoutTestOutput.txt file, as it's our way to validate
        // that when the timeout expires it cancels the test context token.
        string timeoutFile = Path.Combine(
            GetAssetFullPath(TestAssetName, targetFramework: targetFramework),
            "..",
            "TimeoutTestOutput.txt");
        File.Exists(timeoutFile).Should().BeTrue();
    }
}
