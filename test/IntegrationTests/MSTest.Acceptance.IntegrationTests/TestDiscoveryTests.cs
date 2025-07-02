// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class TestDiscoveryTests : AcceptanceTestBase<TestDiscoveryTests.TestAssetFixture>
{
    private const string AssetName = "TestDiscovery";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTests_FindsAllTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("Test1");
        testHostResult.AssertOutputContains("Test2");
        testHostResult.AssertOutputContains("Display name: 1, one");
        testHostResult.AssertOutputContains("Display name: 2, two");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTests_WithFilter_FindsOnlyFilteredOnes(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --filter Name=Test1");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("Test1");
        testHostResult.AssertOutputDoesNotContain("Test2");
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
#file TestDiscovery.csproj
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
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    public void Test1() {}

    [TestMethod]
    public void Test2() {}

    [DynamicData(nameof(GetData), DynamicDataDisplayName = nameof(GetDisplayName))]
    [TestMethod]
    public void TestWithData(int _1, string _2)
    {
    }

    public static IEnumerable<(int, string)> GetData()
    {
        yield return (1, "one");
        yield return (2, "two");
    }

    public static string GetDisplayName(MethodInfo methodInfo, object[] data)
    {
        return $"Display name: {data[0]}, {data[1]}";
    }
}
""";
    }
}
