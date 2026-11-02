// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the GitHub Actions report extension: drives the extension through a real
/// Microsoft.Testing.Platform session (with <c>GITHUB_ACTIONS=true</c> and a temporary
/// <c>GITHUB_STEP_SUMMARY</c> file) and asserts that non-test-result exit codes surface both as a
/// job-summary callout and a run-level <c>::error</c> annotation, while an ordinary passing run stays
/// quiet and an ordinary test failure only produces per-test annotations.
/// </summary>
[TestClass]
public sealed class GitHubActionsReportTests : AcceptanceTestBase<GitHubActionsReportTests.TestAssetFixture>
{
    internal const string AssetName = "GitHubActionsReportBehavior";

    internal const string LocatedFailureCommand = "::error file=src/MyTests.cs,line=7,col=1,title=Test failed%3A FailingTest::Expected 1 but got 2";
    internal const string LocatedSkippedCommand = "::warning file=src/MyTests.cs,line=9,col=1,title=Test skipped%3A SkippedTest::Not today";
    internal const string ZeroTestsCommand = "::error title=Test run failed (exit code 8%3A ZeroTests)::No tests were found to run. This often means a test filter matched nothing.";

    private const string DisplayMessageProtocolVersions = "1.0.0;1.1.0;1.2.0;1.3.0";

    public TestContext TestContext { get; set; } = null!;

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenRunPasses_NoExitCodeCalloutAndSummaryUsesSuccessIcon(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "pass");

        result.AssertExitCodeIs(ExitCode.Success);

