// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class LifecycleAttributesVoidThreadingTests : AcceptanceTestBase<LifecycleAttributesVoidThreadingTests.TestAssetFixture>
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task LifecycleAttributesVoidThreading_WhenMainIsNotSTA_RunsettingsAsksForSTA_OnWindows_ThreadIsSTA(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_THREAD_STATE_IS_STA"] = "1",
        }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "LifecycleAttributesVoid";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
        {
            return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
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

#file LifecycleAttributesVoid.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <GenerateTestingPlatformEntryPoint>true</GenerateTestingPlatformEntryPoint>
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

#file LifecycleAttributesVoid.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class LifecycleAttributesVoidTests
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        AssertCorrectThreadApartmentState();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        AssertCorrectThreadApartmentState();
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        AssertCorrectThreadApartmentState();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        AssertCorrectThreadApartmentState();
    }

    [TestInitialize]
    public void TestInitialize()
    {
        AssertCorrectThreadApartmentState();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        AssertCorrectThreadApartmentState();
    }

    [TestMethod]
    public void TestMethod()
    {
    }

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
