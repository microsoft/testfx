// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;
public class CompatTests : CLITestBase
{
    private const string OldAdapterTestProject = "CompatTestProject";
    private const string LatestAdapterTestProject = "DesktopTestProjectx86Debug";

    public void DiscoverCompatTests()
    {
        InvokeVsTestForDiscovery(new string[] { OldAdapterTestProject, LatestAdapterTestProject });
        var listOfTests = new string[]
        {
            "CompatTestProject.UnitTest1.PassingTest",
            "CompatTestProject.UnitTest1.FailingTest",
            "CompatTestProject.UnitTest1.SkippingTest",
            "SampleUnitTestProject.UnitTest1.PassingTest",
            "SampleUnitTestProject.UnitTest1.FailingTest",
            "SampleUnitTestProject.UnitTest1.SkippingTest",
        };
        ValidateDiscoveredTests(listOfTests);
    }

    public void RunAllCompatTests()
    {
        InvokeVsTestForExecution(new string[] { OldAdapterTestProject, LatestAdapterTestProject });

        ValidatePassedTestsContain(
            "CompatTestProject.UnitTest1.PassingTest",
            "SampleUnitTestProject.UnitTest1.PassingTest");

        ValidateFailedTestsContain(
            true,
            "CompatTestProject.UnitTest1.FailingTest");

        ValidateFailedTestsContain(
            true,
            "SampleUnitTestProject.UnitTest1.FailingTest");

        ValidateSkippedTestsContain(
            "CompatTestProject.UnitTest1.SkippingTest",
            "SampleUnitTestProject.UnitTest1.SkippingTest");
    }
}
