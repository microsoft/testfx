// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber);

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed class TestProgressState
{
    // Multiple test-host instances can report one attempt concurrently (for example, shards), so all per-execution
    // result, attempt, duration, and discovery state is protected by this lock.
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

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
    private readonly List<string> _discoveredTestDisplayNames = [];
    private int _discoveredTests;
    private int _failedTests;
    private int _passedTests;
    private int _skippedTests;
    private int _retriedFailedTests;
    private int _tryCount;
    private TestNodeResultsState? _testNodeResultsState;
#pragma warning disable IDE0032 // Use auto property - synchronized access requires a backing field.
    private bool _success;
#pragma warning restore IDE0032

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

    public int DiscoveredTests
    {
        get
        {
            lock (_lock)
            {
                return _discoveredTests;
            }
        }

        internal set
        {
            lock (_lock)
            {
                _discoveredTests = value;
            }
        }
    }

    public int FailedTests
    {
        get
        {
            lock (_lock)
            {
                return _failedTests;
            }
        }
    }

    public int PassedTests
    {
        get
        {
            lock (_lock)
            {
                return _passedTests;
            }
        }
    }

    public int SkippedTests
    {
        get
        {
            lock (_lock)
            {
                return _skippedTests;
            }
        }
    }

    /// <summary>Gets the number of tests whose earlier-attempt failure was superseded by a later retry; rendered as the "/r{N}" segment.</summary>
    public int RetriedFailedTests
    {
        get
        {
            lock (_lock)
            {
                return _retriedFailedTests;
            }
        }
    }

    /// <summary>Gets the total number of tests: the discovered count in discovery mode, otherwise the live passed/skipped/failed tally.</summary>
    public int TotalTests
    {
        get
        {
            lock (_lock)
            {
                return IsDiscovery ? _discoveredTests : _passedTests + _skippedTests + _failedTests;
            }
        }
    }

    public TestNodeResultsState? TestNodeResultsState
    {
        get
        {
            lock (_lock)
            {
                return _testNodeResultsState;
            }
        }

        internal set
        {
            lock (_lock)
            {
                _testNodeResultsState = value;
            }
        }
    }

    public int SlotIndex { get; internal set; }

    public long Id { get; internal set; }

    public long Version { get; internal set; }

    public List<string> DiscoveredTestDisplayNames
    {
        get
        {
            lock (_lock)
            {
                return [.. _discoveredTestDisplayNames];
            }
        }

        internal set
        {
            lock (_lock)
            {
                _discoveredTestDisplayNames.Clear();
                _discoveredTestDisplayNames.AddRange(value);
            }
        }
    }

    public bool IsDiscovery { get; }

    /// <summary>Gets or sets a value indicating whether the assembly run completed successfully (set by the orchestrator on completion).</summary>
    public bool Success
    {
        get
        {
            lock (_lock)
            {
                return _success;
            }
        }

        internal set
        {
            lock (_lock)
            {
                _success = value;
            }
        }
    }

    /// <summary>Gets the highest attempt number seen for this assembly; greater than 1 indicates retries.</summary>
    public int TryCount
    {
        get
        {
            lock (_lock)
            {
                return _tryCount;
            }
        }
    }

    public void ReportPassingTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Passed = entry.Passed + 1 }, static @this => @this._passedTests++);

    public void ReportSkippedTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Skipped = entry.Skipped + 1 }, static @this => @this._skippedTests++);

    public void ReportFailedTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Failed = entry.Failed + 1 }, static @this => @this._failedTests++);

    internal void ReportDiscoveredTest(string? displayName)
    {
        lock (_lock)
        {
            _discoveredTests++;
            if (displayName is not null)
            {
                _discoveredTestDisplayNames.Add(displayName);
            }
        }
    }

    internal TestNodeResultsState GetOrCreateTestNodeResultsState(Func<TestNodeResultsState> factory)
    {
        lock (_lock)
        {
            return _testNodeResultsState ??= factory();
        }
    }

    /// <summary>
    /// Records (or clears) the last-seen duration reported for a test node so it can be ranked in the "slowest
    /// tests" summary section. Keyed by <paramref name="testNodeUid"/> so a retry (which re-reports the same uid)
    /// replaces the earlier attempt's timing rather than adding a duplicate entry. A <see langword="null"/>
    /// <paramref name="duration"/> means the latest attempt reported no timing, so the earlier attempt's stale
    /// duration is removed rather than kept. Only invoked when the slowest-tests feature is enabled.
    /// </summary>
    public void RecordTestDuration(string testNodeUid, string displayName, TimeSpan? duration)
    {
        lock (_lock)
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
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> recorded tests ordered from slowest to fastest. Ties are broken by
    /// display name (ordinal) so the ranking is deterministic for snapshot-based tests.
    /// </summary>
    public IReadOnlyList<(string DisplayName, TimeSpan Duration)> GetSlowestTests(int count)
    {
        lock (_lock)
        {
            return count <= 0 || _testUidToDuration.Count == 0
                ? []
                : [.. _testUidToDuration.Values
                    .OrderByDescending(static entry => entry.Duration)
                    .ThenBy(static entry => entry.DisplayName, StringComparer.Ordinal)
                    .Take(count)];
        }
    }

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
        lock (_lock)
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
                resolvedAttemptNumber = _tryCount + 1;
            }

            _instanceIdToAttemptNumber.Add(instanceId, resolvedAttemptNumber);
            _tryCount = Math.Max(_tryCount, resolvedAttemptNumber);
            return resolvedAttemptNumber;
        }
    }

    private void ReportGenericTestResult(
        string testNodeUid,
        string instanceId,
        Func<TestNodeInfoEntry, TestNodeInfoEntry> incrementTestNodeInfoEntry,
        Action<TestProgressState> incrementCountAction)
    {
        lock (_lock)
        {
            int currentAttemptNumber = GetAttemptNumberCore(instanceId);

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
                    _retriedFailedTests += value.Failed;
                    _passedTests -= value.Passed;
                    _skippedTests -= value.Skipped;
                    _failedTests -= value.Failed;
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
    }

    internal int GetAttemptNumber(string instanceId)
    {
        lock (_lock)
        {
            return GetAttemptNumberCore(instanceId);
        }
    }

    private int GetAttemptNumberCore(string instanceId)
        => _instanceIdToAttemptNumber.TryGetValue(instanceId, out int attemptNumber)
            ? attemptNumber
            : throw ApplicationStateGuard.Unreachable();
}
