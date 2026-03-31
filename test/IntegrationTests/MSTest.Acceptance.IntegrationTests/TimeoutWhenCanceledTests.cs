// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TimeoutWhenCanceledTests : AcceptanceTestBase<TimeoutWhenCanceledTests.TestAssetFixture>
{
    private static readonly Dictionary<string, (string MethodFullName, string Prefix, string EnvVarSuffix, string RunSettingsEntryName)> InfoByKind = new()
    {
        ["assemblyInit"] = ("TestClass.AssemblyInit", "Assembly initialize", "ASSEMBLYINIT", "AssemblyInitializeTimeout"),
        ["classInit"] = ("TestClass.ClassInit", "Class initialize", "CLASSINIT", "ClassInitializeTimeout"),
        ["baseClassInit"] = ("TestClassBase.ClassInitBase", "Class initialize", "BASE_CLASSINIT", "ClassInitializeTimeout"),
    };

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyInit_WhenTestContextCanceled_AssemblyInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(tfm, "TESTCONTEXT_CANCEL_", "assemblyInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInit_WhenTestContextCanceled_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(tfm, "TESTCONTEXT_CANCEL_", "classInit");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ClassInitBase_WhenTestContextCanceled_ClassInitializeTaskIsCanceled(string tfm)
        => await RunAndAssertTestWasCanceledAsync(tfm, "TESTCONTEXT_CANCEL_", "baseClassInit");

    private static async Task RunAndAssertTestWasCanceledAsync(string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} method '{InfoByKind[entryKind].MethodFullName}' was canceled");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TimeoutCodeWithSixtySecTimeout";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", "[Timeout(60000)]")
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
