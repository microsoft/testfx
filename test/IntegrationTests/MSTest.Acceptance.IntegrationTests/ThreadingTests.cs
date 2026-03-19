// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ThreadingTests : AcceptanceTestBase<ThreadingTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_WhenMainIsNotSTA_NoRunsettingsProvided_ThreadIsNotSTA(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_WhenMainIsNotSTA_RunsettingsAsksForSTA_OnWindows_ThreadIsSTA(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--settings {runSettingsFilePath}",
            environmentVariables: new()
            {
                ["MSTEST_THREAD_STATE_IS_STA"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_WhenMainIsNotSTA_RunsettingsAsksForSTA_OnNonWindows_ThreadIsNotSTAAndWarningIsEmitted(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_THREAD_STATE_IS_STA"] = "0",
        }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
        testHostResult.AssertOutputContains("Runsettings entry '<ExecutionApartmentState>STA</ExecutionApartmentState>' is not supported on non-Windows OSes");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_WhenMainIsNotSTA_RunsettingsAsksForMTA_ThreadIsNotSTA(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_THREAD_STATE_IS_STA"] = "0",
        }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
        testHostResult.AssertOutputDoesNotContain("Runsettings entry '<ExecutionApartmentState>STA</ExecutionApartmentState>' is not supported on non-Windows OSes");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestThreading";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
        {
            return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
                .PatchCodeWithReplace("$GenerateEntryPoint$", "true")
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

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

#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <GenerateTestingPlatformEntryPoint>$GenerateEntryPoint$</GenerateTestingPlatformEntryPoint>
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
        if (Environment.GetEnvironmentVariable("MSTEST_THREAD_STATE_IS_STA") == "1")
        {
            Assert.AreEqual(ApartmentState.STA, apartmentState);
        }
        else
        {
            Assert.AreNotEqual(ApartmentState.STA, apartmentState);
        }
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
