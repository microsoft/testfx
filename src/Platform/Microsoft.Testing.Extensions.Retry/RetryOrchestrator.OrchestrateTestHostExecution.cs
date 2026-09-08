// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed partial class RetryOrchestrator
{
    public async Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken)
    {
        InitializeEnvironment();

        ILogger logger = _serviceProvider.GetLoggerFactory().CreateLogger<RetryOrchestrator>();
        IConfiguration configuration = _serviceProvider.GetConfiguration();

        ITestApplicationModuleInfo currentTestApplicationModuleInfo = _serviceProvider.GetTestApplicationModuleInfo();
        ExecutableInfo executableInfo = currentTestApplicationModuleInfo.GetCurrentExecutableInfo();

        int userMaxRetryCount = GetUserMaxRetryCount();

        // Find out the retry args indices so we can clean up the command line when we restart.
        string[] originalExecutableArguments = [.. executableInfo.Arguments];
        string[] executableArguments = [.. executableInfo.ExpandedArguments];
        List<int> indexToCleanup = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);

        // Override the result directory with the attempt one
        string resultDirectory = configuration.GetTestResultDirectory();

        List<int> exitCodes = [];
        IOutputDevice outputDevice = _serviceProvider.GetOutputDevice();
        IFileSystem fileSystem = _serviceProvider.GetFileSystem();

        int attemptCount = 0;
        string[]? lastListOfFailedId = null;
        string? currentTryResultFolder = null;
        bool thresholdPolicyKickedIn = false;
        string retryRootFolder = CreateRetriesDirectory(resultDirectory);
        bool retryInterrupted = false;
        List<RetryAttemptArtifact> attemptArtifacts = [];

        IPlatformOpenTelemetryService? otelService = _serviceProvider.GetPlatformOTelService();
        ICounter<int>? retryCounter = CreateRetryCounter(otelService);

        // Retry summary accounting (single-assembly). The orchestrator is the only component that observes every
        // attempt, so it reconciles them into one headline:
        //   retried = union of the scheduled retry sets whose following attempt reported at least one result
        //   flaky   = union of the tests each attempt reported as recovered
        // Recovery is reported by the attempt that observed it rather than inferred here as "retried and no longer
        // in the failed set": that inference also matches a test which never ran, so an attempt that crashed or
        // matched no filter would silently promote its pending tests to "passed" and "flaky".
        var orchestrationStopwatch = Stopwatch.StartNew();
        int suiteTotalTests = 0;
        int suiteSkippedTests = 0;
        bool suiteCountsKnown = false;
        Dictionary<string, string> retriedTests = [];
        Dictionary<string, string> flakyTestsByUid = [];
        int finalFailedTestResults = 0;
        int retriedExecutions = 0;
        bool sawSkippedResults = false;
        Dictionary<string, string> pendingRetriedTests = [];

        TimeSpan? retryDelay = GetRetryDelay();

        while (attemptCount < userMaxRetryCount + 1)
        {
            attemptCount++;

            // Each attempt is a child span of the orchestrator, so a trace shows exactly how many attempts a run
            // needed and how long each of them took.
            using IPlatformActivity? attemptActivity = otelService?.StartActivity(
                "RetryAttempt",
                tags:
                [
                    new(TestingPlatformSemanticConventions.Attributes.TestCaseRetryAttempt, attemptCount),
                    new("test.retry.max_attempts", userMaxRetryCount + 1),
                    new("test.retry.scheduled_count", lastListOfFailedId?.Length ?? 0),
                ]);

            if (attemptCount > 1)
            {
                retryCounter?.Add(
                    lastListOfFailedId?.Length ?? 0,
                    [new(TestingPlatformSemanticConventions.Attributes.TestCaseRetryAttempt, attemptCount)]);
            }

            if (attemptCount > 1 && retryDelay is { } delay)
            {
                await WaitBeforeNextAttemptAsync(outputDevice, delay, attemptCount, userMaxRetryCount, cancellationToken).ConfigureAwait(false);
            }

            currentTryResultFolder = Path.Combine(retryRootFolder, attemptCount.ToString(CultureInfo.InvariantCulture));
            fileSystem.CreateDirectory(currentTryResultFolder);

            // Prepare the pipe server that collects the child process's failed test UIDs.
            using RetryFailedTestsPipeServer retryFailedTestsPipeServer = new(_serviceProvider, lastListOfFailedId ?? [], logger);

            RetryTestHostRunner.AttemptResult attemptResult = await ExecuteAttemptAsync(
                fileSystem,
                logger,
                outputDevice,
                executableInfo,
                executableArguments,
                originalExecutableArguments,
                indexToCleanup,
                currentTryResultFolder,
                retryRootFolder,
                retryFailedTestsPipeServer,
                lastListOfFailedId,
                attemptCount,
                userMaxRetryCount,
                cancellationToken).ConfigureAwait(false);

            CollectRecoveredArtifacts(
                fileSystem,
                attemptResult.RecoveredArtifactManifestPath,
                retryFailedTestsPipeServer.Artifacts,
                logger);

            attemptArtifacts.AddRange(RetryArtifactProcessor.SnapshotAttemptArtifacts(
                fileSystem,
                retryFailedTestsPipeServer.Artifacts,
                attemptCount,
                currentTryResultFolder,
                retryRootFolder));

            if (attemptResult.ExitedBeforeConnect)
            {
                exitCodes.Add((int)ExitCode.GenericFailure);
                retryInterrupted = true;
                break;
            }

            exitCodes.Add(attemptResult.ExitCode);

            int failedThisAttempt = retryFailedTestsPipeServer.FailedTests.Count;
            sawSkippedResults |= retryFailedTestsPipeServer.SkippedTests > 0;
            if (attemptCount == 1)
            {
                // The first attempt runs the full suite, so its counts are the suite's. Skipped tests are tracked
                // separately because a retry attempt re-runs only the failed set, so no later attempt can observe
                // them — and because "total" must include them to line up with the platform run summary.
                //
                // An attempt that dies before its session finishes never reports counts, leaving them at zero. That
                // is recorded rather than presented as a suite of zero tests, which would print a total smaller
                // than the failed count.
                suiteCountsKnown = retryFailedTestsPipeServer.CountsReported;
                suiteTotalTests = retryFailedTestsPipeServer.TotalTestRan + retryFailedTestsPipeServer.SkippedTests;
                suiteSkippedTests = retryFailedTestsPipeServer.SkippedTests;
            }

            // Results this attempt actually observed. Reporting counts is not the same as having run something: an
            // attempt whose filter matched nothing reports a well-formed set of zeros, which must not be mistaken
            // for "nothing is failing any more".
            int observedResults = retryFailedTestsPipeServer.TotalTestRan + retryFailedTestsPipeServer.SkippedTests;
            bool attemptObservedResults = retryFailedTestsPipeServer.CountsReported && observedResults > 0;

            if (pendingRetriedTests.Count > 0)
            {
                if (attemptObservedResults)
                {
                    // Scheduling a retry is not enough to count it: the child can die before the selected tests
                    // produce results. Commit the pending retry only after the following attempt reports counts.
                    foreach (KeyValuePair<string, string> retriedTest in pendingRetriedTests)
                    {
                        retriedTests[retriedTest.Key] = retriedTest.Value;
                    }

                    retriedExecutions += observedResults;
                }

                pendingRetriedTests.Clear();
            }

            // Whatever this attempt recovered is final: a recovered test is not carried into the next attempt's
            // failed set, so it is never re-run and cannot regress.
            foreach (string recoveredUid in retryFailedTestsPipeServer.RecoveredTests)
            {
                if (retriedTests.TryGetValue(recoveredUid, out string? displayName))
                {
                    flakyTestsByUid[recoveredUid] = displayName;
                }
            }

            // A retried test that came back skipped also stops being retried, but it never passed, so it is not
            // reported as flaky above. Its outcome for the run therefore moved from failed to skipped while the
            // suite's skipped count — captured on the first attempt, where it was still failing — does not follow,
            // so it lands in the derived succeeded count instead. Correcting that needs the first attempt's
            // per-test skipped breakdown, because a folded data-driven test can contribute both failing and
            // skipped results under one uid and would otherwise be double-counted. Left as a known imprecision:
            // the total and failed counts stay correct and the block always adds up, which a naive adjustment
            // did not.

            // The run's failing count always reflects the most recent attempt that actually ran something, which
            // re-ran every test still failing. Counted per result so it stays in the same unit as the total. An
            // attempt that died before its session finished, or that ran nothing because its filter matched no
            // test, must not silently reset this to zero: that would render a red verdict above "failed: 0" and
            // derive the still-failing tests as succeeded.
            if (attemptObservedResults)
            {
                finalFailedTestResults = retryFailedTestsPipeServer.FailedTestResults;
            }

            if (attemptResult.ExitCode != (int)ExitCode.Success)
            {
                if (attemptResult.ExitCode != (int)ExitCode.AtLeastOneTestFailed)
                {
                    await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TestSuiteFailedWithWrongExitCode, attemptResult.ExitCode)), cancellationToken).ConfigureAwait(false);
                    retryInterrupted = true;
                    break;
                }

                // Check thresholds only on the first attempt (computed against the full suite).
                if (attemptCount == 1 && await RetryThresholdPolicy.EvaluateAsync(_commandLineOptions, this, outputDevice, retryFailedTestsPipeServer, cancellationToken).ConfigureAwait(false))
                {
                    thresholdPolicyKickedIn = true;
                    break;
                }

                // Only announce an attempt as "retrying" when another attempt will actually follow; the final
                // failed attempt is reported by the summary verdict instead. Amber (not red) keeps mid-run
                // failures visually "expected", reserving red for the give-up summary.
                bool willRetry = attemptCount < userMaxRetryCount + 1;
                if (willRetry)
                {
                    await outputDevice.DisplayAsync(
                        this,
                        new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryAttemptFailedWillRetry, attemptCount, userMaxRetryCount + 1, failedThisAttempt))
                        {
                            ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow },
                        },
                        cancellationToken).ConfigureAwait(false);

                    // Keep the scheduled retry contribution pending. The next attempt may crash before producing
                    // test results, and such a launch must not be reported as an observed retry.
                    pendingRetriedTests.Clear();
                    foreach (KeyValuePair<string, string> failedTest in retryFailedTestsPipeServer.FailedTests)
                    {
                        pendingRetriedTests[failedTest.Key] = failedTest.Value;
                    }
                }

                lastListOfFailedId = [.. retryFailedTestsPipeServer.FailedTests.Keys];
            }
            else
            {
                break;
            }
        }

        orchestrationStopwatch.Stop();

        await ReportSummaryAsync(
            outputDevice,
            exitCodes,
            attemptCount,
            userMaxRetryCount,
            suiteCountsKnown,
            suiteTotalTests,
            suiteSkippedTests,
            finalFailedTestResults,
            retriedTests.Count,
            retriedExecutions,
            flakyTestsByUid.Values,
            (thresholdPolicyKickedIn || retryInterrupted) && attemptCount < userMaxRetryCount + 1,
            orchestrationStopwatch.Elapsed,
            cancellationToken).ConfigureAwait(false);

        await ProcessArtifactsAsync(
            outputDevice,
            fileSystem,
            logger,
            attemptArtifacts,
            attemptCount,
            suiteCountsKnown,
            suiteTotalTests,
            suiteSkippedTests,
            finalFailedTestResults,
            sawSkippedResults,
            orchestrationStopwatch.Elapsed, exitCodes[^1],
            currentTryResultFolder,
            resultDirectory,
            cancellationToken).ConfigureAwait(false);

        return exitCodes[^1];
    }
}
