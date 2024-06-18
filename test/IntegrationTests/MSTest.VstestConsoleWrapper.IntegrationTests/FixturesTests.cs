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
    private static readonly string PassingTest = "FixturesTestProject1.UnitTest1.PassingTest";

    private readonly string[] _tests = new[]
    {
        AssemblyInitialize,
        AssemblyCleanup,
        ClassInitialize,
        ClassCleanup,
        TestMethod,
        PassingTest,
    };

    public void FixturesDisabled_DoesNotReport_FixtureTests()
    {
        string runSettings = GetRunSettings(false, true, true, true, true, true);

        // Discover tests,
        InvokeVsTestForDiscovery([AssetName], runSettings);
        ValidateDiscoveredTests([TestMethod, PassingTest]);

        // Tests,
        InvokeVsTestForExecution([AssetName], runSettings);
        ValidatePassedTests([TestMethod, PassingTest]);
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

        ValidatePassedTests([AssemblyCleanup]);
        ValidateFailedTests(false, [AssemblyInitialize, TestMethod, PassingTest]);
        ValidateSkippedTests([ClassInitialize, ClassCleanup]);
    }

    public void AssemblyCleanup_OnlyFails_AssemblyCleanup()
    {
        string runSettings = GetRunSettings(true, true, false, true, true, true);

        InvokeVsTestForExecution([AssetName], runSettings);
        ValidatePassedTests([AssemblyInitialize, ClassInitialize, ClassCleanup, PassingTest]);
        // TestMethod fails because AssemblyCleanup is executed after it and hence it fails.
        ValidateFailedTests(false, [AssemblyCleanup, TestMethod]);
    }

    public void ClassInitialize_OnlyFails_ClassInitialize()
    {
        string runSettings = GetRunSettings(true, true, true, false, true, true);

        InvokeVsTestForExecution([AssetName], runSettings);
        ValidateFailedTests(false, [ClassInitialize, TestMethod, PassingTest]);
        ValidatePassedTests([AssemblyInitialize, AssemblyCleanup, ClassCleanup]);
    }

    public void ClassCleanup_OnlyFails_ClassCleanup()
    {
        string runSettings = GetRunSettings(true, true, true, true, false, true);

        InvokeVsTestForExecution([AssetName], runSettings);
        ValidatePassedTests([AssemblyInitialize, AssemblyCleanup, ClassInitialize, PassingTest]);
        // TestMethod fails because ClassCleanup is executed after it and hence it fails.
        ValidateFailedTests(false, [ClassCleanup, TestMethod]);
    }

    public void RunOnlyFixtures_DoesNot_Run_Fixtures()
    {
        string runSettings = GetRunSettings(true, true, true, true, true, true);

        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: nameof(AssemblyInitialize));
        ValidateSkippedTests(AssemblyInitialize);
        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: nameof(AssemblyCleanup));
        ValidateSkippedTests(AssemblyCleanup);
        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: nameof(ClassInitialize));
        ValidateSkippedTests(ClassInitialize);
        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: nameof(ClassCleanup));
        ValidateSkippedTests(ClassCleanup);
        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: "ClassCleanup|AssemblyCleanup");
        ValidateSkippedTests([AssemblyCleanup, ClassCleanup]);
    }

    public void RunSingleTest_Runs_Assembly_And_Class_Fixtures()
    {
        string runSettings = GetRunSettings(true, true, true, true, true, true);

        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: nameof(PassingTest));
        ValidatePassedTests([AssemblyInitialize, AssemblyCleanup, ClassInitialize, ClassCleanup, PassingTest]);
    }

    public void RunSingleTest_AssemblyInitialize_Failure_Skips_ClassFixtures()
    {
        string runSettings = GetRunSettings(true, false, true, true, true, true);

        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: nameof(PassingTest));
        ValidateFailedTests(false, [AssemblyInitialize, PassingTest]);
        ValidatePassedTests([AssemblyCleanup]);
        // Class fixtures are not executed if AssemblyInitialize fails.
        ValidateSkippedTests([ClassInitialize, ClassCleanup]);
    }

    public void RunSingleTest_ClassInitialize_Failure_Runs_AssemblyFixtures()
    {
        string runSettings = GetRunSettings(true, true, true, false, true, true);

        InvokeVsTestForExecution([AssetName], runSettings, testCaseFilter: nameof(PassingTest));
        ValidateFailedTests(false, [ClassInitialize, PassingTest]);
        ValidatePassedTests([AssemblyInitialize, AssemblyCleanup, ClassCleanup]);
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
        <ConsiderFixturesAsSpecialTests>{fixturesEnabled}</ConsiderFixturesAsSpecialTests>
    </MSTest>
</RunSettings>
";
}
