// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class ConfigurationSettingsTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public ConfigurationSettingsTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestConfigJson_AndRunSettingsHasMstest_Throws(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPathWithMSTestRunSettings, TestAssetFixture.ProjectNameWithMSTestRunSettings, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings");

        // Assert
        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.AssertStandardErrorContains("You are using both 'runsettings' and 'testconfig' files. Please use only one type of configuration file.");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestConfigJson_AndRunSettingsHasMstestv2_Throws(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPathWithMSTestV2RunSettings, TestAssetFixture.ProjectNameWithMSTestV2RunSettings, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings");

        // Assert
        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.AssertStandardErrorContains("You are using both 'runsettings' and 'testconfig' files. Please use only one type of configuration file.");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestConfigJson_AndRunSettingsWithoutMstest_OverrideRunConfigration(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings");

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TestConfigJson_WithoutRunSettings_BuildSuccess(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "ConfigurationSettings";
        public const string ProjectNameWithMSTestRunSettings = "ConfigurationMSTestSettings";
        public const string ProjectNameWithMSTestV2RunSettings = "ConfigurationMSTestV2Settings";

        public string ProjectPath => GetAssetPath(ProjectName);

        public string ProjectPathWithMSTestRunSettings => GetAssetPath(ProjectNameWithMSTestRunSettings);

        public string ProjectPathWithMSTestV2RunSettings => GetAssetPath(ProjectNameWithMSTestV2RunSettings);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$AppendSettings$", string.Empty));

            yield return (ProjectNameWithMSTestRunSettings, ProjectNameWithMSTestRunSettings,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectNameWithMSTestRunSettings)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$AppendSettings$", MSTestSettings));

            yield return (ProjectNameWithMSTestV2RunSettings, ProjectNameWithMSTestV2RunSettings,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectNameWithMSTestV2RunSettings)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$AppendSettings$", MSTestV2Settings));
        }

        private const string MSTestSettings = """
<mstest>
</mstest>
""";

        private const string MSTestV2Settings = """
<mstestv2>
</mstestv2>
""";

        private const string SourceCode = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>Preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="*.testconfig.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file my.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
    <RunConfiguration>
        <CollectSourceInformation>true</CollectSourceInformation>
        <EXECUTIONTHREADAPARTMENTSTATE>STA</EXECUTIONTHREADAPARTMENTSTATE>
        <DisableAppDomain>true</DisableAppDomain>
    </RunConfiguration>

    $AppendSettings$
</RunSettings>

#file $ProjectName$.testconfig.json
{
  "mstest": {
    "timeout": {
      "assemblyInitialize": 300,
      "assemblyCleanup": 300,
      "classInitialize": 200,
      "classCleanup": 200,
      "testInitialize": 100,
      "testCleanup": 100,
      "test": 60,
      "useCooperativeCancellation": true
    },
    "parallelism": {
      "enabled": true,
      "workers": 4,
      "scope": "class"
    },
    "execution": {
      "collectSourceInformation": true,
      "executionApartmentState": "MTA",
      "disableAppDomain": false,
      "mapInconclusiveToFailed": true,
      "mapNotRunnableToFailed": true,
      "treatDiscoveryWarningsAsErrors": true,
      "considerEmptyDataSourceAsInconclusive": true,
      "treatClassAndAssemblyCleanupWarningsAsErrors": true,
      "considerFixturesAsSpecialTests": true
    },
    "deployment": {
      "enabled": true,
      "deployTestSourceDependencies": false,
      "deleteDeploymentDirectoryAfterTestRunIsComplete": true
    },
    "assemblyResolution": [
      {
        "path": "C:\\project\\dependencies",
        "includeSubDirectories": "true"
      },
      {
        "path": "C:\\project\\libs",
        "includeSubDirectories": "true"
      },
      {
        "path": "C:\\project\\plugins",
        "includeSubDirectories": "true"
      }
    ]
  }
}



#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod()
    {
    }
}
""";
    }
}
