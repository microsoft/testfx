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

    /// <summary>
    /// Gets a value indicating whether the first attempt reported its outcome counts. An attempt that dies before
    /// its test session finishes never does, so the totals would otherwise read as a run of zero tests — printing a
    /// total smaller than the failed count.
    /// </summary>
    public required bool SuiteCountsKnown { get; init; }

    public required int SuiteTotalTests { get; init; }

    /// <summary>
    /// Gets the number of skipped tests in the suite. Retries only re-run failed tests, so a skipped test keeps that
    /// outcome for the whole run and the first attempt's count is authoritative.
    /// </summary>
    public required int SuiteSkippedTests { get; init; }

    /// <summary>
    /// Gets the number of failing test results after the final attempt. Counted per result, like
    /// <see cref="SuiteTotalTests"/> and <see cref="SuiteSkippedTests"/>, so the block's arithmetic holds for a
    /// folded data-driven test whose rows share one test node uid.
    /// </summary>
    public required int FinalFailedTests { get; init; }

    /// <summary>
    /// Gets the number of distinct tests that were scheduled for retry and whose retry attempt reported at least
    /// one result.
    /// </summary>
    public required int RetriedTests { get; init; }

    /// <summary>
    /// Gets the number of extra test results those retries produced.
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
/// Renders the retry summary output and publishes the final logical-run artifacts to the result directory.
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

        // Counts, ordered and labelled to mirror the platform run summary. Every value here is a per-result count,
        // the same unit the platform block above uses, so the two agree even for a folded data-driven test whose
        // rows share a single test node uid.
        //
        // "succeeded" is derived rather than reported: no single attempt knows it. The first attempt runs the whole
        // suite and later attempts only re-run failures, so total and skipped are fixed for the run, and whatever
        // is neither still-failing nor skipped ended up passing.
        //
        // When the first attempt never reported its counts (it died before its session finished) the totals are
        // unknown, and printing zeros would claim a run of no tests while the failed count says otherwise. Only the
        // count that is actually known — the failures observed so far — is shown in that case.
        if (summary.SuiteCountsKnown)
        {
            int succeeded = Math.Max(0, summary.SuiteTotalTests - summary.FinalFailedTests - summary.SuiteSkippedTests);

            await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryTotalLine, summary.SuiteTotalTests)), cancellationToken).ConfigureAwait(false);

            await outputDevice.DisplayAsync(
                producer,
                new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailedLine, summary.FinalFailedTests))
                {
                    ForegroundColor = summary.FinalFailedTests > 0 ? new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed } : null,
                },
                cancellationToken).ConfigureAwait(false);

            await outputDevice.DisplayAsync(
                producer,
                new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummarySucceededLine, succeeded))
                {
                    ForegroundColor = summary.FinalFailedTests == 0 && succeeded > 0 ? new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGreen } : null,
                },
                cancellationToken).ConfigureAwait(false);

            await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummarySkippedLine, summary.SuiteSkippedTests)), cancellationToken).ConfigureAwait(false);
        }
        else if (summary.FinalFailedTests > 0)
        {
            await outputDevice.DisplayAsync(
                producer,
                new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailedLine, summary.FinalFailedTests))
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed },
                },
                cancellationToken).ConfigureAwait(false);
        }

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
                    new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFlakyTestLine, MakeControlCharactersVisible(flakyTest)))
                    {
                        ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow },
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Replaces control characters with their Unicode "control picture" equivalents, so a display name carrying a
    /// newline or an ESC sequence cannot forge extra summary lines or steer the terminal. Test display names come
    /// from user code (including data-driven arguments), so they are untrusted here.
    /// </summary>
    /// <remarks>
    /// Mirrors the policy of the terminal reporter's own <c>MakeControlCharactersVisible</c>, which is private to a
    /// type embedded in Microsoft.Testing.Platform and so cannot be shared with this extension.
    /// </remarks>
    private static string MakeControlCharactersVisible(string text)
    {
        StringBuilder? builder = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsControl(c))
            {
                builder ??= new StringBuilder(text.Length).Append(text, 0, i);
                builder.Append((char)(0x2400 + c));
            }
            else
            {
                builder?.Append(c);
            }
        }

        return builder?.ToString() ?? text;
    }

    public static Task MoveArtifactsAsync(
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        IFileSystem fileSystem,
        ILogger logger,
        string currentTryResultFolder,
        string resultDirectory,
        CancellationToken cancellationToken)
        => MoveArtifactsAsync(
            producer,
            outputDevice,
            fileSystem,
            logger,
            currentTryResultFolder,
            resultDirectory,
            new Dictionary<string, string>(),
            cancellationToken);

    public static async Task MoveArtifactsAsync(
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        IFileSystem fileSystem,
        ILogger logger,
        string currentTryResultFolder,
        string resultDirectory,
        IReadOnlyDictionary<string, string> replacements,
        CancellationToken cancellationToken)
    {
        string[] filesToMove = fileSystem.GetFiles(currentTryResultFolder, "*.*", SearchOption.AllDirectories);
        if (filesToMove.Length == 0)
        {
            return;
        }

        // Preserve the final attempt under Retries like every earlier attempt, and publish copies at the top level.
        // A retry-aware post-processor can replace a final-attempt report with one describing the logical run.
        foreach (string file in filesToMove)
        {
            string relativePath = file.Substring(currentTryResultFolder.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string finalFileLocation = Path.Combine(
                resultDirectory,
                relativePath);
            string normalizedFile = Path.GetFullPath(file);
            string sourceFile = replacements.TryGetValue(normalizedFile, out string? replacement) ? replacement : file;

            // Create the directory if missing
            fileSystem.CreateDirectory(Path.GetDirectoryName(finalFileLocation)!);

            logger.LogDebug($"Copying file '{sourceFile}' to '{finalFileLocation}'");
            fileSystem.CopyFile(sourceFile, finalFileLocation, overwrite: true);
        }

        await outputDevice.DisplayAsync(
            producer,
            new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryArtifactsPublished, filesToMove.Length, resultDirectory))
            {
                ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkGray },
            },
            cancellationToken).ConfigureAwait(false);
    }
}
