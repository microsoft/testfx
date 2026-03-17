// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ParameterizedDataRowTests : AcceptanceTestBase<ParameterizedDataRowTests.TestAssetFixture>
{
    private const string DataRowAssetName = "DataRowTests";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task UsingDataRowThatDoesNotRoundTripUsingDataContractJsonSerializerWithAppDomains(string currentTfm)
        => await UsingDataRowThatDoesNotRoundTripUsingDataContractJsonSerializerCore(currentTfm, "AppDomainEnabled");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task UsingDataRowThatDoesNotRoundTripUsingDataContractJsonSerializerWithoutAppDomains(string currentTfm)
        => await UsingDataRowThatDoesNotRoundTripUsingDataContractJsonSerializerCore(currentTfm, "AppDomainDisabled");

    private static async Task UsingDataRowThatDoesNotRoundTripUsingDataContractJsonSerializerCore(string currentTfm, string runSettings)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.GetAssetPath(DataRowAssetName), DataRowAssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettings}.runsettings --filter ClassName=ParameterizedTestSerializationIssue2390");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (DataRowAssetName, DataRowAssetName,
                SourceCodeDataRow
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCodeDataRow = """
#file DataRowTests.csproj
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
    <None Update="AppDomainEnabled.runsettings">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

  <ItemGroup>
    <None Update="AppDomainDisabled.runsettings">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    </ItemGroup>

</Project>
  
#file AppDomainEnabled.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <!-- Currently, the default is already false, but we want to ensure the
             test runs with AppDomain enabled even if we changed the default -->
        <DisableAppDomain>false</DisableAppDomain>
    </RunConfiguration>
</RunSettings>

#file AppDomainDisabled.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <DisableAppDomain>true</DisableAppDomain>
    </RunConfiguration>
</RunSettings>

#file UnitTest1.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Test for https://github.com/microsoft/testfx/issues/2390
[TestClass]
public class ParameterizedTestSerializationIssue2390
{
    [TestMethod]
    [DataRow((byte)0, new object[] { (byte)0 })]
    [DataRow((short)0, new object[] { (short)0 })]
    [DataRow(0L, new object[] { 0L })]
    public void CheckNestedInputTypes(object expected, object nested)
    {
        object[] array = (object[])nested;
        object actual = Assert.ContainsSingle(array);

#if NETFRAMEWORK
        var appDomainEnabled = Environment.GetCommandLineArgs().Contains("AppDomainEnabled.runsettings");
        if (appDomainEnabled)
        {
            // Buggy behavior, because of app domains.
            Assert.AreEqual(typeof(int), actual.GetType(), AppDomain.CurrentDomain.FriendlyName);
        }
        else
#endif
        {
            Assert.AreEqual(expected.GetType(), actual.GetType(), AppDomain.CurrentDomain.FriendlyName);
            Assert.AreEqual(expected, actual);
        }
    }
}

""";
    }

    public TestContext TestContext { get; set; }
}
