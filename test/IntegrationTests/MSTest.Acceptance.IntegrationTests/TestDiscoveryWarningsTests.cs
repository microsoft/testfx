﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class TestDiscoveryWarningsTests : AcceptanceTestBase<TestDiscoveryWarningsTests.TestAssetFixture>
{
    private const string AssetName = "TestDiscoveryWarnings";
    private const string BaseClassAssetName = "TestDiscoveryWarningsBaseClass";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTests_ShowsWarningsForTestsThatFailedToDiscover(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        // Delete the TestDiscoveryWarningsBaseClass.dll from the test bin folder on purpose, to break discovering
        // because the type won't be loaded on runtime, and mstest will write warning.
        File.Delete(Path.Combine(testHost.DirectoryName, $"{BaseClassAssetName}.dll"));

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests");

        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.AssertOutputContains("System.IO.FileNotFoundException: Could not load file or assembly 'TestDiscoveryWarningsBaseClass");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public string BaseTargetAssetPath => GetAssetPath(BaseClassAssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (BaseClassAssetName, BaseClassAssetName,
            BaseClassSourceCode.PatchTargetFrameworks(TargetFrameworks.All));

            yield return (AssetName, AssetName,
            SourceCode.PatchTargetFrameworks(TargetFrameworks.All)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestDiscoveryWarnings.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../TestDiscoveryWarningsBaseClass/TestDiscoveryWarningsBaseClass.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using Base;

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass1 : BaseClass
{
    [TestMethod]
    public void Test1_1() {}
}

[TestClass]
public class TestClass2
{
    [TestMethod]
    public void Test2_1() {}
}
""";

        private const string BaseClassSourceCode = """
#file TestDiscoveryWarningsBaseClass.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <IsPackable>false</IsPackable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

</Project>


#file UnitTest1.cs
namespace Base;

public class BaseClass
{
}
""";
    }
}