        // A clean run must not emit an exit-code annotation or a warning callout.
        result.AssertOutputDoesNotContain("::error title=Test run failed");
        Assert.Contains("✅ Test Run Summary", summary);
        Assert.DoesNotContain("[!WARNING]", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenTestFails_EmitsPerTestAnnotationButNoExitCodeCallout(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "fail");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        // The per-test failure annotation is emitted, but AtLeastOneTestFailed is a test-result outcome,
        // so no additional run-level exit-code annotation/callout should appear.
        result.AssertOutputContains("::error");
        result.AssertOutputDoesNotContain("::error title=Test run failed");
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.DoesNotContain("[!WARNING]", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenTestFailsWithException_SummaryExpandsTheFailureIntoACollapsibleSection(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "failex");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        Assert.Contains("<details>", summary);
        Assert.Contains("<code>FailingTest</code>", summary);
        Assert.Contains("**Exception:** `System.InvalidOperationException`", summary);
        Assert.Contains("Expected 42 but got 41", summary);
        Assert.Contains("</details>", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenRunPassesAndSummaryIsOnFailure_SummaryIsNotCreated(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "pass", extraArgs: "--report-gh-step-summary on-failure");

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.AreEqual(string.Empty, summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenFailureDetailsAreDisabled_SummaryKeepsTheCompactFailureList(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "failex", extraArgs: "--report-gh-failure-details off");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        Assert.Contains("### ❌ Failures (1)", summary);
        Assert.DoesNotContain("<details>", summary);
        Assert.DoesNotContain("**Exception:**", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenTestFailsAndSummaryIsOnFailure_SummaryIsCreated(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "fail", extraArgs: "--report-gh-step-summary on-failure");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        Assert.Contains("❌ Test Run Summary", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenZeroTestsRan_EmitsExitCodeAnnotationAndCallout(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "zero");

        result.AssertExitCodeIs(ExitCode.ZeroTests);

        // The run failed without any failing test: expect both a run-level annotation and a summary callout.
        result.AssertOutputContains("::error title=Test run failed");
        result.AssertOutputContains("ZeroTests");
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.Contains("[!WARNING]", summary);
        Assert.Contains("ZeroTests", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenZeroTestsRanAndSummaryIsOnFailure_SummaryIsCreated(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "zero", extraArgs: "--report-gh-step-summary on-failure");

        result.AssertExitCodeIs(ExitCode.ZeroTests);
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.Contains("ZeroTests", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenMinimumExpectedTestsViolated_EmitsExitCodeAnnotationAndCalloutEvenThoughTestPassed(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "pass", extraArgs: "--minimum-expected-tests 5");

        result.AssertExitCodeIs(ExitCode.MinimumExpectedTestsPolicyViolation);

        // A single test passed, yet the run failed the minimum-expected-tests policy — the failure must surface.
        result.AssertOutputContains("::error title=Test run failed");
        result.AssertOutputContains("MinimumExpectedTestsPolicyViolation");
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.Contains("MinimumExpectedTestsPolicyViolation", summary);
    }

    [TestMethod]
    public async Task WhenCoverageThresholdViolated_SummaryUsesPlatformVerdict()
    {
        (TestHostResult result, string summary) = await RunAsync(
            TargetFrameworks.NetCurrent, testMode: "coverage", extraArgs: "--coverage-threshold-line 80");

        result.AssertExitCodeIs(ExitCode.CoverageThresholdFailed);
        result.AssertOutputContains("CoverageThresholdFailed");
        Assert.Contains("CoverageThresholdFailed", summary);
        Assert.Contains(
            """
            | Scope | Metric | Actual | Required | Result |
            | --- | --- | ---: | ---: | --- |
            | Overall | Line | 79.0% | 80.0% | ❌ Failed |
            """.ReplaceLineEndings("\n"),
            summary.ReplaceLineEndings("\n"));
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenTestNodesCarryAFileLocation_AnnotationsArePinnedToTheDeclaredSource(string tfm)
    {
        // Neither node carries an exception, so there is no stack trace to resolve: the annotations can only be
        // pinned by falling back to the location the test framework reported for the test itself.
        string workspace = Path.Combine(TestContext.TestRunDirectory!, $"gh-workspace-{Guid.NewGuid():N}");
        string sourceFile = Path.Combine(workspace, "src", "MyTests.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "// test source");

        (TestHostResult result, _) = await RunAsync(
            tfm,
            testMode: "location",
            extraEnvironmentVariables: new Dictionary<string, string?>
            {
                ["GITHUB_WORKSPACE"] = workspace,
                ["GH_TEST_FILE"] = sourceFile,
            });

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        result.AssertOutputContains("::error file=src/MyTests.cs,line=7,col=1,title=Test failed%3A FailingTest::Expected 1 but got 2");
        result.AssertOutputContains("::warning file=src/MyTests.cs,line=9,col=1,title=Test skipped%3A SkippedTest::Not today");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenTestsAreDataDriven_SummaryDistinguishesRows(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(
            tfm,
            testMode: "data-driven",
            extraArgs: "--report-gh-step-summary-sections slow-tests");

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.AreEqual(
            $$"""
            <!-- microsoft-testing-platform:github:project-section -->
            ## ✅ Test Run Summary — {{AssetName}} ({{tfm}})

            ### ⏱ Slowest tests

            - `Tests.RunDataTest (5)` — 5.00s
            - `Tests.RunDataTest (3)` — 3.00s
            - `Tests.RunDataTest (1)` — 1.00s


            """.ReplaceLineEndings("\n"),
            summary.ReplaceLineEndings("\n"));
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenStepSummarySectionsIsSlowTests_OnlyTheSlowestSectionIsRendered(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(
            tfm,
            testMode: "timed",
            extraArgs: "--report-gh-step-summary-sections slow-tests");

        result.AssertExitCodeIs(ExitCode.Success);

        // The slow-tests section is rendered (the tests carry a real duration)...
        Assert.Contains("### ⏱ Slowest tests", summary);
        Assert.Contains("SlowTest", summary);

        // ...but the test-results totals table is gated out by the section selection.
        Assert.DoesNotContain("| Total | Passed | Failed | Skipped | Flaky | Duration |", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenStepSummarySectionsIsTestResults_TheSlowestSectionIsOmitted(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(
            tfm,
            testMode: "timed",
            extraArgs: "--report-gh-step-summary-sections test-results");

        result.AssertExitCodeIs(ExitCode.Success);

        // The test-results totals table is rendered...
        Assert.Contains("| Total | Passed | Failed | Skipped | Flaky | Duration |", summary);

        // ...but the slow-tests section is gated out by the section selection.
        Assert.DoesNotContain("### ⏱ Slowest tests", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenHistoryIsEnabled_SamplesPersistAndAccumulateAcrossRuns(string tfm)
    {
        string historyPath = Path.Combine(TestContext.TestRunDirectory!, $"gh-history-{Guid.NewGuid():N}.json");

        // Each run is identified by a distinct GITHUB_RUN_ID (and a fixed RUNNER_OS so the persisted samples pass the
        // history schema validation on the next read), so the two runs contribute two distinct samples for the test.
        static Dictionary<string, string?> RunEnvironment(string runId) => new()
        {
            ["GITHUB_RUN_ID"] = runId,
            ["RUNNER_OS"] = "TestOs",
        };

        (TestHostResult firstResult, _) = await RunAsync(
            tfm,
            testMode: "pass",
            extraArgs: $"--report-gh-history \"{historyPath}\"",
            extraEnvironmentVariables: RunEnvironment("run-1"));
        firstResult.AssertExitCodeIs(ExitCode.Success);

        Assert.IsTrue(File.Exists(historyPath), $"Expected the history snapshot to be created at '{historyPath}'.");

        (TestHostResult secondResult, _) = await RunAsync(
            tfm,
            testMode: "pass",
            extraArgs: $"--report-gh-history \"{historyPath}\"",
            extraEnvironmentVariables: RunEnvironment("run-2"));
        secondResult.AssertExitCodeIs(ExitCode.Success);

        using var snapshot = JsonDocument.Parse(File.ReadAllText(historyPath));
        JsonElement root = snapshot.RootElement;

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());

        JsonElement[] samples = [.. root.GetProperty("samples").EnumerateArray()];

        // The second run merged with the first, so both runs' samples for the passing test survive.
        Assert.HasCount(2, samples);
        Assert.IsTrue(samples.All(sample => sample.GetProperty("displayName").GetString() == "PassingTest"));
        Assert.IsTrue(samples.All(sample => sample.GetProperty("outcome").GetString() == "passed"));

        string[] runIds = [.. samples.Select(sample => sample.GetProperty("runId").GetString()!).OrderBy(id => id, StringComparer.Ordinal)];
        Assert.AreEqual("run-1", runIds[0]);
        Assert.AreEqual("run-2", runIds[1]);
    }

    [TestMethod]
    [DataRow("pass", (int)ExitCode.Success, DisplayName = "GitHubWire_Pass")]
    [DataRow("location", (int)ExitCode.AtLeastOneTestFailed, DisplayName = "GitHubWire_FailureAndSkipped")]
    [DataRow("zero", (int)ExitCode.ZeroTests, DisplayName = "GitHubWire_ZeroTests")]
    public async Task DotnetTestPipe_GitHubCommands_AreForwardedWithCorrelation(string mode, int expectedExit)
    {
        string workspace = Path.Combine(TestContext.TestRunDirectory!, $"gh-wire-{Guid.NewGuid():N}");
        Dictionary<string, string?> environment = CreateForwardingEnvironment(workspace, mode);
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            host,
            extraArguments: "--report-gh --report-gh-slow-test-notices off",
            environmentVariables: environment,
            supportedProtocolVersions: DisplayMessageProtocolVersions,
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs((ExitCode)expectedExit);
        Assert.AreEqual("1.3.0", result.NegotiatedProtocolVersion);
        (string? ExecutionId, string? InstanceId, byte? Level, string? Text)[] forwarded =
        [
            .. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.DisplayMessage)
                .Select(message => DotnetTestPipeProtocol.DecodeDisplayMessageBody(message.Body)),
        ];
        Assert.AreSequenceEqual(GetExpectedCommandPayloads(mode), forwarded.Select(message => message.Text));

        Assert.IsNotNull(result.ReceivedHandshake);
        Assert.AreEqual(environment["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"], result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.ExecutionId]);
        string instanceId = result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.InstanceId];
        Assert.IsFalse(string.IsNullOrWhiteSpace(instanceId));
        foreach ((string? executionId, string? forwardedInstanceId, byte? level, string? _) in forwarded)
        {
            Assert.AreEqual(environment["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"], executionId);
            Assert.AreEqual(instanceId, forwardedInstanceId);
            Assert.AreEqual(DotnetTestPipeProtocol.DisplayMessageLevels.Information, level);
        }

        // The SDK owns presentation. Neither success nor intentionally failing child runs may leak
        // raw commands to stdout/stderr as an alternative to forwarding the protocol frames.
        Assert.AreEqual(string.Empty, result.TestHostResult.StandardOutput);
        Assert.AreEqual(string.Empty, result.TestHostResult.StandardError);
        AssertModuleSummary(File.ReadAllText(environment["GITHUB_STEP_SUMMARY"]!), AssetName, mode);
    }

    [TestMethod]
    [DataRow("1.0.0", "1.0.0", DisplayName = "GitHubWire_Legacy100")]
    [DataRow("1.0.0;1.1.0", "1.1.0", DisplayName = "GitHubWire_Legacy110")]
    [DataRow("1.0.0;1.1.0;1.2.0", "1.2.0", DisplayName = "GitHubWire_Legacy120")]
    public async Task DotnetTestPipe_LegacyProtocol_DropsGitHubCommands(string versions, string expectedVersion)
    {
        string workspace = Path.Combine(TestContext.TestRunDirectory!, $"gh-legacy-{Guid.NewGuid():N}");
        Dictionary<string, string?> environment = CreateForwardingEnvironment(workspace, "location");
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            host,
            extraArguments: "--report-gh",
            environmentVariables: environment,
            supportedProtocolVersions: versions,
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        Assert.AreEqual(expectedVersion, result.NegotiatedProtocolVersion);
        Assert.IsEmpty(result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.DisplayMessage));
        Assert.AreEqual(string.Empty, result.TestHostResult.StandardOutput);
        Assert.AreEqual(string.Empty, result.TestHostResult.StandardError);
        AssertModuleSummary(File.ReadAllText(environment["GITHUB_STEP_SUMMARY"]!), AssetName, "location");
    }

    [TestMethod]
    [DataRow(false, "--report-gh", false, false, false, DisplayName = "GitHubWire_OutsideActions")]
    [DataRow(true, "", false, false, false, DisplayName = "GitHubWire_MasterSwitchAbsent")]
    [DataRow(true, "--report-gh --report-gh-groups off", false, true, true, DisplayName = "GitHubWire_GroupsOff")]
    [DataRow(true, "--report-gh --report-gh-annotations off", true, false, true, DisplayName = "GitHubWire_AnnotationsOff")]
    [DataRow(true, "--report-gh --report-gh-groups off --report-gh-annotations off --report-gh-slow-test-notices off", false, false, true, DisplayName = "GitHubWire_AllCommandsOff")]
    [DataRow(true, "--report-gh --report-gh-step-summary off", true, true, false, DisplayName = "GitHubWire_SummaryOff")]
    public async Task DotnetTestPipe_GitHubFeatureGates_ArePreserved(
        bool githubActions, string arguments, bool groups, bool annotations, bool summary)
    {
        string workspace = Path.Combine(TestContext.TestRunDirectory!, $"gh-gates-{Guid.NewGuid():N}");
        Dictionary<string, string?> environment = CreateForwardingEnvironment(workspace, "location", githubActions);
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            host,
            extraArguments: arguments,
            environmentVariables: environment,
            supportedProtocolVersions: DisplayMessageProtocolVersions,
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        Assert.AreEqual("1.3.0", result.NegotiatedProtocolVersion);
        Assert.AreSequenceEqual(
            GetExpectedCommandPayloads("location", groups: groups, annotations: annotations),
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.DisplayMessage)
                .Select(message => DotnetTestPipeProtocol.DecodeDisplayMessageBody(message.Body).Text));
        Assert.AreEqual(string.Empty, result.TestHostResult.StandardOutput);
        Assert.AreEqual(string.Empty, result.TestHostResult.StandardError);
        string summaryPath = environment["GITHUB_STEP_SUMMARY"]!;
        if (summary)
        {
            AssertModuleSummary(File.ReadAllText(summaryPath), AssetName, "location");
        }
        else
        {
            Assert.IsFalse(File.Exists(summaryPath), "Disabled reporting must not create a job summary.");
        }
    }

    [TestMethod]
    public async Task ListTestsJson_WithGitHubReporting_DoesNotRenderWorkflowCommands()
    {
        string workspace = Path.Combine(TestContext.TestRunDirectory!, $"gh-json-{Guid.NewGuid():N}");
        Dictionary<string, string?> environment = CreateForwardingEnvironment(workspace, "discovery");
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        TestHostResult result = await host.ExecuteAsync(
            "--report-gh --list-tests json",
            environmentVariables: environment,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.AreEqual(string.Empty, result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.AreEqual(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        JsonElement test = Assert.ContainsSingle(document.RootElement.GetProperty("tests").EnumerateArray());
        Assert.AreEqual("test-1", test.GetProperty("uid").GetString());
        Assert.AreEqual("PassingTest", test.GetProperty("displayName").GetString());
    }

    internal static Dictionary<string, string?> CreateForwardingEnvironment(string workspace, string mode, bool githubActions = true)
    {
        string sourceFile = Path.Combine(workspace, "src", "MyTests.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(
            sourceFile,
            """
            // Source locations supplied by the generated test framework.
            //
            // These declarations are deliberately outside any actual test framework.
            // TestFileLocationProperty identifies the corresponding lines below.
            //
            // Failing test:
            // FailingTest
            // Skipped test:
            // SkippedTest
            """,
            Encoding.UTF8);
        return new()
        {
            ["GITHUB_ACTIONS"] = githubActions ? "true" : "false",
            ["TF_BUILD"] = "false",
            ["GITHUB_WORKSPACE"] = workspace,
            ["GITHUB_STEP_SUMMARY"] = Path.Combine(workspace, "summary.md"),
            ["GH_TEST_MODE"] = mode,
            ["GH_TEST_FILE"] = sourceFile,
            ["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = Guid.NewGuid().ToString("N"),
        };
    }

    internal static string[] GetExpectedCommandPayloads(
        string mode, string assemblyName = AssetName, bool groups = true, bool annotations = true)
    {
        List<string> expected = [];
        if (groups)
        {
            expected.Add($"::group::Tests: {assemblyName} ({TargetFrameworks.NetCurrent})");
        }

        if (annotations && mode == "location")
        {
            // Annotation producers intentionally prepend a newline; forwarding must preserve it.
            expected.Add("\n" + LocatedFailureCommand);
            expected.Add("\n" + LocatedSkippedCommand);
        }
        else if (annotations && mode == "zero")
        {
            expected.Add("\n" + ZeroTestsCommand);
        }

        if (groups)
        {
            expected.Add("::endgroup::");
        }

        return [.. expected];
    }

    internal static void AssertModuleSummary(string summary, string assemblyName, string mode)
    {
        string[] lines = summary.ReplaceLineEndings("\n").Split('\n');
        // The passing module's name is a prefix of the failing module's name. Require the
        // heading's identity boundary so the latter cannot stand in for a missing passing module.
        string aggregateHeading = "<summary>" + assemblyName + " (";
        int heading = Array.FindIndex(
            lines,
            line => (line.StartsWith('#') && line.Contains(assemblyName + " (", StringComparison.Ordinal))
                || line.StartsWith(aggregateHeading, StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, heading, $"Missing summary for {assemblyName}:{Environment.NewLine}{summary}");
        int level = lines[heading].TakeWhile(character => character == '#').Count();
        // SDKs advertising artifact post-processing render collapsible module sections; an SDK
        // without that capability leaves the independently appended per-project Markdown sections.
        // In either shape, inspect this module's own table, never the overall/neighboring totals.
        IEnumerable<string> moduleLines = lines[heading].StartsWith(aggregateHeading, StringComparison.Ordinal)
            ? lines.Skip(heading + 1).TakeWhile(line => !line.StartsWith("<summary>", StringComparison.Ordinal) || line.StartsWith("<summary><code>", StringComparison.Ordinal))
            : lines.Skip(heading + 1).TakeWhile(line => !line.StartsWith('#') || line.TakeWhile(character => character == '#').Count() > level);
        string moduleSummary = string.Join("\n", moduleLines);
        string totals = mode switch
        {
            "location" => "| 2 | 0 | 1 | 1 | 0 | 0ms |",
            "zero" => "| 0 | 0 | 0 | 0 | 0 | 0ms |",
            _ => "| 1 | 1 | 0 | 0 | 0 | 0ms |",
        };
        Assert.Contains(totals, moduleSummary.Split('\n'));
        if (mode == "zero")
        {
            Assert.Contains("ZeroTests", moduleSummary);
        }
        else if (mode == "location")
        {
            Assert.Contains("FailingTest", moduleSummary);
            Assert.Contains("Expected 1 but got 2", moduleSummary);
        }
    }

    private async Task<(TestHostResult Result, string Summary)> RunAsync(
        string tfm,
        string testMode,
        string extraArgs = "",
        Dictionary<string, string?>? extraEnvironmentVariables = null)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string stepSummaryPath = Path.Combine(TestContext.TestRunDirectory!, $"gh-step-summary-{Guid.NewGuid():N}.md");

        var environmentVariables = new Dictionary<string, string?>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["TF_BUILD"] = "false",
            ["GITHUB_STEP_SUMMARY"] = stepSummaryPath,
            ["GH_TEST_MODE"] = testMode,
        };

        if (extraEnvironmentVariables is not null)
        {
            foreach (KeyValuePair<string, string?> variable in extraEnvironmentVariables)
            {
                environmentVariables[variable.Key] = variable.Value;
            }
        }

        TestHostResult result = await testHost.ExecuteAsync(
            $"--report-gh {extraArgs}".Trim(),
            environmentVariables: environmentVariables,
            cancellationToken: TestContext.CancellationToken);

        string summary = File.Exists(stepSummaryPath) ? File.ReadAllText(stepSummaryPath) : string.Empty;
        return (result, summary);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file GitHubActionsReportBehavior.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.GitHubActionsReport" Version="$MicrosoftTestingExtensionsGitHubActionsReportVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());
        builder.AddGitHubActionsProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage), typeof(TestCoverageMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // The behavior under test is selected by the GH_TEST_MODE environment variable set by the acceptance test:
        //   "zero"     -> publish no tests (exit code ZeroTests)
        //   "fail"     -> publish a single failing test (exit code AtLeastOneTestFailed)
        //   "failex"   -> publish a single failing test carrying a real exception, so the job summary can expand it
        //                 into a collapsible section with the exception type and stack trace
        //   "pass"     -> publish a single passing test (exit code Success, unless --minimum-expected-tests forces a violation)
        //   "timed"    -> publish several passing tests, each carrying a TimingProperty with a distinct duration, so the
        //                 job summary's slow-tests section has real durations to rank
        //   "data-driven" -> publish timed data rows sharing a method identity, distinguished by display-name arguments
        //   "location" -> publish a failing and a skipped test, both carrying a TestFileLocationProperty and no
        //                 exception, so the annotations can only be pinned through the declared-location fallback
        //   "discovery" -> publish one discovered test, leaving --list-tests json responsible for its rendering
        //   "slow"     -> keep one test in progress across several slow-reporter scans
        string mode = Environment.GetEnvironmentVariable("GH_TEST_MODE_" + typeof(Program).Assembly.GetName().Name)
            ?? Environment.GetEnvironmentVariable("GH_TEST_MODE")
            ?? "pass";

        if (mode == "fail")
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "FailingTest",
                    Properties = new PropertyBag(new FailedTestNodeStateProperty("Expected 1 but got 2")),
                }));
        }
        else if (mode == "failex")
        {
            // Throw and catch so the exception carries a real stack trace, which is what the collapsible
            // failure section renders alongside the exception type.
            Exception exception;
            try
            {
                throw new InvalidOperationException("Expected 42 but got 41");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "FailingTest",
                    Properties = new PropertyBag(new FailedTestNodeStateProperty(exception, "Expected 42 but got 41")),
                }));
        }
        else if (mode == "location")
        {
            string file = Environment.GetEnvironmentVariable("GH_TEST_FILE")!;

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "FailingTest",
                    Properties = new PropertyBag(
                        new FailedTestNodeStateProperty("Expected 1 but got 2"),
                        new TestFileLocationProperty(file, new LinePositionSpan(new LinePosition(7, -1), new LinePosition(7, -1)))),
                }));

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-2",
                    DisplayName = "SkippedTest",
                    Properties = new PropertyBag(
                        new SkippedTestNodeStateProperty("Not today"),
                        new TestFileLocationProperty(file, new LinePositionSpan(new LinePosition(9, -1), new LinePosition(9, -1)))),
                }));
        }
        else if (mode is "timed" or "data-driven")
        {
            (string Uid, string DisplayName, TimeSpan Duration)[] timedTests = mode == "data-driven"
                ?
                [
                    ("test-1", "RunDataTest (5)", TimeSpan.FromSeconds(5)),
                    ("test-2", "Tests.RunDataTest (3)", TimeSpan.FromSeconds(3)),
                    ("test-3", "RunDataTest (1)", TimeSpan.FromSeconds(1)),
                ]
                :
                [
                    ("test-1", "SlowTest", TimeSpan.FromSeconds(5)),
                    ("test-2", "MediumTest", TimeSpan.FromSeconds(3)),
                    ("test-3", "FastTest", TimeSpan.FromSeconds(1)),
                ];

            foreach ((string uid, string displayName, TimeSpan duration) in timedTests)
            {
                DateTimeOffset start = DateTimeOffset.UtcNow;
                var properties = new PropertyBag(
                    PassedTestNodeStateProperty.CachedInstance,
                    new TimingProperty(new TimingInfo(start, start + duration, duration)));
                if (mode == "data-driven")
                {
                    properties.Add(new TestMethodIdentifierProperty(
                        "GitHubActionsReportBehavior", string.Empty, "Tests", "RunDataTest", 0, ["System.Int32"], "System.Void"));
                }

                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                    context.Request.Session.SessionUid,
                    new TestNode()
                    {
                        Uid = uid,
                        DisplayName = displayName,
                        Properties = properties,
                    }));
            }
        }
        else if (mode == "slow")
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "SlowTest",
                    Properties = new PropertyBag(InProgressTestNodeStateProperty.CachedInstance),
                }));

            // This is the simulated test workload, not a synchronization delay. Five scan intervals
            // leave room for loaded CI agents; threshold/backoff boundaries are tested with a fake clock.
            await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "SlowTest",
                    Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
                }));
        }
        else if (mode is "pass" or "coverage" or "discovery")
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "PassingTest",
                    Properties = new PropertyBag(mode == "discovery"
                        ? DiscoveredTestNodeStateProperty.CachedInstance
                        : PassedTestNodeStateProperty.CachedInstance),
                }));

            if (mode == "coverage")
            {
                await context.MessageBus.PublishAsync(this, new TestCoverageMessage(
                    context.Request.Session.SessionUid, CoverageScope.Overall, CoverageMetric.Line, 79, 100, Uid));
            }
        }

        // mode == "zero": publish nothing.
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        internal static string GetSourceCode(params string[] targetFrameworks)
            => Sources
                .PatchTargetFrameworks(targetFrameworks)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsGitHubActionsReportVersion$", MicrosoftTestingExtensionsGitHubActionsReportVersion);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            GetSourceCode(TargetFrameworks.All));
    }
}
