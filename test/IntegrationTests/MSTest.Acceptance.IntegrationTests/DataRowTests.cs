// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class DataRowTests : AcceptanceTestBase
{
    private const string SourceCode = """
#file DataSourceTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file MyTestClass.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MyTestClass
{
    [DataTestMethod]
    [DataRow((byte)0, new object[] { (byte)0 })]
    [DataRow((short)0, new object[] { (short)0 })]
    [DataRow((long)0, new object[] { (long)0 })]
    public void CheckNestedInputTypes(object org, object nested)
    {
        Assert.AreEqual(org.GetType(), (((object[])nested)[0].GetType()));
    }
}
""";

    private readonly AcceptanceFixture _acceptanceFixture;

    public DataRowTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext) => _acceptanceFixture = acceptanceFixture;

    public async Task TestDataRowNumericalInArrayDoesNotLoseOriginalType()
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "DataRowTests",
            SourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent.Arguments),
            addPublicFeeds: true);

        await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c Release",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0);

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "DataSourceTests", TargetFrameworks.NetCurrent.Arguments);

        TestHostResult result = await testHost.ExecuteAsync();
        result.AssertExitCodeIs(ExitCodes.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);
    }
}
