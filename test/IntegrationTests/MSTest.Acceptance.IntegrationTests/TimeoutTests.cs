// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class TimeoutTests : AcceptanceTestBase<TimeoutTests.TestAssetFixture>
{
    private static readonly Dictionary<string, (string MethodFullName, string Prefix, string EnvVarSuffix, string RunSettingsEntryName)> InfoByKind = new()
    {
        ["assemblyInit"] = ("TestClass.AssemblyInit", "Assembly initialize", "ASSEMBLYINIT", "AssemblyInitializeTimeout"),
        ["assemblyCleanup"] = ("TestClass.AssemblyCleanupMethod", "Assembly cleanup", "ASSEMBLYCLEANUP", "AssemblyCleanupTimeout"),
        ["classInit"] = ("TestClass.ClassInit", "Class initialize", "CLASSINIT", "ClassInitializeTimeout"),
        ["baseClassInit"] = ("TestClassBase.ClassInitBase", "Class initialize", "BASE_CLASSINIT", "ClassInitializeTimeout"),
        ["classCleanup"] = ("TestClass.ClassCleanupMethod", "Class cleanup", "CLASSCLEANUP", "ClassCleanupTimeout"),
        ["baseClassCleanup"] = ("TestClassBase.ClassCleanupBase", "Class cleanup", "BASE_CLASSCLEANUP", "ClassCleanupTimeout"),
        ["testInit"] = ("TestClass.TestInit", "Test initialize", "TESTINIT", "TestInitializeTimeout"),
        ["testCleanup"] = ("TestClass.TestCleanupMethod", "Test cleanup", "TESTCLEANUP", "TestCleanupTimeout"),
    };

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTestContextCanceled_AssemblyInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(AssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout,
            tfm, "TESTCONTEXT_CANCEL_", "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTimeoutExpires_AssemblyInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout,
            tfm, "LONG_WAIT_", "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_AssemblyInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTestContextCanceled_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(AssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout, tfm,
            "TESTCONTEXT_CANCEL_", "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpires_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTestContextCanceled_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(AssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout, tfm,
            "TESTCONTEXT_CANCEL_", "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpires_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_FromRunSettings_AssemblyInitializeIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_FromRunSettings_ClassInitializeIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_FromRunSettings_ClassInitializeIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_AssemblyInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_ClassInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_ClassInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassCleanupBase_WhenTimeoutExpires_ClassCleanupTaskIsCanceled(string tfm)
       => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
           "LONG_WAIT_", "baseClassCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "classCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_FromRunSettings_ClassCleanupIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "classCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task BaseClassCleanup_WhenTimeoutExpires_FromRunSettings_ClassCleanupIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "baseClassCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "classCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task BaseClassCleanup_WhenTimeoutExpires_ClassCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "baseClassCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "assemblyCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_FromRunSettings_AssemblyCleanupIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "assemblyCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "assemblyCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "testInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_FromRunSettings_TestInitializeIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "testInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "testInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "testCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_FromRunSettings_TestCleanupIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "testCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "testCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_ASSEMBLYINIT"] = "1" });

        testHostResult.AssertOutputContains("AssemblyInit started");
        testHostResult.AssertOutputContains("Assembly initialize method 'TestClass.AssemblyInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_ASSEMBLYCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("AssemblyCleanup started");
        testHostResult.AssertOutputContains("Assembly cleanup method 'TestClass.AssemblyCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_CLASSINIT"] = "1" });

        testHostResult.AssertOutputContains("ClassInit started");
        testHostResult.AssertOutputContains("Class initialize method 'TestClass.ClassInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("ClassInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_CLASSCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("ClassCleanup started");
        testHostResult.AssertOutputContains("Class cleanup method 'TestClass.ClassCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_TESTINIT"] = "1" });

        testHostResult.AssertOutputContains("TestInit started");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.TestInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("TestInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_TESTCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("TestCleanup started");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.TestCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("TestCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_ASSEMBLYINIT"] = "1" });

        testHostResult.AssertOutputContains("AssemblyInit started");
        testHostResult.AssertOutputContains("Assembly initialize method 'TestClass.AssemblyInit' timed out after 1000ms");
        testHostResult.AssertOutputContains("AssemblyInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_ASSEMBLYCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("AssemblyCleanup started");
        testHostResult.AssertOutputContains("AssemblyCleanup Thread.Sleep completed");
        testHostResult.AssertOutputContains("Assembly cleanup method 'TestClass.AssemblyCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_CLASSINIT"] = "1" });

        testHostResult.AssertOutputContains("ClassInit started");
        testHostResult.AssertOutputContains("Class initialize method 'TestClass.ClassInit' timed out after 1000ms");
        testHostResult.AssertOutputContains("ClassInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_CLASSCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("ClassCleanup started");
        testHostResult.AssertOutputContains("ClassCleanup Thread.Sleep completed");
        testHostResult.AssertOutputContains("Class cleanup method 'TestClass.ClassCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_TESTINIT"] = "1" });

        testHostResult.AssertOutputContains("TestInit started");
        testHostResult.AssertOutputDoesNotContain("TestInit completed");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.TestInit' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_TESTCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("TestCleanup started");
        testHostResult.AssertOutputDoesNotContain("TestCleanup completed");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.TestCleanup' timed out after 1000ms");
    }

    private static async Task RunAndAssertTestWasCanceledAsync(string rootFolder, string assetName, string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(rootFolder, assetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' was canceled");
    }

    private static async Task RunAndAssertTestTimedOutAsync(string rootFolder, string assetName, string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(rootFolder, assetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' timed out after 1000ms");
    }

    private static async Task RunAndAssertWithRunSettingsAsync(string tfm, int timeoutValue, bool assertAttributePrecedence, string entryKind)
    {
        string runSettingsEntry = InfoByKind[entryKind].RunSettingsEntryName;
        string runSettings = $"""
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <{runSettingsEntry}>{timeoutValue}</{runSettingsEntry}>
    </MSTest>
</RunSettings>
""";

        // if assertAttributePrecedence is set we will use CodeWithOneSecTimeoutAssetPath
        timeoutValue = assertAttributePrecedence ? 1000 : timeoutValue;

        TestHost testHost = assertAttributePrecedence
            ? TestHost.LocateFrom(AssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm)
            : TestHost.LocateFrom(AssetFixture.CodeWithNoTimeoutAssetPath, TestAssetFixture.CodeWithNoTimeout, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.runsettings");
        File.WriteAllText(runSettingsFilePath, runSettings);

        var stopwatch = Stopwatch.StartNew();
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new() { { $"TIMEOUT_{InfoByKind[entryKind].EnvVarSuffix}", "1" } });
        stopwatch.Stop();

        if (assertAttributePrecedence)
        {
            Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 25);
        }

        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' timed out after {timeoutValue}ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TimeoutWithInvalidArg_WithoutLetterSuffix_OutputInvalidMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TimeoutWithInvalidArg_WithInvalidLetterSuffix_OutputInvalidMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5y");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TimeoutWithInvalidArg_WithInvalidFormat_OutputInvalidMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5h6m");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenTimeoutValueSmallerThanTestDuration_OutputContainsCancelingMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 1s");

        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.AssertOutputContains("Canceling the test session");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenTimeoutValueGreaterThanTestDuration_OutputDoesNotContainCancelingMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 30s");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult.AssertOutputDoesNotContain("Canceling the test session");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInCtor_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TestMethodTimeoutAssetPath, TestAssetFixture.TestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_CTOR"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInTestInit_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TestMethodTimeoutAssetPath, TestAssetFixture.TestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_TESTINIT"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInTestCleanup_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TestMethodTimeoutAssetPath, TestAssetFixture.TestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_TESTCLEANUP"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInTestMethod_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TestMethodTimeoutAssetPath, TestAssetFixture.TestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_TEST"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeTimeout_WhenMethodTimeoutAndWaitInCtor_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTestMethodTimeoutAssetPath, TestAssetFixture.CooperativeTestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_CTOR"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeTimeout_WhenMethodTimeoutAndWaitInTestInit_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTestMethodTimeoutAssetPath, TestAssetFixture.CooperativeTestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_TESTINIT"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test initialize method 'TimeoutTest.UnitTest1.TestInit' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeTimeout_WhenMethodTimeoutAndWaitInTestCleanup_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTestMethodTimeoutAssetPath, TestAssetFixture.CooperativeTestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_TESTCLEANUP"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test cleanup method 'TimeoutTest.UnitTest1.TestCleanup' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeTimeout_WhenMethodTimeoutAndWaitInTestMethod_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CooperativeTestMethodTimeoutAssetPath, TestAssetFixture.CooperativeTestMethodTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["LONG_WAIT_TEST"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string AssetName = "TimeoutTest";
        public const string CodeWithOneSecTimeout = nameof(CodeWithOneSecTimeout);
        public const string CodeWithSixtySecTimeout = nameof(CodeWithSixtySecTimeout);
        public const string CodeWithNoTimeout = nameof(CodeWithNoTimeout);
        public const string CooperativeTimeout = nameof(CooperativeTimeout);
        public const string TestMethodTimeout = nameof(TestMethodTimeout);
        public const string CooperativeTestMethodTimeout = nameof(CooperativeTestMethodTimeout);

        private const string TestCode = """
#file TimeoutTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>

    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs

using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace TimeoutTest;
[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public void TestA()
    {
        Assert.IsTrue(true);
        Thread.Sleep(10000);
    }
}
""";

        private const string CooperativeTimeoutSourceCode = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file UnitTest1.cs
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [Timeout(1000, CooperativeCancellation = true)]
    [AssemblyInitialize]
    public static async Task AssemblyInit(TestContext testContext)
        => await DoWork("ASSEMBLYINIT", "AssemblyInit", testContext);

    [Timeout(1000, CooperativeCancellation = true)]
    [AssemblyCleanup]
    public static async Task AssemblyCleanup(TestContext testContext)
        => await DoWork("ASSEMBLYCLEANUP", "AssemblyCleanup", testContext);

    [Timeout(1000, CooperativeCancellation = true)]
    [ClassInitialize]
    public static async Task ClassInit(TestContext testContext)
        => await DoWork("CLASSINIT", "ClassInit", testContext);

    [Timeout(1000, CooperativeCancellation = true)]
    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task ClassCleanup(TestContext testContext)
        => await DoWork("CLASSCLEANUP", "ClassCleanup", testContext);

    public TestContext TestContext { get; set; }

    [Timeout(1000, CooperativeCancellation = true)]
    [TestInitialize]
    public async Task TestInit()
        => await DoWork("TESTINIT", "TestInit", TestContext);

    [Timeout(1000, CooperativeCancellation = true)]
    [TestCleanup]
    public async Task TestCleanup()
        => await DoWork("TESTCLEANUP", "TestCleanup", TestContext);

    [TestMethod]
    public void TestMethod()
    {
    }

    private static async Task DoWork(string envVarSuffix, string stepName, TestContext testContext)
    {
        Console.WriteLine($"{stepName} started");

        if (Environment.GetEnvironmentVariable($"TASKDELAY_{envVarSuffix}") == "1")
        {
            await Task.Delay(10_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            // We want to wait more than the timeout value to ensure the timeout is hit
            await Task.Delay(2_000);
            Console.WriteLine($"{stepName} Thread.Sleep completed");
            if (Environment.GetEnvironmentVariable($"CHECKTOKEN_{envVarSuffix}") == "1")
            {
                testContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
            }

        }

        Console.WriteLine($"{stepName} completed");
    }
}
""";

        private const string SourceCode = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class TestClassBase
{
    $TimeoutAttribute$
    [ClassInitialize(inheritanceBehavior: InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassInitBase(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TESTCONTEXT_CANCEL_BASE_CLASSINIT") == "1")
        {
            testContext.CancellationTokenSource.Cancel();
            await Task.Delay(10_000);
        }
        else if (Environment.GetEnvironmentVariable("LONG_WAIT_BASE_CLASSINIT") == "1")
        {
            await Task.Delay(10_000);
        }
        else if (Environment.GetEnvironmentVariable("TIMEOUT_BASE_CLASSINIT") == "1")
        {
            await Task.Delay(10_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    $TimeoutAttribute$
    [ClassCleanup(inheritanceBehavior: InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassCleanupBase()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_BASE_CLASSCLEANUP") == "1" || Environment.GetEnvironmentVariable("TIMEOUT_BASE_CLASSCLEANUP") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

}

[TestClass]
public class TestClass : TestClassBase
{
    $TimeoutAttribute$
    [AssemblyInitialize]
    public static async Task AssemblyInit(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TESTCONTEXT_CANCEL_ASSEMBLYINIT") == "1")
        {
            testContext.CancellationTokenSource.Cancel();
            await Task.Delay(10_000);
        }
        else if (Environment.GetEnvironmentVariable("LONG_WAIT_ASSEMBLYINIT") == "1")
        {
            await Task.Delay(10_000);
        }
        else if (Environment.GetEnvironmentVariable("TIMEOUT_ASSEMBLYINIT") == "1")
        {
            await Task.Delay(60_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    $TimeoutAttribute$
    [AssemblyCleanup]
    public static async Task AssemblyCleanupMethod()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_ASSEMBLYCLEANUP") == "1" || Environment.GetEnvironmentVariable("TIMEOUT_ASSEMBLYCLEANUP") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    $TimeoutAttribute$
    [ClassInitialize]
    public static async Task ClassInit(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TESTCONTEXT_CANCEL_CLASSINIT") == "1")
        {
            testContext.CancellationTokenSource.Cancel();
            await Task.Delay(10_000);
        }
        else if (Environment.GetEnvironmentVariable("LONG_WAIT_CLASSINIT") == "1")
        {
            await Task.Delay(10_000);
        }
        else if (Environment.GetEnvironmentVariable("TIMEOUT_CLASSINIT") == "1")
        {
            await Task.Delay(60_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    $TimeoutAttribute$
    [ClassCleanup]
    public static async Task ClassCleanupMethod()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_CLASSCLEANUP") == "1" || Environment.GetEnvironmentVariable("TIMEOUT_CLASSCLEANUP") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    $TimeoutAttribute$
    [TestInitialize]
    public async Task TestInit()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TESTINIT") == "1" || Environment.GetEnvironmentVariable("TIMEOUT_TESTINIT") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    $TimeoutAttribute$
    [TestCleanup]
    public async Task TestCleanupMethod()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TESTCLEANUP") == "1" || Environment.GetEnvironmentVariable("TIMEOUT_TESTCLEANUP") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    [TestMethod]
    public Task Test1() => Task.CompletedTask;
}
""";

        private const string TestMethodTimeoutCode = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>

    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace TimeoutTest;
[TestClass]
public class UnitTest1
{
    private readonly TestContext _testContext;

    public UnitTest1(TestContext testContext)
    {
        _testContext = testContext;
        if (Environment.GetEnvironmentVariable("LONG_WAIT_CTOR") == "1")
        {
            Task.Delay(10_000, _testContext.CancellationTokenSource.Token).Wait();
        }
    }

    [TestInitialize]
    public async Task TestInit()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TESTINIT") == "1")
        {
            await Task.Delay(10_000, _testContext.CancellationTokenSource.Token);
        }
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TESTCLEANUP") == "1")
        {
            await Task.Delay(10_000, _testContext.CancellationTokenSource.Token);
        }
    }

    [TestMethod]
    [Timeout(1000$TimeoutExtraArgs$)]
    public async Task TestMethod()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TEST") == "1")
        {
            await Task.Delay(10_000, _testContext.CancellationTokenSource.Token);
        }
    }
}
""";

        public string NoExtensionTargetAssetPath => GetAssetPath(AssetName);

        public string CodeWithOneSecTimeoutAssetPath => GetAssetPath(CodeWithOneSecTimeout);

        public string CodeWithSixtySecTimeoutAssetPath => GetAssetPath(CodeWithSixtySecTimeout);

        public string CodeWithNoTimeoutAssetPath => GetAssetPath(CodeWithNoTimeout);

        public string CooperativeTimeoutAssetPath => GetAssetPath(CooperativeTimeout);

        public string TestMethodTimeoutAssetPath => GetAssetPath(TestMethodTimeout);

        public string CooperativeTestMethodTimeoutAssetPath => GetAssetPath(CooperativeTestMethodTimeout);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CodeWithNoTimeout, CodeWithNoTimeout,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", string.Empty)
                .PatchCodeWithReplace("$ProjectName$", CodeWithNoTimeout)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CodeWithOneSecTimeout, CodeWithOneSecTimeout,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", "[Timeout(1000)]")
                .PatchCodeWithReplace("$ProjectName$", CodeWithOneSecTimeout)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CodeWithSixtySecTimeout, CodeWithSixtySecTimeout,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", "[Timeout(60000)]")
                .PatchCodeWithReplace("$ProjectName$", CodeWithSixtySecTimeout)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CooperativeTimeout, CooperativeTimeout,
                CooperativeTimeoutSourceCode
                .PatchCodeWithReplace("$ProjectName$", CooperativeTimeout)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (TestMethodTimeout, TestMethodTimeout,
                TestMethodTimeoutCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", TestMethodTimeout)
                .PatchCodeWithReplace("$TimeoutExtraArgs$", string.Empty)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CooperativeTestMethodTimeout, CooperativeTestMethodTimeout,
                TestMethodTimeoutCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", CooperativeTestMethodTimeout)
                .PatchCodeWithReplace("$TimeoutExtraArgs$", ", CooperativeCancellation = true")
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
