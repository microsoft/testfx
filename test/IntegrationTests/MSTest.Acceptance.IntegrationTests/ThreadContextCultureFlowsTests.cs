// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ThreadContextCultureFlowsTests : AcceptanceTestBase<ThreadContextCultureFlowsTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingSTAThread_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingSTAThreadAndTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta-timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "CultureFlowsThreadContextProject";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file sta.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
    </RunConfiguration>
</RunSettings>

#file sta-timeout.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
    </RunConfiguration>
    <MSTest>
        <AssemblyInitializeTimeout>10001</AssemblyInitializeTimeout>
        <ClassInitializeTimeout>10002</ClassInitializeTimeout>
        <TestInitializeTimeout>10003</TestInitializeTimeout>
        <TestTimeout>30004</TestTimeout>
        <TestCleanupTimeout>10005</TestCleanupTimeout>
        <ClassCleanupTimeout>10006</ClassCleanupTimeout>
        <AssemblyCleanupTimeout>10007</AssemblyCleanupTimeout>
    </MSTest>
</RunSettings>

#file timeout.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <MSTest>
        <AssemblyInitializeTimeout>10001</AssemblyInitializeTimeout>
        <ClassInitializeTimeout>10002</ClassInitializeTimeout>
        <TestInitializeTimeout>10003</TestInitializeTimeout>
        <TestTimeout>30004</TestTimeout>
        <TestCleanupTimeout>10005</TestCleanupTimeout>
        <ClassCleanupTimeout>10006</ClassCleanupTimeout>
        <AssemblyCleanupTimeout>10007</AssemblyCleanupTimeout>
    </MSTest>
</RunSettings>

#file CultureFlowsThreadContextProject.csproj
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
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file UnitTest1.cs
namespace CultureFlowsThreadContextProject;

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private const string AssemblyInitCultureCodeName = "th-TH";
    private const string ClassInitCultureCodeName = "tr-TR";
    private const string TestInitCultureCodeName = "sv-SE";
    private const string TestMethodCultureCodeName = "ak-GH";
    private const string TestCleanupCultureCodeName = "pt-BR";
    private const string ClassCleanupCultureCodeName = "hu-HU";

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        CultureInfo.CurrentCulture = new CultureInfo(AssemblyInitCultureCodeName);
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        Assert.AreEqual(AssemblyInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "ClassInitialize culture should have been the one set by AssemblyInitialize");
        CultureInfo.CurrentCulture = new CultureInfo(ClassInitCultureCodeName);
    }

    [TestInitialize]
    public void TestInitialize()
    {
        Assert.AreEqual(ClassInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "TestInitialize culture should have been the one set by ClassInitialize");
        CultureInfo.CurrentCulture = new CultureInfo(TestInitCultureCodeName);
    }

    [TestMethod]
    public void TestMethod1()
    {
        Assert.AreEqual(TestInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "TestMethod culture should have been the one set by TestInitialize");
        CultureInfo.CurrentCulture = new CultureInfo(TestMethodCultureCodeName);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Assert.AreEqual(TestMethodCultureCodeName, CultureInfo.CurrentCulture.Name,
            "TestCleanup culture should have been the one set by TestMethod");
        CultureInfo.CurrentCulture = new CultureInfo(TestCleanupCultureCodeName);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Assert.AreEqual(ClassInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "ClassCleanup culture should have been the one set by ClassInitialize");
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Assert.AreEqual(AssemblyInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "AssemblyCleanup culture should have been the one set by AssemblyInitialize");
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
