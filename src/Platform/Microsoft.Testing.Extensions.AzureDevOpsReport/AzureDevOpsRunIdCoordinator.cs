// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsRunIdCoordinator
{
    private const string CoordinationFilePrefix = "azdo-runid";

    /// <summary>
    /// Build id recorded for a legacy plain-PID lease, which carries no build context of its own.
    /// </summary>
    private const int LegacyLeaseBuildId = 0;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly IEnvironment _environment;
    private readonly ILogger _logger;
    private readonly AzureDevOpsTestResultsPublisherOptions _options;

    public AzureDevOpsRunIdCoordinator(IFileSystem fileSystem, ITask task, IClock clock, IEnvironment environment, ILogger logger, AzureDevOpsTestResultsPublisherOptions options)
    {
        _fileSystem = fileSystem;
        _task = task;
        _clock = clock;
        _environment = environment;
        _logger = logger;
        _options = options;
    }

    public async Task<AzureDevOpsCoordinatedRun> AcquireRunAsync(AzureDevOpsPublishConfiguration configuration, Func<CancellationToken, Task<int>> createRunAsync, CancellationToken cancellationToken)
    {
        _fileSystem.CreateDirectory(configuration.ResultsDirectory);

        string runIdFilePath = Path.Combine(configuration.ResultsDirectory, GetRunIdFileName(configuration.BuildId));
        string ownerFilePath = Path.Combine(configuration.ResultsDirectory, GetOwnerFileName(configuration.BuildId));
        string participantFilePath = Path.Combine(configuration.ResultsDirectory, GetParticipantFileName(configuration.BuildId, _environment.ProcessId));
        bool ownsOwnerFile = false;

        try
        {
            await WriteParticipantLeaseAsync(participantFilePath, configuration.BuildId, cancellationToken).ConfigureAwait(false);

            ownsOwnerFile = await TryAcquireOwnerAsync(ownerFilePath, configuration.BuildId, cancellationToken).ConfigureAwait(false);
            if (ownsOwnerFile)
            {
                int runId = await createRunAsync(cancellationToken).ConfigureAwait(false);
                await TryWriteRunIdFileAsync(runIdFilePath, configuration, runId, cancellationToken).ConfigureAwait(false);
                return new AzureDevOpsCoordinatedRun(runId, true, configuration.BuildId, configuration.ResultsDirectory, runIdFilePath, ownerFilePath, participantFilePath);
            }

            AzureDevOpsRunIdFile? runIdFile = await WaitForRunIdFileAsync(runIdFilePath, ownerFilePath, configuration.BuildId, cancellationToken).ConfigureAwait(false);
            if (runIdFile is null)
            {
                ownsOwnerFile = await TryAcquireOwnerAsync(ownerFilePath, configuration.BuildId, cancellationToken).ConfigureAwait(false);
                if (ownsOwnerFile)
                {
                    int runId = await createRunAsync(cancellationToken).ConfigureAwait(false);
                    await TryWriteRunIdFileAsync(runIdFilePath, configuration, runId, cancellationToken).ConfigureAwait(false);
                    return new AzureDevOpsCoordinatedRun(runId, true, configuration.BuildId, configuration.ResultsDirectory, runIdFilePath, ownerFilePath, participantFilePath);
                }

                runIdFile = await WaitForRunIdFileAsync(runIdFilePath, ownerFilePath, configuration.BuildId, cancellationToken).ConfigureAwait(false);
            }

            if (runIdFile is not null
                && string.Equals(runIdFile.CollectionUri, configuration.CollectionUri, StringComparison.OrdinalIgnoreCase)
                && string.Equals(runIdFile.Project, configuration.Project, StringComparison.Ordinal))
            {
                return new AzureDevOpsCoordinatedRun(runIdFile.RunId, false, configuration.BuildId, configuration.ResultsDirectory, runIdFilePath, ownerFilePath, participantFilePath);
            }

            if (runIdFile is not null)
            {
                throw new InvalidOperationException(AzureDevOpsResources.AzureDevOpsLivePublishingRunIdFileMismatch);
            }

            // A deterministic surviving participant could be elected when the owner lease expires before writing azdo-runid.<buildId>.json.
            throw new InvalidOperationException(AzureDevOpsResources.AzureDevOpsLivePublishingMissingRunIdFile);
        }
        catch
        {
            TryDeleteFile(participantFilePath);
            if (ownsOwnerFile)
            {
                TryDeleteFile(runIdFilePath);
                TryDeleteFile(ownerFilePath);
            }

            throw;
        }
    }

    /// <summary>
    /// Creates a handle to a run established by an ancestor process, which also owns completing it.
    /// </summary>
    /// <remarks>
    /// No coordination files are involved: file-based coordination exists to elect an owner among peer
    /// processes that cannot see each other, whereas here the owner is already known and outlives all of
    /// them. <see cref="RenewLeaseAsync"/> and <see cref="FinalizeRunAsync"/> are therefore no-ops for the
    /// returned run.
    /// </remarks>
    public static AzureDevOpsCoordinatedRun CreateInheritedRun(int runId, AzureDevOpsPublishConfiguration configuration)
        => new(runId, IsOwner: false, configuration.BuildId, configuration.ResultsDirectory, RunIdFilePath: string.Empty, OwnerFilePath: string.Empty, ParticipantFilePath: string.Empty)
        {
            IsInherited = true,
        };

    public async Task RenewLeaseAsync(AzureDevOpsCoordinatedRun coordinatedRun, CancellationToken cancellationToken)
    {
        if (coordinatedRun.IsInherited)
        {
            return;
        }

        await WriteLeaseFileAsync(coordinatedRun.ParticipantFilePath, CreateLeaseFile(coordinatedRun.BuildId), overwrite: true, cancellationToken).ConfigureAwait(false);

        if (coordinatedRun.IsOwner)
        {
            await WriteLeaseFileAsync(coordinatedRun.OwnerFilePath, CreateLeaseFile(coordinatedRun.BuildId), overwrite: true, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task FinalizeRunAsync(AzureDevOpsCoordinatedRun coordinatedRun, Func<CancellationToken, Task> finalizeRunAsync, CancellationToken cancellationToken)
    {
        // The ancestor process that created the run completes it once every participant has exited.
        // Completing it here would close the run while later attempts are still to come.
        if (coordinatedRun.IsInherited)
        {
            return;
        }

        TryDeleteFile(coordinatedRun.ParticipantFilePath);

        if (!coordinatedRun.IsOwner)
        {
            return;
        }

        // Two bounds, because "still waiting" and "not responding" are different situations. A participant
        // whose process is provably alive is still publishing, and completing the run out from under it
        // makes Azure DevOps reject everything it sends afterwards — losing results that used to reach a
        // (separate) run before builds were consolidated. So keep waiting for live participants, up to a
        // hard cap so a leaked process cannot stall the build forever. Participants we cannot prove alive
        // (unreadable, mid-write) only get the short grace period.
        DateTimeOffset unresponsiveDeadline = _clock.UtcNow + _options.CoordinationFinalizeTimeout;
        DateTimeOffset hardDeadline = _clock.UtcNow + _options.CoordinationFinalizeMaxWaitTime;

        while (true)
        {
            string[] participantFiles = _fileSystem.GetFiles(coordinatedRun.ResultsDirectory, GetParticipantSearchPattern(coordinatedRun.BuildId), SearchOption.TopDirectoryOnly);

            // Never wait on ourselves. Deleting our own lease above is best-effort (an antivirus or
            // indexer holding the handle makes it fail and is exactly what TryDeleteFile tolerates), and
            // this process trivially passes every liveness check — so without this the owner would keep
            // renewing the grace period against its own leftover file until the hard cap.
            participantFiles = [.. participantFiles.Where(participantFile => !string.Equals(participantFile, coordinatedRun.ParticipantFilePath, PathComparison.Comparison))];
            participantFiles = CleanupStaleParticipants(participantFiles);

            if (participantFiles.Length == 0)
            {
                break;
            }

            if (AnyParticipantProcessAlive(participantFiles))
            {
                unresponsiveDeadline = _clock.UtcNow + _options.CoordinationFinalizeTimeout;
            }

            DateTimeOffset now = _clock.UtcNow;
            bool hardDeadlineExpired = now >= hardDeadline;
            bool unresponsiveDeadlineExpired = now >= unresponsiveDeadline;
            if (hardDeadlineExpired || unresponsiveDeadlineExpired || cancellationToken.IsCancellationRequested)
            {
                TimeSpan timeout = hardDeadlineExpired
                    ? _options.CoordinationFinalizeMaxWaitTime
                    : _options.CoordinationFinalizeTimeout;
                _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingFinalizeWaitTimedOut, timeout, participantFiles.Length));
                break;
            }

            await _task.Delay(_options.CoordinationReadRetryDelay, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await finalizeRunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteFile(coordinatedRun.OwnerFilePath);
            TryDeleteFile(coordinatedRun.RunIdFilePath);
        }
    }

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
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (JsonException)
                {
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
    private async Task WriteParticipantLeaseAsync(string participantFilePath, int buildId, CancellationToken cancellationToken)
    {
        AzureDevOpsLeaseFile lease = CreateLeaseFile(buildId);
        if (await TryWriteLeaseFileAsync(participantFilePath, lease, overwrite: false, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await WriteLeaseFileAsync(participantFilePath, lease, overwrite: true, cancellationToken).ConfigureAwait(false);
    }

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
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }

        return new LeaseReadResult(LeaseFileStatus.TransientReadError, null);
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

    /// <summary>
    /// Logs a warning, swallowing any failure from the logging providers.
    /// </summary>
    /// <remarks>
    /// The aggregate logger invokes each provider directly, so a failing provider propagates. Every caller
    /// here is already recovering from something, and letting a diagnostic replace the failure it was
    /// describing would lose both.
    /// </remarks>
    private void TryLogWarning(string message)
    {
        try
        {
            _logger.LogWarning(message);
        }
        catch (Exception)
        {
            // There is nowhere left to report this: the diagnostic logger is the fallback sink.
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
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private async Task WriteJsonFileAsync<TPayload>(string path, TPayload payload, bool overwrite, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(payload, JsonSerializerOptions);
        using IFileStream stream = _fileSystem.NewFileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(stream.Stream, Utf8EncodingWithoutBom, 1024, leaveOpen: true);
#if NET
        await writer.WriteAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
        await writer.WriteAsync(json).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
#endif
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (_fileSystem.ExistFile(path))
            {
                _fileSystem.DeleteFile(path);
            }
        }
        catch (IOException ex)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}: {ex.Message}");
        }
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

    private static string GetOwnerFileName(int buildId)
        => $"{CoordinationFilePrefix}.{buildId}.owner";

    private static string GetParticipantFileName(int buildId, int processId)
        => $"{CoordinationFilePrefix}.{buildId}.participant.{processId}.json";

    private static string GetParticipantSearchPattern(int buildId)
        => $"{CoordinationFilePrefix}.{buildId}.participant.*.json";

    private static string GetRunIdFileName(int buildId)
        => $"{CoordinationFilePrefix}.{buildId}.json";
}

internal sealed record AzureDevOpsCoordinatedRun(
    int RunId,
    bool IsOwner,
    int BuildId,
    string ResultsDirectory,
    string RunIdFilePath,
    string OwnerFilePath,
    string ParticipantFilePath)
{
    /// <summary>
    /// Gets a value indicating whether the run was created by an ancestor process, which also owns closing it.
    /// </summary>
    /// <remarks>
    /// Declared as a property rather than a positional parameter so that adding it does not change the
    /// record's constructor and deconstructor signatures. An inherited run has no coordination files of
    /// its own: the ancestor decides when the run ends, so there is no owner to elect and no participant
    /// set to drain.
    /// </remarks>
    public bool IsInherited { get; init; }
}

internal sealed record AzureDevOpsRunIdFile(
    [property: JsonPropertyName("runId")] int RunId,
    [property: JsonPropertyName("buildId")] int BuildId,
    [property: JsonPropertyName("collectionUri")] string CollectionUri,
    [property: JsonPropertyName("project")] string Project,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);

internal sealed record AzureDevOpsLeaseFile(
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("buildId")] int BuildId,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);
