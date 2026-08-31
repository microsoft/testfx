// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsRunIdCoordinator
{
    /// <summary>
    /// Build id recorded for a legacy plain-PID lease, which carries no build context of its own.
    /// </summary>
    private const int LegacyLeaseBuildId = 0;

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private async Task<bool> TryAcquireOwnerAsync(string ownerFilePath, int buildId, CancellationToken cancellationToken)
    {
        AzureDevOpsLeaseFile lease = CreateLeaseFile(buildId);
        if (await TryWriteLeaseFileAsync(ownerFilePath, lease, overwrite: false, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        LeaseReadResult existing = ReadLease(ownerFilePath);

        // If the file exists but we couldn't read it (likely partial write from the current owner),
        // refuse to take over — otherwise we'd race and create a duplicate Azure DevOps run.
        if (existing.Status == LeaseFileStatus.TransientReadError)
        {
            return false;
        }

        if (existing.Status is LeaseFileStatus.NotFound
            || (existing.Status == LeaseFileStatus.Expired && !IsOwnerStillAlive(existing)))
        {
            TryDeleteFile(ownerFilePath);
            return await TryWriteLeaseFileAsync(ownerFilePath, lease, overwrite: false, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// Returns whether the process that wrote a lease is still running.
    /// </summary>
    /// <remarks>
    /// Liveness, not wall-clock expiry, is the authoritative signal that an owner is still in charge. Not
    /// every owner renews its lease: a run orchestrator holds one for the whole orchestration without a
    /// natural point to refresh it, so on a build longer than
    /// <see cref="AzureDevOpsTestResultsPublisherOptions.CoordinationFileExpiration"/> its lease goes stale
    /// while it is very much still publishing. Taking over then would create a second Azure DevOps run for
    /// the same build. Refusing to take over from a live process errs towards one run rather than two,
    /// which is also how <see cref="CleanupStaleParticipants"/> already treats participants.
    /// <para>
    /// The legacy plain-PID lease format is excluded: <see cref="ReadLease"/> deliberately reports it as
    /// expired so it can be replaced, and it carries no build id to tell us which run it belongs to. A
    /// reused pid would otherwise let a stale file from an older version block takeover indefinitely.
    /// </para>
    /// </remarks>
    private static bool IsOwnerStillAlive(LeaseReadResult lease)
        => lease.Lease is { BuildId: not LegacyLeaseBuildId } ownerLease && IsProcessAlive(ownerLease.ProcessId);

    private AzureDevOpsLeaseFile CreateLeaseFile(int buildId)
        => new(_environment.ProcessId, buildId, _clock.UtcNow + _options.CoordinationFileExpiration);

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private LeaseReadResult ReadLease(string path)
    {
        if (!_fileSystem.ExistFile(path))
        {
            return new LeaseReadResult(LeaseFileStatus.NotFound, null);
        }

        try
        {
            string content = _fileSystem.ReadAllText(path);
            AzureDevOpsLeaseFile? lease = JsonSerializer.Deserialize<AzureDevOpsLeaseFile>(content, JsonSerializerOptions);
            if (lease is not null)
            {
                return new LeaseReadResult(
                    lease.ExpiresAt > _clock.UtcNow ? LeaseFileStatus.Active : LeaseFileStatus.Expired,
                    lease);
            }

            if (int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out int processId))
            {
                // Legacy plain-PID lease format: treat as expired so the caller can take over. The sentinel
                // build id marks it as carrying no build context, which is what keeps IsOwnerStillAlive from
                // treating a reused pid as a live owner.
                return new LeaseReadResult(LeaseFileStatus.Expired, new AzureDevOpsLeaseFile(processId, LegacyLeaseBuildId, DateTimeOffset.MinValue));
            }

            // The file exists but neither parser yielded a usable value. It might be mid-write —
            // surface this as a transient read error so the caller doesn't race the writer.
            return new LeaseReadResult(LeaseFileStatus.TransientReadError, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new LeaseReadResult(LeaseFileStatus.TransientReadError, null);
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private Task WriteLeaseFileAsync(string path, AzureDevOpsLeaseFile payload, bool overwrite, CancellationToken cancellationToken)
        => WriteJsonFileAsync(path, payload, overwrite, cancellationToken);

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private async Task<bool> TryWriteLeaseFileAsync(string path, AzureDevOpsLeaseFile payload, bool overwrite, CancellationToken cancellationToken)
    {
        try
        {
            await WriteLeaseFileAsync(path, payload, overwrite, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string GetOwnerFileName(int buildId)
        => $"{CoordinationFilePrefix}.{buildId}.owner";
}
