// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
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
