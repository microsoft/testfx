// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber, int LastRetryAttemptNumber, int Attempts);

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed partial class TestProgressState
{
    public void ReportPassingTest(string testNodeUid, string displayName, string instanceId)
        => ReportPassingTest(testNodeUid, displayName, instanceId, retryAttemptNumber: 1);

    public void ReportSkippedTest(string testNodeUid, string displayName, string instanceId)
        => ReportSkippedTest(testNodeUid, displayName, instanceId, retryAttemptNumber: 1);

    public void ReportFailedTest(string testNodeUid, string displayName, string instanceId)
        => ReportFailedTest(testNodeUid, displayName, instanceId, retryAttemptNumber: 1);

    public void ReportPassingTest(string testNodeUid, string displayName, string instanceId, int retryAttemptNumber)
        => ReportGenericTestResult(testNodeUid, displayName, instanceId, retryAttemptNumber, static entry => entry with { Passed = entry.Passed + 1 }, static @this => @this._passedTests++);

    public void ReportSkippedTest(string testNodeUid, string displayName, string instanceId, int retryAttemptNumber)
        => ReportGenericTestResult(testNodeUid, displayName, instanceId, retryAttemptNumber, static entry => entry with { Skipped = entry.Skipped + 1 }, static @this => @this._skippedTests++);

    public void ReportFailedTest(string testNodeUid, string displayName, string instanceId, int retryAttemptNumber)
        => ReportGenericTestResult(testNodeUid, displayName, instanceId, retryAttemptNumber, static entry => entry with { Failed = entry.Failed + 1 }, static @this => @this._failedTests++);

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
        string displayName,
        string instanceId,
        int retryAttemptNumber,
        Func<TestNodeInfoEntry, TestNodeInfoEntry> incrementTestNodeInfoEntry,
        Action<TestProgressState> incrementCountAction)
    {
        if (retryAttemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAttemptNumber));
        }

        lock (_lock)
        {
            int currentAttemptNumber = GetAttemptNumberCore(instanceId);

            if (_testUidToResults.TryGetValue(testNodeUid, out TestNodeInfoEntry value))
            {
                int comparison = CompareAttempts(currentAttemptNumber, retryAttemptNumber, value.LastAttemptNumber, value.LastRetryAttemptNumber);
                if (comparison == 0)
                {
                    // Another result for the same test node in the same attempt — just increment. When the uid has
                    // already been superseded once, this result belongs to a retry attempt and is itself an extra
                    // execution: a folded data-driven test reports one result per row, so counting only the first
                    // would undercount the extra runs those rows actually cost.
                    if (_retriedUids.Contains(testNodeUid))
                    {
                        _retriedExecutions++;
                    }

                    _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry(value);
                }
                else if (comparison > 0)
                {
                    // Retry: discard the previous attempt's contribution to the live tally and re-count this attempt.
                    _retriedFailedTests += value.Failed;
                    _passedTests -= value.Passed;
                    _skippedTests -= value.Skipped;
                    _failedTests -= value.Failed;
                    _retriedUids.Add(testNodeUid);
                    _retriedExecutions++;

                    // Remember that an earlier attempt failed. Whether that makes the test flaky depends on the
                    // final tally, which is only known once the run ends, so the decision is deferred to
                    // IsFlakyCore rather than made here.
                    if (value.Failed > 0)
                    {
                        _uidWithEarlierFailure[testNodeUid] = displayName;
                    }

                    _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber, LastRetryAttemptNumber: retryAttemptNumber, Attempts: value.Attempts + 1));
                }
                else
                {
                    // A result for an attempt older than the latest one we saw — unexpected ordering.
                    throw ApplicationStateGuard.Unreachable();
                }
            }
            else
            {
                _testUidToResults.Add(testNodeUid, incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber, LastRetryAttemptNumber: retryAttemptNumber, Attempts: 1)));
            }

            incrementCountAction(this);
        }
    }

    /// <summary>
    /// Orders two attempts of the same test node. The out-of-process host attempt is the major component and the
    /// in-process retry attempt the minor one, so the two retry mechanisms compose instead of colliding: host
    /// attempt 2 / retry 1 is later than host attempt 1 / retry 5.
    /// </summary>
    private static int CompareAttempts(int attemptNumber, int retryAttemptNumber, int otherAttemptNumber, int otherRetryAttemptNumber)
    {
        int comparison = attemptNumber.CompareTo(otherAttemptNumber);
        return comparison != 0 ? comparison : retryAttemptNumber.CompareTo(otherRetryAttemptNumber);
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
