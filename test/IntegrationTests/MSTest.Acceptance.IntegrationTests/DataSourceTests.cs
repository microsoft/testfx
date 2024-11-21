// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class DataSourceTests : AcceptanceTestBase
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
        <PackageReference Include="MSTest.TestFramework.Csv" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework.Xml" Version="$MSTestVersion$" />

        <None Include="TestData.csv">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
        <None Include="TestData.xml">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>
</Project>

#file App.config
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="microsoft.visualstudio.testtools" type="Microsoft.VisualStudio.TestTools.UnitTesting.TestConfigurationSection, Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions"/>
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
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MyTestClass
{
    public TestContext TestContext { get; set; }

    [DataTestMethod]
    [DataSource("TestData")]
    public void TestSumDataSource()
    {
        int expected = (int)TestContext.DataRow["expectedSum"];
        int num1 = (int)TestContext.DataRow["num1"];
        int num2 = (int)TestContext.DataRow["num2"];
        Assert.AreEqual(expected, num1 + num2);
    }

    [TestMethod]
    [CsvDataSource("TestData.csv")]
    public void TestSumCsv(DataRow dataRow)
    {
        int expected = (int)dataRow["expectedSum"];
        int num1 = (int)dataRow["num1"];
        int num2 = (int)dataRow["num2"];
        Assert.AreEqual(expected, num1 + num2);
    }

    [TestMethod]
    [XmlDataSource("TestData.xml")]
    public void TestSumXml(DataRow dataRow)
    {
        int expected = (int)dataRow["expectedSum"];
        int num1 = (int)dataRow["num1"];
        int num2 = (int)dataRow["num2"];
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

#file TestData.xml

<Root>
    <MyTable>
        <Num1>1</Num1>
        <Num2>1</Num2>
        <ExpectedSum>2</ExpectedSum>
    </MyTable>
    <MyTable>
        <Num1>5</Num1>
        <Num2>6</Num2>
        <ExpectedSum>11</ExpectedSum>
    </MyTable>
    <MyTable>
        <Num1>10</Num1>
        <Num2>30</Num2>
        <ExpectedSum>40</ExpectedSum>
    </MyTable>
    <MyTable>
        <Num1>1</Num1>
        <Num2>1</Num2>
        <ExpectedSum>1</ExpectedSum>
    </MyTable>
</Root>
""";

    private readonly AcceptanceFixture _acceptanceFixture;

    public DataSourceTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext) => _acceptanceFixture = acceptanceFixture;

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
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0);

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "DataSourceTests", "net472");

        TestHostResult result = await testHost.ExecuteAsync();
        result.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 3, passed: 10, skipped: 0);
    }
}
