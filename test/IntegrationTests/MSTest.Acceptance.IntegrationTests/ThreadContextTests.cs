// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ThreadContextTests : AcceptanceTestBase<ThreadContextTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenCultureIsNotSet_TestMethodFails(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.InitToTestProjectPath, TestAssetFixture.InitToTestProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 2, passed: 0, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInAssemblyInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_ASSEMBLY_INIT");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInClassInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_CLASS_INIT");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInTestInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_TEST_INIT");

    private static async Task SetCultureInFixtureMethodAndRunTests(string tfm, string envVarKey)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.InitToTestProjectPath, TestAssetFixture.InitToTestProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { [envVarKey] = "true" });
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string InitToTestProjectName = "InitToTestThreadContextProject";
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

    // Test methods should execute on the class context, and should be isolated.
    // Changes in one shouldn't affect the other.
    // This also makes the behavior of parallelizing and non-parallelizing tests consistent.
    private const string CultureToBeSetInTestMethodAndNotObservedInAnother = "fr-FR";

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
        CultureInfo.CurrentCulture = new CultureInfo(CultureToBeSetInTestMethodAndNotObservedInAnother);
    }

    [TestMethod]
    public void TestMethod2()
    {
        Assert.AreEqual(CultureCodeName, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(CultureToBeSetInTestMethodAndNotObservedInAnother);
    }
}
""";

        public string InitToTestProjectPath => GetAssetPath(InitToTestProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (InitToTestProjectName, InitToTestProjectName,
                InitToTestSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
    }

    public TestContext TestContext { get; set; }
}
