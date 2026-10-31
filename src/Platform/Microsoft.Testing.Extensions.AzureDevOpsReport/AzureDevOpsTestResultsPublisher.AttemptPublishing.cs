// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
    /// <summary>
    /// Records canonical identity metadata after creation and appends first attempts when needed,
    /// then targets their stable sub-results with the attempts' attachments.
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
            if (seeds[i].Attempts.Count == 1
                && (_resultIdStore is null || seeds[i].Parent.Attachments.Count == 0))
            {
                // Azure DevOps stores result comments on PATCH, not POST. Metadata alone must not
                // turn a single execution into a rerun or append an artificial first attempt.
                parents[i] = seeds[i].Parent.Result with { Id = seeds[i].ResultId };
                continue;
            }

            AzureDevOpsTestCaseResult[] attemptResults = [.. seeds[i].Attempts.Select(static attempt => attempt.Result)];
            IReadOnlyList<AzureDevOpsTestSubResult> subResults = AzureDevOpsResultIdStore.CreateAttempts(attemptResults, firstSequenceId: 1);
            (DateTimeOffset? startedDate, DateTimeOffset? completedDate) = GetDateRange(attemptResults);
            parents[i] = seeds[i].Parent.Result with
            {
                Id = seeds[i].ResultId,
                ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
                CustomFields = CreateRerunCustomFields(seeds[i].Parent.Result.Outcome, subResults),
                SubResults = subResults,
                DurationInMs = AzureDevOpsResultIdStore.SumResultDurations(attemptResults),
                StartedDate = startedDate,
                CompletedDate = completedDate,
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
            // mapping for appended attempts before propagating cancellation or falling back so a later
            // attempt cannot replay an uncertain first sub-result and duplicate sequence 1. Metadata-only
            // updates append nothing, so their original mapping remains safe to reuse. The POST contained only the final
            // outcome, so preserve earlier in-process executions as independent results rather than losing
            // their outcomes and diagnostics.
            List<AzureDevOpsTestCaseResultWithAttachments> previousAttempts = [];
            for (int i = 0; i < seeds.Count; i++)
            {
                AzureDevOpsTestCaseResultWithAttachments parent = seeds[i].Parent;
                if (parents[i].SubResults is not null && _resultIdStore?.TryGet(parent.Result) is { } published)
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

            string warning = parents.Any(static parent => parent.Comment is not null)
                ? AzureDevOpsResources.AzureDevOpsLivePublishingCanonicalMetadataFailed
                : AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed;
            TryLogWarning($"{warning} {ex.Message}");
            return;
        }

        for (int i = 0; i < seeds.Count; i++)
        {
            if (parents[i].SubResults is null)
            {
                deferredAttachments.Add((seeds[i].ResultId, TestSubResultId: null, seeds[i].Parent.Attachments));
                continue;
            }

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

    private static (DateTimeOffset? StartedDate, DateTimeOffset? CompletedDate) GetDateRange(
        IReadOnlyList<AzureDevOpsTestCaseResult> attempts)
    {
        DateTimeOffset? earliest = null;
        DateTimeOffset? latest = null;
        for (int i = 0; i < attempts.Count; i++)
        {
            AzureDevOpsTestCaseResult attempt = attempts[i];
            if (attempt.StartedDate is { } startedDate && (earliest is null || startedDate < earliest))
            {
                earliest = startedDate;
            }

            if (attempt.CompletedDate is { } completedDate && (latest is null || completedDate > latest))
            {
                latest = completedDate;
            }
        }

        return (earliest, latest);
    }
}
