// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AnalyzersTests : AcceptanceTestBase<NopAssetFixture>
{
    [TestMethod]
    public async Task AnalyzersShouldBeEnabledWhenUsingMetapackage()
    {
        string code = """
#file AnalyzersMetapackage.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <RunAnalyzers>true</RunAnalyzers>

    <!-- This also serves as a test that VSTest is generating the entrypoint, even when it's a transitive dependency -->
    <OutputType>Exe</OutputType>

    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
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

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync("AnalyzersMetapackage", code);
        DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, warnAsError: false);
        result.AssertOutputContains("MSTEST0014");
    }

    [TestMethod]
    public async Task AnalyzersShouldBeEnabledWhenUsingTestFrameworkPackage()
    {
        string code = """
#file AnalyzersTestFrameworkPackage.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <RunAnalyzers>true</RunAnalyzers>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
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

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync("AnalyzersTestFrameworkPackage", code);
        DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, warnAsError: false);
        result.AssertOutputContains("MSTEST0014");
    }

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
    [DataRow("None", new string[0], new[] { "MSTEST0003", "MSTEST0004", "MSTEST0014", "MSTEST0016", "MSTEST0021" })]
    [DataRow("", new[] { "warning MSTEST0003", "warning MSTEST0014" }, new[] { "MSTEST0004", "MSTEST0016", "MSTEST0021" })]
    [DataRow("Default", new[] { "warning MSTEST0003", "warning MSTEST0014" }, new[] { "MSTEST0004", "MSTEST0016", "MSTEST0021" })]
    [DataRow("Recommended", new[] { "error MSTEST0003", "warning MSTEST0014", "warning MSTEST0016" }, new[] { "MSTEST0004", "MSTEST0021" })]
    [DataRow("All", new[] { "error MSTEST0003", "warning MSTEST0004", "warning MSTEST0014", "warning MSTEST0016" }, new[] { "MSTEST0021" })]
    public async Task VerifyMSTestAnalysisModeForDifferentAnalyzers(string analysisMode, string[] contains, string[] doesNotContain)
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
    <MSTestAnalysisMode>$MSTestAnalysisMode$</MSTestAnalysisMode>
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
public sealed MyEmptyTestClass // generates MSTEST0016
{
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
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
    .PatchCodeWithReplace("$MSTestAnalysisMode$", analysisMode);

        // MSTEST0003 is TestMethodShouldBeValidAnalyzer, which is escalated to error in Recommended and All.
        // MSTEST0004 is PublicTypeShouldBeTestClassAnalyzer. Info and not enabled by default.
        // MSTEST0014 is DataRowShouldBeValidAnalyzer. Warn and enabled by default.
        // MSTEST0016 is TestClassShouldHaveTestMethodAnalyzer. Info and enabled by default.
        // MSTEST0021 is PreferDisposeOverTestCleanupAnalyzer, which is disabled even in All mode.
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(VerifyMSTestAnalysisModeForDifferentAnalyzers), code);
        await AssertAnalysisModeAsync(analysisMode, contains, doesNotContain, testAsset.TargetAssetPath);
    }

    private static async Task AssertAnalysisModeAsync(string mode, string[] contains, string[] doesNotContain, string targetAssetPath)
    {
        async Task<DotnetMuxerResult> BuildTaskAsync() => await DotnetCli.RunAsync($"build {targetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, warnAsError: false, retryCount: 0);

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
            StringAssert.Contains(output, containsElement, $"Expected to find '{containsElement}' for analysisMode {mode}");
        }

        foreach (string doesNotContainElement in doesNotContain)
        {
            Assert.IsFalse(output.Contains(doesNotContainElement), $"Expected to not find '{doesNotContainElement}' for analysisMode {mode}");
        }
    }
}
