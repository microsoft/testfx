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

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public DynamicDataTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDynamicDataTest_WithSettingConsiderEmptyDataSourceAsInconclusive_Passes(string currentTfm)
        => await RunTests(currentTfm, true, true, true);

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToTestDataSourceTest_WithSettingConsiderEmptyDataSourceAsInconclusive_Passes(string currentTfm)
        => await RunTests(currentTfm, true, false, true);

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDynamicDataTest_WithSettingConsiderEmptyDataSourceAsInconclusiveToFalse_Fails(string currentTfm)
        => await RunTests(currentTfm, false, true, true);

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToTestDataSourceTest_WithSettingConsiderEmptyDataSourceAsInconclusiveToFalse_Fails(string currentTfm)
    => await RunTests(currentTfm, false, false, true);

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDynamicDataTest_WithoutSettingConsiderEmptyDataSourceAsInconclusive_Fails(string currentTfm)
        => await RunTests(currentTfm, false, true, false);

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToTestDataSourceTest_WithoutSettingConsiderEmptyDataSourceAsInconclusive_Fails(string currentTfm)
        => await RunTests(currentTfm, false, false, false);

    private async Task RunTests(string currentTfm, bool isEmptyDataInconclusive, bool isDynamicData, bool useRunSettings)
    {
        _testAssetFixture.IsDynamicData = isDynamicData;
        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, _testAssetFixture.AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(SetupRunSettingsAndGetArgs(useRunSettings, isEmptyDataInconclusive));

        testHostResult.AssertExitCodeIs(isEmptyDataInconclusive ? ExitCodes.Success : ExitCodes.AtLeastOneTestFailed);

        testHostResult.AssertOutputContains(isEmptyDataInconclusive ? "skipped Test" : "failed Test");

        string? SetupRunSettingsAndGetArgs(bool useRunSettings, bool isEmptyDataInconclusive)
        {
            if (useRunSettings)
            {
                return null;
            }

            string runSettings = $"""
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <ConsiderEmptyDataSourceAsInconclusive>{isEmptyDataInconclusive}</ConsiderEmptyDataSourceAsInconclusive>
    </MSTest>
</RunSettings>
""";

            string runSettingsFilePath = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.runsettings");
            File.WriteAllText(runSettingsFilePath, runSettings);
            return $"--settings {runSettingsFilePath}";
        }
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string DynamicAssetName = "DynamicData";
        private const string SourceAssetName = "SourceData";

        public bool IsDynamicData { get; set; }

        public string AssetName => IsDynamicData ? DynamicAssetName : SourceAssetName;

        public string SourceCode => IsDynamicData ? SourceCodeDynamicData : SourceCodeTestDataSource;

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

        private const string SourceCodeDynamicData = """
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
    [DynamicData(nameof(AdditionalData))]
    [DynamicData(nameof(AdditionalData2))]
    public void Test()
    {
    }

    public static IEnumerable<int> AdditionalData => Array.Empty<int>();

    public static IEnumerable<int> AdditionalData2
    {
        get
        {
            yield return 2;
        }
    }
}
""";

        private const string SourceCodeTestDataSource = """
#file DynamicData.csproj
<Project Sdk="Microsoft.NET.Sdk">
    
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
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
using System.Reflection;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    [CustomTestDataSource]
    [CustomEmptyTestDataSource]
    public void Test(int a, int b, int c)
    {
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CustomTestDataSourceAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object[]> GetData(MethodInfo methodInfo) => [[1, 2, 3], [4, 5, 6]];

    public string GetDisplayName(MethodInfo methodInfo, object[] data) => data != null ? string.Format(CultureInfo.CurrentCulture, "{0} ({1})", methodInfo.Name, string.Join(",", data)) : null;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CustomEmptyTestDataSourceAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object[]> GetData(MethodInfo methodInfo) => [];

    public string GetDisplayName(MethodInfo methodInfo, object[] data) => data != null ? string.Format(CultureInfo.CurrentCulture, "{0} ({1})", methodInfo.Name, string.Join(",", data)) : null;
}
""";
    }
}
