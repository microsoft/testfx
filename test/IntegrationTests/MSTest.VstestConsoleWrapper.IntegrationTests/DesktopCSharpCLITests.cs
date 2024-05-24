// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class DesktopCSharpCLITests : CLITestBase
{
    private const string X86DebugTestProject = "DesktopTestProjectx86Debug";
    private const string X64DebugTestProject = "DesktopTestProjectx64Debug";
    private const string X86ReleaseTestProject = "DesktopTestProjectx86Release";
    private const string X64ReleaseTestProject = "DesktopTestProjectx64Release";
    private const string RunSetting =
        @"<RunSettings>   
                <RunConfiguration>  
                    <TargetPlatform>x64</TargetPlatform>   
                </RunConfiguration>  
            </RunSettings>";

    public void DiscoverTestsx86Debug()
    {
        string[] sources = [X86DebugTestProject];
        DoDiscoveryAndValidateDiscoveredTests(sources);
    }

    public void DiscoverTestsx64Debug()
    {
        string[] sources = [X64DebugTestProject];
        DoDiscoveryAndValidateDiscoveredTests(sources, RunSetting);
    }

    public void DiscoverTestsx86Release()
    {
        string[] sources = [X86ReleaseTestProject];
        DoDiscoveryAndValidateDiscoveredTests(sources);
    }

    public void DiscoverTestsx64Release()
    {
        string[] sources = [X64ReleaseTestProject];
        DoDiscoveryAndValidateDiscoveredTests(sources, RunSetting);
    }

    public void RunAllTestsx86Debug()
    {
        string[] sources = [X86DebugTestProject];
        RunAllTestsAndValidateResults(sources);
    }

    public void RunAllTestsx64Debug()
    {
        string[] sources = [X64DebugTestProject];
        RunAllTestsAndValidateResults(sources, RunSetting);
    }

    public void RunAllTestsx86Release()
    {
        string[] sources = [X86ReleaseTestProject];
        RunAllTestsAndValidateResults(sources);
    }

    public void RunAllTestsx64Release()
    {
        string[] sources = [X64ReleaseTestProject];
        RunAllTestsAndValidateResults(sources, RunSetting);
    }

    private void DoDiscoveryAndValidateDiscoveredTests(string[] sources, string runSettings = "")
    {
        InvokeVsTestForDiscovery(sources, runSettings);
        string[] listOfTests = new string[] { "SampleUnitTestProject.UnitTest1.PassingTest", "SampleUnitTestProject.UnitTest1.FailingTest", "SampleUnitTestProject.UnitTest1.SkippingTest" };
        ValidateDiscoveredTests(listOfTests);
    }

    private void RunAllTestsAndValidateResults(string[] sources, string runSettings = "")
    {
        InvokeVsTestForExecution(sources, runSettings);
        ValidatePassedTests("SampleUnitTestProject.UnitTest1.PassingTest");
        ValidateFailedTests(false, "SampleUnitTestProject.UnitTest1.FailingTest");
        ValidateSkippedTests("SampleUnitTestProject.UnitTest1.SkippingTest");
    }
}
