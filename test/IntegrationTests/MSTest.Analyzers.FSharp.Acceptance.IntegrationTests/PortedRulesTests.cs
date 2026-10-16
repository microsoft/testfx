// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace MSTest.Analyzers.FSharp.Acceptance.IntegrationTests;

public sealed partial class FSharpAnalyzersTests
{
    [TestMethod]
    public async Task DataRows_PreserveDistinctGenericDiagnosticsAtTheSameLocation()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(null, null)>]
    member _.Run<'T, 'U>(first: 'T, second: 'U) = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, FindingsExitCode, "MSTEST0014");
        Assert.HasCount(2, diagnostics);
        Assert.AreSequenceEqual(
            new[] { "The type of the generic parameter 'T' could not be inferred.", "The type of the generic parameter 'U' could not be inferred." },
            diagnostics.Select(diagnostic => diagnostic.GetProperty("message").GetProperty("text").GetString()!).Order().ToArray());
    }

    [TestMethod]
    [DataRow("""{"MSTEST0017":"invalid"}""", "Invalid severity", DisplayName = "InvalidConfiguration_InvalidSeverity")]
    [DataRow("""{"MSTEST0017":"none","MSTEST0017":"error"}""", "Duplicate analyzer setting", DisplayName = "InvalidConfiguration_DuplicateSetting")]
    public async Task InvalidConfiguration_FailsWithoutSuccessShapedFallback(string settings, string expectedError)
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""";
        using TestAsset asset = await GenerateConsumerAsync(source);
        await File.WriteAllTextAsync(Path.Combine(asset.TargetAssetPath, "mstest-analyzers.json"), settings, testContext.CancellationToken);
        await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\"", cancellationToken: testContext.CancellationToken);
        string analyzerPath = await AssertPackedProvenanceAsync(asset.TargetAssetPath);
        string reportPath = Path.Combine(asset.TargetAssetPath, "diagnostics.sarif");
        (int exitCode, string standardOutput, string standardError) = await RunAnalyzerCliAsync(asset.TargetAssetPath, analyzerPath, reportPath);
        Assert.AreEqual(22, exitCode, standardOutput + standardError);
        Assert.Contains(expectedError, standardOutput + standardError);
        Assert.IsFalse(File.Exists(reportPath), "Invalid configuration must not produce a success-shaped diagnostic report.");
    }

    [TestMethod]
    public void RuleInventory_AccountsForEveryCurrentDiagnostic()
    {
        string source = File.ReadAllText(Path.Combine(Constants.Root, "src", "Analyzers", "MSTest.Analyzers", "Helpers", "DiagnosticIds.cs"));
        string[] current = [.. Regex.Matches(source, @"(?m)^\s*public const string \w+ = ""(MSTEST\d+)"";").Select(match => match.Groups[1].Value)];
        string[] excluded = ["MSTEST0026", "MSTEST0027", "MSTEST0028", "MSTEST0033", "MSTEST0040", "MSTEST0047", "MSTEST0055"];
        (string Rule, string Source, bool ShouldReport)[] cases = [.. DataRuleCases.Cases, .. DeclarationRuleCases.Cases, .. ExpressionRuleCases.Cases];
        string[] pilotAndCrossAssembly = ["MSTEST0003", "MSTEST0017", "MSTEST0066", "MSTEST0082"];
        string[] ported = [.. cases.Select(scenario => scenario.Rule).Concat(pilotAndCrossAssembly).Distinct().Order()];
        Assert.HasCount(77, ported);
        Assert.AreSequenceEqual(current.Except(excluded).Order().ToArray(), ported);
        foreach (string rule in ported.Except(pilotAndCrossAssembly))
        {
            Assert.IsNotEmpty(cases.Where(scenario => scenario.Rule == rule && scenario.ShouldReport), $"{rule} has no positive package-consumer case.");
            Assert.IsNotEmpty(cases.Where(scenario => scenario.Rule == rule && !scenario.ShouldReport), $"{rule} has no negative package-consumer case.");
        }
    }

    [TestMethod]
    public async Task DefaultConfiguration_DoesNotEnableOpposingDesignPreferences()
    {
        const string source = """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
    [<TestInitialize>]
    member _.Initialize() = ()
    [<TestCleanup>]
    member _.Cleanup() = ()
    interface IDisposable with
        member _.Dispose() = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, useDefaults: true));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task InheritedFixture_ReportsOnlyMismatchedFrameworkIdentity(bool legacy)
    {
        string frameworkPackage = Directory.GetFiles(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework.*.nupkg")
            .Concat(Directory.GetFiles(Constants.ArtifactsTmpPackages, "MSTest.TestFramework.*.nupkg")).Single();
        using System.IO.Compression.ZipArchive frameworkArchive = System.IO.Compression.ZipFile.OpenRead(frameworkPackage);
        using Stream manifest = frameworkArchive.GetEntry("MSTest.TestFramework.nuspec")!.Open();
        string version = legacy ? "3.11.1" : XDocument.Load(manifest).Descendants().Single(element => element.Name.LocalName == "version").Value;
        string source = $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    inherit Legacy.Fixture()
    [<TestMethod>]
    member _.Run() = ()
#file Legacy/Legacy.fsproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <Compile Include="Fixture.fs" />
    <PackageReference Include="MSTest.TestFramework" Version="{{version}}" PrivateAssets="all" />
  </ItemGroup>
</Project>
#file Legacy/Fixture.fs
namespace Legacy
open Microsoft.VisualStudio.TestTools.UnitTesting
type OldTestAttribute() =
    inherit TestMethodAttribute()
type Fixture() =
    [<TestInitialize>]
    member _.Initialize() = ()
    [<OldTest>]
    member _.InheritedTest() = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, legacy ? FindingsExitCode : 0, "MSTEST0082");
        if (legacy)
        {
            Assert.HasCount(2, diagnostics);
            foreach (JsonElement diagnostic in diagnostics)
            {
                Assert.AreEqual("MSTEST0082", diagnostic.GetProperty("ruleId").GetString());
                Assert.Contains("Microsoft.VisualStudio.TestPlatform.TestFramework", diagnostic.GetProperty("message").GetProperty("text").GetString()!);
            }
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    public static IEnumerable<TestDataRow<(string Rule, string Source, bool ShouldReport)>> PortedRuleCases
    {
        get
        {
            IEnumerable<(string Rule, string Source, bool ShouldReport)> cases = DataRuleCases.Cases.Concat(DeclarationRuleCases.Cases).Concat(ExpressionRuleCases.Cases);
            int index = 0;
            foreach ((string Rule, string Source, bool ShouldReport) scenario in cases)
            {
                yield return new(scenario) { DisplayName = $"PortedRules_{scenario.Rule}_{(scenario.ShouldReport ? "Positive" : "Negative")}_{++index}" };
            }
        }
    }

    public static IEnumerable<TestDataRow<(string Rule, string Source, bool ShouldReport, IReadOnlyDictionary<string, string> Settings)>> ConfiguredRuleCases =>
        DeclarationRuleCases.ConfiguredCases.Select((scenario, index) => new TestDataRow<(string Rule, string Source, bool ShouldReport, IReadOnlyDictionary<string, string> Settings)>(scenario)
        {
            DisplayName = $"ConfiguredRules_{scenario.Rule}_{(scenario.ShouldReport ? "Positive" : "Negative")}_{index + 1}",
        });

    [TestMethod]
    [DynamicData(nameof(ConfiguredRuleCases))]
    public async Task ConfiguredRules_HonorProjectCapabilities(string rule, string source, bool shouldReport, IReadOnlyDictionary<string, string> settings)
    {
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, rule, settings);
        Assert.AreEqual(shouldReport, diagnostics.Length > 0);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual(rule, diagnostic.GetProperty("ruleId").GetString());
        }
    }

    [TestMethod]
    [DynamicData(nameof(PortedRuleCases))]
    public async Task PortedRules_ReportOnlyApplicableDiagnostics(string rule, string source, bool shouldReport)
    {
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, rule);
        if (shouldReport)
        {
            Assert.IsNotEmpty(diagnostics);
            foreach (JsonElement diagnostic in diagnostics)
            {
                Assert.AreEqual(rule, diagnostic.GetProperty("ruleId").GetString());
                Assert.IsNotNull(diagnostic.GetProperty("message").GetProperty("text").GetString());
                JsonElement region = diagnostic.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region");
                Assert.IsGreaterThan(0, region.GetProperty("startLine").GetInt32());
            }
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }
}
