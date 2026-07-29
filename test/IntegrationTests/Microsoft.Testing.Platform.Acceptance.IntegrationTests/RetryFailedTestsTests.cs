#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class RetryFailedTestsTests : AcceptanceTestBase<RetryFailedTestsTests.TestAssetFixture>
{
    private const string AssetName = "RetryFailedTests";

    /// <summary>
    /// Asserts that <paramref name="value"/> occurs exactly once in the test host output. Used for the run-summary
    /// header under retry: only the first attempt (which executes the whole suite) prints one, while the retry
    /// attempts — which re-run just the previously-failed tests — have theirs suppressed in favour of the single
    /// reconciled retry summary. A plain "contains" assertion would not catch a regression that re-introduced the
    /// per-attempt summaries.
    /// </summary>
    private static void AssertOutputContainsExactlyOnce(TestHostResult testHostResult, string value)
    {
        int count = 0;
        int index = testHostResult.StandardOutput.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = testHostResult.StandardOutput.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        Assert.AreEqual(1, count, $"Expected '{value}' exactly once in the output, found {count}.{Environment.NewLine}{testHostResult.StandardOutput}");
    }

    internal static IEnumerable<(string Arguments, bool FailOnly)> GetMatrix()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            foreach (bool failOnly in new[] { true, false })
            {
                yield return (tfm, failOnly);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetMatrix))]
    public async Task RetryFailedTests_OnlyRetryTimes_Succeeds(string tfm, bool failOnly)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --results-directory {resultDirectory} --report-trx",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "FAIL", failOnly ? "1" : "0" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        if (!failOnly)
        {
            testHostResult.AssertExitCodeIs(ExitCode.Success);
            testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");
            testHostResult.AssertOutputContains("  total: 3");
            testHostResult.AssertOutputContains("  failed: 0");
            testHostResult.AssertOutputContains("  succeeded: 3");
            testHostResult.AssertOutputContains("  skipped: 0");
            testHostResult.AssertOutputContains("  flaky: 1 (passed after retry)");
            // The single retried test cost exactly one extra run; the old "(+N retried)" suffix conflated the two.
            testHostResult.AssertOutputContains("  retried: 1 test(s), 1 extra run(s)");
            // ...and it is named, which is the whole point of the flaky report.
            testHostResult.AssertOutputContains("Flaky tests:");
            testHostResult.AssertOutputContains("TestMethod1 (failed -> passed)");

            // The first attempt runs the whole suite, so its summary is accurate and is kept.
            testHostResult.AssertOutputContains("Test run summary: Failed!");
            // The retry attempt re-ran only TestMethod1, so its summary would have claimed "total: 1" for a
            // three-test suite. It is suppressed; the retry summary reconciles the attempts instead.
            testHostResult.AssertOutputDoesNotContain("Test run summary: Passed!");

            string[] trxFiles = Directory.GetFiles(resultDirectory, "*.trx", SearchOption.AllDirectories);
            Assert.HasCount(2, trxFiles);
            string trxContents1 = File.ReadAllText(trxFiles[0]);
            string trxContents2 = File.ReadAllText(trxFiles[1]);
            Assert.AreNotEqual(trxContents1, trxContents2);
            string id1 = Regex.Match(trxContents1, "<TestRun id=\"(.+?)\"").Groups[1].Value;
            string id2 = Regex.Match(trxContents2, "<TestRun id=\"(.+?)\"").Groups[1].Value;
            Assert.AreEqual(id1, id2);
        }
        else
        {
            testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            testHostResult.AssertOutputContains("Retry summary: Failed! after 4/4 attempts");
            testHostResult.AssertOutputContains("Retry: attempt 1/4 failed - 1 failing test(s), retrying");
            testHostResult.AssertOutputContains("Retry: attempt 2/4 failed - 1 failing test(s), retrying");
            testHostResult.AssertOutputContains("Retry: attempt 3/4 failed - 1 failing test(s), retrying");
            // The final (4th) attempt is reported by the summary verdict, not by an amber "retrying" line.
            testHostResult.AssertOutputDoesNotContain("Retry: attempt 4/4 failed");
            testHostResult.AssertOutputContains("  total: 3");
            testHostResult.AssertOutputContains("  failed: 1");
            testHostResult.AssertOutputContains("  succeeded: 2");
            // The test was retried but never recovered, so it is retried-but-not-flaky: no flaky count, no listing.
            testHostResult.AssertOutputContains("  retried: 1 test(s), 3 extra run(s)");
            testHostResult.AssertOutputDoesNotContain("  flaky:");
            testHostResult.AssertOutputDoesNotContain("Flaky tests:");
            // Only the first attempt's summary survives; the three retry attempts each re-ran a single test and
            // would otherwise have printed three more "total: 1" blocks.
            AssertOutputContainsExactlyOnce(testHostResult, "Test run summary:");
        }
    }

    /// <summary>
    /// A test that fails and then comes back <em>skipped</em> on the retry has not recovered, so it must not be
    /// counted or listed as flaky. This guards the accounting against inferring recovery from "was retried and is
    /// no longer in the failed set", which also matches a test that never produced a passing result.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_WhenRetriedTestIsSkipped_IsNotReportedAsFlaky(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 1 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "FAIL", "1" },
                { "SKIPONRETRY", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        // The retry produced no failing result, so the run ends green — but the test never passed either.
        testHostResult.AssertOutputContains("Retry summary:");
        testHostResult.AssertOutputContains("  retried: 1 test(s), 1 extra run(s)");
        testHostResult.AssertOutputDoesNotContain("  flaky:");
        testHostResult.AssertOutputDoesNotContain("Flaky tests:");
        testHostResult.AssertOutputDoesNotContain("TestMethod1 (failed -> passed)");

        // Known imprecision: the outcome counts come from the first attempt's suite composition with only the
        // failures refreshed, so a test whose outcome changed from failed to skipped lands in "succeeded" rather
        // than "skipped". Correcting it needs the first attempt's per-test skipped breakdown (a folded data-driven
        // test can contribute both failing and skipped results under one uid). Asserted as-is so the block is
        // known to stay internally consistent — total == failed + succeeded + skipped — which is what a naive
        // correction broke.
        testHostResult.AssertOutputContains("  total: 3");
        testHostResult.AssertOutputContains("  failed: 0");
        testHostResult.AssertOutputContains("  succeeded: 3");
        testHostResult.AssertOutputContains("  skipped: 0");
    }

    /// <summary>
    /// When a retry attempt dies before its test session finishes it reports no counts at all. The summary must
    /// keep the last figures it actually received instead of treating the missing report as "zero failures", which
    /// would render a red verdict above a green-looking tally.
    /// </summary>
    [TestMethod]
    // Uses FailFast, and crash dumps are not supported on .NET Framework at the moment.
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_WhenRetryAttemptCrashes_KeepsLastReportedCounts(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 1 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "FAIL", "1" },
                { "CRASHONRETRY", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("Retry summary: Failed!");

        // The crashed retry reported nothing, so the run's figures are the first attempt's: one test still failing
        // out of three. Resetting to "failed: 0" would contradict the red verdict directly above it.
        testHostResult.AssertOutputContains("  total: 3");
        testHostResult.AssertOutputContains("  failed: 1");
        testHostResult.AssertOutputContains("  succeeded: 2");

        // Nothing recovered, so nothing is flaky.
        testHostResult.AssertOutputDoesNotContain("  flaky:");
        testHostResult.AssertOutputDoesNotContain("Flaky tests:");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_WithDelay_StripsDelayFromChildArgs(string tfm)
    {
        // The retry asset has AddRetryProvider() registered. If --retry-failed-tests-delay is NOT stripped from
        // child-process arguments, the child will receive --retry-failed-tests-delay without --retry-failed-tests
        // (the orchestrator strips the latter), causing validation to fail. A successful run therefore proves
        // arg-stripping is working.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --retry-failed-tests-delay 0 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "FAIL", "0" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");
    }

    [TestMethod]
    [DynamicData(nameof(GetMatrix))]
    public async Task RetryFailedTests_MaxPercentage_Succeeds(string tfm, bool fail)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --retry-failed-tests-max-percentage 50 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "RESULTDIR", resultDirectory },
                { "METHOD1", "1" },
                { fail ? "METHOD2" : "UNUSED", "1" },
            },
            cancellationToken: TestContext.CancellationToken);

        string retriesPath = Path.Combine(resultDirectory, "Retries");
        Assert.IsTrue(Directory.Exists(retriesPath));
        string[] retriesDirectories = Directory.GetDirectories(retriesPath);
        Assert.HasCount(1, retriesDirectories);
        string createdDirName = Path.GetFileName(retriesDirectories[0]);

        // Asserts that we are not using long names, to reduce long path issues.
        // See https://github.com/microsoft/testfx/issues/4002
        Assert.AreEqual(5, createdDirName.Length, $"Expected directory '{createdDirName}' to be of length 5.");

        if (fail)
        {
            testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            testHostResult.AssertOutputContains("Failure threshold policy is enabled, failed tests will not be restarted.");
            testHostResult.AssertOutputContains("Percentage failed threshold is 50% and 66.67% tests failed (2/3)");
            // The threshold stopped retrying after the first attempt. That attempt no longer prints its own
            // summary, so the retry summary is what accounts for the run — using the "stopped early" wording
            // rather than "1/4 attempts", which would imply the other three ran and failed.
            testHostResult.AssertOutputContains("Retry summary: Failed! after 1 attempt(s) (retrying stopped early)");
            testHostResult.AssertOutputContains("  total: 3");
            testHostResult.AssertOutputContains("  failed: 2");
            testHostResult.AssertOutputContains("  succeeded: 1");
            testHostResult.AssertOutputDoesNotContain("  retried:");
            // Retrying never started, so only the first attempt's summary is present.
            AssertOutputContainsExactlyOnce(testHostResult, "Test run summary:");
        }
        else
        {
            testHostResult.AssertExitCodeIs(ExitCode.Success);
            testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");
            testHostResult.AssertOutputContains("  total: 3");
            testHostResult.AssertOutputContains("  succeeded: 3");
            AssertOutputContainsExactlyOnce(testHostResult, "Test run summary:");
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetMatrix))]
    public async Task RetryFailedTests_MaxTestsCount_Succeeds(string tfm, bool fail)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --retry-failed-tests-max-tests 1 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "RESULTDIR", resultDirectory },
                { "METHOD1", "1" },
                { fail ? "METHOD2" : "UNUSED", "1" },
            }, cancellationToken: TestContext.CancellationToken);

        if (fail)
        {
            testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            testHostResult.AssertOutputContains("Failure threshold policy is enabled, failed tests will not be restarted.");
            testHostResult.AssertOutputContains("Maximum failed tests threshold is 1 and 2 tests failed");
            testHostResult.AssertOutputContains("Retry summary: Failed! after 1 attempt(s) (retrying stopped early)");
            testHostResult.AssertOutputContains("  total: 3");
            testHostResult.AssertOutputContains("  failed: 2");
            testHostResult.AssertOutputContains("  succeeded: 1");
            AssertOutputContainsExactlyOnce(testHostResult, "Test run summary:");
        }
        else
        {
            testHostResult.AssertExitCodeIs(ExitCode.Success);
            testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");
            testHostResult.AssertOutputContains("  total: 3");
            testHostResult.AssertOutputContains("  succeeded: 3");
            AssertOutputContainsExactlyOnce(testHostResult, "Test run summary:");
        }
    }

    [TestMethod]
    // We use crash dump, not supported in NetFramework at the moment
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_MoveFiles_Succeeds(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --crashdump --retry-failed-tests 1 --results-directory {resultDirectory}",
            new()
            {
                        { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                        { "RESULTDIR", resultDirectory },
                        { "CRASH", "1" },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        string[] entries = [.. Directory.GetFiles(resultDirectory, "*.*", SearchOption.AllDirectories).Where(x => !x.Contains("Retries", StringComparison.OrdinalIgnoreCase))];

        // 1 trx file
        Assert.ContainsSingle(x => x.EndsWith("trx", StringComparison.OrdinalIgnoreCase), entries);

        // Number of dmp files seems to differ locally and in CI
        int dumpFilesCount = entries.Count(x => x.EndsWith("dmp", StringComparison.OrdinalIgnoreCase));

        if (dumpFilesCount == 2)
        {
            // Dump file inside the trx structure
            Assert.ContainsSingle(x => x.Contains($"{Path.DirectorySeparatorChar}In{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && x.EndsWith("dmp", StringComparison.OrdinalIgnoreCase), entries);
        }
        else if (dumpFilesCount is 0 or > 2)
        {
            Assert.Fail($"Expected 1 or 2 dump files, but found {dumpFilesCount}");
        }
    }

    [TestMethod]
    public async Task RetryFailedTests_PassingFromFirstTime_UsingTestTarget_MoveFiles_Succeeds()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{AssetFixture.TargetAssetPath}\" -c Release -t:DispatchToInnerBuildsWithMTPTestTarget -p:TestingPlatformCommandLineArguments=\"--retry-failed-tests 1 --results-directory %22{resultDirectory}%22\"",
            workingDirectory: AssetFixture.TargetAssetPath, cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);

        // File names are on the form: RetryFailedTests_tfm_architecture.log
        string[] logFilesFromInvokeTestingPlatformTask = Directory.GetFiles(resultDirectory, "RetryFailedTests_*_*.log");
        Assert.HasCount(TargetFrameworks.All.Length, logFilesFromInvokeTestingPlatformTask);
        foreach (string logFile in logFilesFromInvokeTestingPlatformTask)
        {
            string logFileContents = File.ReadAllText(logFile);
            // Nothing was retried, so the single attempt keeps its own summary and the retry summary reports the
            // same run without any retry accounting.
            Assert.Contains("Test run summary: Passed!", logFileContents);
            Assert.Contains("Retry summary: Passed! on the first attempt (no retries needed)", logFileContents);
            Assert.Contains("total: 3", logFileContents);
            Assert.Contains("succeeded: 3", logFileContents);
            Assert.DoesNotContain("retried:", logFileContents);
            Assert.DoesNotContain("flaky:", logFileContents);
        }
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_WithPreexistingFilterUid_ReplacesFilterOnRetry(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));

        // Use --filter-uid to select tests 1 and 2. Test 1 will fail on first attempt, pass on second.
        // Test 3 is not in the filter, so it should never run.
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --filter-uid 1 --filter-uid 2 --report-trx --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");
        testHostResult.AssertOutputContains("Retry: attempt 1/4 failed - 1 failing test(s), retrying");

        // Verify that the retry attempt only ran the failed test (UID 1).
        // The TRX in the top-level results directory (not under Retries/) is from the last attempt.
        string[] topLevelTrxFiles = Directory.GetFiles(resultDirectory, "*.trx", SearchOption.TopDirectoryOnly);
        Assert.HasCount(1, topLevelTrxFiles);

        string trxContent = File.ReadAllText(topLevelTrxFiles[0]);
        Assert.Contains("TestMethod1", trxContent);
        Assert.DoesNotContain("TestMethod2", trxContent);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_WithPreexistingTreeNodeFilter_ReplacesFilterOnRetry(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));

        // Use --treenode-filter to select tests TestMethod1 and TestMethod2. The filter intentionally starts
        // with '/' because TreeNodeFilter expressions are matched against slash-prefixed node paths such as
        // '/TestMethod1'. Test 1 will fail on first attempt, pass on second. TestMethod3 is not matched by
        // the filter, so it should never run.
        // On retry, the orchestrator strips --treenode-filter and replaces it with --filter-uid for the failed
        // tests only; this test guards against issue #5673 (Retry + tree node filter must not stack filters).
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --treenode-filter \"/(TestMethod1|TestMethod2)\" --report-trx --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");
        testHostResult.AssertOutputContains("Retry: attempt 1/4 failed - 1 failing test(s), retrying");

        // Verify that the first attempt honored the treenode-filter.
        string[] retryTrxFiles = Directory.GetFiles(Path.Combine(resultDirectory, "Retries"), "*.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, retryTrxFiles);

        string retryTrxContent = File.ReadAllText(retryTrxFiles[0]);
        Assert.Contains("TestMethod1", retryTrxContent);
        Assert.Contains("TestMethod2", retryTrxContent);
        Assert.DoesNotContain("TestMethod3", retryTrxContent);

        // Verify that the retry attempt only ran the failed test (TestMethod1) - i.e. the treenode-filter was
        // dropped and replaced by --filter-uid 1.
        // The TRX in the top-level results directory (not under Retries/) is from the last attempt.
        string[] topLevelTrxFiles = Directory.GetFiles(resultDirectory, "*.trx", SearchOption.TopDirectoryOnly);
        Assert.HasCount(1, topLevelTrxFiles);

        string trxContent = File.ReadAllText(topLevelTrxFiles[0]);
        Assert.Contains("TestMethod1", trxContent);
        Assert.DoesNotContain("TestMethod2", trxContent);
        Assert.DoesNotContain("TestMethod3", trxContent);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_WithMinimumExpectedTests_StripsThresholdOnRetry(string tfm)
    {
        // Regression test for https://github.com/microsoft/testfx/issues/5639.
        // --minimum-expected-tests must be honored on the first attempt but stripped from retry-attempt
        // arguments. Without the strip, the retry (which only re-runs the previously failed tests) would
        // always trip the policy and exit with code 9 (MinimumExpectedTestsPolicyViolation) even though
        // the full first-attempt run satisfied the threshold.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));

        // METHOD1=1 causes TestMethod1 to fail on the first attempt and pass on the second. The asset has
        // 3 tests in total. With --minimum-expected-tests 3, the first attempt runs all 3 (so the policy
        // is satisfied) and the retry attempt runs only TestMethod1 (so without the fix the policy would
        // fail with "tests ran 1, minimum expected 3").
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --minimum-expected-tests 3 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");
        testHostResult.AssertOutputDoesNotContain("Minimum expected tests policy violation");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        private const string TestCode = """
#file RetryFailedTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
        <TestingPlatformCaptureOutput>false</TestingPlatformCaptureOutput>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file global.json
{
  "test": {
    "runner": "VSTest"
  }
}

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.MSBuild;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        TreeNodeFilterExtension treeNodeFilterExtension = new();
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(new TrxReportCapability()),
            (_,__) => new DummyTestFramework());
        builder.AddCrashDumpProvider();
        builder.AddTrxReportProvider();
        builder.AddRetryProvider();
        builder.AddMSBuild();
        builder.AddTreeNodeFilterService(treeNodeFilterExtension);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TreeNodeFilterExtension : IExtension
{
    public string Uid => nameof(TreeNodeFilterExtension);
    public string Version => "1.0.0";
    public string DisplayName => nameof(TreeNodeFilterExtension);
    public string Description => nameof(TreeNodeFilterExtension);
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

public class TrxReportCapability : ITrxReportCapability
{
    bool ITrxReportCapability.IsSupported { get; } = true;
    void ITrxReportCapability.Enable()
    {
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        bool fail = Environment.GetEnvironmentVariable("FAIL") == "1";
        // Tests are using this env variable so it won't be null.
        string resultDir = Environment.GetEnvironmentVariable("RESULTDIR")!;
        bool crash = Environment.GetEnvironmentVariable("CRASH") == "1";

        var filter = (context.Request as TestExecutionRequest)?.Filter;
        var uidFilter = filter as TestNodeUidListFilter;
        var treeNodeFilter = filter as TreeNodeFilter;

        var testMethod1Identifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "TestMethod1", 0, Array.Empty<string>(), string.Empty);
        var testMethod2Identifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "TestMethod2", 0, Array.Empty<string>(), string.Empty);
        var testMethod3Identifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "TestMethod3", 0, Array.Empty<string>(), string.Empty);

        if (IsIncluded(uidFilter, treeNodeFilter, "1", "TestMethod1"))
        {
            // SKIPONRETRY makes TestMethod1 fail on the first attempt and come back SKIPPED on the retry. A skipped
            // test has not recovered, so it must not be counted or listed as flaky. Inferring recovery from
            // "no longer in the failed set" would wrongly report it as 'failed -> passed'.
            bool skipOnRetry = Environment.GetEnvironmentVariable("SKIPONRETRY") == "1" && uidFilter is not null;

            // CRASHONRETRY lets the FIRST attempt report its results normally and kills the RETRY attempt before it
            // can report anything. The summary must then keep the last counts it actually received rather than
            // resetting them to zero.
            if (Environment.GetEnvironmentVariable("CRASHONRETRY") == "1" && uidFilter is not null)
            {
                Environment.FailFast("CRASHONRETRY");
            }

            if (skipOnRetry)
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "1", DisplayName = "TestMethod1", Properties = new(new SkippedTestNodeStateProperty(), testMethod1Identifier) }));
            }
            else if (TestMethod1(fail, resultDir, crash))
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "1", DisplayName = "TestMethod1", Properties = new(PassedTestNodeStateProperty.CachedInstance, testMethod1Identifier) }));
            }
            else
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "1", DisplayName = "TestMethod1", Properties = new(new FailedTestNodeStateProperty(), testMethod1Identifier) }));
            }
        }

        if (IsIncluded(uidFilter, treeNodeFilter, "2", "TestMethod2"))
        {
            if (TestMethod2(fail, resultDir))
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "2", DisplayName = "TestMethod2", Properties = new(PassedTestNodeStateProperty.CachedInstance, testMethod2Identifier) }));
            }
            else
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "2", DisplayName = "TestMethod2", Properties = new(new FailedTestNodeStateProperty(), testMethod2Identifier) }));
            }
        }

        if (IsIncluded(uidFilter, treeNodeFilter, "3", "TestMethod3"))
        {
            if (TestMethod3(fail, resultDir))
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "3", DisplayName = "TestMethod3", Properties = new(PassedTestNodeStateProperty.CachedInstance, testMethod3Identifier) }));
            }
            else
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "3", DisplayName = "TestMethod3", Properties = new(new FailedTestNodeStateProperty(), testMethod3Identifier) }));
            }
        }

        context.Complete();
    }

    private static bool IsIncluded(TestNodeUidListFilter? uidFilter, TreeNodeFilter? treeNodeFilter, string uid, string displayName)
    {
        if (uidFilter is not null && !uidFilter.TestNodeUids.Any(n => n.Value == uid))
        {
            return false;
        }

        if (treeNodeFilter is not null && !treeNodeFilter.MatchesFilter("/" + displayName, new PropertyBag()))
        {
            return false;
        }

        return true;
    }

    private bool TestMethod1(bool fail, string resultDir, bool crash)
    {
        if (crash)
        {
            Environment.FailFast("CRASH");
        }

        bool envVar = Environment.GetEnvironmentVariable("METHOD1") is null;

        if (envVar) return true;

        string succeededFile = Path.Combine(resultDir, "M1_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!fail)
        {
            if (fileExits) return true;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        return assert;
    }

    private bool TestMethod2(bool fail, string resultDir)
    {
        bool envVar = Environment.GetEnvironmentVariable("METHOD2") is null;
        System.Console.WriteLine("envVar " + envVar);

        if (envVar) return true;

        string succeededFile = Path.Combine(resultDir,"M2_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!fail)
        {
            if (fileExits) return true;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        return assert;
    }

    private bool TestMethod3(bool fail, string resultDir)
    {
        bool envVar = Environment.GetEnvironmentVariable("METHOD3") is null;

        if (envVar) return true;

        string succeededFile = Path.Combine(resultDir,"M3_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!fail)
        {
            if (fileExits) return true;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        return assert;
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
