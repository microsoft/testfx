// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class TimeoutTests : CLITestBase
{
    private const string TestAssetName = "TimeoutTestProject";

    public void ValidateTimeoutTests_net462() => ValidateTimeoutTests("net462");

    public void ValidateTimeoutTests_netcoreapp31() => ValidateTimeoutTests("netcoreapp3.1");

    private void ValidateTimeoutTests(string targetFramework)
    {
        InvokeVsTestForExecution([TestAssetName], testCaseFilter: "TimeoutTest|RegularTest", targetFramework: targetFramework);

        ValidateFailedTestsCount(targetFramework == "net462" ? 6 : 4);

        string[] abortedTests =
        [
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenUserCancelsTestContextToken_AbortTest",
            "TimeoutTestProject.TimeoutTestClass.RegularTest_WhenUserCancelsTestContextToken_AbortTest",
        ];
        ValidatedCancellationTokenCancelledTests(abortedTests);

        if (targetFramework == "net462")
        {
            string[] threadAbortedTests =
            [
                "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenUserCallsThreadAbort_AbortTest",
                "TimeoutTestProject.TimeoutTestClass.RegularTest_WhenUserCallsThreadAbort_AbortTest"
            ];
            ValidateThreadAbortedTests(threadAbortedTests);
        }

        string[] failedTests =
        [
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenTimeoutReached_CancelsTestContextToken",
            "TimeoutTestProject.TimeoutTestClass.TimeoutTest_WhenTimeoutReached_ForcesTestAbort",
        ];

        ValidateFailedTestsContain(false, failedTests.ToArray());

        // We should find the <TargetFramework>/TimeoutTestOutput.txt file, as it's our way to validate
        // that when the timeout expires it cancels the test context token.
        string timeoutFile = Path.Combine(
            GetAssetFullPath(TestAssetName, targetFramework: targetFramework),
            "..",
            "TimeoutTestOutput.txt");
        File.Exists(timeoutFile).Should().BeTrue();
    }

    private void ValidatedCancellationTokenCancelledTests(params string[] failedTests)
    {
        foreach (string test in failedTests)
        {
            TestResult testFound = RunEventsHandler.FailedTests.FirstOrDefault(f => test.Equals(f.TestCase?.FullyQualifiedName, StringComparison.Ordinal) ||
                test.Equals(f.DisplayName, StringComparison.Ordinal));
            testFound.Should().NotBeNull("Test '{0}' does not appear in failed tests list.", test);
            testFound.ErrorMessage.Should().EndWith("execution has been aborted.");
        }

        ValidateFailedTestsContain(false, failedTests);
    }

    private void ValidateThreadAbortedTests(params string[] failedTests)
    {
        foreach (string test in failedTests)
        {
            TestResult testFound = RunEventsHandler.FailedTests.FirstOrDefault(f => test.Equals(f.TestCase?.FullyQualifiedName, StringComparison.Ordinal) ||
                test.Equals(f.DisplayName, StringComparison.Ordinal));
            testFound.Should().NotBeNull("Test '{0}' does not appear in failed tests list.", test);
            testFound.ErrorMessage.Should().StartWith("Exception thrown while executing test. If using extension of TestMethodAttribute then please contact vendor. Error message: Thread was being aborted.");
        }

        ValidateFailedTestsContain(false, failedTests);
    }
}
