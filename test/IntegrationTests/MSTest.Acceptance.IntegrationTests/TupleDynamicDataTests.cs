// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class TupleDynamicDataTests : AcceptanceTestBase
{
    private const string AssetName = "TupleDynamicDataTests";
    private readonly TestAssetFixture _testAssetFixture;

    public TupleDynamicDataTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task CanUseLongTuplesAndValueTuplesForAllFrameworks(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings");

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("""
            1, 2, 3, 4, 5, 6, 7, 8
            9, 10, 11, 12, 13, 14, 15, 16
            1, 2, 3, 4, 5, 6, 7, 8
            9, 10, 11, 12, 13, 14, 15, 16
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10
            11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10
            11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            """);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 8, skipped: 0);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file TupleDynamicDataTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
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
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private static readonly StringBuilder s_builder = new();

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Console.WriteLine(s_builder.ToString());
    }

    [DynamicData(nameof(DataTuple8))]
    [DynamicData(nameof(DataValueTuple8))]
    [TestMethod]
    public void TestMethod1(int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8)
    {
        s_builder.AppendLine($"{p1}, {p2}, {p3}, {p4}, {p5}, {p6}, {p7}, {p8}");
    }

    [DynamicData(nameof(DataTuple10))]
    [DynamicData(nameof(DataValueTuple10))]
    [TestMethod]
    public void TestMethod1(int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9, int p10)
    {
        s_builder.AppendLine($"{p1}, {p2}, {p3}, {p4}, {p5}, {p6}, {p7}, {p8}, {p9}, {p10}");
    }

    public static IEnumerable<Tuple<int, int, int, int, int, int, int, Tuple<int>>> DataTuple8 =>
    [
        (1, 2, 3, 4, 5, 6, 7, 8).ToTuple(),
        (9, 10, 11, 12, 13, 14, 15, 16).ToTuple(),
    ];

    public static IEnumerable<ValueTuple<int, int, int, int, int, int, int, ValueTuple<int>>> DataValueTuple8 =>
    [
        (1, 2, 3, 4, 5, 6, 7, 8),
        (9, 10, 11, 12, 13, 14, 15, 16),
    ];

    public static IEnumerable<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>> DataTuple10 =>
    [
        (1, 2, 3, 4, 5, 6, 7, 8, 9, 10).ToTuple(),
        (11, 12, 13, 14, 15, 16, 17, 18, 19, 20).ToTuple(),
    ];

    public static IEnumerable<ValueTuple<int, int, int, int, int, int, int, ValueTuple<int, int, int>>> DataValueTuple10 =>
    [
        (1, 2, 3, 4, 5, 6, 7, 8, 9, 10),
        (11, 12, 13, 14, 15, 16, 17, 18, 19, 20),
    ];
}


#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
