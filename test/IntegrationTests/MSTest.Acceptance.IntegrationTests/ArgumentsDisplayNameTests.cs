// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ArgumentsDisplayNameTests : AcceptanceTestBase<ArgumentsDisplayNameTests.TestAssetFixture>
{
    private const string AssetName = "ArgumentsDisplayName";

    private static readonly string[] ExpectedNames =
    [
        "Parse_ReturnsNull (empty input)",
        "Validate_ReturnsFalse (empty input)",
        "Inline (label)",
        "Functional Case FC100.1",
        "Inline ( {m} {a} \"label\" )",
        "Inline (\"default\")",
        "Inline (blank full name)",
        "Dynamic unfolded (row label)",
        "Functional Case FC200.1",
        "Functional Case FC200.2",
        "Source default",
        "Dynamic unfolded (category)",
        "Dynamic unfolded (ignored)",
        "Dynamic folded (row label)",
        "Functional Case FC200.1",
        "Functional Case FC200.2",
        "Source default",
        "Dynamic folded (category)",
        "Dynamic folded (ignored)",
        "Fallback (unserializable)",
    ];

    public TestContext TestContext { get; set; }

    public static IEnumerable<(string Tfm, MetadataMode MetadataMode)> MtpCases
    {
        get
        {
            foreach (string tfm in TargetFrameworks.All)
            {
                yield return (tfm, MetadataMode.Reflection);
            }

            if (!AcceptanceSourceGen.IsGloballyDisabled)
            {
                yield return (TargetFrameworks.NetCurrent, MetadataMode.SourceGeneration);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(MtpCases))]
    public async Task Mtp_AppliesRowLabelsDuringDiscoveryAndExecution(string tfm, MetadataMode metadataMode)
    {
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, tfm, metadataMode: metadataMode);
        using TempDirectory results = new();
        TestHostResult result = await host.ExecuteAsync(
            $"--output Detailed --report-trx --report-trx-filename rows.trx --results-directory \"{results.Path}\"",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 18, skipped: 2);
        AssertResultNames(Path.Combine(results.Path, "rows.trx"));

        result = await host.ExecuteAsync(
            "--list-tests --filter \"Name~Parse_ReturnsNull&Name~empty input\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("Parse_ReturnsNull (empty input)");
        result.AssertOutputContains("found 1 test(s)");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Vstest_AppliesRowLabelsIncludingSerializationFallback(string tfm)
    {
        string projectPath = AssetFixture.GetAssetPath(AssetName);
        string extension = tfm.StartsWith("net4", StringComparison.Ordinal) ? ".exe" : ".dll";
        string assemblyPath = Path.Combine(projectPath, "bin", "Release", tfm, $"{AssetName}{extension}");
        using TempDirectory results = new();
        using CommandLine commandLine = new();
        string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
        string dotnetPath = Path.Combine(dotnetRoot, $"dotnet{Constants.ExecutableExtension}");
        var environmentVariables = new Dictionary<string, string?> { ["DOTNET_ROOT"] = dotnetRoot };
        int discoveryExitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{dotnetPath}\" vstest \"{assemblyPath}\" /ListTests",
            environmentVariables: environmentVariables,
            workingDirectory: projectPath,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, discoveryExitCode, $"{commandLine.StandardOutput}{Environment.NewLine}{commandLine.ErrorOutput}");
        Assert.Contains("Parse_ReturnsNull (empty input)", commandLine.StandardOutput);
        Assert.Contains("    Fallback", commandLine.StandardOutput);
        Assert.DoesNotContain("Fallback (unserializable)", commandLine.StandardOutput);

        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{dotnetPath}\" vstest \"{assemblyPath}\" /Logger:\"trx;LogFileName=rows.trx\" /ResultsDirectory:\"{results.Path}\"",
            environmentVariables: environmentVariables,
            workingDirectory: projectPath,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, exitCode, $"{commandLine.StandardOutput}{Environment.NewLine}{commandLine.ErrorOutput}");
        AssertResultNames(Path.Combine(results.Path, "rows.trx"));
    }

    [TestMethod]
    public async Task Mtp_RowLabelsPreserveCategoryMetadata()
    {
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await host.ExecuteAsync(
            "--output Detailed --filter TestCategory=row-category",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        result.AssertOutputContains("passed Dynamic unfolded (category)");
    }

    [TestMethod]
    [DataRow("DisplayName = \"full\", ArgumentsDisplayName = \"label\"", true)]
    [DataRow("DisplayName = \"full\"", false)]
    [DataRow("ArgumentsDisplayName = \"label\"", false)]
    public async Task Analyzer_ReportsConflictsThroughPackedFrameworkDependency(string properties, bool expectsDiagnostic)
    {
        string source = $$"""
            #file ConflictingDisplayNames.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{TargetFrameworks.NetCurrent}}</TargetFramework>
                <LangVersion>12</LangVersion>
                <RunAnalyzers>true</RunAnalyzers>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MSTest.TestFramework" Version="{{MSTestVersion}}" />
              </ItemGroup>
            </Project>

            #file Tests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
            [TestClass]
            public sealed class Tests
            {
                [TestMethod]
                [DataRow("input", {{properties}})]
                public void Test(string value) => Assert.AreEqual("input", value);

                public static TestDataRow<string> Create()
                    => new TestDataRow<string>("input") { {{properties}} };
            }
            """;
        using TestAsset asset = await TestAsset.GenerateAssetAsync("ConflictingDisplayNames", source);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -c Release",
            workingDirectory: asset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        if (expectsDiagnostic)
        {
            Assert.AreNotEqual(0, result.ExitCode, result.ToString());
            result.AssertOutputContains("MSTEST0089");
            result.AssertOutputContains("'ArgumentsDisplayName' is ignored because 'DisplayName' overrides the entire test case name");
        }
        else
        {
            Assert.AreEqual(0, result.ExitCode, result.ToString());
            result.AssertOutputDoesNotContain("MSTEST0089");
        }
    }

    private static void AssertResultNames(string trxPath)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement[] results = XDocument.Load(trxPath).Descendants(ns + "UnitTestResult")
            .Where(result => !result.Descendants(ns + "UnitTestResult").Any())
            .ToArray();

        Assert.AreSequenceEqual(
            ExpectedNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            results.Select(result => (string?)result.Attribute("testName")).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.HasCount(18, results.Where(result => (string?)result.Attribute("outcome") == "Passed"));
        Assert.HasCount(2, results.Where(result => (string?)result.Attribute("outcome") == "NotExecuted"));
        Assert.AreEqual(results.Length, results.Select(result => (string?)result.Attribute("executionId")).Distinct().Count());
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName, SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion));

        private const string SourceCode = """
#file ArgumentsDisplayName.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
  </ItemGroup>
</Project>

#file TestClass.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable MSTEST0089 // Intentional conflicts verify runtime naming precedence.

[TestClass]
public sealed class NamingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("", ArgumentsDisplayName = "empty input")]
    public void Parse_ReturnsNull(string input)
    {
        Assert.AreEqual("", input);
        Assert.AreEqual("Parse_ReturnsNull (empty input)", TestContext.TestDisplayName);
    }

    [TestMethod]
    [DataRow("", ArgumentsDisplayName = "empty input")]
    public void Validate_ReturnsFalse(string input)
    {
        Assert.AreEqual("", input);
        Assert.AreEqual("Validate_ReturnsFalse (empty input)", TestContext.TestDisplayName);
    }

    [TestMethod(DisplayName = "Inline")]
    [DataRow("label", ArgumentsDisplayName = "label")]
    [DataRow("full", DisplayName = "Functional Case FC100.1", ArgumentsDisplayName = "ignored label")]
    [DataRow("literal", ArgumentsDisplayName = " {m} {a} \"label\" ")]
    [DataRow("default", ArgumentsDisplayName = " \t")]
    [DataRow("blank", DisplayName = " \t", ArgumentsDisplayName = "blank full name")]
    public void InlineRows(string value)
    {
        string expected = value switch
        {
            "label" => "Inline (label)",
            "full" => "Functional Case FC100.1",
            "literal" => "Inline ( {m} {a} \"label\" )",
            "default" => "Inline (\"default\")",
            "blank" => "Inline (blank full name)",
            _ => throw new InvalidOperationException(value),
        };
        Assert.AreEqual(expected, TestContext.TestDisplayName);
    }

    [TestMethod(DisplayName = "Dynamic unfolded", UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [DynamicData(nameof(Rows), DynamicDataDisplayName = nameof(SourceDisplayName))]
    public void DynamicUnfolded(string value, int number) => AssertDynamicName(value, number, "Dynamic unfolded");

    [TestMethod(DisplayName = "Dynamic folded", UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(Rows), DynamicDataDisplayName = nameof(SourceDisplayName))]
    public void DynamicFolded(string value, int number) => AssertDynamicName(value, number, "Dynamic folded");

    private void AssertDynamicName(string value, int number, string methodDisplayName)
    {
        Assert.AreEqual(42, number);
        string expected = value switch
        {
            "label" => methodDisplayName + " (row label)",
            "full" => "Functional Case FC200.1",
            "both" => "Functional Case FC200.2",
            "default" => "Source default",
            "category" => methodDisplayName + " (category)",
            _ => throw new InvalidOperationException("Ignored rows must not execute: " + value),
        };
        Assert.AreEqual(expected, TestContext.TestDisplayName);
    }

    public static IEnumerable<TestDataRow<(string, int)>> Rows =>
    [
        new(("label", 42)) { ArgumentsDisplayName = "row label" },
        new(("full", 42)) { DisplayName = "Functional Case FC200.1" },
        new(("both", 42)) { DisplayName = "Functional Case FC200.2", ArgumentsDisplayName = "ignored label" },
        new(("default", 42)) { ArgumentsDisplayName = " \t" },
        new(("category", 42)) { ArgumentsDisplayName = "category", TestCategories = ["row-category"] },
        new(("ignored", 42)) { ArgumentsDisplayName = "ignored", IgnoreMessage = "not ready" },
    ];

    public static string SourceDisplayName(MethodInfo method, object[] data)
        => data[0] is "default" ? "Source default" : throw new InvalidOperationException("Row metadata must take precedence.");

    [TestMethod(DisplayName = "Fallback")]
    [DynamicData(nameof(UnserializableRows))]
    public void Unserializable(Type value)
    {
        Assert.AreEqual(typeof(string), value);
        Assert.AreEqual("Fallback (unserializable)", TestContext.TestDisplayName);
    }

    public static IEnumerable<TestDataRow<Type>> UnserializableRows =>
    [
        new(typeof(string)) { ArgumentsDisplayName = "unserializable" },
    ];
}
""";
    }
}
