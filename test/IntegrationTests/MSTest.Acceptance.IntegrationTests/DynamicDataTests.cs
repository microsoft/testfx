// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class DynamicDataTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;
    private const string AssetName = "DynamicData";

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public DynamicDataTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDynamicDataTest_WithSettingConsiderEmptyDataSourceAsInconclusive_Passes(string currentTfm)
    {
        string runSettings = $"""
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <ConsiderEmptyDataSourceAsInconclusive>true</ConsiderEmptyDataSourceAsInconclusive>
    </MSTest>
</RunSettings>
""";
        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);

        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.runsettings");
        File.WriteAllText(runSettingsFilePath, runSettings);

        var testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        // testHostResult.AssertOutputContains($"""
        //    Name: MSTest
        //    Version: {MSTestVersion}
        // """);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file DynamicData.csproj
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

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    [DynamicData(nameof(AdditionData))]
    public void Test()
    {
    }

    public static IEnumerable<int> AdditionData
    {
    get
    {
        return Array.Empty<int>();
    }
}
}
""";
    }
}
