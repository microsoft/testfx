// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestNodeStateChangeAggregatorTests
{
    [TestMethod]
    [DynamicData(nameof(TerminalStates))]
    public void BuildAggregatedChange_InProgressThenTerminalForSameUid_OmitsInProgress(TestNodeStateProperty terminalState)
    {
        var runId = Guid.NewGuid();
        ServerTestHost.TestNodeStateChangeAggregator aggregator = new(runId);
        TestNodeUpdateMessage inProgress = CreateUpdate("test", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage terminal = CreateUpdate("test", terminalState);

        aggregator.OnStateChange(inProgress);
        aggregator.OnStateChange(terminal);

        TestNodeStateChangedEventArgs aggregatedChange = aggregator.BuildAggregatedChange();

        Assert.AreEqual(runId, aggregatedChange.RunId);
        Assert.IsNotNull(aggregatedChange.Changes);
        Assert.HasCount(1, aggregatedChange.Changes);
        Assert.AreSame(terminal, aggregatedChange.Changes[0]);
    }

    [TestMethod]
    public void BuildAggregatedChange_TerminalThenInProgressForSameUid_OmitsInProgress()
    {
        ServerTestHost.TestNodeStateChangeAggregator aggregator = new(Guid.NewGuid());
        TestNodeUpdateMessage terminal = CreateUpdate("test", PassedTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage inProgress = CreateUpdate("test", InProgressTestNodeStateProperty.CachedInstance);

        aggregator.OnStateChange(terminal);
        aggregator.OnStateChange(inProgress);

        TestNodeStateChangedEventArgs aggregatedChange = aggregator.BuildAggregatedChange();

        Assert.IsNotNull(aggregatedChange.Changes);
        Assert.HasCount(1, aggregatedChange.Changes);
        Assert.AreSame(terminal, aggregatedChange.Changes[0]);
    }

    [TestMethod]
    [DynamicData(nameof(NonTerminalStates))]
    public void BuildAggregatedChange_NonTerminalStateForSameUid_RetainsInProgress(TestNodeStateProperty nonTerminalState)
    {
        ServerTestHost.TestNodeStateChangeAggregator aggregator = new(Guid.NewGuid());
        TestNodeUpdateMessage inProgress = CreateUpdate("test", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage nonTerminal = CreateUpdate("test", nonTerminalState);

        aggregator.OnStateChange(inProgress);
        aggregator.OnStateChange(nonTerminal);

        TestNodeStateChangedEventArgs aggregatedChange = aggregator.BuildAggregatedChange();

        Assert.IsNotNull(aggregatedChange.Changes);
        Assert.HasCount(2, aggregatedChange.Changes);
        Assert.AreSame(inProgress, aggregatedChange.Changes[0]);
        Assert.AreSame(nonTerminal, aggregatedChange.Changes[1]);
    }

    [TestMethod]
    public void BuildAggregatedChange_NoStateForSameUid_RetainsInProgress()
    {
        ServerTestHost.TestNodeStateChangeAggregator aggregator = new(Guid.NewGuid());
        TestNodeUpdateMessage inProgress = CreateUpdate("test", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage noState = CreateUpdate("test");

        aggregator.OnStateChange(inProgress);
        aggregator.OnStateChange(noState);

        TestNodeStateChangedEventArgs aggregatedChange = aggregator.BuildAggregatedChange();

        Assert.IsNotNull(aggregatedChange.Changes);
        Assert.HasCount(2, aggregatedChange.Changes);
        Assert.AreSame(inProgress, aggregatedChange.Changes[0]);
        Assert.AreSame(noState, aggregatedChange.Changes[1]);
    }

    [TestMethod]
    public void BuildAggregatedChange_TerminalForDifferentUid_RetainsInProgress()
    {
        ServerTestHost.TestNodeStateChangeAggregator aggregator = new(Guid.NewGuid());
        TestNodeUpdateMessage inProgress = CreateUpdate("in-progress", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage terminal = CreateUpdate("terminal", PassedTestNodeStateProperty.CachedInstance);

        aggregator.OnStateChange(inProgress);
        aggregator.OnStateChange(terminal);

        TestNodeStateChangedEventArgs aggregatedChange = aggregator.BuildAggregatedChange();

        Assert.IsNotNull(aggregatedChange.Changes);
        Assert.HasCount(2, aggregatedChange.Changes);
        Assert.AreSame(inProgress, aggregatedChange.Changes[0]);
        Assert.AreSame(terminal, aggregatedChange.Changes[1]);
    }

    [TestMethod]
    public void BuildAggregatedChange_WhenSuppressingInProgress_PreservesRetainedOrderAndIdentity()
    {
        ServerTestHost.TestNodeStateChangeAggregator aggregator = new(Guid.NewGuid());
        TestNodeUpdateMessage before = CreateUpdate("before");
        TestNodeUpdateMessage inProgress = CreateUpdate("test", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage noState = CreateUpdate("test");
        TestNodeUpdateMessage discovered = CreateUpdate("test", DiscoveredTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage terminal = CreateUpdate("test", PassedTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage after = CreateUpdate("after", InProgressTestNodeStateProperty.CachedInstance);

        aggregator.OnStateChange(before);
        aggregator.OnStateChange(inProgress);
        aggregator.OnStateChange(noState);
        aggregator.OnStateChange(discovered);
        aggregator.OnStateChange(terminal);
        aggregator.OnStateChange(after);

        TestNodeStateChangedEventArgs aggregatedChange = aggregator.BuildAggregatedChange();

        Assert.IsNotNull(aggregatedChange.Changes);
        Assert.HasCount(5, aggregatedChange.Changes);
        Assert.AreSame(before, aggregatedChange.Changes[0]);
        Assert.AreSame(noState, aggregatedChange.Changes[1]);
        Assert.AreSame(discovered, aggregatedChange.Changes[2]);
        Assert.AreSame(terminal, aggregatedChange.Changes[3]);
        Assert.AreSame(after, aggregatedChange.Changes[4]);
    }

    [TestMethod]
    public void BuildAggregatedChange_SeparateAggregatorBatches_DoNotInfluenceEachOther()
    {
        TestNodeUpdateMessage inProgress = CreateUpdate("test", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage terminal = CreateUpdate("test", PassedTestNodeStateProperty.CachedInstance);
        ServerTestHost.TestNodeStateChangeAggregator inProgressBatch = new(Guid.NewGuid());
        ServerTestHost.TestNodeStateChangeAggregator terminalBatch = new(Guid.NewGuid());

        inProgressBatch.OnStateChange(inProgress);
        terminalBatch.OnStateChange(terminal);

        TestNodeStateChangedEventArgs firstAggregatedChange = inProgressBatch.BuildAggregatedChange();
        TestNodeStateChangedEventArgs secondAggregatedChange = terminalBatch.BuildAggregatedChange();

        Assert.IsNotNull(firstAggregatedChange.Changes);
        Assert.HasCount(1, firstAggregatedChange.Changes);
        Assert.AreSame(inProgress, firstAggregatedChange.Changes[0]);
        Assert.IsNotNull(secondAggregatedChange.Changes);
        Assert.HasCount(1, secondAggregatedChange.Changes);
        Assert.AreSame(terminal, secondAggregatedChange.Changes[0]);
    }

    [TestMethod]
    public void BuildAggregatedChange_DoesNotMutateBufferedChanges()
    {
        ServerTestHost.TestNodeStateChangeAggregator aggregator = new(Guid.NewGuid());
        TestNodeUpdateMessage before = CreateUpdate("before");
        TestNodeUpdateMessage inProgress = CreateUpdate("test", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage terminal = CreateUpdate("test", PassedTestNodeStateProperty.CachedInstance);
        aggregator.OnStateChange(before);
        aggregator.OnStateChange(inProgress);
        aggregator.OnStateChange(terminal);

        TestNodeStateChangedEventArgs firstAggregatedChange = aggregator.BuildAggregatedChange();
        terminal.TestNode.Properties._testNodeStateProperty = DiscoveredTestNodeStateProperty.CachedInstance;
        TestNodeStateChangedEventArgs secondAggregatedChange = aggregator.BuildAggregatedChange();

        Assert.IsNotNull(firstAggregatedChange.Changes);
        Assert.IsNotNull(secondAggregatedChange.Changes);
        Assert.HasCount(2, firstAggregatedChange.Changes);
        Assert.HasCount(3, secondAggregatedChange.Changes);
        Assert.AreSame(before, firstAggregatedChange.Changes[0]);
        Assert.AreSame(terminal, firstAggregatedChange.Changes[1]);
        Assert.AreSame(before, secondAggregatedChange.Changes[0]);
        Assert.AreSame(inProgress, secondAggregatedChange.Changes[1]);
        Assert.AreSame(terminal, secondAggregatedChange.Changes[2]);
    }

    public static IEnumerable<object[]> TerminalStates()
    {
        yield return [PassedTestNodeStateProperty.CachedInstance];
        yield return [SkippedTestNodeStateProperty.CachedInstance];
        yield return [new FailedTestNodeStateProperty()];
        yield return [new ErrorTestNodeStateProperty()];
        yield return [new TimeoutTestNodeStateProperty()];
#pragma warning disable CS0618, MTP0001
        yield return [new CancelledTestNodeStateProperty()];
#pragma warning restore CS0618, MTP0001
    }

    public static IEnumerable<object[]> NonTerminalStates()
    {
        yield return [DiscoveredTestNodeStateProperty.CachedInstance];
        yield return [new CustomTestNodeStateProperty()];
    }

    private static TestNodeUpdateMessage CreateUpdate(string uid, TestNodeStateProperty? state = null)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = new TestNodeUid(uid),
                DisplayName = uid,
                Properties = state is null ? new PropertyBag() : new PropertyBag(state),
            });

    private sealed class CustomTestNodeStateProperty : TestNodeStateProperty
    {
        public CustomTestNodeStateProperty()
            : base(null)
        {
        }
    }
}
