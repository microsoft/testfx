// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TupleDynamicDataTests : AcceptanceTestBase<TupleDynamicDataTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CanUseLongTuplesAndValueTuplesForAllFrameworks(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName=CanUseLongTuplesAndValueTuplesForAllFrameworks --settings my.runsettings", cancellationToken: TestContext.CancellationToken);

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
            Hello, , World
            Hello2, , World2
            Hello, , World
            Hello2, , World2
            """);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 12, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TupleSupportDoesNotBreakObjectArraySupport(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName=TupleSupportDoesNotBreakObjectArraySupport --settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("""
            Length: 1
            (Hello, World)
            """);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TupleDynamicDataTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
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

#file CanUseLongTuplesAndValueTuplesForAllFrameworks.cs
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CanUseLongTuplesAndValueTuplesForAllFrameworks
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
    public void TestMethod2(int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9, int p10)
    {
        s_builder.AppendLine($"{p1}, {p2}, {p3}, {p4}, {p5}, {p6}, {p7}, {p8}, {p9}, {p10}");
    }

    [DynamicData(nameof(DataTupleString3))]
    [DynamicData(nameof(DataValueTupleString3))]
    [TestMethod]
    public void TestMethod3(string p1, string p2, string p3)
    {
        s_builder.AppendLine($"{p1}, {p2}, {p3}");
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

    public static IEnumerable<Tuple<string, string, string>> DataTupleString3 =>
    [
        ("Hello", (string)null, "World").ToTuple(),
        ("Hello2", (string)null, "World2").ToTuple(),
    ];

    public static IEnumerable<ValueTuple<string, string, string>> DataValueTupleString3 =>
    [
        ("Hello", null, "World"),
        ("Hello2", null, "World2"),
    ];
}

#file TupleSupportDoesNotBreakObjectArraySupport.cs
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TupleSupportDoesNotBreakObjectArraySupport
{
    [TestMethod]
    [DynamicData(nameof(GetData))]
    public void TestMethod1(object[] data)
    {
        Console.WriteLine($"Length: {data.Length}");
        Assert.AreEqual(1, data.Length);

        Console.WriteLine(data[0]);
        Assert.AreEqual(("Hello", "World"), data[0]);
    }

    private static IEnumerable<object[]> GetData()
    {
        yield return new object[] { ("Hello", "World") };
    }
}

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>
""";
    }

    public TestContext TestContext { get; set; }
}
