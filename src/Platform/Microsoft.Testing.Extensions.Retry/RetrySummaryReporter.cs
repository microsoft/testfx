// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Policy;

/// <summary>
/// Accounting reconciled across retry attempts and used to render the single headline retry summary.
/// </summary>
internal readonly struct RetryRunSummary
{
    public required List<int> ExitCodes { get; init; }

    public required int AttemptCount { get; init; }

    public required int UserMaxRetryCount { get; init; }

    public required int SuiteTotalTests { get; init; }

    public required int FirstAttemptFailedTests { get; init; }

    public required int FinalFailedTests { get; init; }

    public required int RetriedExecutions { get; init; }

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
        int flakyTests = Math.Max(0, summary.FirstAttemptFailedTests - summary.FinalFailedTests);
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
            await outputDevice.DisplayAsync(producer, new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailed, summary.AttemptCount, totalAttempts)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed } }, cancellationToken).ConfigureAwait(false);
        }

        if (!runSucceeded && summary.FinalFailedTests > 0)
        {
            await outputDevice.DisplayAsync(producer, new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFailedLine, summary.FinalFailedTests)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkRed } }, cancellationToken).ConfigureAwait(false);
        }

        // "flaky" = failed at least once but eventually passed — the headline value of the retry feature.
        if (flakyTests > 0)
        {
            await outputDevice.DisplayAsync(producer, new FormattedTextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryFlakyLine, flakyTests)) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.DarkYellow } }, cancellationToken).ConfigureAwait(false);
        }

        string totalLine = string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryTotalLine, summary.SuiteTotalTests);
        if (summary.RetriedExecutions > 0)
        {
            totalLine += string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryRetriedSuffix, summary.RetriedExecutions);
        }

        await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(totalLine), cancellationToken).ConfigureAwait(false);
        await outputDevice.DisplayAsync(producer, new TextOutputDeviceData(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetrySummaryDurationLine, RetryOrchestratorHelper.FormatDuration(summary.Elapsed))), cancellationToken).ConfigureAwait(false);
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
