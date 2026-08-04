// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    // Keyed by (storage, name) because automatedTestName is TestNode.Uid.Value, which is only unique
    // within a test application: two assemblies in one build can legitimately use the same uid.
    private readonly Dictionary<string, AzureDevOpsPublishedResult> _results = [];

    private bool _hasUnsavedChanges;
    private bool _hasAdvancedExistingHistory;

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
        => _results.TryGetValue(CreateKey(result.AutomatedTestStorage, result.AutomatedTestName), out AzureDevOpsPublishedResult? published)
            ? published
            : null;

    /// <summary>
    /// Records the result id Azure DevOps assigned to a newly created result, along with its first attempt.
    /// </summary>
    public void RecordCreated(AzureDevOpsTestCaseResult result, int resultId)
    {
        _results[CreateKey(result.AutomatedTestStorage, result.AutomatedTestName)] =
            new AzureDevOpsPublishedResult(result.AutomatedTestStorage, result.AutomatedTestName, resultId, [ToSubResult(result, sequenceId: 1)]);
        _hasUnsavedChanges = true;
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
    {
        int nextSequenceId = published.Attempts.Count == 0 ? 1 : published.Attempts[^1].SequenceId + 1;
        List<AzureDevOpsTestSubResult> attempts = [.. published.Attempts, ToSubResult(result, nextSequenceId)];

        // Azure DevOps caps sub-results per result; keep the most recent attempts because they are the ones
        // that explain the parent outcome. A retry sequence never gets close to this.
        if (attempts.Count > AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult)
        {
            attempts.RemoveRange(0, attempts.Count - AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult);
        }

        return attempts;
    }

    /// <summary>
    /// Records an attempt history that Azure DevOps has accepted.
    /// </summary>
    public void RecordAttempts(AzureDevOpsPublishedResult published, IReadOnlyList<AzureDevOpsTestSubResult> attempts)
    {
        _results[CreateKey(published.Storage, published.Name)] = published with { Attempts = attempts };
        _hasUnsavedChanges = true;
        _hasAdvancedExistingHistory = true;
    }

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
        if (!_fileSystem.ExistFile(_filePath))
        {
            return true;
        }

        try
        {
            _fileSystem.DeleteFile(_filePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
        if (!_hasUnsavedChanges)
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
                entries[index++] = new AzureDevOpsResultMapEntry(published.Storage, published.Name, published.Id, published.Attempts);
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

    private static AzureDevOpsTestSubResult ToSubResult(AzureDevOpsTestCaseResult result, int sequenceId)
        => new(
            sequenceId,
            result.TestCaseTitle,
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

            // Result ids are scoped by run, not only by build. A map left behind by another run would
            // address unrelated results even when it belongs to the same build (for example after a
            // crashed orchestration whose process id was later reused). Start over instead.
            if (map?.Results is null || map.BuildId != _buildId || map.RunId != _runId)
            {
                return;
            }

            foreach (AzureDevOpsResultMapEntry entry in map.Results)
            {
                // Entries come from disk, so nothing about them is guaranteed however the record is
                // annotated: a truncated or hand-edited file can yield nulls and non-positive ids.
                if (entry is { Id: > 0, Storage: { Length: > 0 } storage, Name: { Length: > 0 } name }
                    && IsValidAttemptHistory(entry.Attempts))
                {
                    _results[CreateKey(storage, name)] = new AzureDevOpsPublishedResult(storage, name, entry.Id, entry.Attempts);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
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
                || attempt.DisplayName is null
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

    private void TryDeleteFile(string path)
    {
        try
        {
            if (_fileSystem.ExistFile(path))
            {
                _fileSystem.DeleteFile(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
    private static string CreateKey(string storage, string name)
        => $"{storage.Length.ToString(CultureInfo.InvariantCulture)}:{storage.ToLowerInvariant()}:{name}";
}

/// <summary>A test already published to the run, with every attempt seen so far.</summary>
internal sealed record AzureDevOpsPublishedResult(
    string Storage,
    string Name,
    int Id,
    IReadOnlyList<AzureDevOpsTestSubResult> Attempts);

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
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("attempts")] IReadOnlyList<AzureDevOpsTestSubResult>? Attempts);

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
