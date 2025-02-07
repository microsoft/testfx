// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class ParameterizedTestTests : AcceptanceTestBase<ParameterizedTestTests.TestAssetFixture>
{
    private const string DynamicDataAssetName = "DynamicData";
    private const string DataSourceAssetName = "DataSource";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDynamicDataTest_WithSettingConsiderEmptyDataSourceAsInconclusive_Passes(string currentTfm)
        => await RunTestsAsync(currentTfm, DynamicDataAssetName, true);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDataSourceTest_WithSettingConsiderEmptyDataSourceAsInconclusive_Passes(string currentTfm)
        => await RunTestsAsync(currentTfm, DataSourceAssetName, true);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDynamicDataTest_WithSettingConsiderEmptyDataSourceAsInconclusiveToFalse_Fails(string currentTfm)
        => await RunTestsAsync(currentTfm, DynamicDataAssetName, false);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDataSourceTest_WithSettingConsiderEmptyDataSourceAsInconclusiveToFalse_Fails(string currentTfm)
    => await RunTestsAsync(currentTfm, DataSourceAssetName, false);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDynamicDataTest_WithoutSettingConsiderEmptyDataSourceAsInconclusive_Fails(string currentTfm)
        => await RunTestsAsync(currentTfm, DynamicDataAssetName, null);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SendingEmptyDataToDataSourceTest_WithoutSettingConsiderEmptyDataSourceAsInconclusive_Fails(string currentTfm)
        => await RunTestsAsync(currentTfm, DataSourceAssetName, null);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task UsingTestDataRowVariousCases(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.GetAssetPath(DynamicDataAssetName), DynamicDataAssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName=TestDataRowTests --list-tests");
        testHostResult.AssertOutputContains("""
            The following Tests are available:
            TestDataRowSingleParameterFolded
            TestDataRowSingleParameterUnfolded ("TestDataRowSingleParameterUnfolded - Only setting value")
            TestDataRowSingleParameterUnfolded ("TestDataRowSingleParameterUnfolded - Ignoring")
            Display name for third row - TestDataRowSingleParameterUnfolded
            Display name for fourth row - TestDataRowSingleParameterUnfolded
            TestDataRowTwoParametersFolded
            TestDataRowTwoParametersUnfolded ("TestDataRowTwoParametersUnfolded - Only setting value 1","Only setting value 2")
            TestDataRowTwoParametersUnfolded ("TestDataRowTwoParametersUnfolded - Ignoring1","Ignoring2")
            Display name for third row - TestDataRowTwoParametersUnfolded
            Display name for fourth row - TestDataRowTwoParametersUnfolded
            TestDataRowParameterIsTupleFolded
            TestDataRowParameterIsTupleUnfolded ((TestDataRowParameterIsTupleUnfolded - Only setting value 1, Only setting value 2))
            TestDataRowParameterIsTupleUnfolded ((TestDataRowParameterIsTupleUnfolded - Ignoring1, Ignoring2))
            Display name for third row - TestDataRowParameterIsTupleUnfolded
            Display name for fourth row - TestDataRowParameterIsTupleUnfolded
            """);

        testHostResult = await testHost.ExecuteAsync("--filter ClassName=TestDataRowTests");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 9, skipped: 15);
        testHostResult.AssertOutputMatchesRegex("""
            skipped TestDataRowSingleParameterFolded \("TestDataRowSingleParameterFolded - Ignoring"\) \(\d+ms\)
              Ignore reason for second row - TestDataRowSingleParameterFolded
            skipped Display name for third row - TestDataRowSingleParameterFolded \(\d+ms\)
              Ignore reason for third row - TestDataRowSingleParameterFolded
            skipped TestDataRowSingleParameterUnfolded \("TestDataRowSingleParameterUnfolded - Ignoring"\) \(\d+ms\)
              Ignore reason for second row - TestDataRowSingleParameterUnfolded
            skipped Display name for third row - TestDataRowSingleParameterUnfolded \(\d+ms\)
              Ignore reason for third row - TestDataRowSingleParameterUnfolded
            skipped Display name for fourth row - TestDataRowSingleParameterUnfolded \(\d+ms\)
              Ignore reason for third row - TestDataRowSingleParameterUnfolded
            skipped TestDataRowTwoParametersFolded \("TestDataRowTwoParametersFolded - Ignoring1","Ignoring2"\) \(\d+ms\)
              Ignore reason for second row - TestDataRowTwoParametersFolded
            skipped Display name for third row - TestDataRowTwoParametersFolded \(\d+ms\)
              Ignore reason for third row - TestDataRowTwoParametersFolded
            skipped TestDataRowTwoParametersUnfolded \("TestDataRowTwoParametersUnfolded - Ignoring1","Ignoring2"\) \(\d+ms\)
              Ignore reason for second row - TestDataRowTwoParametersUnfolded
            skipped Display name for third row - TestDataRowTwoParametersUnfolded \(\d+ms\)
              Ignore reason for third row - TestDataRowTwoParametersUnfolded
            skipped Display name for fourth row - TestDataRowTwoParametersUnfolded \(\d+ms\)
              Ignore reason for third row - TestDataRowTwoParametersUnfolded
            skipped TestDataRowParameterIsTupleFolded \(\(TestDataRowParameterIsTupleFolded - Ignoring1, Ignoring2\)\) \(\d+ms\)
              Ignore reason for second row - TestDataRowParameterIsTupleFolded
            skipped Display name for third row - TestDataRowParameterIsTupleFolded \(\d+ms\)
              Ignore reason for third row - TestDataRowParameterIsTupleFolded
            skipped TestDataRowParameterIsTupleUnfolded \(\(TestDataRowParameterIsTupleUnfolded - Ignoring1, Ignoring2\)\) \(\d+ms\)
              Ignore reason for second row - TestDataRowParameterIsTupleUnfolded
            skipped Display name for third row - TestDataRowParameterIsTupleUnfolded \(\d+ms\)
              Ignore reason for third row - TestDataRowParameterIsTupleUnfolded
            skipped Display name for fourth row - TestDataRowParameterIsTupleUnfolded \(\d+ms\)
              Ignore reason for third row - TestDataRowParameterIsTupleUnfolded
            """);
    }

    private static async Task RunTestsAsync(string currentTfm, string assetName, bool? isEmptyDataInconclusive)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.GetAssetPath(assetName), assetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(SetupRunSettingsAndGetArgs(isEmptyDataInconclusive));

        bool isSuccess = isEmptyDataInconclusive.HasValue && isEmptyDataInconclusive.Value;

        testHostResult.AssertExitCodeIs(isSuccess ? ExitCodes.Success : ExitCodes.AtLeastOneTestFailed);

        testHostResult.AssertOutputContains(isSuccess ? "skipped Test" : "failed Test");

        string? SetupRunSettingsAndGetArgs(bool? isEmptyDataInconclusive)
        {
            if (!isEmptyDataInconclusive.HasValue)
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
            return $"--settings {runSettingsFilePath} --filter ClassName=TestClass";
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (DynamicDataAssetName, DynamicDataAssetName,
                SourceCodeDynamicData
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
            yield return (DataSourceAssetName, DataSourceAssetName,
                SourceCodeDataSource
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCodeDynamicData = """
#file DynamicData.csproj
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
    public void Test(int i)
    {
    }

    public static IEnumerable<object[]> AdditionalData => Array.Empty<object[]>();

    public static IEnumerable<object[]> AdditionalData2
    {
        get
        {
            yield return new object[] { 2 };
        }
    }
}

[TestClass]
public class TestDataRowTests
{
    [TestMethod]
    [DynamicData(nameof(TestDataRowSingleParameterFoldedData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestDataRowSingleParameterFolded(string _)
    {
    }

    [TestMethod]
    [DynamicData(nameof(TestDataRowSingleParameterUnfoldedData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    public void TestDataRowSingleParameterUnfolded(string _)
    {
    }

    [TestMethod]
    [DynamicData(nameof(TestDataRowTwoParametersFoldedData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestDataRowTwoParametersFolded(string _1, string _2)
    {
    }

    [TestMethod]
    [DynamicData(nameof(TestDataRowTwoParametersUnfoldedData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    public void TestDataRowTwoParametersUnfolded(string _1, string _2)
    {
    }

    [TestMethod]
    [DynamicData(nameof(TestDataRowParameterIsTupleFoldedData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestDataRowParameterIsTupleFolded((string, string) tuple)
    {
    }

    [TestMethod]
    [DynamicData(nameof(TestDataRowParameterIsTupleUnfoldedData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    public void TestDataRowParameterIsTupleUnfolded((string, string) tuple)
    {
    }

    public static IEnumerable<TestDataRow<string>> TestDataRowSingleParameterFoldedData => GetDataForSingleStringParameter(nameof(TestDataRowSingleParameterFolded));

    public static IEnumerable<TestDataRow<string>> TestDataRowSingleParameterUnfoldedData => GetDataForSingleStringParameter(nameof(TestDataRowSingleParameterUnfolded));

    public static IEnumerable<TestDataRow<(string, string)>> TestDataRowTwoParametersUnfoldedData => GetDataForTwoStringParameters(nameof(TestDataRowTwoParametersUnfolded));

    public static IEnumerable<TestDataRow<(string, string)>> TestDataRowTwoParametersFoldedData => GetDataForTwoStringParameters(nameof(TestDataRowTwoParametersFolded));

    public static IEnumerable<TestDataRow<(string, string)>> TestDataRowParameterIsTupleFoldedData => GetDataForTwoStringParameters(nameof(TestDataRowParameterIsTupleFolded));

    public static IEnumerable<TestDataRow<(string, string)>> TestDataRowParameterIsTupleUnfoldedData => GetDataForTwoStringParameters(nameof(TestDataRowParameterIsTupleUnfolded));

    private static IEnumerable<TestDataRow<string>> GetDataForSingleStringParameter(string testName)
    {
        yield return new($"{testName} - Only setting value");
        yield return new($"{testName} - Ignoring") { IgnoreMessage = $"Ignore reason for second row - {testName}" };
        yield return new($"{testName} - Ignoring and setting display name") { DisplayName = $"Display name for third row - {testName}", IgnoreMessage = $"Ignore reason for third row - {testName}" };
        yield return new($"{testName} - Setting display name") { DisplayName = $"Display name for fourth row - {testName}" };
    }

    private static IEnumerable<TestDataRow<(string, string)>> GetDataForTwoStringParameters(string testName)
    {
        yield return new(($"{testName} - Only setting value 1", "Only setting value 2"));
        yield return new(($"{testName} - Ignoring1", "Ignoring2")) { IgnoreMessage = $"Ignore reason for second row - {testName}" };
        yield return new(($"{testName} - Ignoring and setting display name 1", "Ignoring and setting display name 2")) { DisplayName = $"Display name for third row - {testName}", IgnoreMessage = $"Ignore reason for third row - {testName}" };
        yield return new(($"{testName} - Setting display name 1", "Setting display name 2")) { DisplayName = $"Display name for fourth row - {testName}" };
    }
}
""";

        private const string SourceCodeDataSource = """
#file DataSource.csproj
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
