// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Reading and writing the on-disk coordination file that carries the map between attempts.
/// </summary>
internal sealed partial class AzureDevOpsResultIdStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Persists the map for the next attempt, tolerating any failure to write.
    /// </summary>
    /// <remarks>
    /// Called once per session rather than after every publish: only another process reads the file, and
    /// the attempts of one orchestration never overlap, so writing it more often would rewrite the whole
    /// map repeatedly for no benefit.
    /// <para>
    /// The results this describes are already in Azure DevOps, so a failed write only costs the next
    /// attempt the ability to merge into them — it publishes its own result instead, which is what happened
    /// before reruns were expressed at all. Failing the run over it would be far worse.
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The map payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The map payload type is internal, fixed, and controlled by this extension.")]
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        // Nothing was published, so there is nothing for the next attempt to merge into. Writing an empty
        // map would only leave a file behind in the results directory.
        if (!_hasUnsavedChanges || !_canPersist)
        {
            return;
        }

        // A temporary file next to the target keeps the move on the same volume, so it stays atomic.
        string temporaryFilePath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            var entries = new AzureDevOpsResultMapEntry[_results.Count];
            int index = 0;
            foreach (AzureDevOpsPublishedResult published in _results.Values)
            {
                entries[index++] = new AzureDevOpsResultMapEntry(published.Storage, published.Name, published.Title, published.Id, published.Attempts)
                {
                    LastPublishedSubResultSequenceId = published.LastPublishedSubResultSequenceId,
                    TotalDurationInMs = published.TotalDurationInMs,
                    StartedDate = published.StartedDate,
                    CompletedDate = published.CompletedDate,
                };
            }

            string json = JsonSerializer.Serialize(new AzureDevOpsResultMapFile(_buildId, _runId, entries), JsonSerializerOptions);

            using (IFileStream stream = _fileSystem.NewFileStream(temporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream.Stream, Utf8EncodingWithoutBom, 1024, leaveOpen: true))
            {
#if NET
                await writer.WriteAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
                await writer.WriteAsync(json).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
#endif
            }

            _fileSystem.ReplaceFile(temporaryFilePath, _filePath);
            _hasUnsavedChanges = false;
            _hasAdvancedExistingHistory = false;
        }
        catch (Exception ex)
        {
            TryDeleteFile(temporaryFilePath);

            // TryInvalidatePersistedMap removes the old map before any PATCH, so this is normally absent.
            // Keep a still-valid old map after create-only changes: the new results may be duplicated on
            // the next attempt, but previously known results can still merge correctly. If an existing
            // history somehow advanced without prior invalidation, remove it rather than risk data loss.
            if (_hasAdvancedExistingHistory)
            {
                TryDeleteFile(_filePath);
            }

            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToWriteCoordinationFile} {_filePath}: {ex.Message}");
        }
    }

    // Match PublishTestResults@2: the original execution is Attempt# 0, while sequence ids are 1-based.
    private static AzureDevOpsTestSubResult ToSubResult(AzureDevOpsTestCaseResult result, int sequenceId)
        => new(
            sequenceId,
            $"Attempt# {(sequenceId - 1).ToString(CultureInfo.InvariantCulture)} - {result.TestCaseTitle}",
            result.Outcome,
            result.DurationInMs,
            result.ErrorMessage,
            result.StackTrace,
            result.StartedDate,
            result.CompletedDate);

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The map payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The map payload type is internal, fixed, and controlled by this extension.")]
    private async Task LoadAsync()
    {
        try
        {
            if (!_fileSystem.ExistFile(_filePath))
            {
                return;
            }

            string content = await _fileSystem.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            AzureDevOpsResultMapFile? map = JsonSerializer.Deserialize<AzureDevOpsResultMapFile>(content, JsonSerializerOptions);

            // Result ids are scoped by run, not only by build. A map left behind by another run is foreign
            // state: ignore it and never overwrite its path when this session later creates results.
            if (map?.Results is null || map.BuildId != _buildId || map.RunId != _runId)
            {
                _canPersist = false;
                return;
            }

            var candidates = new List<(AzureDevOpsResultMapEntry Entry, string Key, long? RetainedDuration)>();
            Dictionary<string, int> keyCounts = [];
            Dictionary<int, int> resultIdCounts = [];
            foreach (AzureDevOpsResultMapEntry? entry in map.Results)
            {
                // Entries come from disk, so nothing about them is guaranteed however the record is
                // annotated: a truncated or hand-edited file can yield nulls and non-positive ids.
                if (entry is null)
                {
                    continue;
                }

                // A malformed key still makes a positive server result id ambiguous. Count IDs before
                // validating any other field so a later well-formed entry cannot claim that same result.
                if (entry.Id > 0)
                {
                    resultIdCounts[entry.Id] = resultIdCounts.TryGetValue(entry.Id, out int idCount) ? idCount + 1 : 1;
                }

                if (entry.Id > 0
                    && entry.Storage is { Length: > 0 } storage
                    && entry.Name is { Length: > 0 } name
                    && entry.Title is { Length: > 0 } title)
                {
                    string key = CreateKey(storage, name, title);
                    keyCounts[key] = keyCounts.TryGetValue(key, out int keyCount) ? keyCount + 1 : 1;

                    // Count usable identities before validating payload fields. An invalid entry still
                    // makes its key and result id ambiguous; accepting another entry that claims either
                    // would guess ownership from untrusted data.
                    if (!IsValidAttemptHistory(entry.Attempts))
                    {
                        continue;
                    }

                    bool hasUnpublishedFirstAttempt = entry.LastPublishedSubResultSequenceId == 0
                        && entry.Attempts.Count == 1
                        && entry.Attempts[0].SequenceId == 1;
                    bool hasFullyPublishedHistory = entry.LastPublishedSubResultSequenceId == entry.Attempts[^1].SequenceId;
                    if (!hasUnpublishedFirstAttempt && !hasFullyPublishedHistory)
                    {
                        continue;
                    }

                    long? retainedDuration = SumDurations(entry.Attempts);
                    if (entry.TotalDurationInMs is < 0
                        || (entry.TotalDurationInMs is { } totalDuration && retainedDuration is { } retained && totalDuration < retained))
                    {
                        continue;
                    }

                    candidates.Add((entry, key, retainedDuration));
                }
            }

            foreach ((AzureDevOpsResultMapEntry entry, string key, long? retainedDuration) in candidates)
            {
                if (keyCounts[key] != 1 || resultIdCounts[entry.Id] != 1)
                {
                    _ambiguousKeys.Add(key);
                    continue;
                }

                _results[key] = new AzureDevOpsPublishedResult(entry.Storage!, entry.Name!, entry.Title!, entry.Id, entry.Attempts!)
                {
                    LastPublishedSubResultSequenceId = entry.LastPublishedSubResultSequenceId!.Value,
                    TotalDurationInMs = entry.TotalDurationInMs ?? retainedDuration,
                    StartedDate = entry.StartedDate ?? GetEarliestStartedDate(entry.Attempts!),
                    CompletedDate = entry.CompletedDate ?? GetLatestCompletedDate(entry.Attempts!),
                };
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            // Degrade to an empty map: every test looks unseen and is published as its own result, which
            // is the behaviour that predates reruns. Losing the merge is acceptable; losing results is not.
            _results.Clear();
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToReadCoordinationFile} {_filePath}: {ex.Message}");
        }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Leaving a temporary file behind is harmless; it is never read.
        }
    }

    /// <summary>
    /// Logs a warning, swallowing any failure from the logging providers, for the same reason the
    /// coordinator does: every caller here is already on a recovery path.
    /// </summary>
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

    /// <summary>
    /// Compares map keys the way Azure DevOps matches the underlying fields: the storage is a file name, so
    /// it is matched case-insensitively, while the test name is matched exactly.
    /// </summary>
    /// <remarks>
    /// The storage is length-prefixed so that the two parts cannot run together: a test uid may contain any
    /// character, including whatever separator we might otherwise pick, and two different tests must never
    /// produce the same key or one would be published as a rerun of the other.
    /// </remarks>
    private static string CreateKey(string storage, string name, string title)
        => $"{storage.Length.ToString(CultureInfo.InvariantCulture)}:{storage.ToLowerInvariant()}:{name.Length.ToString(CultureInfo.InvariantCulture)}:{name}:{title}";
}
