// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
    private async Task UploadPendingRunAttachmentsAsync(CancellationToken cancellationToken)
    {
        if (_publishConfiguration is null || CurrentRunId is null)
        {
            return;
        }

        while (_pendingRunAttachments.TryDequeue(out AzureDevOpsTestResultAttachment? attachment))
        {
            try
            {
                await _client.UploadTestRunAttachmentAsync(_publishConfiguration, CurrentRunId.Value, attachment, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation aborts the drain; the attachment is lost. The only caller is session
                // finishing, where cancellation means the test host is tearing down anyway.
                throw;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedAttachmentCount);
                TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingRunAttachmentFailed} {ex.Message}");
            }
        }
    }

    private async Task UploadResultAttachmentsAsync(int testCaseResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> attachments, CancellationToken cancellationToken)
    {
        if (_publishConfiguration is null || CurrentRunId is null || attachments.Count == 0)
        {
            return;
        }

        foreach (AzureDevOpsTestResultAttachment attachment in attachments)
        {
            try
            {
                await _client.UploadTestResultAttachmentAsync(_publishConfiguration, CurrentRunId.Value, testCaseResultId, attachment, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedAttachmentCount);
                TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingResultAttachmentFailed} {ex.Message}");
            }
        }
    }

    private async Task BackgroundFlushLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.FlushInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await FlushPendingResultsAsync(force: false, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            }
        }
    }

    private async Task FlushPendingResultsAsync(bool force, CancellationToken cancellationToken)
    {
        if (_publishConfiguration is null || CurrentRunId is null)
        {
            return;
        }

        await _flushSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // _publishConfiguration and CurrentRunId are validated once above; the batch loop exits
            // via ShouldFlushUnsafe or when there is no more work to publish.
            while (true)
            {
                if (!ShouldFlushUnsafe(force))
                {
                    return;
                }

                List<AzureDevOpsTestCaseResultWithAttachments> batch = [];
                while (batch.Count < _options.BatchSize && _retryResults.Count > 0)
                {
                    batch.Add(_retryResults.Pop());
                }

                while (batch.Count < _options.BatchSize && _pendingResults.TryDequeue(out AzureDevOpsTestCaseResultWithAttachments? result))
                {
                    batch.Add(result);
                }

                if (batch.Count == 0)
                {
                    return;
                }

                // A test this build already published is a further attempt at it, not a new test, so it
                // updates that result instead of adding a second one for the same test. Only an
                // orchestrated run has a store, so an ordinary run takes the create path for everything.
                List<AzureDevOpsTestCaseResultWithAttachments> creations = [];
                List<(AzureDevOpsPublishedResult Published, AzureDevOpsTestCaseResultWithAttachments Attempt)> updates = [];
                foreach (AzureDevOpsTestCaseResultWithAttachments item in batch)
                {
                    if (_resultIdStore?.TryGet(item.Result) is { } published)
                    {
                        updates.Add((published, item));
                    }
                    else
                    {
                        creations.Add(item);
                    }
                }

                if (creations.Count > 0 && !await TryCreateResultsAsync(creations, cancellationToken).ConfigureAwait(false))
                {
                    // Nothing in this batch reached Azure DevOps: the creations failed, and the updates were
                    // not attempted. Requeue the batch as it was, so the next flush retries it in order.
                    RequeueUnsafe(batch);
                    return;
                }

                if (updates.Count > 0 && !await TryUpdateResultsAsync(updates, cancellationToken).ConfigureAwait(false))
                {
                    RequeueUnsafe([.. updates.Select(update => update.Attempt)]);
                    return;
                }
            }
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Publishes results Azure DevOps has not seen in this build yet, recording the ids it assigns them so
    /// that a later attempt can update them.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the batch did not reach Azure DevOps and the caller should requeue it.
    /// </returns>
    private async Task<bool> TryCreateResultsAsync(List<AzureDevOpsTestCaseResultWithAttachments> batch, CancellationToken cancellationToken)
    {
        IReadOnlyList<int>? resultIds;
        try
        {
            if (_coordinatedRun is not null && _runIdCoordinator is not null)
            {
                await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
            }

            var resultsOnly = new AzureDevOpsTestCaseResult[batch.Count];
            for (int i = 0; i < batch.Count; i++)
            {
                resultsOnly[i] = batch[i].Result;
            }

            resultIds = await _client.PublishTestResultsAsync(_publishConfiguration!, CurrentRunId!.Value, resultsOnly, cancellationToken).ConfigureAwait(false);
            _lastFlushTime = _clock.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Transport/HTTP failure — AzDO may not have accepted the batch, so it's safe to requeue and
            // retry. Reset the interval countdown so a transient failure does not cause a tight retry loop.
            _lastFlushTime = _clock.UtcNow;
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            return false;
        }

        // POST succeeded. If we couldn't parse the response we cannot upload result-level attachments for
        // this batch, nor remember the ids for a later attempt, but we MUST NOT republish (that would
        // create duplicate result rows in AzDO).
        if (resultIds is null)
        {
            if (BatchHasAttachments(batch))
            {
                Interlocked.Add(ref _failedAttachmentCount, CountAttachments(batch));
                TryLogWarning(AzureDevOpsResources.AzureDevOpsLivePublishingResultIdParseFailedWarning);
            }

            return true;
        }

        for (int i = 0; i < batch.Count; i++)
        {
            _resultIdStore?.RecordCreated(batch[i].Result, resultIds[i]);
            await UploadAttachmentsForResultAsync(resultIds[i], batch[i].Attachments, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Folds further attempts into the results that already represent those tests, so that one test that
    /// ran several times stays one result with the attempts recorded underneath it.
    /// </summary>
    /// <remarks>
    /// The parent takes the latest attempt's outcome and detail, because that is the outcome the test
    /// ultimately had and the one the pipeline's own exit code already reflects — a test rescued by a retry
    /// should stop being reported as failed. Azure DevOps computes run metrics from the parent, so this is
    /// also what decides whether the run is counted as passing. The earlier attempts are not lost: they
    /// stay visible as sub-results, which is what makes the flakiness apparent.
    /// </remarks>
    private async Task<bool> TryUpdateResultsAsync(
        List<(AzureDevOpsPublishedResult Published, AzureDevOpsTestCaseResultWithAttachments Attempt)> updates,
        CancellationToken cancellationToken)
    {
        var parents = new AzureDevOpsTestCaseResult[updates.Count];
        var attemptHistories = new IReadOnlyList<AzureDevOpsTestSubResult>[updates.Count];
        for (int i = 0; i < updates.Count; i++)
        {
            // Built but not recorded yet: the map may only advance once Azure DevOps has accepted the
            // update, otherwise retrying a failed update would list the same execution twice.
            attemptHistories[i] = AzureDevOpsResultIdStore.BuildNextAttempts(updates[i].Published, updates[i].Attempt.Result);
            parents[i] = updates[i].Attempt.Result with
            {
                Id = updates[i].Published.Id,
                ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
                SubResults = attemptHistories[i],
            };
        }

        try
        {
            if (_coordinatedRun is not null && _runIdCoordinator is not null)
            {
                await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
            }

            await _client.UpdateTestResultsAsync(_publishConfiguration!, CurrentRunId!.Value, parents, cancellationToken).ConfigureAwait(false);
            _lastFlushTime = _clock.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastFlushTime = _clock.UtcNow;
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            return false;
        }

        for (int i = 0; i < updates.Count; i++)
        {
            _resultIdStore!.RecordAttempts(updates[i].Published, attemptHistories[i]);
            await UploadAttachmentsForResultAsync(
                updates[i].Published.Id,
                RenameForAttempt(updates[i].Attempt.Attachments, attemptHistories[i].Count),
                cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Qualifies attachment names with the attempt that produced them.
    /// </summary>
    /// <remarks>
    /// Every attempt uploads against the same parent result, where Azure DevOps accumulates attachments
    /// rather than replacing them, so two attempts would otherwise both contribute a <c>stdout.log</c> with
    /// no way to tell them apart.
    /// </remarks>
    private static IReadOnlyList<AzureDevOpsTestResultAttachment> RenameForAttempt(IReadOnlyList<AzureDevOpsTestResultAttachment> attachments, int attemptNumber)
    {
        if (attachments.Count == 0)
        {
            return attachments;
        }

        var renamed = new AzureDevOpsTestResultAttachment[attachments.Count];
        for (int i = 0; i < attachments.Count; i++)
        {
            string fileName = attachments[i].FileName;
            string extension = Path.GetExtension(fileName);
            renamed[i] = attachments[i].WithFileName(
                $"{Path.GetFileNameWithoutExtension(fileName)}.attempt-{attemptNumber.ToString(CultureInfo.InvariantCulture)}{extension}");
        }

        return renamed;
    }

    private async Task UploadAttachmentsForResultAsync(int testCaseResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> attachments, CancellationToken cancellationToken)
    {
        if (attachments.Count == 0)
        {
            return;
        }

        try
        {
            if (_coordinatedRun is not null && _runIdCoordinator is not null)
            {
                await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
            }

            await UploadResultAttachmentsAsync(testCaseResultId, attachments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Individual upload failures are already counted inside UploadResultAttachmentsAsync (whose
            // logging is non-throwing), so reaching here means RenewLeaseAsync threw and no upload was
            // attempted at all. Count the whole set, otherwise these attachments are dropped uncounted and
            // the end-of-session summary under-reports.
            Interlocked.Add(ref _failedAttachmentCount, attachments.Count);
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingResultAttachmentFailed} {ex.Message}");
        }
    }

    /// <summary>
    /// Returns results to the front of the queue so the next flush retries them in their original order.
    /// </summary>
    /// <remarks>Call only while holding <see cref="_flushSemaphore"/>.</remarks>
    private void RequeueUnsafe(IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> batch)
    {
        // Pushed in reverse so Pop yields them in batch order.
        for (int i = batch.Count - 1; i >= 0; i--)
        {
            _retryResults.Push(batch[i]);
        }
    }

    private static bool BatchHasAttachments(IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> batch)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            if (batch[i].Attachments.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountAttachments(IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> batch)
    {
        int count = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            count += batch[i].Attachments.Count;
        }

        return count;
    }

    private bool ShouldFlushUnsafe(bool force)
    {
        int pendingResultsCount = _retryResults.Count + _pendingResults.Count;

        if (pendingResultsCount == 0)
        {
            return false;
        }

        if (force)
        {
            return true;
        }

        if (_clock.UtcNow - _lastFlushTime >= _options.FlushInterval)
        {
            return true;
        }

        // Only trigger a batch-size based flush from fresh pending results. When a previous publish
        // failed and pushed a full batch back into _retryResults, the next ConsumeAsync would
        // otherwise immediately satisfy this condition and tight-retry on every incoming result —
        // wait for the flush interval (background loop) before retrying instead.
        return _retryResults.Count == 0 && _pendingResults.Count >= _options.BatchSize;
    }
}
