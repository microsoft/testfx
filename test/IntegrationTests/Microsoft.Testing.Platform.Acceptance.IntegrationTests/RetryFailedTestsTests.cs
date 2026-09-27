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

    internal static IEnumerable<(bool RetryArgumentsInResponseFile, bool UseInlineDelimiters, string Tfm)> GetResponseFileMatrix()
    {
        foreach (string tfm in TargetFrameworks.Net)
        {
            yield return (true, false, tfm);
            yield return (true, true, tfm);
            yield return (false, false, tfm);
            yield return (false, true, tfm);
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
            Assert.HasCount(3, trxFiles);
            string[] trxContents = [.. trxFiles.Select(File.ReadAllText)];
            Assert.HasCount(2, trxContents.Distinct(StringComparer.Ordinal));
            string[] ids =
            [
                .. trxContents
                    .Select(contents => Regex.Match(contents, "<TestRun id=\"(.+?)\"").Groups[1].Value)
                    .Distinct(StringComparer.Ordinal),
            ];
            Assert.HasCount(1, ids);
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

    [TestMethod]
    [DynamicData(nameof(GetResponseFileMatrix))]
    public async Task RetryFailedTests_WithArgumentsInResponseFile_Succeeds(bool retryArgumentsInResponseFile, bool useInlineDelimiters, string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, $"response file results {Guid.NewGuid():N}");
        string responseFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.rsp");

        try
        {
            File.WriteAllText(
                responseFile,
                (retryArgumentsInResponseFile, useInlineDelimiters) switch
                {
                    (true, true) => $"""
                                    --retry-failed-tests=1
                                    --retry-failed-tests-max-tests:50
                                    --results-directory="{resultDirectory}"
                                    """,
                    (true, false) => $"""
                                     --retry-failed-tests 1
                                     --retry-failed-tests-max-tests 50
                                     --results-directory "{resultDirectory}"
                                     """,
                    (false, true) => $"--results-directory=\"{resultDirectory}\"",
                    (false, false) => $"--results-directory \"{resultDirectory}\"",
                });

            TestHostResult testHostResult = await testHost.ExecuteAsync(
                retryArgumentsInResponseFile
                    ? $"@{responseFile}"
                    : useInlineDelimiters
                        ? $"@{responseFile} --retry-failed-tests=1 --retry-failed-tests-max-tests:50"
                        : $"@{responseFile} --retry-failed-tests 1 --retry-failed-tests-max-tests 50",
                new()
                {
                    { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                    { "METHOD1", "1" },
                    { "FAIL", "0" },
                    { "RESULTDIR", resultDirectory },
                    { "CHECK_RETRY_RESPONSE_FILE_CLEANUP", "1" },
                },
                cancellationToken: TestContext.CancellationToken);

            testHostResult.AssertExitCodeIs(ExitCode.Success);
            testHostResult.AssertOutputContains("Retry summary: Passed! after 2/2 attempts");
            Assert.IsEmpty(
                Directory.GetFiles(resultDirectory, "retry-*.rsp", SearchOption.AllDirectories),
                "Generated retry response files must be deleted after each attempt.");
        }
        finally
        {
            File.Delete(responseFile);
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
        string summaryPath = Path.Combine(resultDirectory, "github-step-summary.md");
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 1 --results-directory {resultDirectory} --report-gh --report-gh-annotations off --report-gh-groups off",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "GITHUB_ACTIONS", "true" },
                { "GITHUB_STEP_SUMMARY", summaryPath },
                { "METHOD1", "1" },
                { "FAIL", "1" },
                { "SKIPONRETRY", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        // The retry produced no failing result, so the run ends green — but the test never passed either. Assert
        // the exact verdict rather than just the header, so a regression that turns this red still fails the test.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Retry summary: Passed! after 2/2 attempts");
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
        Assert.IsFalse(
            File.Exists(summaryPath),
            "GitHub summary aggregation must fail closed while the final passed/skipped split is ambiguous.");
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
            $"--retry-failed-tests 1 --results-directory {resultDirectory} --report-html --report-junit",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "FAIL", "1" },
                { "CRASHONRETRY", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertOutputContains("Retry summary: Failed! after 2/2 attempts");
        testHostResult.AssertOutputDoesNotContain("(retrying stopped early)");

        // The crashed retry reported nothing, so the run's figures are the first attempt's: one test still failing
        // out of three. Resetting to "failed: 0" would contradict the red verdict directly above it.
        testHostResult.AssertOutputContains("  total: 3");
        testHostResult.AssertOutputContains("  failed: 1");
        testHostResult.AssertOutputContains("  succeeded: 2");

        // The retry was launched but produced no reported counts, so it is not counted as an observed retry.
        testHostResult.AssertOutputDoesNotContain("  retried:");

        // Nothing recovered, so nothing is flaky.
        testHostResult.AssertOutputDoesNotContain("  flaky:");
        testHostResult.AssertOutputDoesNotContain("Flaky tests:");

        string htmlReport = File.ReadAllText(Directory.GetFiles(resultDirectory, "*.html", SearchOption.TopDirectoryOnly).Single());
        const string htmlDataStart = "<script id=\"mtp-data\" type=\"application/json\">";
        const string htmlDataEnd = "</script>";
        int htmlDataStartIndex = htmlReport.IndexOf(htmlDataStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, htmlDataStartIndex, htmlReport);
        htmlDataStartIndex += htmlDataStart.Length;
        int htmlDataEndIndex = htmlReport.IndexOf(htmlDataEnd, htmlDataStartIndex, StringComparison.Ordinal);
        Assert.IsGreaterThan(htmlDataStartIndex, htmlDataEndIndex, htmlReport);
        using var htmlDocument = System.Text.Json.JsonDocument.Parse(
            htmlReport.Substring(htmlDataStartIndex, htmlDataEndIndex - htmlDataStartIndex));
        System.Text.Json.JsonElement htmlRoot = htmlDocument.RootElement;
        Assert.AreEqual(3, htmlRoot.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.AreEqual(1, htmlRoot.GetProperty("summary").GetProperty("failed").GetInt32());
        Assert.IsTrue(htmlRoot.GetProperty("incomplete").GetBoolean());
        Assert.AreEqual("aborted", htmlRoot.GetProperty("runStatus").GetString());
        string[] htmlTestNames =
        [
            .. htmlRoot.GetProperty("tests").EnumerateArray()
                .Select(test => test.GetProperty("displayName").GetString()!),
        ];
        Assert.Contains("TestMethod2", htmlTestNames);
        Assert.Contains("TestMethod3", htmlTestNames);

        string junitReport = File.ReadAllText(Directory.GetFiles(resultDirectory, "*.xml", SearchOption.TopDirectoryOnly).Single());
        var junitDocument = System.Xml.Linq.XDocument.Parse(junitReport);
        System.Xml.Linq.XElement junitRoot = junitDocument.Root!;
        Assert.AreEqual("3", junitRoot.Attribute("tests")!.Value);
        Assert.AreEqual("1", junitRoot.Attribute("failures")!.Value);
        string[] junitTestNames =
        [
            .. junitRoot.Descendants("testcase").Select(test => test.Attribute("name")!.Value),
        ];
        Assert.Contains("TestMethod2", junitTestNames);
        Assert.Contains("TestMethod3", junitTestNames);
        System.Xml.Linq.XElement[] junitProperties = [.. junitRoot.Descendants("property")];
        Assert.Contains(
            property => property.Attribute("name")?.Value == "incomplete"
                && property.Attribute("value")?.Value == "true",
            junitProperties);
        Assert.Contains(
            property => property.Attribute("name")?.Value == "run-status"
                && property.Attribute("value")?.Value == "aborted",
            junitProperties);
    }

    /// <summary>
    /// Exercises the <c>--show-flaky-tests</c> option end to end. The unit tests set the reporter option directly,
    /// so without this nothing verifies that the flag parses, nor that the retry orchestrator's own resolver
    /// (<c>FlakyTestsReportingOptions</c>, which runs in a different process from the terminal reporter) honours
    /// it. Only the flaky count and the named section are suppressed; the retried accounting stays.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_WithShowFlakyTestsOff_SuppressesFlakyCountAndSection(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --show-flaky-tests off --results-directory {resultDirectory}",
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

        // TestMethod1 recovered, so this run is flaky; the option must hide both renderings of that.
        testHostResult.AssertOutputDoesNotContain("  flaky:");
        testHostResult.AssertOutputDoesNotContain("Flaky tests:");
        testHostResult.AssertOutputDoesNotContain("TestMethod1 (failed -> passed)");

        // Retry accounting is not part of the flaky feature and must survive.
        testHostResult.AssertOutputContains("  retried: 1 test(s), 1 extra run(s)");
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
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_MaxTestsCountWithNoRetries_ReportsAttemptsExhausted(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 0 --retry-failed-tests-max-tests 1 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "RESULTDIR", resultDirectory },
                { "METHOD1", "1" },
                { "METHOD2", "1" },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Failure threshold policy is enabled, failed tests will not be restarted.");
        testHostResult.AssertOutputContains("Maximum failed tests threshold is 1 and 2 tests failed");
        testHostResult.AssertOutputContains("Retry summary: Failed! after 1/1 attempts");
        testHostResult.AssertOutputDoesNotContain("(retrying stopped early)");
        testHostResult.AssertOutputContains("  total: 3");
        testHostResult.AssertOutputContains("  failed: 2");
        testHostResult.AssertOutputContains("  succeeded: 1");
        testHostResult.AssertOutputDoesNotContain("  retried:");
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
        Assert.HasCount(2, retryTrxFiles);

        string firstAttemptTrxContent = File.ReadAllText(
            retryTrxFiles.Single(file => Path.GetFileName(Path.GetDirectoryName(file)) == "1"));
        Assert.Contains("TestMethod1", firstAttemptTrxContent);
        Assert.Contains("TestMethod2", firstAttemptTrxContent);
        Assert.DoesNotContain("TestMethod3", firstAttemptTrxContent);

        // Verify that the retry attempt only ran the failed test (TestMethod1) - i.e. the treenode-filter was
        // dropped and replaced by --filter-uid 1.
        string finalAttemptTrxContent = File.ReadAllText(
            retryTrxFiles.Single(file => Path.GetFileName(Path.GetDirectoryName(file)) == "2"));
        Assert.Contains("TestMethod1", finalAttemptTrxContent);
        Assert.DoesNotContain("TestMethod2", finalAttemptTrxContent);
        Assert.DoesNotContain("TestMethod3", finalAttemptTrxContent);

        // TRX intentionally keeps final-attempt semantics, so the top-level file is a copy of attempt 2.
        string[] topLevelTrxFiles = Directory.GetFiles(resultDirectory, "*.trx", SearchOption.TopDirectoryOnly);
        Assert.HasCount(1, topLevelTrxFiles);

        string trxContent = File.ReadAllText(topLevelTrxFiles[0]);
        Assert.AreEqual(finalAttemptTrxContent, trxContent);
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

    internal static IEnumerable<(string Tfm, string? SeededVariable)> GetRunIdMatrix()
    {
        foreach (string tfm in TargetFrameworks.Net)
        {
            // The orchestrator resolves the logical run id as: an explicitly set id wins, else the dotnet test
            // execution id (which already identifies this test application's process tree), else a fresh one.
            // Exercise all three branches.
            yield return (tfm, null);
            yield return (tfm, EnvironmentVariableConstants.TESTINGPLATFORM_LOGICAL_RUN_ID);
            yield return (tfm, EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetRunIdMatrix))]
    public async Task RetryFailedTests_CtrfReports_ShareRunIdButNotReportId(string tfm, string? seededVariable)
    {
        // Each attempt is a separate process that writes its own CTRF document, but together they are one
        // logical run. Per ctrf-io/ctrf#58 those documents SHOULD share a `runId` while each stays a distinct
        // artifact with its own `reportId`. This is the only test that exercises the cross-process contract:
        // the engine unit tests mock IEnvironment, so they cannot observe the orchestrator's seeding.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));

        // METHOD1=1 makes TestMethod1 fail on the first attempt and pass on the second, so exactly two
        // attempts run and each writes a CTRF report.
        Dictionary<string, string?> environmentVariables = new()
        {
            { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
            { "METHOD1", "1" },
            { "RESULTDIR", resultDirectory },
        };

        // When a correlation id is supplied from outside, the attempts must adopt THAT id rather than minting
        // their own — that is what lets a CI job tie several modules or machines into one logical run.
        string? expectedRunId = null;
        if (seededVariable is not null)
        {
            expectedRunId = $"seeded-{Guid.NewGuid():N}";
            environmentVariables[seededVariable] = expectedRunId;
        }

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --report-ctrf --results-directory {resultDirectory}",
            environmentVariables,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Retry summary: Passed! after 2/4 attempts");

        // Every physical attempt stays under Retries/<n>/, and the top-level report is a new consolidated document.
        string[] ctrfFiles =
        [
            .. Directory.GetFiles(resultDirectory, "*.ctrf.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal),
        ];
        Assert.HasCount(3, ctrfFiles, $"Expected two per-attempt reports and one consolidated report.{Environment.NewLine}{string.Join(Environment.NewLine, ctrfFiles)}");

        string[] runIds = [.. ctrfFiles.Select(f => ReadRequiredStringProperty(f, "runId"))];
        string[] reportIds = [.. ctrfFiles.Select(f => ReadRequiredStringProperty(f, "reportId"))];

        Assert.HasCount(1, runIds.Distinct(StringComparer.Ordinal));
        Assert.HasCount(3, reportIds.Distinct(StringComparer.Ordinal));
        Assert.AreNotEqual(runIds[0], reportIds[0], "runId and reportId identify different things and must not be the same value.");

        string consolidatedPath = Directory.GetFiles(resultDirectory, "*.ctrf.json", SearchOption.TopDirectoryOnly).Single();
        using var consolidated = System.Text.Json.JsonDocument.Parse(File.ReadAllText(consolidatedPath));
        System.Text.Json.JsonElement results = consolidated.RootElement.GetProperty("results");
        Assert.AreEqual(3, results.GetProperty("summary").GetProperty("tests").GetInt32());
        Assert.AreEqual(1, results.GetProperty("summary").GetProperty("flaky").GetInt32());
        System.Text.Json.JsonElement flakyTest = results.GetProperty("tests")
            .EnumerateArray()
            .Single(test => test.GetProperty("name").GetString() == "TestMethod1");
        Assert.AreEqual("passed", flakyTest.GetProperty("status").GetString());
        Assert.AreEqual(1, flakyTest.GetProperty("retries").GetInt32());
        Assert.AreEqual("failed", flakyTest.GetProperty("retryAttempts")[0].GetProperty("status").GetString());

        if (expectedRunId is not null)
        {
            Assert.AreEqual(expectedRunId, runIds[0], $"'{seededVariable}' must be honored instead of minting a new run id.");
        }
        else
        {
            Assert.IsTrue(Guid.TryParse(runIds[0], out _), $"An uncorrelated run must mint a GUID run id, got '{runIds[0]}'.");
        }
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_CtrfAbsoluteFileName_PublishesConsolidatedReport(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        string reportPath = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.ctrf.json");

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --report-ctrf --report-ctrf-filename \"{reportPath}\" --results-directory \"{resultDirectory}\"",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        Assert.IsTrue(File.Exists(reportPath));
        Assert.IsEmpty(Directory.GetFiles(resultDirectory, "*.ctrf.json", SearchOption.TopDirectoryOnly));
        Assert.HasCount(
            2,
            Directory.GetFiles(Path.Combine(resultDirectory, "Retries"), "*.ctrf.json", SearchOption.AllDirectories));

        using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(reportPath));
        System.Text.Json.JsonElement results = report.RootElement.GetProperty("results");
        Assert.AreEqual(3, results.GetProperty("summary").GetProperty("tests").GetInt32());
        Assert.AreEqual(1, results.GetProperty("summary").GetProperty("flaky").GetInt32());
        System.Text.Json.JsonElement flakyTest = results.GetProperty("tests")
            .EnumerateArray()
            .Single(test => test.GetProperty("name").GetString() == "TestMethod1");
        Assert.AreEqual("passed", flakyTest.GetProperty("status").GetString());
        Assert.AreEqual("failed", flakyTest.GetProperty("retryAttempts")[0].GetProperty("status").GetString());
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_GitHubActionsSummary_ReportsFlakyTest(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        string summaryPath = Path.Combine(resultDirectory, "github-step-summary.md");

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 1 --results-directory \"{resultDirectory}\" --report-gh --report-gh-annotations off --report-gh-groups off",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "GITHUB_ACTIONS", "true" },
                { "GITHUB_STEP_SUMMARY", summaryPath },
                { "METHOD1", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        Assert.IsTrue(File.Exists(summaryPath));
        string summary = File.ReadAllText(summaryPath);
        Assert.Contains("| Total | Passed | Failed | Skipped | Flaky | Duration |", summary, summary);
        Assert.Contains("| 3 | 3 | 0 | 0 | 1 |", summary, summary);
        Assert.Contains("### ⚠️ Flaky tests (1)", summary, summary);
        Assert.Contains("`DummyClassName.TestMethod1`", summary, summary);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_HtmlAndJUnitReports_AreConsolidatedWithRetryHistory(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 1 --results-directory \"{resultDirectory}\" --report-html --report-junit",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "RESULTDIR", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        string htmlReportPath = Directory.GetFiles(resultDirectory, "*.html", SearchOption.TopDirectoryOnly).Single();
        string htmlReport = File.ReadAllText(htmlReportPath);
        Assert.Contains(@"""total"":3", htmlReport, htmlReport);
        Assert.Contains(@"""passed"":3", htmlReport, htmlReport);
        Assert.Contains(@"""flaky"":1", htmlReport, htmlReport);
        Assert.Contains(@"""retryAttempts""", htmlReport, htmlReport);
        Assert.Contains("Retry history", htmlReport, htmlReport);
        Assert.Contains("badge flaky", htmlReport, htmlReport);

        string junitReportPath = Directory.GetFiles(resultDirectory, "*.xml", SearchOption.TopDirectoryOnly).Single();
        string junitReport = File.ReadAllText(junitReportPath);
        Assert.Contains(@"tests=""3"" failures=""0""", junitReport, junitReport);
        Assert.HasCount(1, Regex.Matches(junitReport, @"<testcase name=""TestMethod1"""), junitReport);

        string retriesDirectory = Path.Combine(resultDirectory, "Retries");
        Assert.HasCount(2, Directory.GetFiles(retriesDirectory, "*.html", SearchOption.AllDirectories));
        Assert.HasCount(2, Directory.GetFiles(retriesDirectory, "*.xml", SearchOption.AllDirectories));
    }

    private static string ReadRequiredStringProperty(string filePath, string propertyName)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(filePath));
        Assert.IsTrue(
            document.RootElement.TryGetProperty(propertyName, out System.Text.Json.JsonElement value),
            $"'{propertyName}' is missing from '{filePath}'.");

        string? text = value.GetString();
        Assert.IsFalse(string.IsNullOrEmpty(text), $"'{propertyName}' must be a non-empty string in '{filePath}'.");
        return text!;
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsCtrfReportVersion$", MicrosoftTestingExtensionsCtrfReportVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsJUnitReportVersion$", MicrosoftTestingExtensionsJUnitReportVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsGitHubActionsReportVersion$", MicrosoftTestingExtensionsGitHubActionsReportVersion));

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
        <PackageReference Include="Microsoft.Testing.Extensions.CtrfReport" Version="$MicrosoftTestingExtensionsCtrfReportVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.GitHubActionsReport" Version="$MicrosoftTestingExtensionsGitHubActionsReportVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HtmlReport" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.JUnitReport" Version="$MicrosoftTestingExtensionsJUnitReportVersion$" />
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
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
        builder.AddCtrfReportProvider();
        builder.AddJUnitReportProvider();
#pragma warning restore TPEXP
        builder.AddGitHubActionsProvider();
        builder.AddHtmlReportProvider();
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

        if (Environment.GetEnvironmentVariable("CHECK_RETRY_RESPONSE_FILE_CLEANUP") == "1"
            && Environment.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER") == "2"
            && Directory.GetFiles(Path.Combine(resultDir, "Retries"), "retry-arguments-1.rsp", SearchOption.AllDirectories).Length != 0)
        {
            throw new InvalidOperationException("The response file from retry attempt 1 still exists during attempt 2.");
        }

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
