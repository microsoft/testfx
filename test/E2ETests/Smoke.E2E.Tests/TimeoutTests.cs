// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests;

using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.MSTestV2.CLIAutomation;

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

    public void ValidateTestRunLifecycle_net462()
    {
        ValidateTestRunLifecycle("net462");
    }

    public void ValidateTestRunLifecycle_netcoreapp31()
    {
        ValidateTestRunLifecycle("netcoreapp3.1");
    }

    private void ValidateTimeoutTests(string targetFramework)
    {
        InvokeVsTestForExecution(new string[] { targetFramework + "\\" + TimeoutTestAssembly }, testCaseFilter: "TimeoutTestClass");

        ValidateFailedTestsCount(targetFramework == "net462" ? 7 : 6);

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
        Verify(File.Exists(GetAssetFullPath(targetFramework + "\\" + "TimeoutTestOutput.txt")));
    }

    private void ValidateTestRunLifecycle(string targetFramework)
    {
        InvokeVsTestForExecution(new[] { targetFramework + "\\" + TimeoutTestAssembly }, testCaseFilter: "TestRunLifecycle");
        Verify(_runEventsHandler.FailedTests.Count == 5);

        var caseWithWaitInCtor = _runEventsHandler.FailedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("TestRunLifecycle_SyncWaitInCtor"));
        Verify(caseWithWaitInCtor.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed);
        Verify(caseWithWaitInCtor.Messages.Single().Text.Contains(
            """
            Ctor was called
            """));

        var caseWithWaitInTestInitialize = _runEventsHandler.FailedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("TestRunLifecycle_SyncWaitInTestInitialize"));
        Verify(caseWithWaitInTestInitialize.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed);
        Verify(caseWithWaitInTestInitialize.Messages.Single().Text.Contains(
            """
            Ctor was called
            TestInitialize was called
            TestCleanup was called
            Dispose was called
            """));

        var caseWithWaitInTestMethod = _runEventsHandler.FailedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("TestRunLifecycle_SyncWaitInTestMethod"));
        Verify(caseWithWaitInTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed);
        Verify(caseWithWaitInTestMethod.Messages.Single().Text.Contains(
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var caseWithWaitInTestCleanup = _runEventsHandler.FailedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("TestRunLifecycle_SyncWaitInTestCleanup"));
        Verify(caseWithWaitInTestCleanup.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed);
        Verify(caseWithWaitInTestCleanup.Messages.Single().Text.Contains(
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            """));

        var caseWithWaitInDispose = _runEventsHandler.FailedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("TestRunLifecycle_SyncWaitInDispose"));
        Verify(caseWithWaitInDispose.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed);
        Verify(caseWithWaitInDispose.Messages.Single().Text.Contains(
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));
    }
}
