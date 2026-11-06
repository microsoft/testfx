// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize] // The methods and metadata cases share one generated asset and its fixture lifecycle.
public sealed class ExpectedDurationTests : AcceptanceTestBase<ExpectedDurationTests.TestAssetFixture>
{
    private const string AssetName = "ExpectedDurationConsumer";
    private const string ProvenanceEnvironmentVariable = "EXPECTED_DURATION_BINARY_PROVENANCE";
    private const string PassedWarning = "Test 'DurationFeature.DurationTests.OverBudgetPass' took 150.1250 ms, exceeding its expected maximum duration of 100 ms.";
    private const string FailedWarning = "Test 'DurationFeature.DurationTests.OverBudgetFail' took 200.5000 ms, exceeding its expected maximum duration of 100 ms.";
    private const string UnfoldedWarning = "Test 'DurationFeature.DurationTests.UnfoldedRows (Unfolded above)' took 150.1250 ms, exceeding its expected maximum duration of 100 ms.";
    private const string FoldedWarning = "Test 'DurationFeature.DurationTests.FoldedRows (Folded above)' took 150.1250 ms, exceeding its expected maximum duration of 100 ms.";
    private const string RetryWarning = "Test 'DurationFeature.DurationTests.RetryFinishesOverBudget' took 150.1250 ms, exceeding its expected maximum duration of 100 ms.";

    private static readonly ExpectedRow[] ExpectedRows =
    [
        new("OverBudgetPass", "OverBudgetPass", "Passed", "body:over-pass", PassedWarning),
        new("OverBudgetFail", "OverBudgetFail", "Failed", "body:over-fail", FailedWarning),
        new("Unannotated", "Unannotated", "Passed", "body:unannotated"),
        new("Ignored", "Ignored", "NotExecuted", null),
        new("InconclusiveOverBudget", "InconclusiveOverBudget", "NotExecuted", "body:inconclusive"),
        new("UnfoldedRows", "Unfolded below", "Passed", "body:unfolded-below"),
        new("UnfoldedRows", "Unfolded equal", "Passed", "body:unfolded-equal"),
        new("UnfoldedRows", "Unfolded above", "Passed", "body:unfolded-above", UnfoldedWarning),
        new("FoldedRows", "Folded below", "Passed", "body:folded-below"),
        new("FoldedRows", "Folded equal", "Passed", "body:folded-equal"),
        new("FoldedRows", "Folded above", "Passed", "body:folded-above", FoldedWarning),
        new("RetryFinishesWithinBudget", "RetryFinishesWithinBudget", "Passed", "body:retry-within-2"),
        new("RetryFinishesOverBudget", "RetryFinishesOverBudget", "Passed", "body:retry-over-2", RetryWarning),
    ];

    public TestContext TestContext { get; set; }

    public static IEnumerable<object[]> MetadataCases
        => MetadataModesToRun.Select(mode => new object[] { mode });

    public static string MetadataCaseName(MethodInfo method, object[] data)
        => $"{method.Name}_{(MetadataMode)data[0]}";

    [TestMethod]
    [DynamicData(nameof(MetadataCases), DynamicDataDisplayName = nameof(MetadataCaseName))]
    public async Task Mtp_ExpectedDuration_PreservesWarningsAndResultAssociations(MetadataMode metadataMode)
    {
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, TargetFrameworks.NetCurrent, metadataMode: metadataMode);
        using TempDirectory passingResults = new();
        TestHostResult passing = await host.ExecuteAsync(
            $"--filter Name=OverBudgetPass --output Normal --report-trx --report-trx-filename duration.trx --results-directory \"{passingResults.Path}\"",
            new()
            {
                ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
                [ProvenanceEnvironmentVariable] = Path.Join(passingResults.Path, "loaded-assemblies.txt"),
            },
            cancellationToken: TestContext.CancellationToken);

