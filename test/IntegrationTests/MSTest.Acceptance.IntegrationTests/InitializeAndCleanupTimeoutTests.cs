// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class InitializeAndCleanupTimeout : AcceptanceTestBase
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

    private readonly TestAssetFixture _testAssetFixture;

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public InitializeAndCleanupTimeout(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTestContextCanceled_AssemblyInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(_testAssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout,
            tfm, "TESTCONTEXT_CANCEL_", "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTimeoutExpires_AssemblyInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout,
            tfm, "LONG_WAIT_", "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_AssemblyInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTestContextCanceled_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(_testAssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout, tfm,
            "TESTCONTEXT_CANCEL_", "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpires_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTestContextCanceled_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(_testAssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout, tfm,
            "TESTCONTEXT_CANCEL_", "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpires_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_FromRunSettings_AssemblyInitializeIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_FromRunSettings_ClassInitializeIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_FromRunSettings_ClassInitializeIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_AssemblyInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_ClassInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_ClassInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanupBase_WhenTimeoutExpires_ClassCleanupTaskIsCanceled(string tfm)
       => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
           "LONG_WAIT_", "baseClassCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "classCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_FromRunSettings_ClassCleanupIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "classCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassCleanup_WhenTimeoutExpires_FromRunSettings_ClassCleanupIsCanceled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "baseClassCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "classCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassCleanup_WhenTimeoutExpires_ClassCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "baseClassCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "assemblyCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_FromRunSettings_AssemblyCleanupIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "assemblyCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "assemblyCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "testInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_FromRunSettings_TestInitializeIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "testInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "testInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "testCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_FromRunSettings_TestCleanupIsCanceled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "testCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "testCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_ASSEMBLYINIT"] = "1" });

        testHostResult.AssertOutputContains("AssemblyInit started");
        testHostResult.AssertOutputContains("Assembly initialize method 'TestClass.AssemblyInit' timed out");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_ASSEMBLYCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("AssemblyCleanup started");
        testHostResult.AssertOutputContains("Assembly cleanup method 'TestClass.AssemblyCleanup' was canceled");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_CLASSINIT"] = "1" });

        testHostResult.AssertOutputContains("ClassInit started");
        testHostResult.AssertOutputContains("Class initialize method 'TestClass.ClassInit' was canceled");
        testHostResult.AssertOutputDoesNotContain("ClassInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassInit completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_CLASSCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("ClassCleanup started");
        testHostResult.AssertOutputContains("Class cleanup method 'TestClass.ClassCleanup' was canceled");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_TESTINIT"] = "1" });

        testHostResult.AssertOutputContains("TestInit started");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.TestInit' was canceled");
        testHostResult.AssertOutputDoesNotContain("TestInit completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_TESTCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("TestCleanup started");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.TestCleanup' was canceled");
        testHostResult.AssertOutputDoesNotContain("TestCleanup completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestMethodTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_TESTMETHOD"] = "1" });

        testHostResult.AssertOutputContains("TestMethod started");
        testHostResult.AssertOutputContains("Test 'TestMethod' execution has been aborted.");
        testHostResult.AssertOutputDoesNotContain("TestMethod completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_ASSEMBLYINIT"] = "1" });

        testHostResult.AssertOutputContains("AssemblyInit started");
        testHostResult.AssertOutputContains("Assembly initialize method 'TestClass.AssemblyInit' timed out");
        testHostResult.AssertOutputContains("AssemblyInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_ASSEMBLYCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("AssemblyCleanup started");
        testHostResult.AssertOutputContains("Assembly cleanup method 'TestClass.AssemblyCleanup' timed out");
        testHostResult.AssertOutputContains("AssemblyCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_CLASSINIT"] = "1" });

        testHostResult.AssertOutputContains("ClassInit started");
        testHostResult.AssertOutputContains("Class initialize method 'TestClass.ClassInit' timed out");
        testHostResult.AssertOutputContains("ClassInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassInit completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_CLASSCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("ClassCleanup started");
        testHostResult.AssertOutputContains("Class cleanup method 'TestClass.ClassCleanup' timed out ");
        testHostResult.AssertOutputContains("ClassCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup completed");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_TESTINIT"] = "1" });

        testHostResult.AssertOutputContains("TestInit started");
        testHostResult.AssertOutputDoesNotContain("TestInit completed");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.TestInit' timed out");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_TESTCLEANUP"] = "1" });

        testHostResult.AssertOutputContains("TestCleanup started");
        testHostResult.AssertOutputDoesNotContain("TestCleanup completed");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.TestCleanup' timed out");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestMethodTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutAssetPath, TestAssetFixture.CooperativeTimeout, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_TESTMETHOD"] = "1" });

        testHostResult.AssertOutputContains("TestMethod started");
        testHostResult.AssertOutputContains("Test 'TestMethod' execution has been aborted.");
    }

    private async Task RunAndAssertTestWasCanceledAsync(string rootFolder, string assetName, string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(rootFolder, assetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' was canceled");
    }

    private async Task RunAndAssertTestTimedOutAsync(string rootFolder, string assetName, string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(rootFolder, assetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' timed out after 1000ms");
    }

    private async Task RunAndAssertWithRunSettingsAsync(string tfm, int timeoutValue, bool assertAttributePrecedence, string entryKind)
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
            ? TestHost.LocateFrom(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm)
            : TestHost.LocateFrom(_testAssetFixture.CodeWithNoTimeoutAssetPath, TestAssetFixture.CodeWithNoTimeout, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.runsettings");
        await File.WriteAllTextAsync(runSettingsFilePath, runSettings);

        var stopwatch = Stopwatch.StartNew();
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new() { { $"TIMEOUT_{InfoByKind[entryKind].EnvVarSuffix}", "1" } });
        stopwatch.Stop();

        if (assertAttributePrecedence)
        {
            Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 25);
        }

        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' timed out after {timeoutValue}ms");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string CodeWithOneSecTimeout = nameof(CodeWithOneSecTimeout);
        public const string CodeWithSixtySecTimeout = nameof(CodeWithSixtySecTimeout);
        public const string CodeWithNoTimeout = nameof(CodeWithNoTimeout);
        public const string CooperativeTimeout = nameof(CooperativeTimeout);

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
    private static TestContext _assemblyTestContext;
    private static TestContext _classTestContext;

    [Timeout(100, CooperativeCancellation = true)]
    [AssemblyInitialize]
    public static async Task AssemblyInit(TestContext testContext)
    {
        _assemblyTestContext = testContext;
        await DoWork("ASSEMBLYINIT", "AssemblyInit", testContext);
    }

    [Timeout(100, CooperativeCancellation = true)]
    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
        => await DoWork("ASSEMBLYCLEANUP", "AssemblyCleanup", _assemblyTestContext);

    [Timeout(100, CooperativeCancellation = true)]
    [ClassInitialize]
    public static async Task ClassInit(TestContext testContext)
    {
        _classTestContext = testContext;
        await DoWork("CLASSINIT", "ClassInit", testContext);
    }

    [Timeout(100, CooperativeCancellation = true)]
    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task ClassCleanup()
        => await DoWork("CLASSCLEANUP", "ClassCleanup", _classTestContext);

    public TestContext TestContext { get; set; }

    [Timeout(100, CooperativeCancellation = true)]
    [TestInitialize]
    public async Task TestInit()
        => await DoWork("TESTINIT", "TestInit", TestContext);

    [Timeout(100, CooperativeCancellation = true)]
    [TestCleanup]
    public async Task TestCleanup()
        => await DoWork("TESTCLEANUP", "TestCleanup", TestContext);

    [Timeout(100, CooperativeCancellation = true)]
    [TestMethod]
    public async Task TestMethod()
        => await DoWork("TESTMETHOD", "TestMethod", TestContext);

    private static async Task DoWork(string envVarSuffix, string stepName, TestContext testContext)
    {
        Console.WriteLine($"{stepName} started");

        if (Environment.GetEnvironmentVariable($"TASKDELAY_{envVarSuffix}") == "1")
        {
            await Task.Delay(10_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            System.Threading.Thread.Sleep(200);
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

        public string CodeWithOneSecTimeoutAssetPath => GetAssetPath(CodeWithOneSecTimeout);

        public string CodeWithSixtySecTimeoutAssetPath => GetAssetPath(CodeWithSixtySecTimeout);

        public string CodeWithNoTimeoutAssetPath => GetAssetPath(CodeWithNoTimeout);

        public string CooperativeTimeoutAssetPath => GetAssetPath(CooperativeTimeout);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
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
        }
    }
}
