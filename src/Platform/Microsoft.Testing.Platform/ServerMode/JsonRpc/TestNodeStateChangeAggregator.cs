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
            => new(RunId, _stateChanges.ToArray());
    }
}
