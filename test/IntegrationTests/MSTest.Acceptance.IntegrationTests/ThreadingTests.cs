// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class ThreadingTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public ThreadingTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task RunnerGeneratedMain_WhenNoRunsettingsProvided_ThreadIsMTA(string tfm)
    {
        TestHost testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task RunnerGeneratedMain_WhenRunsettingsAsksForSTA_ThreadIsSTA(string tfm)
    {
        TestHost testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_EXPECTED_APARTMENTSTATE"] = "STA",
        });

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task RunnerGeneratedMain_WhenRunsettingsAsksForMTA_ThreadIsMTA(string tfm)
    {
        TestHost testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_EXPECTED_APARTMENTSTATE"] = "MTA",
        });

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task RunnerManualMain_WhenMainIsSTAThreadAndNoRunsettingsProvided_ThreadIsSTA(string tfm)
    {
        TestHost testHost = TestHost.LocateFrom(_testAssetFixture.STAThreadProjectPath, TestAssetFixture.STAThreadProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["MSTEST_EXPECTED_APARTMENTSTATE"] = "STA",
        });

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task RunnerManualMain_WhenMainIsSTAThreadAndRunsettingsAsksForSTA_ThreadIsSTA(string tfm)
    {
        TestHost testHost = TestHost.LocateFrom(_testAssetFixture.STAThreadProjectPath, TestAssetFixture.STAThreadProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_EXPECTED_APARTMENTSTATE"] = "STA",
        });

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task RunnerManualMain_WhenMainIsSTAThreadAndRunsettingsAsksForMTA_ThreadIsMTA(string tfm)
    {
        TestHost testHost = TestHost.LocateFrom(_testAssetFixture.STAThreadProjectPath, TestAssetFixture.STAThreadProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_EXPECTED_APARTMENTSTATE"] = "MTA",
        });

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture)
        : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestThreading";
        public const string STAThreadProjectName = "STATestThreading";

        public string ProjectPath => GetAssetPath(ProjectName);

        public string STAThreadProjectPath => GetAssetPath(STAThreadProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
                .PatchCodeWithReplace("$GenerateEntryPoint$", "true")
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (STAThreadProjectName, STAThreadProjectName,
                (SourceCode + ProgramFileSourceCode)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", STAThreadProjectName)
                .PatchCodeWithReplace("$GenerateEntryPoint$", "false")
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
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
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
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
        await Task.CompletedTask;
    }

    [TestMethod]
    public Task TestMethod3()
    {
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

    private void AssertCorrectThreadApartmentState()
    {
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        if (Environment.GetEnvironmentVariable("MSTEST_EXPECTED_APARTMENTSTATE") == "STA")
        {
            Assert.AreEqual(ApartmentState.STA, apartmentState);
        }
        else
        {
            Assert.AreEqual(ApartmentState.MTA, apartmentState);
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
}
