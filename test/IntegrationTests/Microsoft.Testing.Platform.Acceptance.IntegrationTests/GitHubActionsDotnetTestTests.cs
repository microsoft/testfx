// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Exercises GitHub workflow commands through the real SDK, not only the pipe harness.
/// Each test builds a fresh packed-package consumer, so method-level parallelism never
/// shares mutable bin/obj output. Passing runs are essential: an SDK dumping a failed
/// child's captured output must not masquerade as working command forwarding.
/// </summary>
[TestClass]
public sealed class GitHubActionsDotnetTestTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = GitHubActionsReportTests.AssetName;
    private const string FailingAssetName = "GitHubActionsReportBehaviorFailing";
    private const string SolutionName = "GitHubActions.slnx";
    private const string ReportingArguments = "--report-gh --report-gh-slow-test-notices off";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("project", DisplayName = "GitHubSdk_PassingProject")]
    [DataRow("solution", DisplayName = "GitHubSdk_PassingSolution")]
    [DataRow("bare", DisplayName = "GitHubSdk_PassingBare")]
    public async Task DotnetTest_PassingModule_ForwardsUnprefixedGitHubGroups(string invocation)
    {
        using TestAsset asset = await GenerateConsumerAsync(multipleModules: false);
        Dictionary<string, string?> environment = CreateEnvironment(asset.TargetAssetPath, "pass");
        await BuildConsumerAsync(asset, environment);

        // A separate summary prevents a working direct executable from making a broken SDK run
        // look successful merely because its predecessor left the expected summary behind.
        Dictionary<string, string?> directEnvironment = new(environment)
        {
            ["GITHUB_STEP_SUMMARY"] = Path.Combine(asset.TargetAssetPath, "direct-summary.md"),
        };
        var host = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult direct = await host.ExecuteAsync(
            ReportingArguments,
            environmentVariables: directEnvironment,
            cancellationToken: TestContext.CancellationToken);
        direct.AssertExitCodeIs(ExitCode.Success);
        Assert.AreSequenceEqual(ExpectedCommandLines("pass"), GetWorkflowLines(direct.StandardOutput));
        Assert.IsEmpty(GetWorkflowLines(direct.StandardError));
        GitHubActionsReportTests.AssertModuleSummary(
            File.ReadAllText(directEnvironment["GITHUB_STEP_SUMMARY"]!), AssetName, "pass");

        if (invocation == "bare")
        {
            // Bare discovery requires exactly one project or solution in the working directory.
            File.Delete(Path.Combine(asset.TargetAssetPath, SolutionName));
        }

        DotnetMuxerResult result = await RunDotnetTestAsync(asset, environment, invocation);

        result.AssertExitCodeIs(ExitCode.Success);
        // Do not trim leading whitespace or look for substrings: workflow commands have to reach
        // stdout as complete, unprefixed lines for the GitHub runner to interpret them.
        Assert.AreSequenceEqual(ExpectedCommandLines("pass"), GetWorkflowLines(result.StandardOutput), result.ToString());
        Assert.IsEmpty(GetWorkflowLines(result.StandardError));
        GitHubActionsReportTests.AssertModuleSummary(
            File.ReadAllText(environment["GITHUB_STEP_SUMMARY"]!), AssetName, "pass");
    }

    [TestMethod]
    [DataRow("location", (int)ExitCode.AtLeastOneTestFailed, DisplayName = "GitHubSdk_FailureAndSkipped")]
    [DataRow("zero", (int)ExitCode.ZeroTests, DisplayName = "GitHubSdk_ZeroTests")]
    public async Task DotnetTest_FailingAndZeroTestModules_PreserveAnnotationsAndExitCodes(string mode, int expectedExit)
    {
        using TestAsset asset = await GenerateConsumerAsync(multipleModules: false);
        Dictionary<string, string?> environment = CreateEnvironment(asset.TargetAssetPath, mode);

        DotnetMuxerResult result = await RunDotnetTestAsync(asset, environment, "project");

        result.AssertExitCodeIs((ExitCode)expectedExit);
        Assert.AreSequenceEqual(ExpectedCommandLines(mode), GetWorkflowLines(result.StandardOutput), result.ToString());
        Assert.IsEmpty(GetWorkflowLines(result.StandardError));
        GitHubActionsReportTests.AssertModuleSummary(
            File.ReadAllText(environment["GITHUB_STEP_SUMMARY"]!), AssetName, mode);
    }

    [TestMethod]
    public async Task DotnetTest_SlowTest_ForwardsUnprefixedNotice()
    {
        using TestAsset asset = await GenerateConsumerAsync(multipleModules: false);
        Dictionary<string, string?> environment = CreateEnvironment(asset.TargetAssetPath, "slow");

        DotnetMuxerResult result = await RunDotnetTestAsync(
            asset, environment, "project",
            "--report-gh --report-gh-slow-test-threshold 1ms");

        result.AssertExitCodeIs(ExitCode.Success);
        string[] commands = GetWorkflowLines(result.StandardOutput);
        string[] notices = [.. commands.Where(line => line.StartsWith("::notice ", StringComparison.Ordinal))];
        Assert.IsNotEmpty(notices, result.ToString());
        foreach (string notice in notices)
        {
            Assert.MatchesRegex(@"^::notice title=Slow test%3A SlowTest::SlowTest still running after [0-9]+s$", notice);
        }

        Assert.AreSequenceEqual(
            ExpectedCommandLines("pass"),
            commands.Where(line => !line.StartsWith("::notice ", StringComparison.Ordinal)));
        Assert.IsEmpty(GetWorkflowLines(result.StandardError));
        GitHubActionsReportTests.AssertModuleSummary(
            File.ReadAllText(environment["GITHUB_STEP_SUMMARY"]!), AssetName, "pass");
    }

    [TestMethod]
    public async Task DotnetTest_MixedModules_PreservesEachModulesCommandsAndSummary()
    {
        using TestAsset asset = await GenerateConsumerAsync(multipleModules: true);
        Dictionary<string, string?> environment = CreateEnvironment(asset.TargetAssetPath, "pass");
        environment[$"GH_TEST_MODE_{AssetName}"] = "pass";
        environment[$"GH_TEST_MODE_{FailingAssetName}"] = "location";

        DotnetMuxerResult result = await RunDotnetTestAsync(asset, environment, "solution");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        string[] expected =
        [
            .. ExpectedCommandLines("pass", AssetName),
            .. ExpectedCommandLines("location", FailingAssetName),
        ];
        // Modules may interleave. Compare the complete command multiset, retaining the two closing
        // commands; a missing module, duplicated annotation, or missing endgroup still fails.
        Assert.AreSequenceEqual(
            expected.OrderBy(line => line, StringComparer.Ordinal),
            GetWorkflowLines(result.StandardOutput).OrderBy(line => line, StringComparer.Ordinal),
            result.ToString());
        Assert.IsEmpty(GetWorkflowLines(result.StandardError));
        string summary = File.ReadAllText(environment["GITHUB_STEP_SUMMARY"]!);
        GitHubActionsReportTests.AssertModuleSummary(summary, AssetName, "pass");
        GitHubActionsReportTests.AssertModuleSummary(summary, FailingAssetName, "location");
    }

    [TestMethod]
    public async Task DotnetTest_HistoryRemainsAssociatedWithItsModuleAndDataRow()
    {
        using TestAsset asset = await GenerateConsumerAsync(multipleModules: true);
        string historyPath = Path.Combine(asset.TargetAssetPath, "history.json");
        Dictionary<string, string?> environment = CreateEnvironment(asset.TargetAssetPath, "history-pass");
        environment["RUNNER_OS"] = "TestOs";
        environment["GITHUB_RUN_ID"] = "run-1";
        environment["GITHUB_RUN_ATTEMPT"] = "1";
        environment[$"GH_TEST_MODE_{AssetName}"] = "history-fail";
        environment[$"GH_TEST_MODE_{FailingAssetName}"] = "history-pass";
        string arguments = $"{ReportingArguments} --report-gh-history \"{historyPath}\"";

        DotnetMuxerResult first = await RunDotnetTestAsync(asset, environment, "solution", arguments);
        first.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        environment["GITHUB_RUN_ID"] = "run-2";
        environment["GITHUB_STEP_SUMMARY"] = Path.Combine(asset.TargetAssetPath, "second-summary.md");
        environment["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = Guid.NewGuid().ToString("N");
        environment[$"GH_TEST_MODE_{AssetName}"] = "history-pass";
        DotnetMuxerResult second = await RunDotnetTestAsync(asset, environment, "solution", arguments);
        second.AssertExitCodeIs(ExitCode.Success);

        string summary = File.ReadAllText(environment["GITHUB_STEP_SUMMARY"]!).ReplaceLineEndings("\n");
        string failedBefore = GetModuleSummary(AssetName);
        string alwaysPassed = GetModuleSummary(FailingAssetName);
        Assert.Contains("| 2 | 2 | 0 | 0 | 0 |", failedBefore);
        Assert.Contains("| 2 | 2 | 0 | 0 | 0 |", alwaysPassed);
        Assert.Contains("Passing tests with previous failures (1)", failedBefore);
        Assert.Contains("- `Tests.HistoryCase (1)` — prior attempts: 0 passed, 1 failed", failedBefore);
        Assert.DoesNotContain("Tests.HistoryCase (2)", failedBefore);
        Assert.DoesNotContain("Passing tests with previous failures", alwaysPassed);
        Assert.DoesNotContain("Flaky tests (", summary);

        using var snapshot = JsonDocument.Parse(File.ReadAllText(historyPath));
        Assert.AreEqual(1, snapshot.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.AreSequenceEqual(
            new[]
            {
                ("run-1", AssetName, "history-1", "failed", false),
                ("run-1", AssetName, "history-2", "passed", false),
                ("run-1", FailingAssetName, "history-1", "passed", false),
                ("run-1", FailingAssetName, "history-2", "passed", false),
                ("run-2", AssetName, "history-1", "passed", false),
                ("run-2", AssetName, "history-2", "passed", false),
                ("run-2", FailingAssetName, "history-1", "passed", false),
                ("run-2", FailingAssetName, "history-2", "passed", false),
            },
            snapshot.RootElement.GetProperty("samples").EnumerateArray()
                .Select(sample => (
                    sample.GetProperty("runId").GetString()!,
                    sample.GetProperty("assemblyName").GetString()!,
                    sample.GetProperty("testId").GetString()!,
                    sample.GetProperty("outcome").GetString()!,
                    sample.GetProperty("isFlaky").GetBoolean()))
                .OrderBy(sample => sample.Item1, StringComparer.Ordinal)
                .ThenBy(sample => sample.Item2, StringComparer.Ordinal)
                .ThenBy(sample => sample.Item3, StringComparer.Ordinal));

        string GetModuleSummary(string assemblyName)
        {
            string[] lines = summary.Split('\n');
            string aggregateHeading = "<summary>" + assemblyName + " (";
            int heading = Array.FindIndex(
                lines,
                line => (line.StartsWith('#') && line.Contains(assemblyName + " (", StringComparison.Ordinal))
                    || line.StartsWith(aggregateHeading, StringComparison.Ordinal));
            Assert.IsGreaterThanOrEqualTo(0, heading, $"Missing summary for {assemblyName}:{Environment.NewLine}{summary}");
            int level = lines[heading].TakeWhile(character => character == '#').Count();
            IEnumerable<string> moduleLines = lines[heading].StartsWith(aggregateHeading, StringComparison.Ordinal)
                ? lines.Skip(heading + 1).TakeWhile(line => !line.StartsWith("<summary>", StringComparison.Ordinal) || line.StartsWith("<summary><code>", StringComparison.Ordinal))
                : lines.Skip(heading + 1).TakeWhile(line => !line.StartsWith('#') || line.TakeWhile(character => character == '#').Count() > level);
            return string.Join("\n", moduleLines);
        }
    }

    private static Dictionary<string, string?> CreateEnvironment(string workspace, string mode)
    {
        Dictionary<string, string?> environment = GitHubActionsReportTests.CreateForwardingEnvironment(workspace, mode);
        // AcceptanceFixture creates a different cache for every outer assembly run. Preserve it
        // explicitly rather than allowing these packed consumers to restore from a user cache.
        environment["NUGET_PACKAGES"] = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? throw new InvalidOperationException("AcceptanceFixture must initialize the isolated NuGet cache.");
        return environment;
    }

    private static string[] ExpectedCommandLines(string mode, string assemblyName = AssetName)
        => [
            .. GitHubActionsReportTests.GetExpectedCommandPayloads(mode, assemblyName)
            .SelectMany(payload => payload.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)),
        ];

    private static string[] GetWorkflowLines(string output)
        => [
            .. output.ReplaceLineEndings("\n").Split('\n')
            .Where(line => line.StartsWith("::", StringComparison.Ordinal)),
        ];

    private static Task<TestAsset> GenerateConsumerAsync(bool multipleModules)
    {
        string source = GitHubActionsReportTests.TestAssetFixture.GetSourceCode(TargetFrameworks.NetCurrent);
        string projects;
        if (multipleModules)
        {
            string failingSource = source
                .Replace(AssetName, FailingAssetName, StringComparison.Ordinal)
                .Replace("#file ", "#file Failing/", StringComparison.Ordinal);
            source = source.Replace("#file ", "#file Passing/", StringComparison.Ordinal)
                + Environment.NewLine + failingSource;
            projects = $"""
                  <Project Path="Passing/{AssetName}.csproj" />
                  <Project Path="Failing/{FailingAssetName}.csproj" />
                """;
        }
        else
        {
            projects = $"""  <Project Path="{AssetName}.csproj" />""";
        }

        source += $$"""

            #file {{SolutionName}}
            <Solution>
            {{projects}}
            </Solution>

            #file global.json
            {
              "test": {
                "runner": "Microsoft.Testing.Platform"
              }
            }
            """;
        return TestAsset.GenerateAssetAsync(nameof(GitHubActionsDotnetTestTests), source);
    }

    private async Task BuildConsumerAsync(TestAsset asset, Dictionary<string, string?> environment)
    {
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{Path.Combine(asset.TargetAssetPath, SolutionName)}\" -c Release",
            workingDirectory: asset.TargetAssetPath,
            environmentVariables: environment.ToDictionary(entry => entry.Key, entry => entry.Value),
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(ExitCode.Success);
    }

    private Task<DotnetMuxerResult> RunDotnetTestAsync(
        TestAsset asset, Dictionary<string, string?> environment, string invocation,
        string reportingArguments = ReportingArguments)
    {
        string selector = invocation switch
        {
            "project" => $"--project \"{Path.Combine(asset.TargetAssetPath, AssetName + ".csproj")}\"",
            "solution" => $"--solution \"{Path.Combine(asset.TargetAssetPath, SolutionName)}\"",
            "bare" => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(invocation)),
        };
        // Intentionally allow a normal build here, including in the negative cases. A nonzero
        // result is accepted only after asserting its contractual product exit and command output.
        return DotnetCli.RunAsync(
            $"test {selector} -c Release --no-ansi --progress off {reportingArguments}",
            workingDirectory: asset.TargetAssetPath,
            environmentVariables: environment.ToDictionary(entry => entry.Key, entry => entry.Value),
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
    }
}
