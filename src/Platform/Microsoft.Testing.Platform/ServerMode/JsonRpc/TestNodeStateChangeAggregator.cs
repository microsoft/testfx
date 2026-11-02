// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
    /// <summary>
    /// Aggregates multiple <see cref="TestNodeUpdateMessage"/> events
    /// into a single <see cref="TestNodeStateChangedEventArgs"/>.
    ///
    /// This is done to minimize the number of RPC messages sent.
    /// </summary>
    /// <remarks>The caller needs to ensure thread-safety.</remarks>
    internal sealed class TestNodeStateChangeAggregator(Guid runId)
    {
        // Note: Currently there's no cascading node changes we need to deal with.
        private readonly List<TestNodeUpdateMessage> _stateChanges = [];

        public Guid RunId { get; } = runId;

        public bool HasChanges
            => _stateChanges.Count > 0;

        public void OnStateChange(TestNodeUpdateMessage stateChangedMessage)
            => _stateChanges.Add(stateChangedMessage);

        public TestNodeStateChangedEventArgs BuildAggregatedChange()
        {
            HashSet<TestNodeUid>? terminalTestNodeUids = null;
            foreach (TestNodeUpdateMessage stateChange in _stateChanges)
            {
                TestNodeStateProperty? state = stateChange.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
                if (IsTerminalState(state))
                {
                    (terminalTestNodeUids ??= []).Add(stateChange.TestNode.Uid);
                }
            }

            TestNodeUpdateMessage[] changes = terminalTestNodeUids is null
                ? [.. _stateChanges]
                : FilterStateChanges(_stateChanges, terminalTestNodeUids);

            return new(RunId, changes);
        }

        // Keep the captured LINQ predicate out of BuildAggregatedChange so batches without terminal states
        // do not allocate its closure.
        private static TestNodeUpdateMessage[] FilterStateChanges(
            List<TestNodeUpdateMessage> stateChanges,
            HashSet<TestNodeUid> terminalTestNodeUids)
            => [.. stateChanges.Where(stateChange =>
                stateChange.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>() is not InProgressTestNodeStateProperty
                || !terminalTestNodeUids.Contains(stateChange.TestNode.Uid))];

#pragma warning disable CS0618, MTP0001
        private static bool IsTerminalState(TestNodeStateProperty? state)
            => state is PassedTestNodeStateProperty
                or SkippedTestNodeStateProperty
                or FailedTestNodeStateProperty
                or ErrorTestNodeStateProperty
                or TimeoutTestNodeStateProperty
                or CancelledTestNodeStateProperty;
#pragma warning restore CS0618, MTP0001
    }
}
