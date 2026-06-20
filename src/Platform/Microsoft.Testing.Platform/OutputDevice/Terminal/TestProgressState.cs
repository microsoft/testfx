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
    // members below (the dictionary/list and the Passed/Skipped/Failed/Retried/TryCount counters) are only ever
    // touched by one thread at a time for a given instance. Do not call these members concurrently for the same
    // assembly without adding synchronization.

    // Tracks the per-test-node tally and the attempt it belongs to, so retries (which re-report the same test node
    // uid under a new instance id) replace rather than double-count the earlier attempt's result.
    private readonly Dictionary<string, TestNodeInfoEntry> _testUidToResults = [];

    // Ordered list of instance ids seen for this assembly. Each new instance id is a retry attempt. In most runs
    // there is exactly one (no retry).
    private readonly List<string> _orderedInstanceIds = [];

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

    /// <summary>Gets the number of attempts (handshakes) seen for this assembly; greater than 1 indicates retries.</summary>
    public int TryCount { get; private set; }

    public void ReportPassingTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Passed = entry.Passed + 1 }, static @this => @this.PassedTests++);

    public void ReportSkippedTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Skipped = entry.Skipped + 1 }, static @this => @this.SkippedTests++);

    public void ReportFailedTest(string testNodeUid, string instanceId)
        => ReportGenericTestResult(testNodeUid, instanceId, static entry => entry with { Failed = entry.Failed + 1 }, static @this => @this.FailedTests++);

    /// <summary>
    /// Registers a handshake for the given <paramref name="instanceId"/>. A previously unseen instance id is a new
    /// retry attempt and bumps <see cref="TryCount"/>; re-seeing the current attempt is a no-op.
    /// </summary>
    internal void NotifyHandshake(string instanceId)
    {
        int index = _orderedInstanceIds.IndexOf(instanceId);
        if (index < 0)
        {
            _orderedInstanceIds.Add(instanceId);
            TryCount++;
        }
        else if (index != _orderedInstanceIds.Count - 1)
        {
            // We received a handshake for an instance id that is not the most recent one — unexpected ordering.
            throw ApplicationStateGuard.Unreachable();
        }
    }

    private void ReportGenericTestResult(
        string testNodeUid,
        string instanceId,
        Func<TestNodeInfoEntry, TestNodeInfoEntry> incrementTestNodeInfoEntry,
        Action<TestProgressState> incrementCountAction)
    {
        int currentAttemptNumber = GetAttemptNumberFromInstanceId(instanceId);

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

    private int GetAttemptNumberFromInstanceId(string instanceId)
    {
        int index = _orderedInstanceIds.IndexOf(instanceId);
        if (index < 0)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        // Attempt numbers are 1-based.
        return index + 1;
    }
}
