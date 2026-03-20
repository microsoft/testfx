// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TimeoutCooperativeCancellationTests : AcceptanceTestBase<TimeoutCooperativeCancellationTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_ASSEMBLYINIT"] = "1" }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("AssemblyInit started");
        testHostResult.AssertOutputContains("Assembly initialize method 'TestClass.AssemblyInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_ASSEMBLYCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("AssemblyCleanup started");
        testHostResult.AssertOutputContains("Assembly cleanup method 'TestClass.AssemblyCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_CLASSINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("ClassInit started");
        testHostResult.AssertOutputContains("Class initialize method 'TestClass.ClassInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("ClassInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_CLASSCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("ClassCleanup started");
        testHostResult.AssertOutputContains("Class cleanup method 'TestClass.ClassCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_TESTINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("TestInit started");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.TestInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("TestInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_TESTCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("TestCleanup started");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.TestCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("TestCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_ASSEMBLYINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("AssemblyInit started");
        testHostResult.AssertOutputContains("Assembly initialize method 'TestClass.AssemblyInit' timed out after 1000ms");
        testHostResult.AssertOutputContains("AssemblyInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("AssemblyInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenAssemblyCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_ASSEMBLYCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("AssemblyCleanup started");
        testHostResult.AssertOutputContains("AssemblyCleanup Thread.Sleep completed");
        testHostResult.AssertOutputContains("Assembly cleanup method 'TestClass.AssemblyCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_CLASSINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("ClassInit started");
        testHostResult.AssertOutputContains("Class initialize method 'TestClass.ClassInit' timed out after 1000ms");
        testHostResult.AssertOutputContains("ClassInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("ClassInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenClassCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_CLASSCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("ClassCleanup started");
        testHostResult.AssertOutputContains("ClassCleanup Thread.Sleep completed");
        testHostResult.AssertOutputContains("Class cleanup method 'TestClass.ClassCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("ClassCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_TESTINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("TestInit started");
        testHostResult.AssertOutputDoesNotContain("TestInit completed");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.TestInit' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenTestCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_TESTCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("TestCleanup started");
        testHostResult.AssertOutputDoesNotContain("TestCleanup completed");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.TestCleanup' timed out after 1000ms");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TimeoutCooperativeTimeout";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
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
    [ClassCleanup]
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
    }

    public TestContext TestContext { get; set; } = default!;
}
