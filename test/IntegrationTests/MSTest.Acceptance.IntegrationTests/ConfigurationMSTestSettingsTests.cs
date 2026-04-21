// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ConfigurationMSTestSettingsTests : AcceptanceTestBase<ConfigurationMSTestSettingsTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestConfigJson_AndRunSettingsHasMstest_Throws(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPathWithMSTestRunSettings, TestAssetFixture.ProjectNameWithMSTestRunSettings, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.AssertStandardErrorContains("Both '.runsettings' and '.testconfig.json' files have been detected. Please select only one of these test configuration files.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectNameWithMSTestRunSettings = "ConfigurationMSTestSettings";

        public string ProjectPathWithMSTestRunSettings => GetAssetPath(ProjectNameWithMSTestRunSettings);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectNameWithMSTestRunSettings, ProjectNameWithMSTestRunSettings,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectNameWithMSTestRunSettings)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$AppendSettings$", MSTestSettings));

        private const string MSTestSettings = """
<mstest>
</mstest>
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
    <None Update="dummyconfigfile_map.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="dummyconfigfile_doNotMap.json">
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

#file dummyconfigfile_map.json
{
  "mstest": {
    "execution": {
      "mapInconclusiveToFailed": true,
    },
  }
}

#file dummyconfigfile_doNotMap.json
{
  "mstest": {
    "execution": {
      "mapInconclusiveToFailed": false,
    },
  }
}

#file $ProjectName$.testconfig.json
{
  "mstest": {
    "timeout": {
      "assemblyInitialize": 300000,
      "assemblyCleanup": 300000,
      "classInitialize": 200000,
      "classCleanup": 200000,
      "testInitialize": 100000,
      "testCleanup": 100000,
      "test": 60000,
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
      "considerEmptyDataSourceAsInconclusive": true
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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod()
    {
    }

    [TestMethod]
    public void TestWithConfigFromCommandLine()
    {
        if (Environment.GetEnvironmentVariable("TestWithConfigFromCommandLine") == "true")
        {
            Assert.Inconclusive("Inconclusive TestWithConfigFromCommandLine");
        }
    }
}
""";
    }
}
