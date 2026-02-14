// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class FrameworkOnlyTests : AcceptanceTestBase<FrameworkOnlyTests.TestAssetFixture>
{
    private const string AssetName = nameof(FrameworkOnlyTests);

    [TestMethod]
    public async Task DynamicDataAttributeGetDataShouldWorkWithoutAdapter()
    {
        // This is an important scenario to not regress.
        // Users shouldn't need to reference adapter, nor do anything
        // special, to be able to call DynamicData.GetData.
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContains("""
            1,2
            3,4
            """);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file FrameworkOnlyTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <OutputType>Exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <!-- Intentionally referencing TestFramework only. -->
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

var methodInfo = typeof(UnitTest1).GetMethod("TestMethod1");
if (methodInfo.GetCustomAttribute<DynamicDataAttribute>() is not { } dynamicData)
{
    Console.WriteLine("Error: Cannot find DynamicDataAttribute");
    return;
}

var data = dynamicData.GetData(methodInfo);
foreach (var row in data)
{
    Console.WriteLine(string.Join(',', row));
}

#file TestClass1.cs
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    [DynamicData(nameof(Data))]
    public void TestMethod1()
    {
    }

    public static IEnumerable<object[]> Data { get; }
        = new[]
        {
           new object[] { 1, 2 },
           new object[] { 3, 4 }
        };
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
