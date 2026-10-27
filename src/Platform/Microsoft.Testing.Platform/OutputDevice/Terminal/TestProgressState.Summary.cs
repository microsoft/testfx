// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber, int LastRetryAttemptNumber, int Attempts);

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed partial class TestProgressState
{
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
    /// A test is flaky when an earlier attempt failed but the final attempt produced only passing results. Callers
    /// must hold <see cref="_lock"/>.
    /// </summary>
    private bool IsFlakyCore(string testNodeUid)
        // A skipped row under a folded uid is not recovery either: not every result of the final attempt passed.
        => _testUidToResults.TryGetValue(testNodeUid, out TestNodeInfoEntry entry)
            && entry.Failed == 0
            && entry.Skipped == 0
            && entry.Passed > 0;

    /// <summary>
    /// Returns the tests that failed at least once but eventually passed, as (display name, total attempts) pairs
    /// ordered by display name so the rendering is deterministic for snapshot-based tests.
    /// </summary>
    public IReadOnlyList<(string DisplayName, int Attempts)> GetFlakyTests()
    {
        lock (_lock)
        {
            if (_uidWithEarlierFailure.Count == 0)
            {
                return [];
            }

            List<(string DisplayName, int Attempts)> flaky = [];
            foreach (KeyValuePair<string, string> entry in _uidWithEarlierFailure)
            {
                if (IsFlakyCore(entry.Key))
                {
                    // The attempt count is tracked per uid as transitions arrive rather than derived from the final
                    // (host attempt, in-process attempt) pair: with both retry mechanisms active the pair advances
                    // on either component, so (host 1, retry 1) -> (host 1, retry 2) -> (host 2, retry 1) is three
                    // executions that neither component alone - nor their maximum - would report as three.
                    flaky.Add((entry.Value, _testUidToResults[entry.Key].Attempts));
                }
            }

            flaky.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName));
            return flaky;
        }
    }
}
