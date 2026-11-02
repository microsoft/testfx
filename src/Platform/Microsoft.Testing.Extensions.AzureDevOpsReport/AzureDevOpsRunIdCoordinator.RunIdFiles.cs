// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsRunIdCoordinator
{
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private async Task<AzureDevOpsRunIdFile?> WaitForRunIdFileAsync(string runIdFilePath, string ownerFilePath, int buildId, CancellationToken cancellationToken)
    {
        DateTimeOffset joinerDeadline = _clock.UtcNow + _options.CoordinationJoinerMaxWaitTime;

        for (int attempt = 0; ; attempt++)
        {
            if (_fileSystem.ExistFile(runIdFilePath))
            {
                try
                {
                    string content = await _fileSystem.ReadAllTextAsync(runIdFilePath).ConfigureAwait(false);
                    AzureDevOpsRunIdFile? runIdFile = JsonSerializer.Deserialize<AzureDevOpsRunIdFile>(content, JsonSerializerOptions);
                    if (runIdFile is not null)
                    {
                        // A run-id file whose expiry has passed is only stale if nobody owns it any more.
                        // An owner that does not renew its lease (a run orchestrator holds one for the whole
                        // orchestration) is still publishing into that run, so deleting the file here would
                        // send every joiner off to create a second run for the same build.
                        if (runIdFile.BuildId != buildId
                            || (runIdFile.ExpiresAt <= _clock.UtcNow && !IsOwnerStillAlive(ReadLease(ownerFilePath))))
                        {
                            TryDeleteFile(runIdFilePath);
                        }
                        else if (runIdFile.BuildId == buildId)
                        {
                            return runIdFile;
                        }
                    }
                }
                catch (IOException ex)
                {
                    // The coordination file may be mid-write. Treat the failure as transient and retry.
                    _ = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Access can be temporarily blocked by another process. Retry within the existing budget.
                    _ = ex;
                }
                catch (JsonException ex)
                {
                    // Partially written JSON is transient while another process publishes the run id.
                    _ = ex;
                }
            }

            // After the base retry budget is exhausted, only keep waiting as long as the owner lease
            // still looks active (or temporarily unreadable). A long CreateTestRunAsync can outlast
            // CoordinationReadRetryCount * CoordinationReadRetryDelay, but we still want to bound the
            // wait by CoordinationJoinerMaxWaitTime so a crashed owner doesn't stall joiners forever.
            if (attempt >= _options.CoordinationReadRetryCount)
            {
                if (_clock.UtcNow >= joinerDeadline)
                {
                    return null;
                }

                LeaseReadResult ownerLease = ReadLease(ownerFilePath);
                bool ownerStillActive = ownerLease.Status is LeaseFileStatus.Active or LeaseFileStatus.TransientReadError
                    || (ownerLease.Status == LeaseFileStatus.Expired && IsOwnerStillAlive(ownerLease));

                if (!ownerStillActive)
                {
                    return null;
                }
            }

            await _task.Delay(_options.CoordinationReadRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private async Task WriteRunIdFileAsync(string runIdFilePath, AzureDevOpsPublishConfiguration configuration, int runId, CancellationToken cancellationToken)
        => await WriteJsonFileAsync(runIdFilePath, new AzureDevOpsRunIdFile(runId, configuration.BuildId, configuration.CollectionUri, configuration.Project, _clock.UtcNow + _options.CoordinationFileExpiration), overwrite: true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Publishes the run id for peers to join, tolerating a failure to write the file.
    /// </summary>
    /// <remarks>
    /// The run already exists in Azure DevOps at this point, and this process is the only one that can
    /// close it. Letting anything propagate here would unwind into the caller's cleanup, which deletes the
    /// leases and never hands back the run, stranding it in "InProgress" forever. So nothing escapes:
    /// not a file-system failure, not cancellation, and not a logger provider that throws while reporting
    /// it. Losing the file costs peers the ability to join this run, which is degraded but recoverable;
    /// losing the run itself is not.
    /// </remarks>
    private async Task TryWriteRunIdFileAsync(string runIdFilePath, AzureDevOpsPublishConfiguration configuration, int runId, CancellationToken cancellationToken)
    {
        try
        {
            await WriteRunIdFileAsync(runIdFilePath, configuration, runId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToWriteCoordinationFile} {runIdFilePath}: {ex.Message}");
        }
    }

    private static string GetRunIdFileName(int buildId)
        => $"{CoordinationFilePrefix}.{buildId}.json";
}
