// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

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
    internal static string GetVerdictText(int totalTests, int failedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests) =>
        wasCancelled
            ? PlatformResources.Aborted
            : totalTests < minimumExpectedTests
                ? string.Format(CultureInfo.CurrentCulture, PlatformResources.MinimumExpectedTestsPolicyViolation, totalTests, minimumExpectedTests)
                : totalTests == 0 || totalTests == skippedTests
                    ? PlatformResources.ZeroTestsRan
                    : failedTests > 0
                        ? $"{PlatformResources.Failed}!"
                        : $"{PlatformResources.Passed}!";

    /// <summary>
    /// Formats a plain-text test run summary suitable for console output (no ANSI escape codes).
    /// Produces the same logical content as <see cref="Terminal.TerminalTestReporter.AppendTestRunSummary"/>
    /// but as a simple multi-line string.
    /// </summary>
    internal static string FormatSummaryText(int totalTests, int failedTests, int passedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests)
    {
        string verdict = GetVerdictText(totalTests, failedTests, skippedTests, wasCancelled, minimumExpectedTests);

        return $"""
            {PlatformResources.TestRunSummary} {verdict}
              {PlatformResources.TotalLowercase}: {totalTests}
              {PlatformResources.FailedLowercase}: {failedTests}
              {PlatformResources.SucceededLowercase}: {passedTests}
              {PlatformResources.SkippedLowercase}: {skippedTests}
            """;
    }
}
