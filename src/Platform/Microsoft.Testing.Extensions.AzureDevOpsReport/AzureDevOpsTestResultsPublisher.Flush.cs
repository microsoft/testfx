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

    private async Task UploadResultAttachmentsAsync(int testCaseResultId, int? testSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> attachments, CancellationToken cancellationToken)
    {
        if (_publishConfiguration is null || CurrentRunId is null || attachments.Count == 0)
        {
            return;
        }

        foreach (AzureDevOpsTestResultAttachment attachment in attachments)
        {
            try
            {
                await _client.UploadTestResultAttachmentAsync(_publishConfiguration, CurrentRunId.Value, testCaseResultId, testSubResultId, attachment, cancellationToken).ConfigureAwait(false);
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
                List<(AzureDevOpsPublishedResult Published, AzureDevOpsTestCaseResultWithAttachments Attempt)> updateCandidates = [];
                List<(int ResultId, int? TestSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments)> deferredAttachments = [];
                foreach (AzureDevOpsTestCaseResultWithAttachments item in batch)
                {
                    if (_resultIdStore?.TryGet(item.Result) is { } published)
                    {
                        updateCandidates.Add((published, item));
                    }
                    else
                    {
                        creations.Add(item);
                    }
                }

                // The store is unchanged while classifying a batch, so two current rows can resolve to the
                // same persisted parent (for example when a formerly unique folded data row is duplicated).
                // Never PATCH a guessed parent twice; ambiguous rows degrade to independent creates.
                Dictionary<int, int> candidateCountsByResultId = [];
                foreach ((AzureDevOpsPublishedResult published, AzureDevOpsTestCaseResultWithAttachments _) in updateCandidates)
                {
                    candidateCountsByResultId[published.Id] = candidateCountsByResultId.TryGetValue(published.Id, out int count) ? count + 1 : 1;
                }

                List<(AzureDevOpsPublishedResult Published, AzureDevOpsTestCaseResultWithAttachments Attempt)> updates = [];
                foreach ((AzureDevOpsPublishedResult published, AzureDevOpsTestCaseResultWithAttachments attempt) in updateCandidates)
                {
                    if (candidateCountsByResultId[published.Id] == 1 && _claimedResultIds.Add(published.Id))
                    {
                        updates.Add((published, attempt));
                    }
                    else
                    {
                        _claimedResultIds.Add(published.Id);
                        creations.Add(attempt);
                    }
                }

                if (creations.Count > 0 && !await TryCreateResultsAsync(creations, deferredAttachments, cancellationToken).ConfigureAwait(false))
                {
                    // Nothing in this batch reached Azure DevOps: the creations failed, and the updates were
                    // not attempted. Release every parent claimed while classifying this untouched batch,
                    // then requeue it as it was so the next flush can retry the intended update path.
                    foreach ((AzureDevOpsPublishedResult published, AzureDevOpsTestCaseResultWithAttachments _) in updates)
                    {
                        _claimedResultIds.Remove(published.Id);
                    }

                    RequeueUnsafe(batch);
                    return;
                }

                if (updates.Count > 0 && !_resultIdStore!.TryInvalidatePersistedMap())
                {
                    // The old map cannot be removed, so a successful PATCH followed by a crash could leave
                    // stale history for the next attempt to replay. Create separate results instead: less
                    // tidy, but every execution remains represented.
                    List<AzureDevOpsTestCaseResultWithAttachments> updateFallbacks = [.. updates.Select(update => update.Attempt)];
                    if (!await TryCreateResultsAsync(updateFallbacks, deferredAttachments, cancellationToken).ConfigureAwait(false))
                    {
                        RequeueUnsafe(updateFallbacks);
                        await UploadDeferredAttachmentsAsync(deferredAttachments, cancellationToken).ConfigureAwait(false);
                        if (!force)
                        {
                            return;
                        }

                        // The claim is retained because the persisted map could not be invalidated. On the
                        // next forced iteration it makes this item ambiguous, so it is reclassified as a
                        // plain creation. A subsequent create failure returns through the normal branch.
                        continue;
                    }
                }
                else if (updates.Count > 0 && !await TryUpdateResultsAsync(updates, deferredAttachments, cancellationToken).ConfigureAwait(false))
                {
                    RequeueUnsafe([.. updates.Select(update => update.Attempt)]);
                    await UploadDeferredAttachmentsAsync(deferredAttachments, cancellationToken).ConfigureAwait(false);
                    if (!force)
                    {
                        return;
                    }

                    // TryUpdateResultsAsync forgot the ambiguous mappings before requeueing. A forced
                    // session-end flush has no later opportunity, so immediately loop and reclassify these
                    // attempts as safe creates. Background flushes return to preserve the retry backoff.
                    continue;
                }

                await UploadDeferredAttachmentsAsync(deferredAttachments, cancellationToken).ConfigureAwait(false);
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
    private async Task<bool> TryCreateResultsAsync(
        List<AzureDevOpsTestCaseResultWithAttachments> batch,
        List<(int ResultId, int? TestSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments)> deferredAttachments,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults;
        try
        {
            if (_coordinatedRun is not null && _runIdCoordinator is not null)
            {
                await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
            }

            var resultsOnly = new AzureDevOpsTestCaseResult[batch.Count];
            for (int i = 0; i < batch.Count; i++)
            {
                resultsOnly[i] = _resultIdStore is not null && batch[i].Attachments.Count > 0
                    ? batch[i].Result with
                    {
                        ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
                        SubResults = [AzureDevOpsResultIdStore.CreateFirstAttempt(batch[i].Result)],
                    }
                    : batch[i].Result;
            }

            publishedResults = await _client.PublishTestResultsWithSubResultsAsync(_publishConfiguration!, CurrentRunId!.Value, resultsOnly, cancellationToken).ConfigureAwait(false);
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
        if (publishedResults is null)
        {
            if (BatchHasAttachments(batch))
            {
                Interlocked.Add(ref _failedAttachmentCount, CountAttachments(batch));
                TryLogWarning(AzureDevOpsResources.AzureDevOpsLivePublishingResultIdParseFailedWarning);
            }

            return true;
        }

        // Record the whole accepted batch before any cancellable attachment upload. Azure DevOps accepted
        // every result in one operation, so the map must describe all of them even if cancellation
        // interrupts the best-effort attachment phase.
        bool failedToResolveSubResultId = false;
        for (int i = 0; i < batch.Count; i++)
        {
            // Folded data-driven rows share one uid. A failure in any row retries the whole uid, including
            // rows that passed or were skipped, so every row must retain its own result id and history.
            if (_resultIdStore is not null)
            {
                _resultIdStore.RecordCreated(batch[i].Result, publishedResults[i].Id);
                _claimedResultIds.Add(publishedResults[i].Id);
            }

            if (batch[i].Attachments.Count > 0)
            {
                int? testSubResultId = null;
                int resolvedSubResultId = 0;
                if (_resultIdStore is not null
                    && !publishedResults[i].TryGetSubResultId(sequenceId: 1, out resolvedSubResultId))
                {
                    Interlocked.Add(ref _failedAttachmentCount, batch[i].Attachments.Count);
                    failedToResolveSubResultId = true;
                    continue;
                }

                if (_resultIdStore is not null)
                {
                    testSubResultId = resolvedSubResultId;
                }

                deferredAttachments.Add((
                    publishedResults[i].Id,
                    testSubResultId,
                    batch[i].Attachments));
            }
        }

        if (failedToResolveSubResultId)
        {
            TryLogWarning(AzureDevOpsResources.AzureDevOpsLivePublishingResultIdParseFailedWarning);
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
        List<(int ResultId, int? TestSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments)> deferredAttachments,
        CancellationToken cancellationToken)
    {
        var parents = new AzureDevOpsTestCaseResult[updates.Count];
        var attemptHistories = new IReadOnlyList<AzureDevOpsTestSubResult>[updates.Count];
        long?[] totalDurations = new long?[updates.Count];
        var startedDates = new DateTimeOffset?[updates.Count];
        var completedDates = new DateTimeOffset?[updates.Count];
        for (int i = 0; i < updates.Count; i++)
        {
            // Built but not recorded yet: the map may only advance once Azure DevOps has accepted the
            // update, otherwise retrying a failed update would list the same execution twice.
            attemptHistories[i] = AzureDevOpsResultIdStore.BuildNextAttempts(updates[i].Published, updates[i].Attempt.Result);
            totalDurations[i] = AzureDevOpsResultIdStore.BuildNextTotalDuration(updates[i].Published, updates[i].Attempt.Result);
            startedDates[i] = Min(updates[i].Published.StartedDate, updates[i].Attempt.Result.StartedDate);
            completedDates[i] = Max(updates[i].Published.CompletedDate, updates[i].Attempt.Result.CompletedDate);
            parents[i] = updates[i].Attempt.Result with
            {
                Id = updates[i].Published.Id,
                ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
                SubResults = attemptHistories[i],
                DurationInMs = totalDurations[i],
                StartedDate = startedDates[i],
                CompletedDate = completedDates[i],
            };
        }

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults;
        try
        {
            if (_coordinatedRun is not null && _runIdCoordinator is not null)
            {
                await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
            }

            publishedResults = await _client.UpdateTestResultsWithSubResultsAsync(_publishConfiguration!, CurrentRunId!.Value, parents, cancellationToken).ConfigureAwait(false);
            _lastFlushTime = _clock.UtcNow;
        }
        catch (Exception ex)
        {
            foreach ((AzureDevOpsPublishedResult published, AzureDevOpsTestCaseResultWithAttachments _) in updates)
            {
                _resultIdStore!.Forget(published);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            _lastFlushTime = _clock.UtcNow;
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            return false;
        }

        // The PATCH accepted the whole batch. Advance every history before an attachment upload can be
        // canceled, otherwise only a prefix of the accepted updates would survive into the next attempt.
        for (int i = 0; i < updates.Count; i++)
        {
            _resultIdStore!.RecordAttempts(
                updates[i].Published,
                attemptHistories[i],
                totalDurations[i],
                startedDates[i],
                completedDates[i]);
        }

        bool failedToResolveSubResultId = false;
        for (int i = 0; i < updates.Count; i++)
        {
            if (updates[i].Attempt.Attachments.Count > 0)
            {
                int sequenceId = attemptHistories[i][^1].SequenceId;
                if (publishedResults is null
                    || !publishedResults[i].TryGetSubResultId(sequenceId, out int testSubResultId))
                {
                    Interlocked.Add(ref _failedAttachmentCount, updates[i].Attempt.Attachments.Count);
                    failedToResolveSubResultId = true;
                    continue;
                }

                deferredAttachments.Add((
                    updates[i].Published.Id,
                    testSubResultId,
                    updates[i].Attempt.Attachments));
            }
        }

        if (failedToResolveSubResultId)
        {
            TryLogWarning(AzureDevOpsResources.AzureDevOpsLivePublishingResultIdParseFailedWarning);
        }

        return true;
    }

    private async Task UploadDeferredAttachmentsAsync(
        List<(int ResultId, int? TestSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments)> deferredAttachments,
        CancellationToken cancellationToken)
    {
        foreach ((int resultId, int? testSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> attachments) in deferredAttachments)
        {
            await UploadAttachmentsForResultAsync(resultId, testSubResultId, attachments, cancellationToken).ConfigureAwait(false);
        }
    }

    private static DateTimeOffset? Min(DateTimeOffset? left, DateTimeOffset? right)
        => left is null ? right : right is null || left <= right ? left : right;

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
        => left is null ? right : right is null || left >= right ? left : right;

    private async Task UploadAttachmentsForResultAsync(int testCaseResultId, int? testSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> attachments, CancellationToken cancellationToken)
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

            await UploadResultAttachmentsAsync(testCaseResultId, testSubResultId, attachments, cancellationToken).ConfigureAwait(false);
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
