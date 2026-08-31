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
/// Remembers which Azure DevOps result id a test was published under, and the attempts published so far,
/// so that a later attempt of the same test updates that result instead of adding a second one.
/// </summary>
/// <remarks>
/// <para>
/// The mapping has to cross process boundaries: each <c>--retry-failed-tests</c> attempt runs in its own
/// test host, so the process that publishes attempt 2 is not the one that published attempt 1. Environment
/// variables cannot carry it because they are fixed at launch and flow parent-to-child, whereas this is
/// sibling-to-sibling and produced after launch. A file is therefore the only viable channel.
/// </para>
/// <para>
/// The file deliberately does not live in <see cref="AzureDevOpsPublishConfiguration.ResultsDirectory"/>:
/// the retry orchestrator gives every attempt its own results directory, so a file written there would be
/// invisible to the next attempt. The orchestrator picks the path instead and hands it down, which also
/// scopes the map to one orchestration — two test hosts that merely run concurrently (for example the same
/// assembly multi-targeted) get different maps and keep publishing independent results, exactly as before.
/// </para>
/// <para>
/// Reads and writes are tolerant by design. Anything unreadable is treated as an empty map, which degrades
/// to today's behaviour of one result per attempt rather than losing a result. Writes go through a
/// temporary file so an interrupted write cannot leave a torn map behind.
/// </para>
/// <para>
/// Not thread-safe: the publisher only touches it while holding its flush semaphore, and the attempts of
/// one orchestration are sequential, so there is never more than one writer.
/// </para>
/// </remarks>
internal sealed class AzureDevOpsResultIdStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly string _filePath;
    private readonly int _buildId;
    private readonly int _runId;

    // Keyed by (storage, name, title): the fully-qualified automated test name can be shared by several
    // folded data-driven rows. The title carries the row identity within that test application.
    private readonly Dictionary<string, AzureDevOpsPublishedResult> _results = [];
    private readonly HashSet<string> _ambiguousKeys = [];

    private bool _hasUnsavedChanges;
    private bool _hasAdvancedExistingHistory;
    private bool _canPersist = true;

    private AzureDevOpsResultIdStore(IFileSystem fileSystem, ILogger logger, string filePath, int buildId, int runId)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _filePath = filePath;
        _buildId = buildId;
        _runId = runId;
    }

    /// <summary>
    /// Opens the store at <paramref name="filePath"/>, reading any state earlier attempts left behind.
    /// </summary>
    public static async Task<AzureDevOpsResultIdStore> OpenAsync(IFileSystem fileSystem, ILogger logger, string filePath, int buildId, int runId)
    {
        AzureDevOpsResultIdStore store = new(fileSystem, logger, filePath, buildId, runId);
        await store.LoadAsync().ConfigureAwait(false);
        return store;
    }

    /// <summary>
    /// Returns what is already published for a test, or <see langword="null"/> when this build has not seen it.
    /// </summary>
    public AzureDevOpsPublishedResult? TryGet(AzureDevOpsTestCaseResult result)
        => _results.TryGetValue(CreateKey(result.AutomatedTestStorage, result.AutomatedTestName, result.TestCaseTitle), out AzureDevOpsPublishedResult? published)
            ? published
            : null;

    /// <summary>
    /// Records the result id Azure DevOps assigned to a newly created result, along with its first attempt.
    /// </summary>
    public void RecordCreated(AzureDevOpsTestCaseResult result, int resultId)
        => RecordCreated(result, resultId, [result]);

    /// <summary>
    /// Records the result id Azure DevOps assigned to a newly created result, along with every execution
    /// already performed by an in-process retry.
    /// </summary>
    public void RecordCreated(AzureDevOpsTestCaseResult result, int resultId, IReadOnlyList<AzureDevOpsTestCaseResult> attempts)
    {
        string key = CreateKey(result.AutomatedTestStorage, result.AutomatedTestName, result.TestCaseTitle);
        if (_ambiguousKeys.Contains(key))
        {
            return;
        }

        if (_results.Remove(key))
        {
            // A folded data-driven test may legally give two rows the same display name. The platform does
            // not expose a stable row id that survives the next process, and matching by completion order
            // could attach history to the wrong row. Forget both mappings so later attempts use safe POSTs.
            _ambiguousKeys.Add(key);
            _hasUnsavedChanges = true;
            return;
        }

        _results[key] = new AzureDevOpsPublishedResult(
            result.AutomatedTestStorage,
            result.AutomatedTestName,
            result.TestCaseTitle,
            resultId,
            CreateAttempts(attempts, firstSequenceId: 1))
        {
            TotalDurationInMs = SumResultDurations(attempts),
            StartedDate = GetEarliestStartedDate(attempts),
            CompletedDate = GetLatestCompletedDate(attempts),
        };
        _hasUnsavedChanges = true;
    }

    /// <summary>
    /// Records the highest attempt sequence Azure DevOps has accepted as a sub-result for a newly created result.
    /// </summary>
    public void RecordPublishedSubResults(AzureDevOpsTestCaseResult result, int resultId, int lastPublishedSubResultSequenceId)
    {
        string key = CreateKey(result.AutomatedTestStorage, result.AutomatedTestName, result.TestCaseTitle);
        if (_results.TryGetValue(key, out AzureDevOpsPublishedResult? published) && published.Id == resultId)
        {
            _results[key] = published with { LastPublishedSubResultSequenceId = lastPublishedSubResultSequenceId };
            _hasUnsavedChanges = true;
        }
    }

    public static AzureDevOpsTestSubResult CreateFirstAttempt(AzureDevOpsTestCaseResult result)
        => ToSubResult(result, sequenceId: 1);

    public static IReadOnlyList<AzureDevOpsTestSubResult> CreateAttempts(
        IReadOnlyList<AzureDevOpsTestCaseResult> results,
        int firstSequenceId)
    {
        var attempts = new List<AzureDevOpsTestSubResult>(results.Count);
        for (int i = 0; i < results.Count; i++)
        {
            attempts.Add(ToSubResult(results[i], firstSequenceId + i));
        }

        TrimAttempts(attempts);
        return attempts;
    }

    /// <summary>
    /// Builds what the attempt history would become if <paramref name="result"/> were published, without
    /// recording it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="RecordAttempts"/> so the map is only advanced once Azure DevOps has
    /// accepted the update. Recording first would double-count the attempt when a failed update is retried,
    /// leaving the same execution listed twice under the test.
    /// </remarks>
    public static IReadOnlyList<AzureDevOpsTestSubResult> BuildNextAttempts(AzureDevOpsPublishedResult published, AzureDevOpsTestCaseResult result)
        => BuildNextAttempts(published, [result]);

    public static IReadOnlyList<AzureDevOpsTestSubResult> BuildNextAttempts(
        AzureDevOpsPublishedResult published,
        IReadOnlyList<AzureDevOpsTestCaseResult> results)
    {
        int nextSequenceId = published.Attempts.Count == 0 ? 1 : published.Attempts[^1].SequenceId + 1;
        List<AzureDevOpsTestSubResult> attempts = [.. published.Attempts, .. CreateAttempts(results, nextSequenceId)];

        TrimAttempts(attempts);

        return attempts;
    }

    /// <summary>
    /// Returns the total duration after <paramref name="result"/> is added, including attempts no longer
    /// retained in the capped sub-result list.
    /// </summary>
    public static long? BuildNextTotalDuration(AzureDevOpsPublishedResult published, AzureDevOpsTestCaseResult result)
        => AddDurations(published.TotalDurationInMs ?? SumDurations(published.Attempts), result.DurationInMs);

    public static long? BuildNextTotalDuration(
        AzureDevOpsPublishedResult published,
        IReadOnlyList<AzureDevOpsTestCaseResult> results)
        => AddDurations(published.TotalDurationInMs ?? SumDurations(published.Attempts), SumResultDurations(results));

    public static long? SumResultDurations(IReadOnlyList<AzureDevOpsTestCaseResult> results)
    {
        long? total = null;
        foreach (AzureDevOpsTestCaseResult result in results)
        {
            total = AddDurations(total, result.DurationInMs);
        }

        return total;
    }

    public static DateTimeOffset? GetEarliestStartedDate(IReadOnlyList<AzureDevOpsTestCaseResult> attempts)
        => attempts.Where(attempt => attempt.StartedDate is not null).Min(attempt => attempt.StartedDate);

    public static DateTimeOffset? GetLatestCompletedDate(IReadOnlyList<AzureDevOpsTestCaseResult> attempts)
        => attempts.Where(attempt => attempt.CompletedDate is not null).Max(attempt => attempt.CompletedDate);

    /// <summary>
    /// Records an attempt history that Azure DevOps has accepted.
    /// </summary>
    public void RecordAttempts(
        AzureDevOpsPublishedResult published,
        IReadOnlyList<AzureDevOpsTestSubResult> attempts,
        int lastPublishedSubResultSequenceId,
        long? totalDurationInMs,
        DateTimeOffset? startedDate,
        DateTimeOffset? completedDate)
    {
        _results[CreateKey(published.Storage, published.Name, published.Title)] = published with
        {
            Attempts = attempts,
            LastPublishedSubResultSequenceId = lastPublishedSubResultSequenceId,
            TotalDurationInMs = totalDurationInMs,
            StartedDate = startedDate,
            CompletedDate = completedDate,
        };
        _hasUnsavedChanges = true;
        _hasAdvancedExistingHistory = true;
    }

    /// <summary>
    /// Removes history that can no longer be safely persisted after an ambiguous PATCH failure.
    /// </summary>
    /// <remarks>
    /// The persisted map was deleted before PATCH. If the request then fails after reaching Azure DevOps,
    /// retaining the pre-PATCH history would let a later save resurrect stale state and overwrite an
    /// accepted attempt. The result remains in Azure DevOps; forgetting it here makes the next publish use
    /// a separate POST instead.
    /// </remarks>
    public void Forget(AzureDevOpsPublishedResult published)
        => _results.Remove(CreateKey(published.Storage, published.Name, published.Title));

    /// <summary>
    /// Removes the persisted map before an existing result is updated.
    /// </summary>
    /// <remarks>
    /// PATCH changes server history in place. If the process dies after Azure DevOps accepts it but before
    /// the updated map is saved, an old map would let the next attempt replace that history with stale data.
    /// Deleting first closes that crash window. When deletion is impossible the caller must avoid PATCH and
    /// create a separate result instead — duplicates are preferable to losing an accepted attempt.
    /// </remarks>
    public bool TryInvalidatePersistedMap()
    {
        try
        {
            // DeleteFile is a no-op when the file is absent. Do not probe with ExistFile first: File.Exists
            // also returns false for access and path errors, which would fail this safety gate open and
            // allow PATCH to proceed while stale history may still be present.
            _fileSystem.DeleteFile(_filePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {_filePath}: {ex.Message}");
            return false;
        }
    }

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

    private static bool IsValidAttemptHistory([NotNullWhen(true)] IReadOnlyList<AzureDevOpsTestSubResult>? attempts)
    {
        if (attempts is null
            || attempts.Count == 0
            || attempts.Count > AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult)
        {
            return false;
        }

        int previousSequenceId = 0;
        foreach (AzureDevOpsTestSubResult attempt in attempts)
        {
            if (attempt is null
                || attempt.SequenceId <= previousSequenceId
                || attempt.SequenceId == int.MaxValue
                || attempt.DisplayName is null
                || attempt.DurationInMs is < 0
                || attempt.Outcome is not (
                    AzureDevOpsLivePublishingConstants.PassedTestOutcome
                    or AzureDevOpsLivePublishingConstants.FailedTestOutcome
                    or AzureDevOpsLivePublishingConstants.NotExecutedTestOutcome
                    or AzureDevOpsLivePublishingConstants.AbortedTestOutcome))
            {
                return false;
            }

            previousSequenceId = attempt.SequenceId;
        }

        return true;
    }

    private static long? SumDurations(IReadOnlyList<AzureDevOpsTestSubResult> attempts)
    {
        long? total = null;
        foreach (AzureDevOpsTestSubResult attempt in attempts)
        {
            total = AddDurations(total, attempt.DurationInMs);
        }

        return total;
    }

    private static long? AddDurations(long? left, long? right)
        => left is null
            ? right
            : right is null
                ? left
                : right.Value > long.MaxValue - left.Value ? long.MaxValue : left.Value + right.Value;

    private static DateTimeOffset? GetEarliestStartedDate(IReadOnlyList<AzureDevOpsTestSubResult> attempts)
        => attempts.Where(attempt => attempt.StartedDate is not null).Min(attempt => attempt.StartedDate);

    private static DateTimeOffset? GetLatestCompletedDate(IReadOnlyList<AzureDevOpsTestSubResult> attempts)
        => attempts.Where(attempt => attempt.CompletedDate is not null).Max(attempt => attempt.CompletedDate);

    private static void TrimAttempts(List<AzureDevOpsTestSubResult> attempts)
    {
        // Azure DevOps caps sub-results per result; keep the most recent attempts because they are the ones
        // that explain the parent outcome. A retry sequence never gets close to this.
        if (attempts.Count > AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult)
        {
            attempts.RemoveRange(0, attempts.Count - AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult);
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

/// <summary>A test already published to the run, with every attempt seen so far.</summary>
internal sealed record AzureDevOpsPublishedResult(
    string Storage,
    string Name,
    string Title,
    int Id,
    IReadOnlyList<AzureDevOpsTestSubResult> Attempts)
{
    public int LastPublishedSubResultSequenceId { get; init; }

    public long? TotalDurationInMs { get; init; }

    public DateTimeOffset? StartedDate { get; init; }

    public DateTimeOffset? CompletedDate { get; init; }
}

/// <summary>
/// On-disk shape of one map entry.
/// </summary>
/// <remarks>
/// Carries its key as ordinary fields rather than being a JSON object keyed by test name, because a test
/// uid can contain any character and would otherwise need escaping. Every member is nullable because the
/// file is untrusted input.
/// </remarks>
internal sealed record AzureDevOpsResultMapEntry(
    [property: JsonPropertyName("storage")] string? Storage,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("attempts")] IReadOnlyList<AzureDevOpsTestSubResult>? Attempts)
{
    [JsonPropertyName("lastPublishedSubResultSequenceId")]
    public int? LastPublishedSubResultSequenceId { get; init; }

    [JsonPropertyName("totalDurationInMs")]
    public long? TotalDurationInMs { get; init; }

    [JsonPropertyName("startedDate")]
    public DateTimeOffset? StartedDate { get; init; }

    [JsonPropertyName("completedDate")]
    public DateTimeOffset? CompletedDate { get; init; }
}

/// <summary>
/// On-disk shape of the result map.
/// </summary>
/// <remarks>
/// Unknown members are ignored on read, so additive fields remain forward-compatible. The build and run
/// ids are required discriminators: a map that predates either one is deliberately ignored, because result
/// ids are only meaningful within the run that created them.
/// </remarks>
internal sealed record AzureDevOpsResultMapFile(
    [property: JsonPropertyName("buildId")] int BuildId,
    [property: JsonPropertyName("runId")] int RunId,
    [property: JsonPropertyName("results")] IReadOnlyList<AzureDevOpsResultMapEntry>? Results);
