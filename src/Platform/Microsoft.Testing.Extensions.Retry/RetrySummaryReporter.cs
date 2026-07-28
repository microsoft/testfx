// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Policy;

/// <summary>
/// Accounting reconciled across retry attempts and used to render the retry summary.
/// </summary>
internal readonly struct RetryRunSummary
{
    public required List<int> ExitCodes { get; init; }

    public required int AttemptCount { get; init; }

    public required int UserMaxRetryCount { get; init; }

    public required int SuiteTotalTests { get; init; }

    public required int FinalFailedTests { get; init; }

    /// <summary>
    /// Gets the number of distinct tests that were re-run at least once.
    /// </summary>
    public required int RetriedTests { get; init; }

    /// <summary>
    /// Gets the number of extra test executions those retries cost.
    /// </summary>
    public required int RetriedExecutions { get; init; }

    /// <summary>
    /// Gets the display names of the tests that failed at least once but were not failing at the end, sorted.
    /// </summary>
    public required IReadOnlyList<string> FlakyTests { get; init; }

    public required bool ShowFlakyTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether the retry loop stopped before exhausting the configured attempts (threshold
    /// policy, or an attempt that exited with an unexpected code). The verdict then describes the attempts that
    /// actually ran rather than implying all of them were used.
    /// </summary>
    public required bool StoppedEarly { get; init; }

    public required TimeSpan Elapsed { get; init; }
}

/// <summary>
/// Renders the retry summary output and moves the last attempt's artifacts to the final result directory.
/// </summary>
internal static class RetrySummaryReporter
{
    public static async Task ReportSummaryAsync(
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        RetryRunSummary summary,
        CancellationToken cancellationToken)
    {
        bool runSucceeded = summary.ExitCodes[^1] == (int)ExitCode.Success;
        int totalAttempts = summary.UserMaxRetryCount + 1;

        // Headline verdict, colored by the FINAL outcome so a run rescued by retry reads as green.
        if (runSucceeded)
        {
            string header = summary.AttemptCount == 1
                ? ExtensionResources.RetrySummaryPassedNoRetry
                : string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryPassed, summary.AttemptCount, totalAttempts);
            await outputDevice.DisplayAsync(producer, new FormattedTextOutputDeviceData(header) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGreen } }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // When retrying stopped early the "N of M attempts" phrasing would suggest the remaining attempts were
            // used up and failed, so a dedicated message states the run simply stopped after N attempts.
            string header = summary.StoppedEarly
                ? string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailedStoppedEarly, summary.AttemptCount)
                : string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailed, summary.AttemptCount, totalAttempts);
            await outputDevice.DisplayAsync(producer, new FormattedTextOutputDeviceData(header) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed } }, cancellationToken).ConfigureAwait(false);
        }

        // Counts, ordered and labelled to mirror the platform run summary so the two blocks read the same way.
        await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryTotalLine, summary.SuiteTotalTests)), cancellationToken).ConfigureAwait(false);

        await outputDevice.DisplayAsync(
            producer,
            new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailedLine, summary.FinalFailedTests))
            {
                ForegroundColor = summary.FinalFailedTests > 0 ? new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed } : null,
            },
            cancellationToken).ConfigureAwait(false);

        // "flaky" = failed at least once but eventually passed — the headline value of the retry feature.
        if (summary.FlakyTests.Count > 0 && summary.ShowFlakyTests)
        {
            await outputDevice.DisplayAsync(producer, new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFlakyLine, summary.FlakyTests.Count)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow } }, cancellationToken).ConfigureAwait(false);
        }

        if (summary.RetriedTests > 0)
        {
            await outputDevice.DisplayAsync(
                producer,
                new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryRetriedLine, summary.RetriedTests, summary.RetriedExecutions))
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGray },
                },
                cancellationToken).ConfigureAwait(false);
        }

        await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryDurationLine, RetryOrchestratorHelper.FormatDuration(summary.Elapsed))), cancellationToken).ConfigureAwait(false);

        // Name the flaky tests. Tests that were retried but never recovered are deliberately not listed: they are
        // already reported as failures with their full error output by the last attempt's own summary.
        if (summary.FlakyTests.Count > 0 && summary.ShowFlakyTests)
        {
            await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(string.Empty), cancellationToken).ConfigureAwait(false);
            await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(ExtensionResources.RetrySummaryFlakyTestsHeader), cancellationToken).ConfigureAwait(false);
            foreach (string flakyTest in summary.FlakyTests)
            {
                await outputDevice.DisplayAsync(
                    producer,
                    new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFlakyTestLine, flakyTest))
                    {
                        ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow },
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static async Task MoveArtifactsAsync(
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        IFileSystem fileSystem,
        ILogger logger,
        string currentTryResultFolder,
        string resultDirectory,
        CancellationToken cancellationToken)
    {
        string[] filesToMove = fileSystem.GetFiles(currentTryResultFolder, "*.*", SearchOption.AllDirectories);
        if (filesToMove.Length == 0)
        {
            return;
        }

        // Move last attempt assets. The per-file detail is demoted to a debug log; the user-facing output is a
        // single collapsed line so a large artifact set no longer spams the console.
        foreach (string file in filesToMove)
        {
            string finalFileLocation = file.Replace(currentTryResultFolder, resultDirectory);

            // Create the directory if missing
            fileSystem.CreateDirectory(Path.GetDirectoryName(finalFileLocation)!);

            logger.LogDebug($"Moving file '{file}' to '{finalFileLocation}'");
#if NETCOREAPP
            fileSystem.MoveFile(file, finalFileLocation, overwrite: true);
#else
            fileSystem.CopyFile(file, finalFileLocation, overwrite: true);
            fileSystem.DeleteFile(file);
#endif
        }

        await outputDevice.DisplayAsync(
            producer,
            new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryArtifactsMoved, filesToMove.Length, resultDirectory))
            {
                ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGray },
            },
            cancellationToken).ConfigureAwait(false);
    }
}
