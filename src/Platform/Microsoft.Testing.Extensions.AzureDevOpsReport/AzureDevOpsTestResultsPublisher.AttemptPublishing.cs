// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
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
}
