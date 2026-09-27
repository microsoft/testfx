// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        if (!AzureDevOpsPublishConfigurationFactory.TryCreate(_commandLineOptions, _configuration, _environment, _testApplicationModuleInfo, out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning))
        {
            await WarnAsync(warning, testSessionContext.CancellationToken).ConfigureAwait(false);
            return;
        }

        _publishConfiguration = publishConfiguration;
        _runIdCoordinator = new AzureDevOpsRunIdCoordinator(_fileSystem, _task, _clock, _environment, _logger, _options);

        try
        {
            // An orchestrator (e.g. retry) creates the run before launching any test host, because the run
            // must span every attempt. When that happened, join the existing run: this process neither
            // created it nor can tell whether it is the last one to publish into it, so it must not
            // complete it either. See AzureDevOpsTestRunOrchestratorLifetime.
            int? inheritedRunId = AzureDevOpsConstants.TryGetInheritedTestRunId(_environment, publishConfiguration.BuildId);
            _coordinatedRun = inheritedRunId is { } inheritedId
                ? AzureDevOpsRunIdCoordinator.CreateInheritedRun(inheritedId, publishConfiguration)
                : await _runIdCoordinator.AcquireRunAsync(
                    publishConfiguration,
                    cancellationToken => _client.CreateTestRunAsync(publishConfiguration, cancellationToken),
                    testSessionContext.CancellationToken).ConfigureAwait(false);
            CurrentRunId = _coordinatedRun.RunId;

            // An orchestrator also hands down where the result map lives, which is what lets a retry
            // attempt update the result an earlier attempt created rather than publish a second one for
            // the same test. Absent (a run with no orchestrator) means every result is simply created.
            if (inheritedRunId is not null
                && AzureDevOpsConstants.TryGetInheritedResultMapPath(_environment, publishConfiguration.BuildId) is { } resultMapPath)
            {
                _resultIdStore = await AzureDevOpsResultIdStore.OpenAsync(
                    _fileSystem,
                    _logger,
                    resultMapPath,
                    publishConfiguration.BuildId,
                    CurrentRunId.Value).ConfigureAwait(false);
            }

            // Results stream into the run as tests complete, but the build's Tests tab only lists the run
            // once it is finalized. Point users at the run itself so they can watch results arrive live,
            // and say when the Tests tab catches up so an empty tab mid-run is not mistaken for a failure.
            // Only the process that created the run reports it: every other process shares the same run id
            // and would just repeat the same line.
            if (_coordinatedRun.IsOwner)
            {
                await DisplayAsync(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        AzureDevOpsResources.AzureDevOpsLivePublishingRunCreated,
                        CurrentRunId.Value,
                        AzureDevOpsPublishConfigurationFactory.BuildTestRunUrl(publishConfiguration, CurrentRunId.Value)),
                    testSessionContext.CancellationToken).ConfigureAwait(false);
            }

            // Start a background loop that flushes pending results on the time-based interval even
            // when no new TestNodeUpdateMessages arrive (e.g. at the tail end of a slow test run).
            _backgroundFlushCts = new CancellationTokenSource();
            _backgroundFlushTask = Task.Run(() => BackgroundFlushLoopAsync(_backgroundFlushCts.Token));
        }
        catch (Exception ex) when (!testSessionContext.CancellationToken.IsCancellationRequested)
        {
            // Reset state before reporting so a failure to display the warning can never leave the
            // publisher half-initialized. The filter tests the caller's token rather than the exception
            // type because the HTTP client surfaces its own internal timeouts as TaskCanceledException;
            // those must be reported, not rethrown as if the user had canceled.
            _publishConfiguration = null;
            _coordinatedRun = null;
            _resultIdStore = null;
            CurrentRunId = null;
            await WarnAsync($"{AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed} {ex.Message}", testSessionContext.CancellationToken).ConfigureAwait(false);
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (_publishConfiguration is null || _coordinatedRun is null || CurrentRunId is not { } currentRunId || _runIdCoordinator is null)
        {
            return;
        }

        // Stop the background flush loop before doing the session-end forced flush so there is no
        // concurrent flush in flight when we drain the last batch.
        if (_backgroundFlushCts is not null && _backgroundFlushTask is not null)
        {
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; multi-target sync cancel is acceptable here.
            _backgroundFlushCts.Cancel();
#pragma warning restore VSTHRD103
            try
            {
                await _backgroundFlushTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Unexpected failure in the background flush loop; the loop already logs per-flush warnings.
                TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                // Cancellation — expected and fine.
            }
        }

        // If cancellation or a framework failure stopped result delivery before the final retry attempt,
        // preserve every execution already received. Publishing them independently is safer than guessing
        // an outcome that never arrived, and moving them into the normal queue also includes them in the
        // unpublished-result warning when cancellation prevents the forced flush.
        DrainIncompleteInProcessRetrySequences();

        try
        {
            await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, testSessionContext.CancellationToken).ConfigureAwait(false);
            await FlushPendingResultsAsync(force: true, testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Best-effort flush: session was canceled; finalization still runs below with a fresh token.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
        }

        // Hand the map to the next attempt. Written once, here, rather than after every batch: only
        // another process ever reads it, and no other process runs while this one does — the orchestrator
        // runs attempts one at a time. Saving per batch would rewrite the whole map on each flush, which is
        // quadratic in the number of tests and would be paid by every run, including those that never
        // retry anything. If this process dies before reaching here the map is simply absent, and the next
        // attempt publishes its own results: degraded, not lost.
        if (_resultIdStore is not null)
        {
            await _resultIdStore.SaveAsync(CancellationToken.None).ConfigureAwait(false);
        }

        // The session-end flush is the last chance to publish: nothing drains the retry queue after
        // this point, so anything still pending is permanently lost. Report it once here rather than
        // per failed batch (which the background loop would repeat every flush interval).
        // Use CancellationToken.None so the warning still surfaces when the session was canceled.
        int unpublishedResultCount = _retryResults.Count + _pendingResults.Count;
        if (unpublishedResultCount > 0)
        {
            await WarnAsync(
                string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingResultsDropped, unpublishedResultCount),
                CancellationToken.None).ConfigureAwait(false);
        }

        await UploadPendingResultAttachmentsAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await UploadPendingRunAttachmentsAsync(testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Best-effort: don't fail finalization if the session was canceled mid-upload.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Defensive: individual upload failures are already handled (and counted) per attachment,
            // so nothing is expected here. Swallow anyway because the platform does not guard lifetime
            // handlers, so throwing would fail an otherwise successful run.
            await WarnAsync($"{AzureDevOpsResources.AzureDevOpsLivePublishingRunAttachmentFailed} {ex.Message}", CancellationToken.None).ConfigureAwait(false);
        }

        // Attachment failures are swallowed per attachment so that one bad file cannot abort the drain.
        // Report them once here, otherwise coverage files and dumps go missing with no explanation.
        int failedAttachmentCount = Volatile.Read(ref _failedAttachmentCount);
        if (failedAttachmentCount > 0)
        {
            await WarnAsync(
                string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, failedAttachmentCount),
                CancellationToken.None).ConfigureAwait(false);
        }

        // Azure DevOps test runs use "Aborted" specifically for cancellation or session-level
        // infrastructure failures. Individual failing tests should still mark the run as
        // "Completed" — only treat process exit codes other than Success/AtLeastOneTestFailed as
        // an abort signal (e.g. TestSessionAborted, TestHostProcessExitedNonGracefully,
        // TestAdapterTestSessionFailure, MinimumExpectedTestsPolicyViolation, etc.).
        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        bool exitCodeIsTestResult = exitCode is (int)ExitCode.Success or (int)ExitCode.AtLeastOneTestFailed;
        string finalState = testSessionContext.CancellationToken.IsCancellationRequested
                || _testApplicationProcessExitCode.HasTestAdapterTestSessionFailure
                || !exitCodeIsTestResult
            ? AzureDevOpsLivePublishingConstants.AbortedTestRunState
            : AzureDevOpsLivePublishingConstants.CompletedTestRunState;

        try
        {
            // Use a fresh, non-canceled token so finalization (marking the run Aborted/Completed)
            // succeeds even when the test session itself has been canceled.
            using var cleanupCts = new CancellationTokenSource(_options.CoordinationFinalizeMaxWaitTime + TimeSpan.FromSeconds(60));
            await _runIdCoordinator.FinalizeRunAsync(
                _coordinatedRun,
                cancellationToken => _client.UpdateTestRunStateAsync(_publishConfiguration, currentRunId, finalState, cancellationToken),
                cleanupCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A run left in "InProgress" is not surfaced in the build's Tests tab, so the user would
            // otherwise see an empty tab with no explanation even though every result was uploaded.
            // Cancellation is handled here too: this step runs on its own cleanupCts, so an expiry (or
            // an HTTP client TaskCanceledException) is a finalization failure rather than a session
            // cancellation, and must not escape and fail an otherwise successful run.
            await WarnAsync(
                $"{AzureDevOpsResources.AzureDevOpsLivePublishingCompleteRunFailed} {ex.Message} {AzureDevOpsResources.AzureDevOpsLivePublishingRunLeftInProgress}",
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    // The extension stays enabled whenever the option is set so that a missing or invalid Azure DevOps
    // configuration can be reported to the user in OnTestSessionStartingAsync. Silently disabling the
    // extension here would leave users with no test run and no explanation for why.
    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName));

    public void Dispose()
    {
        // Dispose must be idempotent: the platform can dispose an extension more than once during
        // teardown (e.g. once when the test host tears down its services and once again during
        // process shutdown). Without this guard the Cancel below throws ObjectDisposedException,
        // which surfaces as an unhandled exception and fails the run even though every test passed.
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        // Signal the background flush loop to stop. The loop is already awaited in
        // OnTestSessionFinishingAsync; this Cancel is a safety net for cases where the
        // session lifecycle methods are not called (e.g. early disposal in tests).
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; synchronous cancel is acceptable in Dispose.
        _backgroundFlushCts?.Cancel();
#pragma warning restore VSTHRD103
        _backgroundFlushCts?.Dispose();
        _flushSemaphore.Dispose();
    }
}
