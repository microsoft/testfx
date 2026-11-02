// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ArgumentsDisplayNameTests : AcceptanceTestBase<ArgumentsDisplayNameTests.TestAssetFixture>
{
    private const string AssetName = "ArgumentsDisplayName";
    private const string InlineMarkupName = """Inline report (literal </script><b>"quoted" & café</b>)""";
    private const string DynamicMarkupName = """Dynamic report (literal </script><b>"quoted" & café</b>)""";
    private const string InlineLongName = "Inline report (row with a deliberately long argument label that must remain complete in reports and terminal output — café 日本語 🧪)";
    private const string DynamicLongName = "Dynamic report (row with a deliberately long argument label that must remain complete in reports and terminal output — café 日本語 🧪)";

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
        InlineMarkupName,
        InlineLongName,
        DynamicMarkupName,
        DynamicLongName,
    ];

    private static readonly (string MethodName, string DisplayName, bool IsIgnored)[] ExpectedReportRows =
    [
        ("Parse_ReturnsNull", "Parse_ReturnsNull (empty input)", false),
        ("Validate_ReturnsFalse", "Validate_ReturnsFalse (empty input)", false),
        ("InlineRows", "Inline (label)", false),
        ("InlineRows", "Functional Case FC100.1", false),
        ("InlineRows", "Inline ( {m} {a} \"label\" )", false),
        ("InlineRows", "Inline (\"default\")", false),
        ("InlineRows", "Inline (blank full name)", false),
        ("DynamicUnfolded", "Dynamic unfolded (row label)", false),
        ("DynamicUnfolded", "Functional Case FC200.1", false),
        ("DynamicUnfolded", "Functional Case FC200.2", false),
        ("DynamicUnfolded", "Source default", false),
        ("DynamicUnfolded", "Dynamic unfolded (category)", false),
        ("DynamicUnfolded", "Dynamic unfolded (ignored)", true),
        ("DynamicFolded", "Dynamic folded (row label)", false),
        ("DynamicFolded", "Functional Case FC200.1", false),
        ("DynamicFolded", "Functional Case FC200.2", false),
        ("DynamicFolded", "Source default", false),
        ("DynamicFolded", "Dynamic folded (category)", false),
        ("DynamicFolded", "Dynamic folded (ignored)", true),
        ("InlineReportRows", InlineMarkupName, false),
        ("InlineReportRows", InlineLongName, false),
        ("DynamicReportRows", DynamicMarkupName, false),
        ("DynamicReportRows", DynamicLongName, false),
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

    public static IEnumerable<(string Tfm, MetadataMode MetadataMode)> ReportCases
        => MtpCases.Where(testCase => testCase.Tfm == TargetFrameworks.NetCurrent);

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
        result.AssertOutputContainsSummary(failed: 0, passed: 22, skipped: 2);
        AssertResultNames(Path.Combine(results.Path, "rows.trx"));

        result = await host.ExecuteAsync(
            "--list-tests --filter \"Name~Parse_ReturnsNull&Name~empty input\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("Parse_ReturnsNull (empty input)");
        result.AssertOutputContains("found 1 test(s)");
    }

    [TestMethod]
    [DynamicData(nameof(ReportCases))]
    public async Task Mtp_RowNamesAndIdentitiesArePreservedInTerminalTrxAndHtml(string tfm, MetadataMode metadataMode)
    {
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, tfm, metadataMode: metadataMode);
        string resultsDirectory = TestContext.TestTempDirectory!;
        string discoveryPath = Path.Combine(resultsDirectory, "discovery.json");
        string terminalPath = Path.Combine(resultsDirectory, "terminal.txt");
        string trxPath = Path.Combine(resultsDirectory, "rows.trx");
        string htmlPath = Path.Combine(resultsDirectory, "rows.html");

        // The unserializable fallback has separate discovery/execution coverage above.
        // Folded data intentionally has one discovered UID but six final report rows.
        const string Filter = "--filter \"ClassName=NamingTests&FullyQualifiedName!~Unserializable\"";
        TestHostResult discovery = await host.ExecuteAsync(
            $"{Filter} --list-tests json --results-directory \"{resultsDirectory}\"",
            new() { ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US" },
            cancellationToken: TestContext.CancellationToken);

        await File.WriteAllTextAsync(discoveryPath, discovery.StandardOutput, TestContext.CancellationToken);
        TestContext.AddResultFile(discoveryPath);
        discovery.AssertExitCodeIs(ExitCode.Success);
        Dictionary<(string MethodName, string DisplayName), string> discoveredUids = AssertDiscoveredReportRows(discovery.StandardOutput);

        TestHostResult result = await host.ExecuteAsync(
            $"{Filter} --output Detailed --show-test-results all --report-trx --report-trx-filename rows.trx --report-html --report-html-filename rows.html --results-directory \"{resultsDirectory}\"",
            new() { ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US" },
            cancellationToken: TestContext.CancellationToken);

        await File.WriteAllTextAsync(terminalPath, result.StandardOutput + result.StandardError, TestContext.CancellationToken);
        TestContext.AddResultFile(terminalPath);
        if (File.Exists(trxPath))
        {
            // This is a nested consumer report, not this acceptance host's TRX.
            // Keep its exact bytes as a text attachment so dotnet test does not
            // try to merge the child rows into the outer acceptance results.
            string trxAttachmentPath = Path.Combine(resultsDirectory, "rows.trx.txt");
            File.Copy(trxPath, trxAttachmentPath);
            TestContext.AddResultFile(trxAttachmentPath);
        }

        if (File.Exists(htmlPath))
        {
            TestContext.AddResultFile(htmlPath);
        }

        TestContext.WriteLine($"Packed consumer: {host.FullName}");
        TestContext.WriteLine($"Report artifacts: {resultsDirectory}");
        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 21, skipped: 2);
        AssertTerminalReportRows(result);
        AssertTrxReportRows(trxPath);
        AssertHtmlReportRows(htmlPath, discoveredUids);
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
        Assert.HasCount(22, results.Where(result => (string?)result.Attribute("outcome") == "Passed"));
        Assert.HasCount(2, results.Where(result => (string?)result.Attribute("outcome") == "NotExecuted"));
        Assert.AreEqual(results.Length, results.Select(result => (string?)result.Attribute("executionId")).Distinct().Count());
    }

    private static Dictionary<(string MethodName, string DisplayName), string> AssertDiscoveredReportRows(string output)
    {
        using var document = JsonDocument.Parse(output);
        JsonElement[] tests = [.. document.RootElement.GetProperty("tests").EnumerateArray()];
        (string MethodName, string DisplayName)[] expected = [.. ExpectedReportRows
            .Where(row => row.MethodName != "DynamicFolded")
            .Select(row => (row.MethodName, row.DisplayName))
            .Append(("DynamicFolded", "Dynamic folded"))];

        Assert.HasCount(18, tests);
        Assert.AreSequenceEqual(
            expected,
            tests.Select(test => (test.GetProperty("type").GetProperty("methodName").GetString()!, test.GetProperty("displayName").GetString()!)),
            SequenceOrder.InAnyOrder);
        Assert.HasCount(18, tests.Select(test => test.GetProperty("uid").GetString()).Distinct(StringComparer.Ordinal));
        foreach (JsonElement test in tests)
        {
            Assert.AreEqual("NamingTests", test.GetProperty("type").GetProperty("typeName").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(test.GetProperty("uid").GetString()), "Every discovered row or folded method needs an identity.");
        }

        return tests.ToDictionary(
            test => (test.GetProperty("type").GetProperty("methodName").GetString()!, test.GetProperty("displayName").GetString()!),
            test => test.GetProperty("uid").GetString()!);
    }

    private static void AssertTerminalReportRows(TestHostResult result)
    {
        // Results can complete in any order. Ignore only the variable duration, not
        // the name, status, whitespace within the label, or duplicate full names.
        string[] lines = [.. result.StandardOutputLines
            .Where(line => line.StartsWith("passed ", StringComparison.Ordinal) || line.StartsWith("skipped ", StringComparison.Ordinal))
            .Select(line => Regex.Replace(line, $" {AcceptanceAssert.DurationPattern}$", string.Empty))];
        Assert.AreSequenceEqual(
            ExpectedReportRows.Select(row => $"{(row.IsIgnored ? "skipped" : "passed")} {row.DisplayName}"),
            lines,
            SequenceOrder.InAnyOrder,
            result.StandardOutput);
    }

    private static void AssertTrxReportRows(string path)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var document = XDocument.Load(path);
        XElement[] results = [.. document.Root!.Element(ns + "Results")!.Elements(ns + "UnitTestResult")];
        var definitions = document.Descendants(ns + "UnitTest").ToDictionary(test => test.Attribute("id")!.Value);
        Assert.HasCount(23, results);
        Assert.HasCount(18, definitions);
        Assert.HasCount(18, results.Select(result => result.Attribute("testId")!.Value).Distinct(StringComparer.Ordinal));
        Assert.HasCount(23, results.Select(result => result.Attribute("executionId")!.Value).Distinct(StringComparer.Ordinal));
        Assert.AreSequenceEqual(
            ExpectedReportRows.Select(row => (row.MethodName, row.DisplayName, row.IsIgnored ? "NotExecuted" : "Passed")),
            results.Select(result => (
                definitions[result.Attribute("testId")!.Value].Element(ns + "TestMethod")!.Attribute("name")!.Value,
                result.Attribute("testName")!.Value,
                result.Attribute("outcome")!.Value)),
            SequenceOrder.InAnyOrder);
        Assert.AreSequenceEqual(
            results.Select(result => (result.Attribute("testId")!.Value, result.Attribute("executionId")!.Value)),
            document.Descendants(ns + "TestEntry").Select(entry => (entry.Attribute("testId")!.Value, entry.Attribute("executionId")!.Value)),
            SequenceOrder.InAnyOrder);
        foreach (XElement result in results)
        {
            Assert.AreNotEqual(Guid.Empty, Guid.Parse(result.Attribute("executionId")!.Value));
            Assert.AreEqual("NamingTests", definitions[result.Attribute("testId")!.Value].Element(ns + "TestMethod")!.Attribute("className")!.Value);
        }

        XElement[] folded = [.. results.Where(result => definitions[result.Attribute("testId")!.Value].Element(ns + "TestMethod")!.Attribute("name")!.Value == "DynamicFolded")];
        Assert.HasCount(6, folded);
        Assert.HasCount(1, folded.Select(result => result.Attribute("testId")!.Value).Distinct(StringComparer.Ordinal));
    }

    private static void AssertHtmlReportRows(string path, Dictionary<(string MethodName, string DisplayName), string> discoveredUids)
    {
        // This verifies the persisted content/identity consumed by the report UI.
        // It does not execute its JavaScript or prove rendered layout/interaction.
        string html = File.ReadAllText(path);
        Match payload = Regex.Match(html, """<script id="mtp-data" type="application/json">(?<json>.*?)</script>""", RegexOptions.Singleline);
        Assert.IsTrue(payload.Success, "The real HTML report must contain its complete embedded data document.");
        using var document = JsonDocument.Parse(payload.Groups["json"].Value);
        JsonElement root = document.RootElement;
        JsonElement[] tests = [.. root.GetProperty("tests").EnumerateArray()];
        Assert.HasCount(23, tests);
        Assert.AreSequenceEqual(
            ExpectedReportRows.Select(row => (row.MethodName, row.DisplayName, row.IsIgnored ? "skipped" : "passed")),
            tests.Select(test => (test.GetProperty("methodName").GetString()!, test.GetProperty("displayName").GetString()!, test.GetProperty("outcome").GetString()!)),
            SequenceOrder.InAnyOrder);
        Assert.AreSequenceEqual(Enumerable.Range(0, 23), tests.Select(test => test.GetProperty("rowKey").GetInt32()), SequenceOrder.InAnyOrder);
        Assert.HasCount(18, tests.Select(test => test.GetProperty("uid").GetString()).Distinct(StringComparer.Ordinal));
        Assert.AreEqual(0, root.GetProperty("exitCode").GetInt32());
        JsonElement summary = root.GetProperty("summary");
        Assert.AreEqual(23, summary.GetProperty("total").GetInt32());
        Assert.AreEqual(21, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(2, summary.GetProperty("skipped").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("failed").GetInt32());

        foreach (JsonElement test in tests)
        {
            string method = test.GetProperty("methodName").GetString()!;
            string name = test.GetProperty("displayName").GetString()!;
            Assert.AreEqual("NamingTests", test.GetProperty("className").GetString());
            Assert.AreEqual(discoveredUids[(method, method == "DynamicFolded" ? "Dynamic folded" : name)], test.GetProperty("uid").GetString());
            if (method == "DynamicFolded")
            {
                Assert.AreEqual(6, test.GetProperty("attemptOf").GetInt32());
            }
            else
            {
                Assert.IsFalse(test.TryGetProperty("attemptIndex", out _), "Distinct unfolded rows must not become retry/attempt groups.");
                Assert.IsFalse(test.TryGetProperty("attemptOf", out _));
            }

            if (method is "InlineReportRows" or "DynamicReportRows")
            {
                Assert.AreEqual("row display: " + name, test.GetProperty("standardOutput").GetString(), "Expandable output must stay associated with its own named row.");
            }
        }

        Assert.AreSequenceEqual(
            Enumerable.Range(1, 6),
            tests.Where(test => test.GetProperty("methodName").GetString() == "DynamicFolded").Select(test => test.GetProperty("attemptIndex").GetInt32()),
            SequenceOrder.InAnyOrder);
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
    <PackageReference Include="Microsoft.Testing.Extensions.HtmlReport" Version="$MicrosoftTestingPlatformVersion$" />
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

    [TestMethod(DisplayName = "Inline report")]
    [DataRow("Inline report (literal </script><b>\"quoted\" & café</b>)", ArgumentsDisplayName = "literal </script><b>\"quoted\" & café</b>")]
    [DataRow("Inline report (row with a deliberately long argument label that must remain complete in reports and terminal output — café 日本語 🧪)", ArgumentsDisplayName = "row with a deliberately long argument label that must remain complete in reports and terminal output — café 日本語 🧪")]
    public void InlineReportRows(string expectedName) => AssertReportName(expectedName);

    [TestMethod(DisplayName = "Dynamic report", UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [DynamicData(nameof(ReportRows))]
    public void DynamicReportRows(string expectedName) => AssertReportName(expectedName);

    public static IEnumerable<TestDataRow<string>> ReportRows =>
    [
        new("Dynamic report (literal </script><b>\"quoted\" & café</b>)") { ArgumentsDisplayName = "literal </script><b>\"quoted\" & café</b>" },
        new("Dynamic report (row with a deliberately long argument label that must remain complete in reports and terminal output — café 日本語 🧪)") { ArgumentsDisplayName = "row with a deliberately long argument label that must remain complete in reports and terminal output — café 日本語 🧪" },
    ];

    private void AssertReportName(string expectedName)
    {
        Assert.AreEqual(expectedName, TestContext.TestDisplayName);
        Console.Write("row display: " + expectedName);
    }
}

#file Utf8Console.cs
#if !NETFRAMEWORK
using System;
using System.Runtime.CompilerServices;
using System.Text;

internal static class Utf8Console
{
    // ProcessFactory decodes redirected output as UTF-8. Arrange the matching
    // child encoding before the generated entry point creates its console.
    [ModuleInitializer]
    internal static void Initialize() => Console.OutputEncoding = new UTF8Encoding(false);
}
#endif
""";
    }
}
