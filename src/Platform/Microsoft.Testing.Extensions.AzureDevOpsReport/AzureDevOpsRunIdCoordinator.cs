// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsRunIdCoordinator
{
    private const string CoordinationFilePrefix = "azdo-runid";

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
        using IFileStream coordinationLock = await AcquireCoordinationLockAsync(configuration.ResultsDirectory, configuration.BuildId, cancellationToken).ConfigureAwait(false);
        await RecoverPendingCleanupAsync(coordinationLock, configuration, ownerFilePath, runIdFilePath, cancellationToken).ConfigureAwait(false);
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
                try
                {
                    await MarkCleanupPendingAsync(coordinationLock).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Preserve the acquisition failure even if its recovery marker cannot be written.
                    TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToWriteCoordinationFile} {coordinationLock.Name}: {ex.Message}");
                }

                TryDeleteFile(ownerFilePath);
                TryDeleteFile(runIdFilePath);
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
            // Admission and the last participant check must be atomic with completion. Otherwise a
            // newly scheduled module can acquire the run after this check but before it is closed.
            using (IFileStream coordinationLock = await AcquireCoordinationLockAsync(coordinatedRun.ResultsDirectory, coordinatedRun.BuildId, cancellationToken).ConfigureAwait(false))
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
                    await CompleteRunAsync(coordinatedRun, coordinationLock, finalizeRunAsync, cancellationToken).ConfigureAwait(false);
                    return;
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
                    // A diagnostic failure must not prevent closing the run.
                    TryLogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingFinalizeWaitTimedOut, timeout, participantFiles.Length));
                    await CompleteRunAsync(coordinatedRun, coordinationLock, finalizeRunAsync, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            await _task.Delay(_options.CoordinationReadRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CompleteRunAsync(AzureDevOpsCoordinatedRun coordinatedRun, IFileStream coordinationLock, Func<CancellationToken, Task> finalizeRunAsync, CancellationToken cancellationToken)
    {
        // A deletion can fail independently of the other file. Persist the closing state before
        // completing the run so surviving files cannot admit a peer to a completed run.
        await MarkCleanupPendingAsync(coordinationLock).ConfigureAwait(false);
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
}
