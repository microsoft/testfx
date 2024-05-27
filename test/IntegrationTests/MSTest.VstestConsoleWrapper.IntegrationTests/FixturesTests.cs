// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class FixturesTests : CLITestBase
{
    private const string AssetName = "FixturesTestProject";

    private static readonly string AssemblyInitialize = "FixturesTestProject1.UnitTest1.AssemblyInitialize(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext)";
    private static readonly string AssemblyCleanup = "FixturesTestProject1.UnitTest1.AssemblyCleanup";
    private static readonly string ClassInitialize = "FixturesTestProject1.UnitTest1.ClassInitialize(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext)";
    private static readonly string ClassCleanup = "FixturesTestProject1.UnitTest1.ClassCleanup";
    private static readonly string TestMethod = "FixturesTestProject1.UnitTest1.Test";

    private readonly string[] _tests = new[]
    {
        AssemblyInitialize,
        AssemblyCleanup,
        ClassInitialize,
        ClassCleanup,
        TestMethod,
    };

    public void FixturesDisabled_DoesNotReport_FixtureTests()
    {
        string runSettings = GetRunSettings(false, true, true, true, true, true);

        // Discover tests,
        InvokeVsTestForDiscovery([AssetName], runSettings);
        ValidateDiscoveredTests(TestMethod);

        // Tests,
        InvokeVsTestForExecution([AssetName], runSettings);
        ValidatePassedTests(TestMethod);
    }

    public void FixturesEnabled_DoesReport_FixtureTests()
    {
        string runSettings = GetRunSettings(true, true, true, true, true, true);

        // Discover tests,
        InvokeVsTestForDiscovery([AssetName], runSettings);

        ValidateDiscoveredTests(_tests);

        // Run tests,
        InvokeVsTestForExecution([AssetName], runSettings);

        ValidatePassedTests(_tests);
    }

    public void AssemblyInitialize_Fails_TestMethod_Class_Skipped()
    {
        string runSettings = GetRunSettings(true, false, true, true, true, true);

        InvokeVsTestForExecution([AssetName], runSettings);

        ValidateFailedTests(false, [AssemblyInitialize, TestMethod]);
        ValidatePassedTests([AssemblyCleanup]);
        ValidateSkippedTests([ClassInitialize, ClassCleanup]);
    }

    public void AssemblyCleanup_OnlyFails_AssemblyCleanup()
    {
        string runSettings = GetRunSettings(true, true, false, true, true, true);

        InvokeVsTestForExecution([AssetName], runSettings);
        ValidateFailedTests(false, [AssemblyCleanup, TestMethod]);
        ValidatePassedTests([AssemblyInitialize, ClassInitialize, ClassCleanup]);
    }

    private string GetRunSettings(bool fixturesEnabled, bool assemblyInitialize, bool assemblyCleanup, bool classInitialize, bool classCleanup, bool test)
        => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<RunSettings>
    <RunConfiguration>
        <EnvironmentVariables>
            <AssemblyInitialize>{assemblyInitialize}</AssemblyInitialize>
            <AssemblyCleanup>{assemblyCleanup}</AssemblyCleanup>
            <ClassInitialize>{classInitialize}</ClassInitialize>
            <ClassCleanup>{classCleanup}</ClassCleanup>
            <Test>{test}</Test>
        </EnvironmentVariables>
    </RunConfiguration>
    <MSTest>
        <FixturesEnabled>{fixturesEnabled}</FixturesEnabled>
    </MSTest>
</RunSettings>
";
}
