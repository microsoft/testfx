// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
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
    /// <remarks>
    /// <paramref name="zeroTestsPolicy"/> controls whether an all-skipped run is treated as a failed "zero tests"
    /// run. Under <see cref="ZeroTestsPolicy.AllowSkipped"/> (the default) only a run that discovered no test at all
    /// fails as zero tests; under <see cref="ZeroTestsPolicy.Strict"/> skipped tests don't count as run, so an
    /// all-skipped run also fails. A run where no test was found is always treated as failed regardless of policy.
    /// </remarks>
    internal static bool IsRunFailed(int totalTests, int failedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests, ZeroTestsPolicy zeroTestsPolicy = ZeroTestsPolicy.AllowSkipped)
    {
        bool notEnoughTests = totalTests < minimumExpectedTests;
        bool noTestsWereFound = totalTests == 0;
        bool allTestsWereSkipped = totalTests > 0 && totalTests == skippedTests;
        bool ranZeroTests = noTestsWereFound || (allTestsWereSkipped && zeroTestsPolicy == ZeroTestsPolicy.Strict);
        bool anyTestFailed = failedTests > 0;

        return anyTestFailed || notEnoughTests || ranZeroTests || wasCancelled;
    }

    /// <summary>
    /// Computes the verdict string for the test run.
    /// </summary>
    /// <remarks>
    /// The two orchestrator-only flags surface non-test failures that must not be masked by the test-count wording:
    /// <list type="bullet">
    /// <item><paramref name="hasHandshakeFailures"/>: at least one assembly failed to hand-shake. This escalates to
    /// "Failed!" ahead of the "Zero tests ran" branch (a handshake failure is not a benign empty run).</item>
    /// <item><paramref name="hasFailedAssemblies"/>: at least one assembly process ended unsuccessfully (e.g. crashed
    /// or returned a non-zero exit code) even though its tests passed. This escalates the verdict wording to "Failed!"
    /// but only AFTER the "Zero tests ran" branch, so a project that legitimately contains zero tests (which exits
    /// non-zero by design) keeps the "Zero tests ran" wording rather than "Failed!". Note this only affects the displayed
    /// verdict text: <see cref="IsRunFailed"/> still treats a zero-test run as a failed run.</item>
    /// </list>
    /// In-process callers never have either condition and pass <see langword="false"/>, so their verdict is unchanged.
    /// <paramref name="zeroTestsPolicy"/> mirrors <see cref="IsRunFailed"/>: under
    /// <see cref="ZeroTestsPolicy.AllowSkipped"/> (the default) an all-skipped run is reported as "Passed!" instead of
    /// "Zero tests ran". Under <see cref="ZeroTestsPolicy.Strict"/> an all-skipped run is reported as "Zero tests ran".
    /// </remarks>
    internal static string GetVerdictText(int totalTests, int failedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests, bool hasHandshakeFailures = false, bool hasFailedAssemblies = false, ZeroTestsPolicy zeroTestsPolicy = ZeroTestsPolicy.AllowSkipped)
        => true switch
        {
            _ when wasCancelled => TerminalResources.Aborted,
            _ when totalTests < minimumExpectedTests => string.Format(CultureInfo.CurrentCulture, TerminalResources.MinimumExpectedTestsPolicyViolation, totalTests, minimumExpectedTests),
            _ when failedTests > 0 => $"{TerminalResources.Failed}!",
            _ when hasHandshakeFailures => $"{TerminalResources.Failed}!",
            _ when totalTests == 0 => TerminalResources.ZeroTestsRan,
            _ when totalTests == skippedTests && zeroTestsPolicy == ZeroTestsPolicy.Strict => TerminalResources.ZeroTestsRan,
            _ when hasFailedAssemblies => $"{TerminalResources.Failed}!",
            _ => $"{TerminalResources.Passed}!",
        };

    /// <summary>
    /// Formats a plain-text test run summary with verdict and counts, suitable for console output
    /// (no ANSI escape codes). Unlike <see cref="Terminal.TerminalTestReporter.AppendTestRunSummary"/>,
    /// this does not include artifacts, duration, or assembly/TFM/architecture context.
    /// </summary>
    internal static string FormatSummaryText(int totalTests, int failedTests, int passedTests, int skippedTests, bool wasCancelled, int minimumExpectedTests, ZeroTestsPolicy zeroTestsPolicy = ZeroTestsPolicy.AllowSkipped)
    {
        string verdict = GetVerdictText(totalTests, failedTests, skippedTests, wasCancelled, minimumExpectedTests, zeroTestsPolicy: zeroTestsPolicy);

        return $"""
            {TerminalResources.TestRunSummary} {verdict}
              {TerminalResources.TotalLowercase}: {totalTests}
              {TerminalResources.FailedLowercase}: {failedTests}
              {TerminalResources.SucceededLowercase}: {passedTests}
              {TerminalResources.SkippedLowercase}: {skippedTests}
            """;
    }
}
