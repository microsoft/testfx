// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TimeoutCooperativeGlobalTestCancellationTests : AcceptanceTestBase<TimeoutCooperativeGlobalTestCancellationTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenGlobalTestInitTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_GLOBALTESTINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("GlobalTestInit started");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.GlobalTestInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("GlobalTestInit Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("GlobalTestInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenGlobalTestInitTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_GLOBALTESTINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("GlobalTestInit started");
        testHostResult.AssertOutputContains("GlobalTestInit Thread.Sleep completed");
        testHostResult.AssertOutputContains("Test initialize method 'TestClass.GlobalTestInit' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("GlobalTestInit completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenGlobalTestCleanupTimeoutExpires_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["TASKDELAY_GLOBALTESTCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("GlobalTestCleanup started");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.GlobalTestCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("GlobalTestCleanup Thread.Sleep completed");
        testHostResult.AssertOutputDoesNotContain("GlobalTestCleanup completed");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CooperativeCancellation_WhenGlobalTestCleanupTimeoutExpiresAndUserChecksToken_StepThrows(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            new() { ["CHECKTOKEN_GLOBALTESTCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("GlobalTestCleanup started");
        testHostResult.AssertOutputContains("GlobalTestCleanup Thread.Sleep completed");
        testHostResult.AssertOutputContains("Test cleanup method 'TestClass.GlobalTestCleanup' timed out after 1000ms");
        testHostResult.AssertOutputDoesNotContain("GlobalTestCleanup completed");
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "TimeoutCooperativeGlobalTimeout";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
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
    public TestContext TestContext { get; set; }

    [Timeout(1000, CooperativeCancellation = true)]
    [GlobalTestInitialize]
    public static async Task GlobalTestInit(TestContext testContext)
        => await DoWork("GLOBALTESTINIT", "GlobalTestInit", testContext);

    [Timeout(1000, CooperativeCancellation = true)]
    [GlobalTestCleanup]
    public static async Task GlobalTestCleanup(TestContext testContext)
        => await DoWork("GLOBALTESTCLEANUP", "GlobalTestCleanup", testContext);

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
