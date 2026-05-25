// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class STAThreadingTests : AcceptanceTestBase<STAThreadingTests.TestAssetFixture>
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_OnWindows_TestConfigJsonAsksForSTA_ThreadIsSTA(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string testConfigFilePath = Path.Combine(testHost.DirectoryName, "sta.testconfig.json");
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--config-file \"{testConfigFilePath}\"",
            environmentVariables: new()
            {
                ["MSTEST_THREAD_STATE_IS_STA"] = "1",
            }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_OnWindows_RunsettingsAsksForSTA_ThreadIsSTA(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--settings {runSettingsFilePath}",
            environmentVariables: new()
            {
                ["MSTEST_THREAD_STATE_IS_STA"] = "1",
            }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_OnWindows_RunsettingsAsksForMTA_ThreadIsMTA(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--settings {runSettingsFilePath}",
            environmentVariables: new()
            {
                ["MSTEST_THREAD_STATE_IS_STA"] = "0",
            }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "STATestThreading";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file sta.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
    </RunConfiguration>
</RunSettings>

#file mta.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>MTA</ExecutionThreadApartmentState>
    </RunConfiguration>
</RunSettings>

#file sta.testconfig.json
{
  "mstest": {
    "execution": {
      "executionApartmentState": "STA"
    }
  }
}

#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="sta.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="mta.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="sta.testconfig.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
        AssertCorrectThreadApartmentState();
    }

    [TestMethod]
    public async Task TestMethod2()
    {
        AssertCorrectThreadApartmentState();
        // Ensure that we continue on a thread pool thread after this await.
        await Task.Yield();
        Assert.IsTrue(Thread.CurrentThread.IsThreadPoolThread);
    }

    [TestMethod]
    public Task TestMethod3()
    {
        if (Environment.GetEnvironmentVariable("MSTEST_THREAD_STATE_IS_STA") == "1")
        {
            // TestMethod2 finished on a thread pool thread.
            // However, here in this method we should still start on STA thread.
            Assert.IsFalse(Thread.CurrentThread.IsThreadPoolThread);
        }

        AssertCorrectThreadApartmentState();
        return Task.CompletedTask;
    }

#if NET
    [TestMethod]
    public async ValueTask TestMethod4()
    {
        AssertCorrectThreadApartmentState();
        await ValueTask.CompletedTask;
    }

    [TestMethod]
    public ValueTask TestMethod5()
    {
        AssertCorrectThreadApartmentState();
        return ValueTask.CompletedTask;
    }
#endif

    private static void AssertCorrectThreadApartmentState()
    {
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        string expectedThreadStateIsSta = Environment.GetEnvironmentVariable("MSTEST_THREAD_STATE_IS_STA");

        if (expectedThreadStateIsSta == "1")
        {
            Assert.AreEqual(ApartmentState.STA, apartmentState);
        }
        else if (expectedThreadStateIsSta == "0")
        {
            Assert.AreNotEqual(ApartmentState.STA, apartmentState);
        }
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
