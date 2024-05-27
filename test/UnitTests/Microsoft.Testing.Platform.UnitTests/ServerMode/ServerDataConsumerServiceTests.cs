// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.TestInfrastructure;

using Moq;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using TestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class ServerDataConsumerServiceTests : TestBase, IAsyncCleanable, IDisposable
{
    private readonly PerRequestServerDataConsumer _service;
    private readonly ServiceProvider _serviceProvider = new();
    private readonly Mock<ITask> _task = new();
    private readonly Mock<IServerTestHost> _serverTestHost = new();

    private readonly Guid _runId = Guid.NewGuid();

    public ServerDataConsumerServiceTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _serviceProvider.TryAddService(new PerRequestTestSessionContext(CancellationToken.None, CancellationToken.None));
        _service = new PerRequestServerDataConsumer(_serviceProvider, _serverTestHost.Object, _runId, _task.Object);
    }

    public async Task ConsumeAsync_WithSessionFileArtifact()
    {
        SessionFileArtifact sessionFileArtifact = new(new SessionUid("1"), new FileInfo("file"), "name", "description");
        DataProducer producer = new();

        await _service.ConsumeAsync(producer, sessionFileArtifact, CancellationToken.None).ConfigureAwait(false);

        List<Artifact> actual = _service.Artifacts;
        List<Artifact> expected =
        [
            new("file", nameof(DataProducer), "file", "name", "description")
        ];

        Uri actualUri = new(actual[0].Uri);

        Assert.AreEqual(expected[0].Uri, Path.GetFileName(actualUri.AbsolutePath));
        Assert.IsTrue(actualUri.IsAbsoluteUri);
        Assert.AreEqual(expected[0].Producer, actual[0].Producer);
        Assert.AreEqual(expected[0].Type, actual[0].Type);
        Assert.AreEqual(expected[0].DisplayName, actual[0].DisplayName);
        Assert.AreEqual(expected[0].Description, actual[0].Description);
    }

    public async Task ConsumeAsync_WithTestNodeUpdatedMessage()
    {
        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty });
        DataProducer producer = new();

        await _service.ConsumeAsync(producer, testNode, CancellationToken.None).ConfigureAwait(false);

        List<Artifact> actual = _service.Artifacts;
        Assert.AreEqual(0, actual.Count);
    }

    public void PopulateTestNodeStatistics_WithMissingNodeType()
    {
        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty });

        _service.PopulateTestNodeStatistics(testNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(statistics.TotalDiscoveredTests, 0);
        Assert.AreEqual(statistics.TotalPassedTests, 0);
        Assert.AreEqual(statistics.TotalFailedTests, 0);
    }

    public void PopulateTestNodeStatistics_WithDuplicateDiscoveredEvents()
    {
        PropertyBag properties = new(
            DiscoveredTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = properties });

        _service.PopulateTestNodeStatistics(testNode);
        _service.PopulateTestNodeStatistics(testNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(statistics.TotalDiscoveredTests, 1);
        Assert.AreEqual(statistics.TotalPassedTests, 0);
        Assert.AreEqual(statistics.TotalFailedTests, 0);
    }

    public void PopulateTestNodeStatistics_WithDiscoveredAndPassedEventsForSameUid()
    {
        PropertyBag properties = new(
            DiscoveredTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = properties });

        PropertyBag otherProperties = new(
            PassedTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage otherTestNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = otherProperties });

        _service.PopulateTestNodeStatistics(testNode);
        _service.PopulateTestNodeStatistics(otherTestNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(statistics.TotalDiscoveredTests, 1);
        Assert.AreEqual(statistics.TotalPassedTests, 1);
        Assert.AreEqual(statistics.TotalFailedTests, 0);
    }

    public void PopulateTestNodeStatistics_WithDuplicatePassedEvents()
    {
        PropertyBag properties = new(PassedTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = properties });

        _service.PopulateTestNodeStatistics(testNode);
        _service.PopulateTestNodeStatistics(testNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(statistics.TotalDiscoveredTests, 0);
        Assert.AreEqual(statistics.TotalPassedTests, 1);
        Assert.AreEqual(statistics.TotalFailedTests, 0);
        Assert.AreEqual(statistics.TotalPassedRetries, 1);
        Assert.AreEqual(statistics.TotalFailedRetries, 0);
    }

    public void PopulateTestNodeStatistics_WithEventsForSameUid()
    {
        PropertyBag properties = new(
            new FailedTestNodeStateProperty("failed"));

        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = properties });

        PropertyBag otherProperties = new(
            PassedTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage otherTestNode = new(new SessionUid(string.Empty), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = otherProperties });

        _service.PopulateTestNodeStatistics(testNode);
        _service.PopulateTestNodeStatistics(otherTestNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(statistics.TotalDiscoveredTests, 0);
        Assert.AreEqual(statistics.TotalPassedTests, 1);
        Assert.AreEqual(statistics.TotalFailedTests, 0);
        Assert.AreEqual(statistics.TotalPassedRetries, 1);
        Assert.AreEqual(statistics.TotalFailedRetries, 0);
    }

    public void PopulateTestNodeStatistics_WithEventsForDifferentUids()
    {
        PropertyBag properties = new(
            PassedTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test1()"), DisplayName = string.Empty, Properties = properties });

        PropertyBag otherProperties = new(
            new FailedTestNodeStateProperty("failed"));

        TestNodeUpdateMessage otherTestNode = new(new SessionUid(string.Empty), new TestNode { Uid = new TestNodeUid("test2()"), DisplayName = string.Empty, Properties = otherProperties });

        _service.PopulateTestNodeStatistics(testNode);
        _service.PopulateTestNodeStatistics(otherTestNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(statistics.TotalDiscoveredTests, 0);
        Assert.AreEqual(statistics.TotalPassedTests, 1);
        Assert.AreEqual(statistics.TotalFailedTests, 1);
        Assert.AreEqual(statistics.TotalPassedRetries, 0);
        Assert.AreEqual(statistics.TotalFailedRetries, 0);
    }

    public async Task CleanupAsync(CleanupContext context)
    {
        if (_service.GetIdleUpdateTaskAsync() is { } idleUpdateTaskAsync)
        {
            await idleUpdateTaskAsync.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }
    }

    public void Dispose() => _service.Dispose();

    private sealed class DataProducer : IDataProducer
    {
        public string Uid => nameof(DataProducer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Type[] DataTypesProduced => [typeof(SessionFileArtifact)];

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
