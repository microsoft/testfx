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
    public async Task TestMethodThreading_MainIsSTAThread_OnWindows_NoRunsettingsProvided_ThreadIsSTA(string tfm)
    {
        // Test cannot work on non-Windows OSes as the main method is marked with [STAThread]
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["MSTEST_THREAD_STATE_IS_STA"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_MainIsSTAThread_OnWindows_RunsettingsAsksForSTA_ThreadIsSTA(string tfm)
    {
        // Test cannot work on non-Windows OSes as the main method is marked with [STAThread]
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_THREAD_STATE_IS_STA"] = "1",
        }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestMethodThreading_MainIsSTAThread_OnWindows_RunsettingsAsksForMTA_ThreadIsMTA(string tfm)
    {
        // Test cannot work on non-Windows OSes as the main method is marked with [STAThread]
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
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
                (SourceCode + ProgramFileSourceCode)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
                .PatchCodeWithReplace("$GenerateEntryPoint$", "false")
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

        private const string ProgramFileSourceCode = """
#file Program.cs
using System;
using Microsoft.Testing.Platform.Builder;

public static class Program
{
    // Async main doesn't respect [STAThread] attribute so do a version with `GetAwaiter().GetResult()`
    // See https://github.com/dotnet/roslyn/issues/22112
    [STAThread]
    public static int Main(string[] args)
    {
        ITestApplicationBuilder builder = TestApplication.CreateBuilderAsync(args).GetAwaiter().GetResult();
        Microsoft.VisualStudio.TestTools.UnitTesting.TestingPlatformBuilderHook.AddExtensions(builder, args);
        using ITestApplication app = builder.BuildAsync().GetAwaiter().GetResult();
        return app.RunAsync().GetAwaiter().GetResult();
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
