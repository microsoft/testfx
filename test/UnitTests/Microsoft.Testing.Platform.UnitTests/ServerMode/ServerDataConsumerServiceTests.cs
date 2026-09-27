// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using TestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ServerDataConsumerServiceTests : IDisposable
{
    private readonly PerRequestServerDataConsumer _service;
    private readonly ServiceProvider _serviceProvider = new();
    private readonly Mock<ITask> _task = new();
    private readonly Mock<IServerTestHost> _serverTestHost = new();

    private readonly Guid _runId = Guid.NewGuid();

    public ServerDataConsumerServiceTests()
    {
        _serviceProvider.TryAddService(new PerRequestTestSessionContext(CancellationToken.None, CancellationToken.None));
        _service = new PerRequestServerDataConsumer(_serviceProvider, _serverTestHost.Object, _runId, _task.Object);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task ConsumeAsync_WithFileArtifact()
    {
        FileArtifact fileArtifact = new(new FileInfo("file"), "name", "description");
        DataProducer producer = new();

        await _service.ConsumeAsync(producer, fileArtifact, CancellationToken.None).ConfigureAwait(false);

        Artifact artifact = Assert.ContainsSingle(_service.Artifacts);
        Uri uri = new(artifact.Uri);
        Assert.AreEqual("file", Path.GetFileName(uri.AbsolutePath));
        Assert.AreEqual(producer.Uid, artifact.Producer);
        Assert.AreEqual("file", artifact.Type);
        Assert.AreEqual("name", artifact.DisplayName);
        Assert.AreEqual("description", artifact.Description);
    }

    [TestMethod]
    public async Task ConsumeAsync_WithTestNodeUpdatedMessage()
    {
        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty });
        DataProducer producer = new();

        await _service.ConsumeAsync(producer, testNode, CancellationToken.None).ConfigureAwait(false);

        List<Artifact> actual = _service.Artifacts;
        Assert.IsEmpty(actual);
    }

    [TestMethod]
    public async Task ConsumeAsync_WithUnrecognizedData_DoesNotAddArtifact()
    {
        await _service.ConsumeAsync(new DataProducer(), new Mock<IData>().Object, CancellationToken.None).ConfigureAwait(false);

        Assert.IsEmpty(_service.Artifacts);
        _serverTestHost.Verify(host => host.SendTestUpdateAsync(It.IsAny<TestNodeStateChangedEventArgs>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_BeforeIdleDelayCompletes_FlushesPendingUpdate()
    {
        TestNodeUpdateMessage update = new(new SessionUid("1"), new TestNode
        {
            Uid = new TestNodeUid("test()"),
            DisplayName = "test",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
        });
        _task.Setup(task => task.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<bool>().Task);
        TestNodeStateChangedEventArgs? sentChange = null;
        _serverTestHost.Setup(host => host.SendTestUpdateAsync(It.IsAny<TestNodeStateChangedEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<TestNodeStateChangedEventArgs, CancellationToken>((change, _) => sentChange = change)
            .Returns(Task.CompletedTask);

        await _service.ConsumeAsync(new DataProducer(), update, CancellationToken.None).ConfigureAwait(false);
        _serverTestHost.Verify(host => host.SendTestUpdateAsync(It.IsAny<TestNodeStateChangedEventArgs>(), It.IsAny<CancellationToken>()), Times.Never);

        await _service.OnTestSessionFinishingAsync(_serviceProvider.GetTestSessionContext()).ConfigureAwait(false);

        _serverTestHost.Verify(host => host.SendTestUpdateAsync(It.IsAny<TestNodeStateChangedEventArgs>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsNotNull(sentChange);
        Assert.AreEqual(_runId, sentChange.RunId);
        Assert.IsNotNull(sentChange.Changes);
        Assert.HasCount(1, sentChange.Changes);
        Assert.AreSame(update, sentChange.Changes[0]);
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_CompletesWithoutSendingUpdates()
    {
        Task result = _service.OnTestSessionStartingAsync(_serviceProvider.GetTestSessionContext());

        Assert.AreSame(Task.CompletedTask, result);
        await result.ConfigureAwait(false);
        _serverTestHost.Verify(host => host.SendTestUpdateAsync(It.IsAny<TestNodeStateChangedEventArgs>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void PopulateTestNodeStatistics_WithMissingNodeType()
    {
        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty });

        _service.PopulateTestNodeStatistics(testNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(0, statistics.TotalDiscoveredTests);
        Assert.AreEqual(0, statistics.TotalPassedTests);
        Assert.AreEqual(0, statistics.TotalFailedTests);
    }

    [TestMethod]
    public void PopulateTestNodeStatistics_WithDuplicateDiscoveredEvents()
    {
        PropertyBag properties = new(
            DiscoveredTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = properties });

        _service.PopulateTestNodeStatistics(testNode);
        _service.PopulateTestNodeStatistics(testNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(1, statistics.TotalDiscoveredTests);
        Assert.AreEqual(0, statistics.TotalPassedTests);
        Assert.AreEqual(0, statistics.TotalFailedTests);
    }

    [TestMethod]
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
        Assert.AreEqual(1, statistics.TotalDiscoveredTests);
        Assert.AreEqual(1, statistics.TotalPassedTests);
        Assert.AreEqual(0, statistics.TotalFailedTests);
    }

    [TestMethod]
    public void PopulateTestNodeStatistics_WithDuplicatePassedEvents()
    {
        PropertyBag properties = new(PassedTestNodeStateProperty.CachedInstance);

        TestNodeUpdateMessage testNode = new(new SessionUid("1"), new TestNode { Uid = new TestNodeUid("test()"), DisplayName = string.Empty, Properties = properties });

        _service.PopulateTestNodeStatistics(testNode);
        _service.PopulateTestNodeStatistics(testNode);

        TestNodeStatistics statistics = _service.GetTestNodeStatistics();
        Assert.AreEqual(0, statistics.TotalDiscoveredTests);
        Assert.AreEqual(1, statistics.TotalPassedTests);
        Assert.AreEqual(0, statistics.TotalFailedTests);
        Assert.AreEqual(1, statistics.TotalPassedRetries);
        Assert.AreEqual(0, statistics.TotalFailedRetries);
    }

    [TestMethod]
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
        Assert.AreEqual(0, statistics.TotalDiscoveredTests);
        Assert.AreEqual(1, statistics.TotalPassedTests);
        Assert.AreEqual(0, statistics.TotalFailedTests);
        Assert.AreEqual(1, statistics.TotalPassedRetries);
        Assert.AreEqual(0, statistics.TotalFailedRetries);
    }

    [TestMethod]
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
        Assert.AreEqual(0, statistics.TotalDiscoveredTests);
        Assert.AreEqual(1, statistics.TotalPassedTests);
        Assert.AreEqual(1, statistics.TotalFailedTests);
        Assert.AreEqual(0, statistics.TotalPassedRetries);
        Assert.AreEqual(0, statistics.TotalFailedRetries);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        if (_service.GetIdleUpdateTaskAsync() is { } idleUpdateTaskAsync)
        {
            await idleUpdateTaskAsync.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }
    }

    void IDisposable.Dispose() => _service.Dispose();

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
