// Copyright (c) Microsoft Corporation. All rights reserved.
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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [DataRow(0)]
    [TestMethod]
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

[TestClass]
public class UnitTest3
{
    [TestCleanup]
    public void MyTestCleanup()
    {
    }
}

[TestClass]
public class UnitTest4
{
    [TestMethod]
    public async void TestMethod1()
    {
        await Task.CompletedTask;
    }
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        // MSTEST0003 is TestMethodShouldBeValidAnalyzer, which is escalated to error in Recommended and All.
        // MSTEST0004 is PublicTypeShouldBeTestClassAnalyzer. Info and not enabled by default.
        // MSTEST0014 is DataRowShouldBeValidAnalyzer. Warn and enabled by default.
        // MSTEST0017 is AssertionArgsShouldBePassedInCorrectOrder. Info and enabled by default.
        // MSTEST0021 is PreferDisposeOverTestCleanupAnalyzer, which is disabled even in All mode.
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(VerifyMSTestAnalysisModeForDifferentAnalyzers), code);
        await AssertAnalysisModeAsync("None", contains: [], doesNotContain: ["MSTEST0003", "MSTEST0004", "MSTEST0014", "MSTEST0017", "MSTEST0021"], testAsset.TargetAssetPath);
        await AssertAnalysisModeAsync(string.Empty, contains: ["warning MSTEST0003", "warning MSTEST0014"], doesNotContain: ["MSTEST0004", "MSTEST0017", "MSTEST0021"], testAsset.TargetAssetPath);
        await AssertAnalysisModeAsync("Default", contains: ["warning MSTEST0003", "warning MSTEST0014"], doesNotContain: ["MSTEST0004", "MSTEST0017", "MSTEST0021"], testAsset.TargetAssetPath);
        await AssertAnalysisModeAsync("Recommended", contains: ["error MSTEST0003", "warning MSTEST0014", "warning MSTEST0017"], doesNotContain: ["MSTEST0004", "MSTEST0021"], testAsset.TargetAssetPath);
        await AssertAnalysisModeAsync("All", contains: ["error MSTEST0003", "warning MSTEST0004", "warning MSTEST0014", "warning MSTEST0017"], doesNotContain: ["MSTEST0021"], testAsset.TargetAssetPath);
    }

    private static async Task AssertAnalysisModeAsync(string mode, string[] contains, string[] doesNotContain, string targetAssetPath)
    {
        // --no-incremental is due to https://github.com/dotnet/sdk/issues/46133.
        // Not sure if it's worth trying to find a workaround for it.
        async Task<DotnetMuxerResult> BuildTaskAsync() => await DotnetCli.RunAsync($"build {targetAssetPath} -p:MSTestAnalysisMode={mode} --no-incremental", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, warnAsError: false, retryCount: 0);

        string output;
        if (mode is "Recommended" or "All")
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(BuildTaskAsync);
            output = ex.Message;
        }
        else
        {
            output = (await BuildTaskAsync()).StandardOutput;
        }

        foreach (string containsElement in contains)
        {
            StringAssert.Contains(output, containsElement, $"Expected to find '{containsElement}' for mode {mode}");
        }

        foreach (string doesNotContainElement in doesNotContain)
        {
            Assert.IsFalse(output.Contains(doesNotContainElement), $"Expected to not find '{doesNotContainElement}' for mode {mode}");
        }
    }
}
