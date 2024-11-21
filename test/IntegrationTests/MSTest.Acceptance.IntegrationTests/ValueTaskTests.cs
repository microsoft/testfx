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
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CanUseValueTaskForAllKnownLocations(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContainsSummary(failed: 1, passed: 2, skipped: 1);
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
                .PatchTargetFrameworks(TargetFrameworks.All)
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
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private static ValueTask CompletedTask =>
#if !NET5_0_OR_GREATER
        // ValueTask.CompletedTask is only available in .NET 5 and later
        default;
#else
        ValueTask.CompletedTask;
#endif

    [AssemblyInitialize]
    public static ValueTask AssemblyInitialize(TestContext testContext) => CompletedTask;

    [AssemblyCleanup]
    public static ValueTask AssemblyCleanup() => CompletedTask;

    [ClassInitialize]
    public static ValueTask ClassInitialize(TestContext testContext) => CompletedTask;

    [ClassCleanup]
    public static ValueTask ClassCleanup() => CompletedTask;

    [TestInitialize]
    public ValueTask TestInit() => CompletedTask;

    [TestCleanup]
    public ValueTask TestCleanup() => CompletedTask;

    [TestMethod]
    public async ValueTask TestMethod1() => await CompletedTask;

    [TestMethod]
    public ValueTask TestMethod2() => CompletedTask;

    [TestMethod]
    public async ValueTask FailedTestMethod()
    {
        await CompletedTask;
        Assert.Fail();
    }

    [TestMethod]
    public async ValueTask InconclusiveTestMethod()
    {
        await CompletedTask;
        Assert.Inconclusive();
    }
}
""";
    }
}
