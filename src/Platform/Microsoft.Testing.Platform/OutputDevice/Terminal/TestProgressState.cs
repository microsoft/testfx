// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber);

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed class TestProgressState
{
    // THREADING: this type is intentionally not internally synchronized. Each TestProgressState instance is owned by
    // a single executionId; the reporter looks it up from a ConcurrentDictionary (_assemblies), but the Microsoft
    // Testing Platform message pipeline delivers events for a given executionId on a single consumer, so the mutating
    // members below (the dictionaries and the Passed/Skipped/Failed/Retried/TryCount counters) are only ever
    // touched by one thread at a time for a given instance. Do not call these members concurrently for the same
    // assembly without adding synchronization.

    // Tracks the per-test-node tally and the attempt it belongs to, so retries (which re-report the same test node
    // uid under a new instance id) replace rather than double-count the earlier attempt's result.
    private readonly Dictionary<string, TestNodeInfoEntry> _testUidToResults = [];

    // Maps each instance id seen for this assembly to its 1-based attempt number. New protocol peers report the
    // attempt explicitly, allowing multiple instances (for example, shards) to belong to the same attempt. Legacy
    // peers omit it, in which case each new instance is inferred to be the next retry attempt.
    private readonly Dictionary<string, int> _instanceIdToAttemptNumber = [];

    // Records the last-seen (display name, duration) for every test node, keyed by test node uid, so the
    // "slowest tests" summary section can rank them. Keyed by uid (not appended to a list) so a retried test
    // replaces its earlier attempt's timing instead of appearing twice, mirroring the pass/fail tally above.
    // Only populated when the slowest-tests feature is enabled (the reporter gates the RecordTestDuration call),
    // so a run without the feature pays no memory cost here.
    private readonly Dictionary<string, (string DisplayName, TimeSpan Duration)> _testUidToDuration = [];

    public TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch, bool isDiscovery)
    {
        Id = id;
        TargetFramework = targetFramework;
        Architecture = architecture;
        Stopwatch = stopwatch;
        Assembly = assembly;
        AssemblyName = Path.GetFileName(assembly);
        IsDiscovery = isDiscovery;
    }

    /// <summary>Gets the assembly path or display name as provided by the caller (used for the summary link).</summary>
    public string Assembly { get; }

    public string AssemblyName { get; }

    public string? TargetFramework { get; }

    public string? Architecture { get; }

    public IStopwatch Stopwatch { get; }

    public int DiscoveredTests { get; internal set; }

    public int FailedTests { get; private set; }

    public int PassedTests { get; private set; }

    public int SkippedTests { get; private set; }

    /// <summary>Gets the number of tests whose earlier-attempt failure was superseded by a later retry; rendered as the "/r{N}" segment.</summary>
    public int RetriedFailedTests { get; private set; }

    /// <summary>Gets the total number of tests: the discovered count in discovery mode, otherwise the live passed/skipped/failed tally.</summary>
    public int TotalTests => IsDiscovery ? DiscoveredTests : PassedTests + SkippedTests + FailedTests;

    public TestNodeResultsState? TestNodeResultsState { get; internal set; }

    public int SlotIndex { get; internal set; }

    public long Id { get; internal set; }

    public long Version { get; internal set; }

    public List<string> DiscoveredTestDisplayNames { get; internal set; } = [];

    public bool IsDiscovery { get; }

    /// <summary>Gets or sets a value indicating whether the assembly run completed successfully (set by the orchestrator on completion).</summary>
    public bool Success { get; internal set; }

    /// <summary>Gets the highest attempt number seen for this assembly; greater than 1 indicates retries.</summary>
    public int TryCount { get; private set; }

    public void ReportPassingTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Passed = entry.Passed + 1 }, static @this => @this.PassedTests++);

    public void ReportSkippedTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Skipped = entry.Skipped + 1 }, static @this => @this.SkippedTests++);

    public void ReportFailedTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Failed = entry.Failed + 1 }, static @this => @this.FailedTests++);

    /// <summary>
    /// Records (or clears) the last-seen duration reported for a test node so it can be ranked in the "slowest
    /// tests" summary section. Keyed by <paramref name="testNodeUid"/> so a retry (which re-reports the same uid)
    /// replaces the earlier attempt's timing rather than adding a duplicate entry. A <see langword="null"/>
    /// <paramref name="duration"/> means the latest attempt reported no timing, so the earlier attempt's stale
    /// duration is removed rather than kept. Only invoked when the slowest-tests feature is enabled.
    /// </summary>
    public void RecordTestDuration(string testNodeUid, string displayName, TimeSpan? duration)
    {
        if (duration.HasValue)
        {
            _testUidToDuration[testNodeUid] = (displayName, duration.Value);
        }
        else
        {
            _testUidToDuration.Remove(testNodeUid);
        }
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> recorded tests ordered from slowest to fastest. Ties are broken by
    /// display name (ordinal) so the ranking is deterministic for snapshot-based tests.
    /// </summary>
    public IReadOnlyList<(string DisplayName, TimeSpan Duration)> GetSlowestTests(int count)
        => count <= 0 || _testUidToDuration.Count == 0
            ? []
            : [.. _testUidToDuration.Values
                .OrderByDescending(static entry => entry.Duration)
                .ThenBy(static entry => entry.DisplayName, StringComparer.Ordinal)
                .Take(count)];

    /// <summary>
    /// Registers a legacy handshake for the given <paramref name="instanceId"/>. A previously unseen instance id is
    /// inferred to be a new retry attempt; re-seeing an instance id is a no-op.
    /// </summary>
    internal void NotifyHandshake(string instanceId)
        => NotifyHandshakeCore(instanceId, attemptNumber: null);

    /// <summary>
    /// Registers a handshake with an explicit 1-based <paramref name="attemptNumber"/>. Different instances can
    /// belong to the same attempt, while one instance cannot change attempts after it has been registered.
    /// </summary>
    internal void NotifyHandshake(string instanceId, int attemptNumber)
        => NotifyHandshakeCore(instanceId, attemptNumber);

    private int NotifyHandshakeCore(string instanceId, int? attemptNumber)
    {
        if (_instanceIdToAttemptNumber.TryGetValue(instanceId, out int registeredAttemptNumber))
        {
            return !attemptNumber.HasValue || attemptNumber.Value == registeredAttemptNumber
                ? registeredAttemptNumber
                : throw ApplicationStateGuard.Unreachable();
        }

        int resolvedAttemptNumber;
        if (attemptNumber.HasValue)
        {
            if (attemptNumber.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber));
            }

            resolvedAttemptNumber = attemptNumber.Value;
        }
        else
        {
            resolvedAttemptNumber = TryCount + 1;
        }

        _instanceIdToAttemptNumber.Add(instanceId, resolvedAttemptNumber);
        TryCount = Math.Max(TryCount, resolvedAttemptNumber);
        return resolvedAttemptNumber;
    }

    private void ReportGenericTestResult(
        string testNodeUid,
        string instanceId,
        Func<TestNodeInfoEntry, TestNodeInfoEntry> incrementTestNodeInfoEntry,
        Action<TestProgressState> incrementCountAction)
    {
        int currentAttemptNumber = GetAttemptNumber(instanceId);

        if (_testUidToResults.TryGetValue(testNodeUid, out TestNodeInfoEntry value))
        {
            if (value.LastAttemptNumber == currentAttemptNumber)
            {
                // Another result for the same test node in the same attempt — just increment.
                _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry(value);
            }
            else if (currentAttemptNumber > value.LastAttemptNumber)
            {
                // Retry: discard the previous attempt's contribution to the live tally and re-count this attempt.
                RetriedFailedTests += value.Failed;
                PassedTests -= value.Passed;
                SkippedTests -= value.Skipped;
                FailedTests -= value.Failed;
                _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber));
            }
            else
            {
                // A result for an attempt older than the latest one we saw — unexpected ordering.
                throw ApplicationStateGuard.Unreachable();
            }
        }
        else
        {
            _testUidToResults.Add(testNodeUid, incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber)));
        }

        incrementCountAction(this);
    }

    internal int GetAttemptNumber(string instanceId)
        => _instanceIdToAttemptNumber.TryGetValue(instanceId, out int attemptNumber)
            ? attemptNumber
            : throw ApplicationStateGuard.Unreachable();
}
