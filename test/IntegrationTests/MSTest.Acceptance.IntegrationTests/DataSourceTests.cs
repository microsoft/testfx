// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class DataSourceTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string SourceCode = """
#file DataSourceTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net472</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />

        <None Include="TestData.csv">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>
</Project>

#file App.config
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="microsoft.visualstudio.testtools" type="Microsoft.VisualStudio.TestTools.UnitTesting.TestConfigurationSection, MSTest.TestFramework.Extensions"/>
  </configSections>
  <connectionStrings>
    <add name="ConnString" connectionString="TestData.csv" providerName="Microsoft.VisualStudio.TestTools.DataSource.CSV"/>
  </connectionStrings>
  <microsoft.visualstudio.testtools>
    <dataSources>
      <add name="TestData" connectionString="ConnString" dataTableName="TestData#csv" dataAccessMethod="Sequential"/>
    </dataSources>
  </microsoft.visualstudio.testtools>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/>
  </startup>
</configuration>


#file MyTestClass.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MyTestClass
{
    public TestContext TestContext { get; set; }

    [DataTestMethod]
    [DataSource("TestData")]
    public void TestSum()
    {
        int expected = (int)TestContext.DataRow["expectedSum"];
        int num1 = (int)TestContext.DataRow["num1"];
        int num2 = (int)TestContext.DataRow["num2"];
        Assert.AreEqual(expected, num1 + num2);
    }

    [TestMethod]
    public void MyTest()
    {
    }
}

#file TestData.csv
num1,num2,expectedSum
1,1,2
5,6,11
10,30,40
1,1,1
""";

    [TestMethod]
    public async Task TestDataSourceFromAppConfig()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Test is specific to .NET Framework.
            return;
        }

        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "DataSourceTests",
            SourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion),
            addPublicFeeds: true);

        await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c Release",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0);

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "DataSourceTests", "net472");

        TestHostResult result = await testHost.ExecuteAsync();
        result.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 1, passed: 4, skipped: 0);
    }
}
