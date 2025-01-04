// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class AnalyzersTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture) : AcceptanceTestBase(testExecutionContext)
{
    public async Task AnalyzerMessagesShouldBeLocalized()
    {
        string code = """
#file Analyzers.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <RunAnalyzers>true</RunAnalyzers>
  </PropertyGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [DataRow(0)]
    public void TestMethod()
    {
    }
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync("Analyzers", code);
        DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath}", acceptanceFixture.NuGetGlobalPackagesFolder.Path, environmentVariables: new()
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = "it-IT",
            ["PreferredUILang"] = "it-IT",
            ["VSLang"] = "1040",
        }, warnAsError: false);
        result.AssertOutputContains("DataRow deve essere impostato solo su un metodo di test");
    }
}
