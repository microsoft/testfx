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
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
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
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { [envVarKey] = "1" });
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Failed: 0, Passed: 1, Skipped: 0, Total: 1");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture)
        : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "ThreadContextProject";
        private const string SourceCode = """
#file ThreadContextProject.csproj
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
namespace ThreadContextProject;

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
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_ASSEMBLY_INIT") == "1")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_CLASS_INIT") == "1")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_TEST_INIT") == "1")
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

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