        await RecordConsumerRunAsync(host, metadataMode, passingResults.Path, "passing", passing.Command, passing.ExitCode, passing.StandardOutput, passing.StandardError);
        passing.AssertExitCodeIs(ExitCode.Success);
        passing.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        AssertReport(Path.Join(passingResults.Path, "duration.trx"), [ExpectedRows[0]]);
        AssertDeliveredWarnings(passing.StandardOutputLines.Concat(passing.StandardErrorLines), [ExpectedRows[0]]);

        using TempDirectory mixedResults = new();
        TestHostResult mixed = await host.ExecuteAsync(
            $"--output Normal --show-test-results all --report-trx --report-trx-filename duration.trx --results-directory \"{mixedResults.Path}\"",
            new()
            {
                ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
                [ProvenanceEnvironmentVariable] = Path.Join(mixedResults.Path, "loaded-assemblies.txt"),
            },
            cancellationToken: TestContext.CancellationToken);

        await RecordConsumerRunAsync(host, metadataMode, mixedResults.Path, "mixed", mixed.Command, mixed.ExitCode, mixed.StandardOutput, mixed.StandardError);
        mixed.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        mixed.AssertOutputContainsSummary(failed: 1, passed: 10, skipped: 2);
        AssertReport(Path.Join(mixedResults.Path, "duration.trx"), ExpectedRows);
        AssertDeliveredWarnings(mixed.StandardOutputLines.Concat(mixed.StandardErrorLines), ExpectedRows);
    }

    [TestMethod]
    [DynamicData(nameof(MetadataCases), DynamicDataDisplayName = nameof(MetadataCaseName))]
    public async Task Vstest_ExpectedDuration_PreservesWarningsAndResultAssociations(MetadataMode metadataMode)
    {
        string projectPath = AssetFixture.GetAssetPath(AssetName);
        var host = TestHost.LocateFrom(projectPath, AssetName, TargetFrameworks.NetCurrent, metadataMode: metadataMode);
        string assemblyPath = Path.Join(host.DirectoryName, AssetName + ".dll");
        string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
        string dotnetPath = Path.Combine(dotnetRoot, $"dotnet{Constants.ExecutableExtension}");
        Dictionary<string, string?> environmentVariables = new()
        {
            ["DOTNET_ROOT"] = dotnetRoot,
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            ["VSLANG"] = "1033",
            ["TESTINGPLATFORM_AZDO_OUTPUT"] = "off",
        };
        using CommandLine command = new();
        using TempDirectory passingResults = new();
        environmentVariables[ProvenanceEnvironmentVariable] = Path.Combine(passingResults.Path, "loaded-assemblies.txt");
        string passingCommand = $"\"{dotnetPath}\" vstest \"{assemblyPath}\" /TestCaseFilter:\"FullyQualifiedName=DurationFeature.DurationTests.OverBudgetPass\" /Logger:\"console;verbosity=normal\" /Logger:\"trx;LogFileName=duration.trx\" /ResultsDirectory:\"{passingResults.Path}\"";
        int passingExit = await command.RunAsyncAndReturnExitCodeAsync(
            passingCommand,
            environmentVariables: environmentVariables,
            workingDirectory: projectPath,
            cancellationToken: TestContext.CancellationToken);

        await RecordConsumerRunAsync(host, metadataMode, passingResults.Path, "passing", passingCommand, passingExit, command.StandardOutput, command.ErrorOutput);
        Assert.AreEqual(0, passingExit, $"{command.StandardOutput}{Environment.NewLine}{command.ErrorOutput}");
        AssertReport(Path.Combine(passingResults.Path, "duration.trx"), [ExpectedRows[0]]);
        AssertDeliveredWarnings(command.StandardOutputLines.Concat(command.ErrorOutputLines), [ExpectedRows[0]]);

        using TempDirectory mixedResults = new();
        environmentVariables[ProvenanceEnvironmentVariable] = Path.Combine(mixedResults.Path, "loaded-assemblies.txt");
        string mixedCommand = $"\"{dotnetPath}\" vstest \"{assemblyPath}\" /Logger:\"console;verbosity=normal\" /Logger:\"trx;LogFileName=duration.trx\" /ResultsDirectory:\"{mixedResults.Path}\"";
        int mixedExit = await command.RunAsyncAndReturnExitCodeAsync(
            mixedCommand,
            environmentVariables: environmentVariables,
            workingDirectory: projectPath,
            cancellationToken: TestContext.CancellationToken);

        await RecordConsumerRunAsync(host, metadataMode, mixedResults.Path, "mixed", mixedCommand, mixedExit, command.StandardOutput, command.ErrorOutput);
        Assert.AreEqual(1, mixedExit, $"{command.StandardOutput}{Environment.NewLine}{command.ErrorOutput}");
        AssertReport(Path.Combine(mixedResults.Path, "duration.trx"), ExpectedRows);
        AssertDeliveredWarnings(command.StandardOutputLines.Concat(command.ErrorOutputLines), ExpectedRows);
    }

    private async Task RecordConsumerRunAsync(
        TestHost host, MetadataMode metadataMode, string resultsDirectory, string runName,
        string command, int exitCode, string standardOutput, string standardError)
    {
        string logPath = Path.Combine(TestContext.TestTempDirectory!, $"{runName}-{metadataMode}.log");
        await File.WriteAllTextAsync(
            logPath,
            $"""
            Consumer: {host.FullName}
            Metadata: {metadataMode}
            MSTest: {MSTestVersion}
            MTP: {MicrosoftTestingPlatformVersion}
            Package cache: {Environment.GetEnvironmentVariable("NUGET_PACKAGES")}
            Results: {resultsDirectory}
            Command: {command}
            Exit: {exitCode}
            Standard output:
            {standardOutput}
            Standard error:
            {standardError}
            """,
            TestContext.CancellationToken);
        TestContext.AddResultFile(logPath);
        TestContext.WriteLine($"Consumer log: {logPath}");
        TestContext.WriteLine($"Retained asset: {AssetFixture.GetAssetPath(AssetName)}");
        TestContext.WriteLine($"Child results: {resultsDirectory}");

        foreach (string fileName in new[] { "duration.trx", "loaded-assemblies.txt" })
        {
            string source = Path.Combine(resultsDirectory, fileName);
            if (File.Exists(source))
            {
                // Attach nested TRX as text, not as another outer test-results module.
                string destination = Path.Combine(TestContext.TestTempDirectory!, $"{runName}-{metadataMode}-{fileName}.txt");
                File.Copy(source, destination);
                TestContext.AddResultFile(destination);
            }
        }
    }

    private static void AssertDeliveredWarnings(IEnumerable<string> lines, ExpectedRow[] expectedRows)
    {
        // Standalone logger messages have no indentation. Captured context output in
        // a failed-test detail block is indented and is not warning-delivery proof.
        string[] warnings = [.. lines.Where(line => line.StartsWith("Test 'DurationFeature.", StringComparison.Ordinal))];
        Assert.AreSequenceEqual(
            expectedRows.Where(row => row.Warning is not null).Select(row => row.Warning!),
            warnings,
            SequenceOrder.InAnyOrder);
    }

    private static void AssertReport(string path, ExpectedRow[] expectedRows)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var document = XDocument.Load(path);
        XElement[] rootResults = [.. document.Root!.Element(ns + "Results")!.Elements(ns + "UnitTestResult")];
        XElement[] results = [.. document.Descendants(ns + "UnitTestResult")
            .Where(result => !result.Descendants(ns + "UnitTestResult").Any())];
        var definitions = document.Descendants(ns + "UnitTest").ToDictionary(test => test.Attribute("id")!.Value);

        Assert.AreSequenceEqual(
            expectedRows.Select(row => (row.Method, row.Name, row.Outcome)),
            results.Select(result => (
                definitions[result.Attribute("testId")!.Value].Element(ns + "TestMethod")!.Attribute("name")!.Value,
                result.Attribute("testName")!.Value,
                result.Attribute("outcome")!.Value)),
            SequenceOrder.InAnyOrder);
        Assert.AreEqual(expectedRows.Length, results.Select(result => result.Attribute("executionId")!.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.AreSequenceEqual(
            rootResults.Select(result => (result.Attribute("testId")!.Value, result.Attribute("executionId")!.Value)),
            document.Descendants(ns + "TestEntry").Select(entry => (entry.Attribute("testId")!.Value, entry.Attribute("executionId")!.Value)),
            SequenceOrder.InAnyOrder);

        XElement counters = document.Descendants(ns + "Counters").Single();
        Assert.AreEqual(expectedRows.Length, (int)counters.Attribute("total")!);
        Assert.AreEqual(expectedRows.Count(row => row.Outcome == "Passed"), (int)counters.Attribute("passed")!);
        Assert.AreEqual(expectedRows.Count(row => row.Outcome == "Failed"), (int)counters.Attribute("failed")!);
        Assert.AreEqual(expectedRows.Count(row => row.Outcome is "Passed" or "Failed"), (int)counters.Attribute("executed")!);
        // VSTest leaves Counters.notExecuted at zero even when it reports skipped
        // leaf rows. The final rows, not that legacy counter, prove the skip count.
        Assert.HasCount(
            expectedRows.Count(row => row.Outcome == "NotExecuted"),
            results.Where(result => result.Attribute("outcome")!.Value == "NotExecuted"));

        foreach (ExpectedRow expected in expectedRows)
        {
            XElement result = results.Single(result => result.Attribute("testName")!.Value == expected.Name);
            Assert.AreNotEqual(Guid.Empty, Guid.Parse(result.Attribute("testId")!.Value), expected.Name);
            Assert.AreNotEqual(Guid.Empty, Guid.Parse(result.Attribute("executionId")!.Value), expected.Name);
            Assert.AreEqual(
                "DurationFeature.DurationTests",
                definitions[result.Attribute("testId")!.Value].Element(ns + "TestMethod")!.Attribute("className")!.Value,
                expected.Name);

            string expectedOutput = expected.Marker is null
                ? string.Empty
                : expected.Warning is null
                    ? $"""
                        TestContext Messages:
                        {expected.Marker}
                        """
                    : $"""
                        TestContext Messages:
                        {expected.Marker}

                        {expected.Warning}
                        """;
            string output = result.Element(ns + "Output")?.Element(ns + "StdOut")?.Value ?? string.Empty;
            Assert.AreEqual(NormalizeReportOutput(expectedOutput), NormalizeReportOutput(output), expected.Name);

            if (expected.Outcome == "Failed")
            {
                string error = result.Element(ns + "Output")!.Element(ns + "ErrorInfo")!.Element(ns + "Message")!.Value;
                Assert.Contains("intentional-duration-failure", error);
            }
        }

        if (expectedRows.Length > 1)
        {
            Assert.HasCount(3, results.Where(result => result.Attribute("testName")!.Value.StartsWith("Unfolded ", StringComparison.Ordinal))
                .Select(result => result.Attribute("testId")!.Value).Distinct(StringComparer.Ordinal));
            Assert.HasCount(1, results.Where(result => result.Attribute("testName")!.Value.StartsWith("Folded ", StringComparison.Ordinal))
                .Select(result => result.Attribute("testId")!.Value).Distinct(StringComparer.Ordinal));
        }
    }

    private static string NormalizeReportOutput(string output) => output.Replace("\r\n", "\n").Trim();

    private sealed record ExpectedRow(string Method, string Name, string Outcome, string? Marker, string? Warning = null);

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [MetadataMode.SourceGeneration, MetadataMode.AotSourceGeneration];

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName, SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion));

        private const string SourceCode = """
#file ExpectedDurationConsumer.csproj
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

#file DurationTests.cs
#pragma warning disable MSTESTEXP // Exercise the existing retry contract.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DurationFeature;

internal static class ConsumerCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // This is an isolated generated consumer process, not the acceptance host.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    }
}

public sealed class ReportedDurationAttribute : TestMethodAttribute
{
    private readonly int _ticks;
    private readonly int _firstAttemptTicks;
    private int _executions;

    public ReportedDurationAttribute(int ticks, int firstAttemptTicks = 0)
    {
        _ticks = ticks;
        _firstAttemptTicks = firstAttemptTicks;
    }

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        TestResult[] results = await base.ExecuteAsync(testMethod);
        int ticks = ++_executions == 1 && _firstAttemptTicks > 0
            ? _firstAttemptTicks
            : testMethod.Arguments is { Length: > 0 } arguments ? (int)arguments[0]! : _ticks;
        foreach (TestResult result in results)
        {
            result.Duration = TimeSpan.FromTicks(ticks);
        }

        return results;
    }
}

[TestClass]
public sealed class DurationTests
{
    public TestContext TestContext { get; set; } = null!;

    [AssemblyInitialize]
    public static void RecordLoadedAssemblies(TestContext context)
    {
        string? path = Environment.GetEnvironmentVariable("EXPECTED_DURATION_BINARY_PROVENANCE");
        if (path is not null)
        {
            File.WriteAllLines(path, AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic
                    && (assembly.GetName().Name!.Contains("MSTest", StringComparison.Ordinal)
                        || assembly.GetName().Name!.StartsWith("Microsoft.Testing.", StringComparison.Ordinal)))
                .Select(assembly => assembly.Location)
                .OrderBy(location => location, StringComparer.Ordinal));
        }
    }

    [ReportedDuration(1_501_250)]
    [ExpectedDuration(100)]
    public void OverBudgetPass()
    {
        TestContext.WriteLine("body:over-pass");
    }

    [ReportedDuration(2_005_000)]
    [ExpectedDuration(100)]
    public void OverBudgetFail()
    {
        TestContext.WriteLine("body:over-fail");
        Assert.Fail("intentional-duration-failure");
    }

    [ReportedDuration(2_500_000)]
    public void Unannotated()
    {
        TestContext.WriteLine("body:unannotated");
    }

    [ReportedDuration(2_500_000)]
    [ExpectedDuration(100)]
    [Ignore("intentional-duration-skip")]
    public void Ignored()
    {
        Assert.Fail("ignored-duration-body-must-not-run");
    }

    [ReportedDuration(2_000_000)]
    [ExpectedDuration(100)]
    public void InconclusiveOverBudget()
    {
        TestContext.WriteLine("body:inconclusive");
        Assert.Inconclusive("intentional-duration-inconclusive");
    }

    [ReportedDuration(0, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [ExpectedDuration(100)]
    [DataRow(999_999, "unfolded-below", DisplayName = "Unfolded below")]
    [DataRow(1_000_000, "unfolded-equal", DisplayName = "Unfolded equal")]
    [DataRow(1_501_250, "unfolded-above", DisplayName = "Unfolded above")]
    public void UnfoldedRows(int ticks, string marker)
    {
        TestContext.WriteLine("body:" + marker);
    }

    public static IEnumerable<TestDataRow<(int, string)>> FoldedData =>
    [
        new((999_999, "folded-below")) { DisplayName = "Folded below" },
        new((1_000_000, "folded-equal")) { DisplayName = "Folded equal" },
        new((1_501_250, "folded-above")) { DisplayName = "Folded above" },
    ];

    [ReportedDuration(0, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [ExpectedDuration(100)]
    [DynamicData(nameof(FoldedData))]
    public void FoldedRows(int ticks, string marker)
    {
        TestContext.WriteLine("body:" + marker);
    }

    [ReportedDuration(500_000, firstAttemptTicks: 2_500_000)]
    [ExpectedDuration(100)]
    [Retry(1)]
    public void RetryFinishesWithinBudget()
    {
        TestContext.WriteLine("body:retry-within-" + TestContext.TestRunCount.ToString(CultureInfo.InvariantCulture));
        if (TestContext.TestRunCount == 1)
        {
            Assert.Fail("intentional-first-attempt-failure");
        }
    }

    [ReportedDuration(1_501_250, firstAttemptTicks: 2_500_000)]
    [ExpectedDuration(100)]
    [Retry(1)]
    public void RetryFinishesOverBudget()
    {
        TestContext.WriteLine("body:retry-over-" + TestContext.TestRunCount.ToString(CultureInfo.InvariantCulture));
        if (TestContext.TestRunCount == 1)
        {
            Assert.Fail("intentional-first-attempt-failure");
        }
    }
}
""";
    }
}
