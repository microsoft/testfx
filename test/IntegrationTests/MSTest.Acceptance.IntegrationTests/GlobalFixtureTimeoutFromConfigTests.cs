// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class GlobalFixtureTimeoutFromConfigTests : AcceptanceTestBase<GlobalFixtureTimeoutFromConfigTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task GlobalTestInitializeTimeout_FromRunSettings_AppliesToGlobalTestInitialize(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            environmentVariables: new() { ["LONG_WAIT_GLOBALINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Global test initialize method 'TestClass.GlobalTestInit' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task GlobalTestCleanupTimeout_FromRunSettings_AppliesToGlobalTestCleanup(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            environmentVariables: new() { ["LONG_WAIT_GLOBALCLEANUP"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Global test cleanup method 'TestClass.GlobalTestCleanup' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task GlobalTestInitializeTimeout_FromRunSettings_DoesNotApplyToTestInitialize(string tfm)
    {
        // Only the global-fixture timeout is configured. A slow per-test [TestInitialize] must NOT be
        // subject to it — proving the dedicated key does not leak into per-test initialize.
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            environmentVariables: new() { ["SHORT_WAIT_TESTINIT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("TestInit completed");
        testHostResult.AssertOutputDoesNotContain("Test initialize method 'TestClass.TestInit' timed out");
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "GlobalFixtureTimeoutFromConfig";

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
    <GlobalTestInitializeTimeout>1000</GlobalTestInitializeTimeout>
    <GlobalTestCleanupTimeout>1000</GlobalTestCleanupTimeout>
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

    [GlobalTestInitialize]
    public static async Task GlobalTestInit(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_GLOBALINIT") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    [GlobalTestCleanup]
    public static async Task GlobalTestCleanup(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_GLOBALCLEANUP") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    [TestInitialize]
    public async Task TestInit()
    {
        if (Environment.GetEnvironmentVariable("SHORT_WAIT_TESTINIT") == "1")
        {
            // Intentionally longer than the configured global timeout (1000ms): if that global key
            // leaked into [TestInitialize], this would time out. It must complete instead.
            await Task.Delay(2_000);
        }

        Console.WriteLine("TestInit completed");
    }

    [TestMethod]
    public void TestMethod()
    {
    }
}
""";
    }

    public TestContext TestContext { get; set; } = default!;
}
