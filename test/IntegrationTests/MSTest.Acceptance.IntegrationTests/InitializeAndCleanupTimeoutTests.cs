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
    public async Task AssemblyInit_WhenTestContextCancelled_AssemblyInitializeTaskIsCancelled(string tfm)
        => await RunAndAssertTestWasCancelledAsync(_testAssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout,
            tfm, "TESTCONTEXT_CANCEL_", "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTimeoutExpires_AssemblyInitializeTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout,
            tfm, "LONG_WAIT_", "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_AssemblyInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTestContextCancelled_ClassInitializeTaskIsCancelled(string tfm)
        => await RunAndAssertTestWasCancelledAsync(_testAssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout, tfm,
            "TESTCONTEXT_CANCEL_", "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpires_ClassInitializeTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTestContextCancelled_ClassInitializeTaskIsCancelled(string tfm)
        => await RunAndAssertTestWasCancelledAsync(_testAssetFixture.CodeWithSixtySecTimeoutAssetPath, TestAssetFixture.CodeWithSixtySecTimeout, tfm,
            "TESTCONTEXT_CANCEL_", "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpires_ClassInitializeTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "TIMEOUT_", "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_FromRunSettings_AssemblyInitializeIsCancelled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_FromRunSettings_ClassInitializeIsCancelled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_FromRunSettings_ClassInitializeIsCancelled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_AssemblyInitializeIsCancelled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "assemblyInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_ClassInitializeIsCancelled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "classInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_ClassInitializeIsCancelled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "baseClassInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanupBase_WhenTimeoutExpires_ClassCleanupTaskIsCancelled(string tfm)
       => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
           "LONG_WAIT_", "baseClassCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "classCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_FromRunSettings_ClassCleanupIsCancelled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "classCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassCleanup_WhenTimeoutExpires_FromRunSettings_ClassCleanupIsCancelled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "baseClassCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupIsCancelled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "classCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassCleanup_WhenTimeoutExpires_ClassCleanupIsCancelled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "baseClassCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "assemblyCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_FromRunSettings_AssemblyCleanupIsCancelled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "assemblyCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupIsCancelled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "assemblyCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "testInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_FromRunSettings_TestInitializeIsCancelled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "testInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeIsCancelled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "testInit");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm,
            "LONG_WAIT_", "testCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_FromRunSettings_TestCleanupIsCancelled(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "testCleanup");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupIsCancelled_AttributeTakesPrecedence(string tfm)
       => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "testCleanup");

    private async Task RunAndAssertTestWasCancelledAsync(string rootFolder, string assetName, string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(rootFolder, assetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' was cancelled");
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

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string CodeWithOneSecTimeout = nameof(CodeWithOneSecTimeout);
        public const string CodeWithSixtySecTimeout = nameof(CodeWithSixtySecTimeout);
        public const string CodeWithNoTimeout = nameof(CodeWithNoTimeout);

        public string CodeWithOneSecTimeoutAssetPath => GetAssetPath(CodeWithOneSecTimeout);

        public string CodeWithSixtySecTimeoutAssetPath => GetAssetPath(CodeWithSixtySecTimeout);

        public string CodeWithNoTimeoutAssetPath => GetAssetPath(CodeWithNoTimeout);

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
        }

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
    }
}
