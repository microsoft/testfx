// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsRunIdCoordinator
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private async Task<IFileStream> AcquireCoordinationLockAsync(string resultsDirectory, int buildId, CancellationToken cancellationToken)
    {
        string path = Path.Combine(resultsDirectory, $"{CoordinationFilePrefix}.{buildId}.lock");
        DateTimeOffset deadline = _clock.UtcNow + _options.CoordinationJoinerMaxWaitTime;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Keep the file: deleting it can give waiters and a new owner different file identities.
                // The exclusive handle, not its contents or age, owns the gate; process exit releases it.
                // Nonempty contents record pending cleanup, not lock ownership.
                return _fileSystem.NewFileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                if (_clock.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingCoordinationLockTimedOut, path, _options.CoordinationJoinerMaxWaitTime),
                        ex);
                }
            }

            await _task.Delay(_options.CoordinationReadRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task MarkCleanupPendingAsync(IFileStream coordinationLock)
    {
        coordinationLock.Stream.SetLength(1);
        await coordinationLock.Stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RecoverPendingCleanupAsync(IFileStream coordinationLock, AzureDevOpsPublishConfiguration configuration, string ownerFilePath, string runIdFilePath, CancellationToken cancellationToken)
    {
        if (coordinationLock.Stream.Length == 0)
        {
            return;
        }

        // Keep the marker until the old generation's files are gone. Retrying under the gate neither joins the
        // completed run nor lets an earlier owner's cleanup delete a successor's files.
        DateTimeOffset deadline = _clock.UtcNow + _options.CoordinationJoinerMaxWaitTime;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // File.Exists can report false for access errors; require the deletes themselves to
            // succeed before clearing the marker. Deleting a nonexistent file is a no-op.
            bool ownerDeleted = TryDeleteFile(ownerFilePath, checkExistence: false);
            bool runIdDeleted = TryDeleteFile(runIdFilePath, checkExistence: false);
            string? undeletedParticipant = null;
            foreach (string participantFilePath in _fileSystem.GetFiles(configuration.ResultsDirectory, GetParticipantSearchPattern(configuration.BuildId), SearchOption.TopDirectoryOnly))
            {
                // No successor can register under this gate. Any surviving participants belong to
                // the closing run and must not make its successor wait on publishers that already left.
                if (!TryDeleteFile(participantFilePath, checkExistence: false))
                {
                    undeletedParticipant = participantFilePath;
                }
            }

            if (ownerDeleted && runIdDeleted && undeletedParticipant is null)
            {
                coordinationLock.Stream.SetLength(0);
                await coordinationLock.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (_clock.UtcNow >= deadline)
            {
                string path = !ownerDeleted ? ownerFilePath : !runIdDeleted ? runIdFilePath : undeletedParticipant!;
                throw new TimeoutException($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}");
            }

            await _task.Delay(_options.CoordinationReadRetryDelay, cancellationToken).ConfigureAwait(false);
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

    private bool TryDeleteFile(string path, bool checkExistence = true)
    {
        try
        {
            if (!checkExistence || _fileSystem.ExistFile(path))
            {
                _fileSystem.DeleteFile(path);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}: {ex.Message}");
            return false;
        }
    }
}
