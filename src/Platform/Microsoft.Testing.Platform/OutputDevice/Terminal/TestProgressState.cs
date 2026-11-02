// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber, int LastRetryAttemptNumber, int Attempts);

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed partial class TestProgressState
{
    // Multiple test-host instances can report one attempt concurrently (for example, shards), so all per-execution
    // result, attempt, duration, and discovery state is protected by this lock.
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    // Tracks the per-test-node tally and the attempt it belongs to, so retries (which re-report the same test node
    // uid, either under a new instance id for the out-of-process orchestrator or under a new in-process retry
    // attempt number) replace rather than double-count the earlier attempt's result. An attempt is the pair
    // (host attempt, in-process retry attempt), ordered lexicographically, so the two retry mechanisms compose.
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

    // Test nodes whose result was superseded by a later attempt while the earlier attempt had at least one failure.
    // Combined with the final tally in _testUidToResults this yields the "flaky" set (failed at least once, but the
    // final attempt passed). Kept separate from the tally so a test that keeps failing is retried-but-not-flaky.
    // The value is the last-seen display name so the summary can list the test by name.
    private readonly Dictionary<string, string> _uidWithEarlierFailure = [];

    // Distinct test nodes that produced results in more than one attempt. This is the "how many tests were retried"
    // figure, as opposed to _retriedExecutions ("how many extra runs did that cost"). The default string comparer is
    // already ordinal, matching the dictionaries above.
    private readonly HashSet<string> _retriedUids = [];

    private readonly List<string> _discoveredTestDisplayNames = [];
    private int _discoveredTests;
    private int _failedTests;
    private int _passedTests;
    private int _skippedTests;
    private int _retriedFailedTests;

    // Total number of extra executions caused by retries (every result that superseded an earlier attempt), so the
    // summary can distinguish "2 tests were retried" from "those retries cost 4 extra runs".
    private int _retriedExecutions;
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

    /// <summary>Gets the number of distinct tests that produced results in more than one attempt.</summary>
    public int RetriedTests
    {
        get
        {
            lock (_lock)
            {
                return _retriedUids.Count;
            }
        }
    }

    /// <summary>Gets the number of extra executions caused by retries (results that superseded an earlier attempt).</summary>
    public int RetriedExecutions
    {
        get
        {
            lock (_lock)
            {
                return _retriedExecutions;
            }
        }
    }

    /// <summary>
    /// Gets the number of tests that failed at least once but whose final attempt passed.
    /// </summary>
    public int FlakyTests
    {
        get
        {
            lock (_lock)
            {
                int count = 0;
                foreach (KeyValuePair<string, string> entry in _uidWithEarlierFailure)
                {
                    if (IsFlakyCore(entry.Key))
                    {
                        count++;
                    }
                }

                return count;
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

    internal TestNodeResultsState GetOrCreateTestNodeResultsState(Func<TestNodeResultsState> factory)
    {
        lock (_lock)
        {
            return _testNodeResultsState ??= factory();
        }
    }
}
