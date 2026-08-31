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

    private async Task UploadPendingResultAttachmentsAsync(CancellationToken cancellationToken)
    {
        while (_pendingResultAttachments.TryDequeue(out (int TestCaseResultId, AzureDevOpsTestResultAttachment Attachment) pending))
        {
            try
            {
                await UploadAttachmentsForResultAsync(
                    pending.TestCaseResultId,
                    testSubResultId: null,
                    [pending.Attachment],
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                Interlocked.Increment(ref _failedAttachmentCount);
                TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingResultAttachmentFailed} {ex.Message}");
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

                bool creationsAccepted = true;
                if (creations.Count > 0)
                {
                    try
                    {
                        creationsAccepted = await TryCreateResultsAsync(creations, deferredAttachments, cancellationToken).ConfigureAwait(false);
                    }
                    catch (FirstAttemptSeedCanceledException)
                    {
                        // Creation already reached Azure DevOps, but its follow-up seed was canceled before
                        // these updates were attempted. Release their claims and put them back so session
                        // finalization can retry them or include them in the unpublished-result warning.
                        foreach ((AzureDevOpsPublishedResult published, AzureDevOpsTestCaseResultWithAttachments _) in updates)
                        {
                            _claimedResultIds.Remove(published.Id);
                        }

                        RequeueUnsafe([.. updates.Select(update => update.Attempt)]);
                        throw;
                    }
                }

                if (!creationsAccepted)
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
                resultsOnly[i] = batch[i].Result;
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
        List<(int ResultId, AzureDevOpsTestCaseResultWithAttachments Parent, IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> Attempts)> initialAttemptSeeds = [];
        for (int i = 0; i < batch.Count; i++)
        {
            IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> attempts = GetAttempts(batch[i]);
            AzureDevOpsTestCaseResult[] attemptResults = [.. attempts.Select(static attempt => attempt.Result)];

            // Folded data-driven rows share one uid. A failure in any row retries the whole uid, including
            // rows that passed or were skipped, so every row must retain its own result id and history.
            if (_resultIdStore is not null)
            {
                _resultIdStore.RecordCreated(batch[i].Result, publishedResults[i].Id, attemptResults);
                _claimedResultIds.Add(publishedResults[i].Id);
            }

            bool hasAttachments = attempts.Any(static attempt => attempt.Attachments.Count > 0);
            if (attempts.Count > 1 || (_resultIdStore is not null && hasAttachments))
            {
                initialAttemptSeeds.Add((publishedResults[i].Id, batch[i], attempts));
            }
            else if (hasAttachments)
            {
                deferredAttachments.Add((
                    publishedResults[i].Id,
                    TestSubResultId: null,
                    batch[i].Attachments));
            }
        }

        if (initialAttemptSeeds.Count > 0)
        {
            try
            {
                await SeedInitialAttemptsAsync(initialAttemptSeeds, deferredAttachments, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                throw new FirstAttemptSeedCanceledException(ex);
            }
        }

        return true;
    }

    /// <summary>
    /// Appends the first attempt after its parent has been created, then targets that stable sub-result with
    /// the attempt's attachments.
    /// </summary>
    /// <remarks>
    /// Azure DevOps appends sub-results supplied by PATCH. Sending the first attempt in the create request and
    /// replaying the full history in a later PATCH therefore duplicates sequence 1. Keeping creation flat and
    /// appending each attempt exactly once also prevents a later PATCH from replacing the sub-result that owns
    /// the attachment.
    /// </remarks>
    private async Task SeedInitialAttemptsAsync(
        List<(int ResultId, AzureDevOpsTestCaseResultWithAttachments Parent, IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> Attempts)> seeds,
        List<(int ResultId, int? TestSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments)> deferredAttachments,
        CancellationToken cancellationToken)
    {
        var parents = new AzureDevOpsTestCaseResult[seeds.Count];
        for (int i = 0; i < seeds.Count; i++)
        {
            AzureDevOpsTestCaseResult[] attemptResults = [.. seeds[i].Attempts.Select(static attempt => attempt.Result)];
            parents[i] = seeds[i].Parent.Result with
            {
                Id = seeds[i].ResultId,
                ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
                SubResults = AzureDevOpsResultIdStore.CreateAttempts(attemptResults, firstSequenceId: 1),
                DurationInMs = AzureDevOpsResultIdStore.SumResultDurations(attemptResults),
                StartedDate = AzureDevOpsResultIdStore.GetEarliestStartedDate(attemptResults),
                CompletedDate = AzureDevOpsResultIdStore.GetLatestCompletedDate(attemptResults),
            };
        }

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults;
        try
        {
            if (_coordinatedRun is not null && _runIdCoordinator is not null)
            {
                await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
            }

            publishedResults = await _client.UpdateTestResultsWithSubResultsAsync(
                _publishConfiguration!,
                CurrentRunId!.Value,
                parents,
                cancellationToken).ConfigureAwait(false);
            _lastFlushTime = _clock.UtcNow;
        }
        catch (Exception ex)
        {
            // The parent POST already succeeded, but the PATCH may have reached Azure DevOps. Forget the
            // mapping before propagating cancellation or falling back so a later attempt cannot replay an
            // uncertain first sub-result and duplicate sequence 1. The POST contained only the final
            // outcome, so preserve earlier in-process executions as independent results rather than losing
            // their outcomes and diagnostics.
            List<AzureDevOpsTestCaseResultWithAttachments> previousAttempts = [];
            foreach ((int resultId, AzureDevOpsTestCaseResultWithAttachments parent, IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> _) in seeds)
            {
                if (_resultIdStore?.TryGet(parent.Result) is { } published)
                {
                    _resultIdStore.Forget(published);
                }

                previousAttempts.AddRange(parent.PreviousAttempts);
            }

            if (previousAttempts.Count > 0)
            {
                RequeueUnsafe(previousAttempts);
            }

            if (ex is OperationCanceledException)
            {
                foreach ((int resultId, AzureDevOpsTestCaseResultWithAttachments parent, IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> _) in seeds)
                {
                    foreach (AzureDevOpsTestResultAttachment attachment in parent.Attachments)
                    {
                        _pendingResultAttachments.Enqueue((resultId, attachment));
                    }
                }

                throw;
            }

            foreach ((int resultId, AzureDevOpsTestCaseResultWithAttachments parent, IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> _) in seeds)
            {
                deferredAttachments.Add((resultId, TestSubResultId: null, parent.Attachments));
            }

            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            return;
        }

        for (int i = 0; i < seeds.Count; i++)
        {
            int lastSequenceId = parents[i].SubResults![^1].SequenceId;
            _resultIdStore?.RecordPublishedSubResults(
                seeds[i].Parent.Result,
                seeds[i].ResultId,
                lastSequenceId);

            int firstSequenceId = lastSequenceId - seeds[i].Attempts.Count + 1;
            for (int attemptIndex = 0; attemptIndex < seeds[i].Attempts.Count; attemptIndex++)
            {
                AzureDevOpsTestCaseResultWithAttachments attempt = seeds[i].Attempts[attemptIndex];
                int sequenceId = firstSequenceId + attemptIndex;
                int? testSubResultId = publishedResults is not null
                    && publishedResults[i].TryGetSubResultId(sequenceId, out int resolvedSubResultId)
                        ? resolvedSubResultId
                        : null;
                deferredAttachments.Add((
                    seeds[i].ResultId,
                    testSubResultId,
                    attempt.Attachments));
            }
        }
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
        var appendedAttempts = new IReadOnlyList<AzureDevOpsTestSubResult>[updates.Count];
        long?[] totalDurations = new long?[updates.Count];
        var startedDates = new DateTimeOffset?[updates.Count];
        var completedDates = new DateTimeOffset?[updates.Count];
        var newAttempts = new IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments>[updates.Count];
        for (int i = 0; i < updates.Count; i++)
        {
            newAttempts[i] = GetAttempts(updates[i].Attempt);
            AzureDevOpsTestCaseResult[] newAttemptResults = [.. newAttempts[i].Select(static attempt => attempt.Result)];

            // Built but not recorded yet: the map may only advance once Azure DevOps has accepted the
            // update, otherwise retrying a failed update would list the same execution twice.
            attemptHistories[i] = AzureDevOpsResultIdStore.BuildNextAttempts(updates[i].Published, newAttemptResults);
            appendedAttempts[i] = [.. attemptHistories[i].Where(attempt => attempt.SequenceId > updates[i].Published.LastPublishedSubResultSequenceId)];
            totalDurations[i] = AzureDevOpsResultIdStore.BuildNextTotalDuration(updates[i].Published, newAttemptResults);
            startedDates[i] = Min(updates[i].Published.StartedDate, AzureDevOpsResultIdStore.GetEarliestStartedDate(newAttemptResults));
            completedDates[i] = Max(updates[i].Published.CompletedDate, AzureDevOpsResultIdStore.GetLatestCompletedDate(newAttemptResults));
            parents[i] = updates[i].Attempt.Result with
            {
                Id = updates[i].Published.Id,
                ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
                SubResults = appendedAttempts[i],
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
                attemptHistories[i][^1].SequenceId,
                totalDurations[i],
                startedDates[i],
                completedDates[i]);
        }

        for (int i = 0; i < updates.Count; i++)
        {
            int lastSequenceId = attemptHistories[i][^1].SequenceId;
            int firstSequenceId = lastSequenceId - newAttempts[i].Count + 1;
            for (int attemptIndex = 0; attemptIndex < newAttempts[i].Count; attemptIndex++)
            {
                AzureDevOpsTestCaseResultWithAttachments attempt = newAttempts[i][attemptIndex];
                if (attempt.Attachments.Count == 0)
                {
                    continue;
                }

                int sequenceId = firstSequenceId + attemptIndex;
                if (publishedResults is null
                    || !publishedResults[i].TryGetSubResultId(sequenceId, out int testSubResultId))
                {
                    deferredAttachments.Add((
                        updates[i].Published.Id,
                        TestSubResultId: null,
                        attempt.Attachments));
                    continue;
                }

                deferredAttachments.Add((
                    updates[i].Published.Id,
                    testSubResultId,
                    attempt.Attachments));
            }
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
            if (GetAttempts(batch[i]).Any(static attempt => attempt.Attachments.Count > 0))
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
            foreach (AzureDevOpsTestCaseResultWithAttachments attempt in GetAttempts(batch[i]))
            {
                count += attempt.Attachments.Count;
            }
        }

        return count;
    }

    private static IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> GetAttempts(AzureDevOpsTestCaseResultWithAttachments result)
        => result.PreviousAttempts.Count == 0 ? [result] : [.. result.PreviousAttempts, result];

    private sealed class FirstAttemptSeedCanceledException(OperationCanceledException innerException)
        : OperationCanceledException(innerException.Message, innerException, innerException.CancellationToken);

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
