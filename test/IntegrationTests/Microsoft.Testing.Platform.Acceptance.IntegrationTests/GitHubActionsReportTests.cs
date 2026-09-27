// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

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
    private const string AssetName = "GitHubActionsReportBehavior";

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
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

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
        //   "location" -> publish a failing and a skipped test, both carrying a TestFileLocationProperty and no
        //                 exception, so the annotations can only be pinned through the declared-location fallback
        string mode = Environment.GetEnvironmentVariable("GH_TEST_MODE") ?? "pass";

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
        else if (mode == "timed")
        {
            (string Uid, string DisplayName, TimeSpan Duration)[] timedTests =
            [
                ("test-1", "SlowTest", TimeSpan.FromSeconds(5)),
                ("test-2", "MediumTest", TimeSpan.FromSeconds(3)),
                ("test-3", "FastTest", TimeSpan.FromSeconds(1)),
            ];

            foreach ((string uid, string displayName, TimeSpan duration) in timedTests)
            {
                DateTimeOffset start = DateTimeOffset.UtcNow;
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                    context.Request.Session.SessionUid,
                    new TestNode()
                    {
                        Uid = uid,
                        DisplayName = displayName,
                        Properties = new PropertyBag(
                            PassedTestNodeStateProperty.CachedInstance,
                            new TimingProperty(new TimingInfo(start, start + duration, duration))),
                    }));
            }
        }
        else if (mode == "pass")
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "PassingTest",
                    Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
                }));
        }

        // mode == "zero": publish nothing.
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsGitHubActionsReportVersion$", MicrosoftTestingExtensionsGitHubActionsReportVersion));
    }
}
