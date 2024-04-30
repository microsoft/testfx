// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class ValueTaskTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public ValueTaskTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    public async Task CanUseValueTaskForAllKnownLocations()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestValueTask";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestValueTask.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [AssemblyInitialize]
    public static ValueTask AssemblyInitialize(TestContext testContext) => ValueTask.CompletedTask;

    [AssemblyCleanup]
    public static ValueTask AssemblyCleanup() => ValueTask.CompletedTask;

    [ClassInitialize]
    public static ValueTask ClassInitialize(TestContext testContext) => ValueTask.CompletedTask;

    [ClassCleanup]
    public static ValueTask ClassCleanup() => ValueTask.CompletedTask;

    [TestInitialize]
    public ValueTask TestInit() => ValueTask.CompletedTask;

    [TestCleanup]
    public ValueTask TestCleanup() => ValueTask.CompletedTask;

    [TestMethod]
    public async ValueTask TestMethod1() => await ValueTask.CompletedTask;

    [TestMethod]
    public ValueTask TestMethod2() => ValueTask.CompletedTask;
}
""";
    }
}
