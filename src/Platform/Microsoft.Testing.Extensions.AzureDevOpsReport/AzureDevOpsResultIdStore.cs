// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

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
internal sealed partial class AzureDevOpsResultIdStore
{
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
}
