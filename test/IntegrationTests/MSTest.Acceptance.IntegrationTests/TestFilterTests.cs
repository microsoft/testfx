﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter tree=one");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTestsWithFilter_UsingTestProperty_FilteredTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter tree=one --list-tests");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("""
The following Tests are available:
Test2
""");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task UsingTestPropertyForOwnerAndPriorityAndTestCategory_TestsFailed(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter tree!~one");

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

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter owner=testOwner");

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

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter TestCategory=category|Priority=1");

        testHostResult.AssertOutputContains("Zero tests ran");
        testHostResult.AssertExitCodeIs(8);
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

</Project>

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
}
""";
    }
}
