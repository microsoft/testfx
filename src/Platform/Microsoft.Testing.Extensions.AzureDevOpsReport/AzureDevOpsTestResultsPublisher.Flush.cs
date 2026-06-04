// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Logging;

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
                _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingRunAttachmentFailed} {ex.Message}");
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
                _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingResultAttachmentFailed} {ex.Message}");
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
                _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
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
            while (_publishConfiguration is not null && CurrentRunId is not null)
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

                    resultIds = await _client.PublishTestResultsAsync(_publishConfiguration, CurrentRunId.Value, resultsOnly, cancellationToken).ConfigureAwait(false);
                    _lastFlushTime = _clock.UtcNow;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Transport/HTTP failure — AzDO may not have accepted the batch, so it's safe to
                    // requeue and retry. Push results in reverse so Pop retries them in batch order.
                    for (int i = batch.Count - 1; i >= 0; i--)
                    {
                        _retryResults.Push(batch[i]);
                    }

                    // Reset the interval countdown so a transient failure does not cause a tight retry loop.
                    _lastFlushTime = _clock.UtcNow;
                    _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
                    return;
                }

                // POST succeeded. If we couldn't parse the response we cannot upload result-level
                // attachments for this batch, but we MUST NOT republish (that would create duplicate
                // result rows in AzDO). Continue with the next batch.
                if (resultIds is null)
                {
                    if (BatchHasAttachments(batch))
                    {
                        _logger.LogWarning(AzureDevOpsResources.AzureDevOpsLivePublishingResultIdParseFailedWarning);
                    }

                    continue;
                }

                for (int i = 0; i < batch.Count; i++)
                {
                    if (batch[i].Attachments.Count == 0)
                    {
                        continue;
                    }

                    try
                    {
                        if (_coordinatedRun is not null && _runIdCoordinator is not null)
                        {
                            await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
                        }

                        await UploadResultAttachmentsAsync(resultIds[i], batch[i].Attachments, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingResultAttachmentFailed} {ex.Message}");
                    }
                }
            }
        }
        finally
        {
            _flushSemaphore.Release();
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
