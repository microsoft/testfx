// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Shared helper for computing test run verdicts and formatting plain-text summaries.
/// Used by both <see cref="Terminal.TerminalTestReporter"/> (verdict logic) and
/// <see cref="SimplifiedConsoleOutputDeviceBase"/> (full plain-text summary).
/// </summary>
internal static class TestRunSummaryHelper
{
    /// <summary>
    /// Determines whether the test run should be considered failed based on the given outcome counters and state.
    /// </summary>
    internal static bool IsRunFailed(int totalTests, int failedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests)
    {
        bool notEnoughTests = totalTests < minimumExpectedTests;
        bool allTestsWereSkipped = totalTests == 0 || totalTests == skippedTests;
        bool anyTestFailed = failedTests > 0;

        return anyTestFailed || notEnoughTests || allTestsWereSkipped || wasCancelled;
    }

    /// <summary>
    /// Computes the verdict string for the test run.
    /// </summary>
    /// <remarks>
    /// When <paramref name="hasHandshakeFailures"/> is <see langword="true"/> (multi-assembly orchestrator only), at
    /// least one assembly failed to hand-shake. Such a failure must surface as "Failed!" and must NOT be masked by the
    /// benign "Zero tests ran" wording, even though the failing assembly contributed zero tests. In-process callers
    /// never have handshake failures and pass <see langword="false"/>, so their verdict is unchanged.
    /// </remarks>
    internal static string GetVerdictText(int totalTests, int failedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests, bool hasHandshakeFailures = false)
        => true switch
        {
            _ when wasCancelled => TerminalResources.Aborted,
            _ when totalTests < minimumExpectedTests => string.Format(CultureInfo.CurrentCulture, TerminalResources.MinimumExpectedTestsPolicyViolation, totalTests, minimumExpectedTests),
            _ when failedTests > 0 => $"{TerminalResources.Failed}!",
            _ when hasHandshakeFailures => $"{TerminalResources.Failed}!",
            _ when totalTests == 0 || totalTests == skippedTests => TerminalResources.ZeroTestsRan,
            _ => $"{TerminalResources.Passed}!",
        };

    /// <summary>
    /// Formats a plain-text test run summary with verdict and counts, suitable for console output
    /// (no ANSI escape codes). Unlike <see cref="Terminal.TerminalTestReporter.AppendTestRunSummary"/>,
    /// this does not include artifacts, duration, or assembly/TFM/architecture context.
    /// </summary>
    internal static string FormatSummaryText(int totalTests, int failedTests, int passedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests)
    {
        string verdict = GetVerdictText(totalTests, failedTests, skippedTests, wasCancelled, minimumExpectedTests);

        return $"""
            {TerminalResources.TestRunSummary} {verdict}
              {TerminalResources.TotalLowercase}: {totalTests}
              {TerminalResources.FailedLowercase}: {failedTests}
              {TerminalResources.SucceededLowercase}: {passedTests}
              {TerminalResources.SkippedLowercase}: {skippedTests}
            """;
    }
}
