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

    private async Task UploadDeferredAttachmentsAsync(
        List<(int ResultId, int? TestSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments)> deferredAttachments,
        CancellationToken cancellationToken)
    {
        foreach ((int resultId, int? testSubResultId, IReadOnlyList<AzureDevOpsTestResultAttachment> attachments) in deferredAttachments)
        {
            await UploadAttachmentsForResultAsync(resultId, testSubResultId, attachments, cancellationToken).ConfigureAwait(false);
        }
    }

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
}
