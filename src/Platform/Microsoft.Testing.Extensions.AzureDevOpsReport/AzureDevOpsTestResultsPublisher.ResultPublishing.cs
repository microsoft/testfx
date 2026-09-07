// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher

internal sealed partial class AzureDevOpsTestResultsPublisher
{
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

    private static IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> GetAttempts(AzureDevOpsTestCaseResultWithAttachments result)
        => result.PreviousAttempts.Count == 0 ? [result] : [.. result.PreviousAttempts, result];

    private sealed class FirstAttemptSeedCanceledException(OperationCanceledException innerException)
        : OperationCanceledException(innerException.Message, innerException, innerException.CancellationToken);

}
