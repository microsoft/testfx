// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.VideoRecorder;

internal sealed partial class VideoRecorderSessionHandler
{
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_recorder is null || value is not TestNodeUpdateMessage update)
        {
            return Task.CompletedTask;
        }

        // A node carries a single state property in practice; use FirstOrDefault rather than
        // SingleOrDefault so a malformed producer can't throw out of the consumer pump.
        TestNodeStateProperty? state = update.TestNode.Properties.FirstOrDefault<TestNodeStateProperty>();
        if (state is null)
        {
            return Task.CompletedTask;
        }

        string testUid = update.TestNode.Uid.Value;

        if (state is InProgressTestNodeStateProperty)
        {
            lock (_stateGate)
            {
                // Remember when we first saw the test running, as a fallback start time if the
                // terminal message doesn't carry authoritative timing.
                if (!_inFlight.ContainsKey(testUid))
                {
                    _inFlight[testUid] = _clock.UtcNow;
                }
            }

            return Task.CompletedTask;
        }

        if (!IsTerminal(state))
        {
            return Task.CompletedTask;
        }

        bool isFailure = state is FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty;

        // Prefer the authoritative wall-clock timing the framework publishes on the terminal
        // message; fall back to the observed in-progress timestamp (and now) otherwise.
        (DateTimeOffset start, DateTimeOffset end) = ResolveTiming(update, testUid);

        lock (_stateGate)
        {
            _inFlight.Remove(testUid);
            if (isFailure)
            {
                _anyTestFailed = true;
            }

            _testRecords.Add(new TestRecord(update.TestNode.DisplayName, start, end, isFailure, OutcomeText(state)));
        }

        TryPruneOldSegments();
        return Task.CompletedTask;
    }

    private (DateTimeOffset Start, DateTimeOffset End) ResolveTiming(TestNodeUpdateMessage update, string testUid)
    {
        TimingProperty? timing = update.TestNode.Properties.FirstOrDefault<TimingProperty>();
        if (timing is not null)
        {
            return (timing.GlobalTiming.StartTime, timing.GlobalTiming.EndTime);
        }

        DateTimeOffset now = _clock.UtcNow;
        DateTimeOffset observedStart;
        lock (_stateGate)
        {
            observedStart = _inFlight.TryGetValue(testUid, out DateTimeOffset value) ? value : now;
        }

        return (observedStart, now);
    }

    private static bool IsTerminal(TestNodeStateProperty state)
        => state is PassedTestNodeStateProperty or FailedTestNodeStateProperty
            or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty or SkippedTestNodeStateProperty;

    private static string OutcomeText(TestNodeStateProperty state)
        => state switch
        {
            FailedTestNodeStateProperty => VideoRecorderResources.OutcomeFailed,
            ErrorTestNodeStateProperty => VideoRecorderResources.OutcomeError,
            TimeoutTestNodeStateProperty => VideoRecorderResources.OutcomeTimeout,
            SkippedTestNodeStateProperty => VideoRecorderResources.OutcomeSkipped,
            _ => VideoRecorderResources.OutcomePassed,
        };
}
