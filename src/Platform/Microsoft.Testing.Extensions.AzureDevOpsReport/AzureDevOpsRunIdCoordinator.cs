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
                await WriteRunIdFileAsync(runIdFilePath, configuration, runId, cancellationToken).ConfigureAwait(false);
                return new AzureDevOpsCoordinatedRun(runId, true, configuration.BuildId, configuration.ResultsDirectory, runIdFilePath, ownerFilePath, participantFilePath);
            }

            AzureDevOpsRunIdFile? runIdFile = await WaitForRunIdFileAsync(runIdFilePath, ownerFilePath, configuration.BuildId, cancellationToken).ConfigureAwait(false);
            if (runIdFile is null)
            {
                ownsOwnerFile = await TryAcquireOwnerAsync(ownerFilePath, configuration.BuildId, cancellationToken).ConfigureAwait(false);
                if (ownsOwnerFile)
                {
                    int runId = await createRunAsync(cancellationToken).ConfigureAwait(false);
                    await WriteRunIdFileAsync(runIdFilePath, configuration, runId, cancellationToken).ConfigureAwait(false);
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

    public async Task RenewLeaseAsync(AzureDevOpsCoordinatedRun coordinatedRun, CancellationToken cancellationToken)
    {
        await WriteLeaseFileAsync(coordinatedRun.ParticipantFilePath, CreateLeaseFile(coordinatedRun.BuildId), overwrite: true, cancellationToken).ConfigureAwait(false);

        if (coordinatedRun.IsOwner)
        {
            await WriteLeaseFileAsync(coordinatedRun.OwnerFilePath, CreateLeaseFile(coordinatedRun.BuildId), overwrite: true, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task FinalizeRunAsync(AzureDevOpsCoordinatedRun coordinatedRun, Func<CancellationToken, Task> finalizeRunAsync, CancellationToken cancellationToken)
    {
        TryDeleteFile(coordinatedRun.ParticipantFilePath);

        if (!coordinatedRun.IsOwner)
        {
            return;
        }

        DateTimeOffset timeoutAt = _clock.UtcNow + _options.CoordinationFinalizeTimeout;

        while (true)
        {
            string[] participantFiles = _fileSystem.GetFiles(coordinatedRun.ResultsDirectory, GetParticipantSearchPattern(coordinatedRun.BuildId), SearchOption.TopDirectoryOnly);
            participantFiles = CleanupStaleParticipants(participantFiles);

            if (participantFiles.Length == 0)
            {
                break;
            }

            if (_clock.UtcNow >= timeoutAt)
            {
                _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingFinalizeWaitTimedOut, _options.CoordinationFinalizeTimeout, participantFiles.Length));
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
                        if (runIdFile.ExpiresAt <= _clock.UtcNow || runIdFile.BuildId != buildId)
                        {
                            TryDeleteFile(runIdFilePath);
                        }
                        else
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
                    || (ownerLease.Status == LeaseFileStatus.Expired
                        && ownerLease.Lease is { } expiredLease
                        && IsProcessAlive(expiredLease.ProcessId));

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

        if (existing.Status is LeaseFileStatus.NotFound or LeaseFileStatus.Expired)
        {
            TryDeleteFile(ownerFilePath);
            return await TryWriteLeaseFileAsync(ownerFilePath, lease, overwrite: false, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

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
                // Legacy plain-PID lease format: treat as expired so the caller can take over.
                return new LeaseReadResult(LeaseFileStatus.Expired, new AzureDevOpsLeaseFile(processId, 0, DateTimeOffset.MinValue));
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
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}: {ex.Message}");
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
    string ParticipantFilePath);

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
