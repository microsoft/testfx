// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class TestFilterTests : AcceptanceTestBase<TestFilterTests.TestAssetFixture>
{
    private const string AssetName = "TestFilter";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RunWithFilter_UsingTestProperty_FilteredTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter tree=one", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTestsWithFilter_UsingTestProperty_FilteredTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter tree=one --list-tests", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputMatchesRegex("""
  Test2
Test discovery summary: found 1 test\(s\)\ - .*\.(dll|exe) \(net.+\|.+\)
  duration:
""");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task UsingTestPropertyForOwnerAndPriorityAndTestCategory_TestsFailed(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter tree!~one", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("""
failed PriorityTest (0ms)
  UTA023: TestClass: Cannot define predefined property Priority on method PriorityTest.
failed OwnerTest (0ms)
  UTA023: TestClass: Cannot define predefined property Owner on method OwnerTest.
failed TestCategoryTest (0ms)
  UTA023: TestClass: Cannot define predefined property TestCategory on method TestCategoryTest.
""");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RunWithFilter_UsingTestPropertyForOwner_FilteredButTestsFailed(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter owner=testOwner", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("""
failed OwnerTest (0ms)
  UTA023: TestClass: Cannot define predefined property Owner on method OwnerTest.
""");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RunWithFilter_UsingTestPropertyForPriorityAndTestCategory_NotFiltered(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter TestCategory=category|Priority=1", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("Zero tests ran");
        testHostResult.AssertExitCodeIs(8);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RunWithFilterFromRunsettings(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings CategoryA.runsettings", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContains("Running test: CategoryAOnly");
        testHostResult.AssertOutputDoesNotContain("Running test: CategoryBOnly");
        testHostResult.AssertOutputContains("Running test: CategoryAAndB");
        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult = await testHost.ExecuteAsync("--settings NoFilter.runsettings", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContains("Running test: CategoryAOnly");
        testHostResult.AssertOutputContains("Running test: CategoryBOnly");
        testHostResult.AssertOutputContains("Running test: CategoryAAndB");
        // PriorityTest, OwnerTest, and TestCategoryTest are reported as failing.
        // See the test UsingTestPropertyForOwnerAndPriorityAndTestCategory_TestsFailed
        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);

        testHostResult = await testHost.ExecuteAsync("--settings CategoryA.runsettings --filter TestCategory~CategoryA", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContains("Running test: CategoryAOnly");
        testHostResult.AssertOutputDoesNotContain("Running test: CategoryBOnly");
        testHostResult.AssertOutputContains("Running test: CategoryAAndB");
        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult = await testHost.ExecuteAsync("--settings CategoryA.runsettings --filter TestCategory~CategoryB", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputDoesNotContain("Running test: CategoryAOnly");
        testHostResult.AssertOutputDoesNotContain("Running test: CategoryBOnly");
        testHostResult.AssertOutputContains("Running test: CategoryAAndB");
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestFilter.csproj
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

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file CategoryA.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <TestCaseFilter>(TestCategory~CategoryA)</TestCaseFilter>
  </RunConfiguration>

  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>

</RunSettings>

#file NoFilter.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>

</RunSettings>

#file UnitTest1.cs

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    [TestProperty("tree", "one")]
    public void Test2() { }

    [TestMethod]
    [TestProperty("Priority", "1")]
    public void PriorityTest() { }

    [TestMethod]
    [TestProperty("Owner", "testOwner")]
    public void OwnerTest() { }

    [TestMethod]
    [TestProperty("TestCategory", "category")]
    public void TestCategoryTest() { }

    [TestMethod]
    [TestCategory("CategoryA")]
    public void CategoryAOnly()
    {
        Console.WriteLine($"Running test: {nameof(CategoryAOnly)}");
    }

    [TestMethod]
    [TestCategory("CategoryB")]
    public void CategoryBOnly()
    {
        Console.WriteLine($"Running test: {nameof(CategoryBOnly)}");
    }

    [TestMethod]
    [TestCategory("CategoryA")]
    [TestCategory("CategoryB")]
    public void CategoryAAndB()
    {
        Console.WriteLine($"Running test: {nameof(CategoryAAndB)}");
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
