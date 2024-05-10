// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class ThreadContextTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public ThreadContextTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenCultureIsNotSet_TestMethodFails(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.InitToTestProjectPath, TestAssetFixture.InitToTestProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContains("Failed: 1, Passed: 0, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInAssemblyInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_ASSEMBLY_INIT");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInClassInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_CLASS_INIT");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInTestInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_TEST_INIT");

    private async Task SetCultureInFixtureMethodAndRunTests(string tfm, string envVarKey)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.InitToTestProjectPath, TestAssetFixture.InitToTestProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { [envVarKey] = "true" });
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingSTAThread_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingSTAThreadAndTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta-timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_TEST_FLOW_CONTEXT"] = "true",
        });
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new()
        {
            ["MSTEST_TEST_FLOW_CONTEXT"] = "true",
        });
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture)
        : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string InitToTestProjectName = "InitToTestThreadContextProject";
        public const string CultureFlowsProjectName = "CultureFlowsThreadContextProject";
        private const string InitToTestSourceCode = """
#file InitToTestThreadContextProject.csproj
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
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
namespace InitToTestThreadContextProject;

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private const string CultureCodeName = "th-TH";

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_ASSEMBLY_INIT") == "true")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_CLASS_INIT") == "true")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_TEST_INIT") == "true")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [TestMethod]
    public void TestMethod1()
    {
        Assert.AreEqual(CultureCodeName, CultureInfo.CurrentCulture.Name);
    }
}
""";

        private const string CultureFlowsSourceCode = """
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
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
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
        Assert.AreEqual(AssemblyInitCultureCodeName, CultureInfo.CurrentCulture.Name, "ClassInitialize culture check failed");
        CultureInfo.CurrentCulture = new CultureInfo(ClassInitCultureCodeName);
    }

    [TestInitialize]
    public void TestInitialize()
    {
        Assert.AreEqual(ClassInitCultureCodeName, CultureInfo.CurrentCulture.Name, "TestInitialize culture check failed");
        CultureInfo.CurrentCulture = new CultureInfo(TestInitCultureCodeName);
    }

    [TestMethod]
    public void TestMethod1()
    {
        Assert.AreEqual(TestInitCultureCodeName, CultureInfo.CurrentCulture.Name, "TestMethod culture check failed");
        CultureInfo.CurrentCulture = new CultureInfo(TestMethodCultureCodeName);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Assert.AreEqual(TestMethodCultureCodeName, CultureInfo.CurrentCulture.Name, "TestCleanup culture check failed");
        CultureInfo.CurrentCulture = new CultureInfo(TestCleanupCultureCodeName);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_FLOW_CONTEXT") == "true")
        {
            Assert.AreEqual(ClassInitCultureCodeName, CultureInfo.CurrentCulture.Name, "ClassCleanup culture check failed");
        }
        else
        {
            Assert.AreEqual(TestCleanupCultureCodeName, CultureInfo.CurrentCulture.Name, "ClassCleanup culture check failed");
            CultureInfo.CurrentCulture = new CultureInfo(ClassCleanupCultureCodeName);
        }
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_FLOW_CONTEXT") == "true")
        {
            Assert.AreEqual(AssemblyInitCultureCodeName, CultureInfo.CurrentCulture.Name, "AssemblyCleanup culture check failed");
        }
        else
        {
            Assert.AreEqual(ClassCleanupCultureCodeName, CultureInfo.CurrentCulture.Name, "AssemblyCleanup culture check failed");
        }
    }
}
""";

        public string InitToTestProjectPath => GetAssetPath(InitToTestProjectName);

        public string CultureFlowsProjectPath => GetAssetPath(CultureFlowsProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (InitToTestProjectName, InitToTestProjectName,
                InitToTestSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CultureFlowsProjectName, CultureFlowsProjectName,
                CultureFlowsSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
