// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsRunIdCoordinator
{
    /// <summary>
    /// Returns whether at least one of the given participants belongs to a process that is still running.
    /// </summary>
    /// <remarks>
    /// Distinguishes "a peer is still publishing" from "a lease is lying around that nobody can vouch for".
    /// Only the former justifies holding the run open past the short grace period.
    /// </remarks>
    private bool AnyParticipantProcessAlive(string[] participantFiles)
    {
        foreach (string participantFile in participantFiles)
        {
            AzureDevOpsLeaseFile? lease = ReadLease(participantFile).Lease;
            int? processId = lease?.ProcessId ?? TryGetPid(participantFile);

            // Our own process is always alive, so counting it would mean waiting for ourselves forever.
            if (processId is { } participantProcessId
                && participantProcessId != _environment.ProcessId
                && IsProcessAlive(participantProcessId))
            {
                return true;
            }
        }

        return false;
    }

    private string[] CleanupStaleParticipants(string[] participantFiles)
    {
        var activeParticipants = new List<string>();

        foreach (string participantFile in participantFiles)
        {
            LeaseReadResult result = ReadLease(participantFile);

            // Treat a participant whose file we couldn't read (e.g. mid-write) as active to avoid
            // racing with a process that is still updating its lease.
            if (result.Status is LeaseFileStatus.TransientReadError or LeaseFileStatus.Active)
            {
                AzureDevOpsLeaseFile? lease = result.Lease;
                if (lease is null || (lease.ExpiresAt > _clock.UtcNow && IsProcessAlive(lease.ProcessId)))
                {
                    activeParticipants.Add(participantFile);
                    continue;
                }
            }
            else if (result.Status == LeaseFileStatus.Expired
                && result.Lease is { } expiredLease
                && IsProcessAlive(expiredLease.ProcessId))
            {
                // Lease has expired according to wall-clock but the participant process is still
                // running (its renewal may simply be stuck). Keep waiting rather than deleting.
                activeParticipants.Add(participantFile);
                continue;
            }
            else if (result.Status == LeaseFileStatus.NotFound)
            {
                continue;
            }
            else if (TryGetPid(participantFile) is int processId && IsProcessAlive(processId))
            {
                activeParticipants.Add(participantFile);
                continue;
            }

            TryDeleteFile(participantFile);
        }

        return [.. activeParticipants];
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private async Task WriteParticipantLeaseAsync(string participantFilePath, int buildId, CancellationToken cancellationToken)
    {
        AzureDevOpsLeaseFile lease = CreateLeaseFile(buildId);
        if (await TryWriteLeaseFileAsync(participantFilePath, lease, overwrite: false, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await WriteLeaseFileAsync(participantFilePath, lease, overwrite: true, cancellationToken).ConfigureAwait(false);
    }

    private static int? TryGetPid(string participantFile)
    {
        string fileName = Path.GetFileNameWithoutExtension(participantFile);
        int lastSeparator = fileName.LastIndexOf('.');
        return lastSeparator < 0
            ? null
            : int.TryParse(fileName[(lastSeparator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int processId)
                ? processId
                : null;
    }

    private static string GetParticipantFileName(int buildId, int processId)
        => $"{CoordinationFilePrefix}.{buildId}.participant.{processId}.json";

    private static string GetParticipantSearchPattern(int buildId)
        => $"{CoordinationFilePrefix}.{buildId}.participant.*.json";
}
