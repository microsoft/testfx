// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ShowOutputOptionTests : AcceptanceTestBase<ShowOutputOptionTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ShowStdout_None_NeverShowsStandardOutput(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--show-stdout none --output detailed --no-progress --no-ansi",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputDoesNotContain("Standard output");
        testHostResult.AssertOutputDoesNotContain("stdout from failing test");
        testHostResult.AssertOutputDoesNotContain("stdout from passing test");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ShowStdout_Failed_ShowsStandardOutputOnlyForFailedTests(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--show-stdout failed --output detailed --no-progress --no-ansi",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("stdout from failing test");
        testHostResult.AssertOutputDoesNotContain("stdout from passing test");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ShowStderr_None_NeverShowsErrorOutput(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--show-stderr none --output detailed --no-progress --no-ansi",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputDoesNotContain("Error output");
        testHostResult.AssertOutputDoesNotContain("stderr from failing test");
        testHostResult.AssertOutputDoesNotContain("stderr from passing test");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ShowStderr_Failed_ShowsErrorOutputOnlyForFailedTests(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--show-stderr failed --output detailed --no-progress --no-ansi",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("stderr from failing test");
        testHostResult.AssertOutputDoesNotContain("stderr from passing test");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "ShowOutputOptionTest";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file ShowOutputOptionTest.csproj
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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void FailingTest()
    {
        Console.WriteLine("stdout from failing test");
        Console.Error.WriteLine("stderr from failing test");
        Assert.Fail("intentional failure");
    }

    [TestMethod]
    public void PassingTest()
    {
        Console.WriteLine("stdout from passing test");
        Console.Error.WriteLine("stderr from passing test");
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
