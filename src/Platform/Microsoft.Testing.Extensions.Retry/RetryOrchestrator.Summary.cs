// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed partial class RetryOrchestrator
{
    private async Task ReportSummaryAsync(
        IOutputDevice outputDevice,
        List<int> exitCodes,
        int attemptCount,
        int userMaxRetryCount,
        bool suiteCountsKnown,
        int suiteTotalTests,
        int suiteSkippedTests,
        int finalFailedTestResults,
        int retriedTests,
        int retriedExecutions,
        IEnumerable<string> flakyTestNames,
        bool stoppedEarly,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        // The summary is reported on every path, including early termination, because the final outcome and flaky
        // tests remain useful when the threshold policy disables retries or an attempt exits unexpectedly.
        List<string> flakyTests = [.. flakyTestNames];
        flakyTests.Sort(StringComparer.Ordinal);

        await RetrySummaryReporter.ReportSummaryAsync(
            this,
            outputDevice,
            new RetryRunSummary
            {
                ExitCodes = exitCodes,
                AttemptCount = attemptCount,
                UserMaxRetryCount = userMaxRetryCount,
                SuiteCountsKnown = suiteCountsKnown,
                SuiteTotalTests = suiteTotalTests,
                SuiteSkippedTests = suiteSkippedTests,
                FinalFailedTests = finalFailedTestResults,
                RetriedTests = retriedTests,
                RetriedExecutions = retriedExecutions,
                FlakyTests = flakyTests,
                ShowFlakyTests = FlakyTestsReportingOptions.IsEnabled(_commandLineOptions),
                StoppedEarly = stoppedEarly,
                Elapsed = elapsed,
            },
            cancellationToken).ConfigureAwait(false);
    }
}
