﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AnalyzersTests : AcceptanceTestBase<NopAssetFixture>
{
    [TestMethod]
    public async Task AnalyzerMessagesShouldBeLocalized()
    {
        string code = """
#file Analyzers.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">

  <PropertyGroup>
    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
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
        DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, environmentVariables: new()
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = "it-IT",
            ["PreferredUILang"] = "it-IT",
            ["VSLang"] = "1040",
        }, warnAsError: false);
        result.AssertOutputContains("DataRow deve essere impostato solo su un metodo di test");
    }

    [TestMethod]
    public async Task VerifyMSTestAnalysisModeForDifferentAnalyzers()
    {
        string code = """
#file VerifyMSTestAnalysisModeForDifferentAnalyzers.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">

  <PropertyGroup>
    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
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
    public void TestMethod1()
    {
    }

    [DataRow(0)]
    public void TestMethod2(int x)
    {
        Assert.AreEqual(x, 0);
    }
}

public class UnitTest2
{
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(VerifyMSTestAnalysisModeForDifferentAnalyzers), code);
        await AssertInAllAnalysisModesAsync(["Default", "Recommended", "All"], "MSTEST0014", testAsset.TargetAssetPath);
        await AssertInAllAnalysisModesAsync(["Recommended", "All"], "MSTEST0017", testAsset.TargetAssetPath);
        await AssertInAllAnalysisModesAsync(["All"], "MSTEST0004", testAsset.TargetAssetPath);
    }

    private static async Task AssertInAllAnalysisModesAsync(string[] enabledIn, string message, string targetAssetPath)
    {
        string[] modes = ["None", string.Empty, "Default", "Recommended", "All"];
        foreach (string mode in modes)
        {
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build {targetAssetPath} -p:MSTestAnalysisMode={mode}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, warnAsError: false);
            if (enabledIn.Contains(mode) || (mode.Length == 0 && enabledIn.Contains("Default")))
            {
                result.AssertOutputContains(message);
            }
            else
            {
                result.AssertOutputDoesNotContain(message);
            }
        }
    }
}
