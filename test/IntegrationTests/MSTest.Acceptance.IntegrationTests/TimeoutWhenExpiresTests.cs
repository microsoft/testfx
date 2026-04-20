// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TimeoutWhenExpiresTests : AcceptanceTestBase<TimeoutWhenExpiresTests.TestAssetFixture>
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
    public async Task AssemblyInit_WhenTimeoutExpires_AssemblyInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_AssemblyInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "TIMEOUT_", "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpires_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "TIMEOUT_", "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpires_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTimeoutExpiresAndTestContextTokenIsUsed_ClassInitializeExits(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "TIMEOUT_", "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_AssemblyInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_ClassInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_ClassInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "baseClassInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassCleanupBase_WhenTimeoutExpires_ClassCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "baseClassCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "classCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassCleanup_WhenTimeoutExpires_ClassCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "classCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task BaseClassCleanup_WhenTimeoutExpires_ClassCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "baseClassCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "assemblyCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "assemblyCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "testInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestInitialize_WhenTimeoutExpires_TestInitializeIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "testInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupTaskIsCanceled(string tfm)
        => await RunAndAssertTestTimedOutAsync(tfm, "LONG_WAIT_", "testCleanup");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestCleanup_WhenTimeoutExpires_TestCleanupIsCanceled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertAttributeTakesPrecedenceAsync(tfm, "testCleanup");

    private static async Task RunAndAssertTestTimedOutAsync(string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' timed out after 1000ms");
    }

    private static async Task RunAndAssertAttributeTakesPrecedenceAsync(string tfm, string entryKind)
    {
        string runSettingsEntry = InfoByKind[entryKind].RunSettingsEntryName;
        string runSettings = $"""
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <{runSettingsEntry}>25000</{runSettingsEntry}>
    </MSTest>
</RunSettings>
""";

        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.runsettings");
        File.WriteAllText(runSettingsFilePath, runSettings);

        var stopwatch = Stopwatch.StartNew();
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new() { { $"TIMEOUT_{InfoByKind[entryKind].EnvVarSuffix}", "1" } });
        stopwatch.Stop();

        Assert.IsLessThan(25, stopwatch.Elapsed.TotalSeconds);
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' timed out after 1000ms");
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "TimeoutCodeWithOneSecTimeout";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", "[Timeout(1000)]")
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

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

    public TestContext TestContext { get; set; } = default!;
}
