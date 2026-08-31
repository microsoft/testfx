// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled)
            {
                return;
            }

            SummarySnapshot snapshot = BuildSnapshot();
            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown assembly name";
            int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
            CiCoverageSummaryData coverage = CiCoverageSummary.Create(_testCoverageResult, testSessionContext.SessionUid);
            CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext, coverage);
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                    resultsDirectory,
                    GitHubActionsSummaryArtifactPostProcessor.Provider,
                    GitHubActionsSummaryArtifactPostProcessor.ProviderSlug,
                    module).ConfigureAwait(false);
                await _messageBus.PublishAsync(
                    this,
                    new SessionFileArtifact(
                        testSessionContext.SessionUid,
                        new FileInfo(fragmentPath),
                        GitHubActionsResources.DisplayName,
                        GitHubActionsResources.Description,
                        GitHubActionsSummaryArtifactPostProcessor.FragmentArtifactKind)).ConfigureAwait(false);
                return;
            }

            await _historyService.WriteAsync([module], testSessionContext.CancellationToken).ConfigureAwait(false);
            if (!_isSummaryEnabled)
            {
                return;
            }

            string? path = _environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
            if (RoslynString.IsNullOrWhiteSpace(path))
            {
                // Outside a GitHub Actions step (or when summaries are unsupported) there is nowhere to
                // write. Stay quiet apart from a low-noise trace so local/dev runs don't get a warning.
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"'{StepSummaryEnvironmentVariable}' is not set; skipping job summary.");
                }

                return;
            }

            if (_writeOnFailureOnly
                && !snapshot.Records.Any(static record => record.Kind == TerminalKind.Failed)
                && !GitHubActionsExitCode.IndicatesFailure(exitCode))
            {
                return;
            }

            // The 1 MiB cap applies to the whole GITHUB_STEP_SUMMARY file, which every test project in the job
            // appends to, so this reporter degrades in stages as the shared file fills up:
            //
            //   below 40%  full section, failures expanded into collapsible diagnostics
            //   40% - 60%  full section, but failures listed as name and duration only
            //   above 60%  one-line verdict for the whole project
            //
            // Shedding the diagnostics first is what keeps the report useful for longest: the list of which tests
            // failed is a line per failure, while the diagnostics behind it are kilobytes.
            //
            // Both the decision and the write happen inside the writer's lock. Deciding beforehand would let every
            // project that finishes at the same moment observe the same file length and each render a full
            // section, so the absolute cap would admit the first few and refuse the rest outright — turning a
            // report where every project degrades gracefully into one where the first get everything and the rest
            // get nothing.
            var writer = new StepSummaryWriter(_fileSystem, path!, _logger, StepSummaryMaxWriteAttempts, StepSummaryRetryDelay);
            if (!await TryAppendRenderedSummaryAsync(writer, snapshot.Records, assemblyName, exitCode, coverage, testSessionContext).ConfigureAwait(false))
            {
                await ReportSectionDroppedAsync(writer, testSessionContext).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    /// <returns>
    /// <see langword="false"/> only when the writer refused the content because it would have taken the file past
    /// GitHub's cap. A write that failed for any other reason has already been reported and returns
    /// <see langword="true"/>, so the caller does not warn about it twice.
    /// </returns>
    private async Task<bool> TryAppendRenderedSummaryAsync(
        StepSummaryWriter writer,
        IReadOnlyList<TestRecord> snapshot,
        string assemblyName,
        int exitCode,
        CiCoverageSummaryData coverage,
        ITestSessionContext testSessionContext)
    {
        try
        {
            return await writer.AppendRenderedStepSummarySectionAsync(
                currentLength =>
                {
                    var budget = SummaryBudget.ForProject(currentLength);
                    bool condense = budget.Stage is SummaryStage.Condensed or SummaryStage.Unlisted;
                    string markdown = condense
                        ? BuildMinimalMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, exitCode)
                        : BuildMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, exitCode, coverage, _sections, _includeFailureDetails, budget);

                    // The note is only warranted once whole project sections start disappearing. Dropping the
                    // expanded diagnostics leaves every project and every failing test still named, which is a
                    // shortened report rather than an incomplete one, and does not need a warning at the top.
                    return (markdown, condense);
                },

                // The project count the note quotes is read from the summary file when the note is written, so it
                // is passed as a factory rather than a string: only the writer holds the file exclusively, and
                // only it can count without racing a sibling project.
                BuildTruncationNotice,
                testSessionContext.CancellationToken,
                GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ReportWriteFailureAsync(writer.Path, ex, testSessionContext).ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    /// Warns that this project's section did not fit in the shared summary, and makes sure the note explaining
    /// that the report is incomplete is present.
    /// </summary>
    private async Task ReportSectionDroppedAsync(StepSummaryWriter writer, ITestSessionContext testSessionContext)
    {
        string overflowWarning = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.StepSummaryLimitExceededWarning,
            (writer.GetSummaryLength() ?? 0).ToString(CultureInfo.InvariantCulture),
            GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture));

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(overflowWarning);
        }

        await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(overflowWarning), testSessionContext.CancellationToken).ConfigureAwait(false);

        // This project's section is dropped entirely, which is exactly what the note at the top of the summary
        // describes, so make sure it is there even if this project was not condensed. The note is a few hundred
        // bytes and dropping the section frees far more room than it takes; if even that does not fit, the writer
        // refuses it and the summary is genuinely full, where silence is what keeps the rest rendered.
        await TryAppendNoticeOnlyAsync(writer, testSessionContext).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the top-of-file note on its own, for when this project's section was dropped entirely.
    /// </summary>
    private async Task TryAppendNoticeOnlyAsync(StepSummaryWriter writer, ITestSessionContext testSessionContext)
    {
        try
        {
            await writer.AppendStepSummaryWithLeadingNoticeAsync(
                string.Empty,
                BuildTruncationNotice,
                testSessionContext.CancellationToken,
                GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ReportWriteFailureAsync(writer.Path, ex, testSessionContext).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Surfaces a failed summary write as a warning: failing to write the summary must not fail the test run.
    /// </summary>
    private async Task ReportWriteFailureAsync(string path, Exception ex, ITestSessionContext testSessionContext)
    {
        string warning = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.StepSummaryWriteFailedWarning, path, ex.Message);
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(warning);
        }

        await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
    }
}
