// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class OpenTelemetryResultHandlerTests : IDisposable
{
    private readonly Mock<IPlatformOpenTelemetryService> _otelService = new();
    private readonly Mock<IEnvironment> _environment = new();
    private readonly FakeCounter<int> _discoveredCounter = new();
    private readonly FakeCounter<int> _startedCounter = new();
    private readonly FakeCounter<int> _completedCounter = new();
    private readonly FakeCounter<int> _passedCounter = new();
    private readonly FakeCounter<int> _failedCounter = new();
    private readonly FakeCounter<int> _skippedCounter = new();
    private readonly FakeCounter<int> _unknownCounter = new();
    private readonly FakeHistogram<double> _durationHistogram = new();
    private readonly OpenTelemetryResultHandler _handler;

    public OpenTelemetryResultHandlerTests()
    {
        _otelService.Setup(s => s.CreateCounter<int>("tests.discovered", null, null, null)).Returns(_discoveredCounter);
        _otelService.Setup(s => s.CreateCounter<int>("tests.started", null, null, null)).Returns(_startedCounter);
        _otelService.Setup(s => s.CreateCounter<int>("tests.completed", null, null, null)).Returns(_completedCounter);
        _otelService.Setup(s => s.CreateCounter<int>("tests.passed", null, null, null)).Returns(_passedCounter);
        _otelService.Setup(s => s.CreateCounter<int>("tests.failed", null, null, null)).Returns(_failedCounter);
        _otelService.Setup(s => s.CreateCounter<int>("tests.skipped", null, null, null)).Returns(_skippedCounter);
        _otelService.Setup(s => s.CreateCounter<int>("tests.unknown", null, null, null)).Returns(_unknownCounter);
        _otelService.Setup(s => s.CreateHistogram<double>("tests.duration", null, null, null)).Returns(_durationHistogram);
        _environment.SetupGet(e => e.NewLine).Returns("\n");

        _handler = new OpenTelemetryResultHandler(_otelService.Object, _environment.Object);
    }

    [TestMethod]
    public void NotifyDiscovered_IncrementsDiscoveredCounter()
    {
        _handler.NotifyDiscovered();

        Assert.AreEqual(1, _discoveredCounter.Value);
    }

    [TestMethod]
    public void NotifyUnknown_IncrementsUnknownCounter()
    {
        _handler.NotifyUnknown();

        Assert.AreEqual(1, _unknownCounter.Value);
    }

    [TestMethod]
    public void NotifyPassed_IncrementsPassedAndCompletedCounters()
    {
        TestNode testNode = CreateTestNode();
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(1, _passedCounter.Value);
        Assert.AreEqual(1, _completedCounter.Value);
    }

    [TestMethod]
    public void NotifyFailed_IncrementsFailedAndCompletedCounters()
    {
        TestNode testNode = CreateTestNode();
        _handler.NotifyFailed(testNode, new FailedTestNodeStateProperty());

        Assert.AreEqual(1, _failedCounter.Value);
        Assert.AreEqual(1, _completedCounter.Value);
    }

    [TestMethod]
    public void NotifySkipped_IncrementsSkippedAndCompletedCounters()
    {
        TestNode testNode = CreateTestNode();
        _handler.NotifySkipped(testNode, SkippedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(1, _skippedCounter.Value);
        Assert.AreEqual(1, _completedCounter.Value);
    }

    [TestMethod]
    public void NotifyInProgress_IncrementsStartedCounter()
    {
        TestNode testNode = CreateTestNode();
        _handler.NotifyInProgress(testNode, null);

        Assert.AreEqual(1, _startedCounter.Value);
    }

    [TestMethod]
    public void NotifyInProgress_WhenActivityIsCreated_TracksActivity()
    {
        Mock<IPlatformActivity> activity = new();
        activity.SetupGet(a => a.Id).Returns("activity-1");
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns(activity.Object);

        TestNode testNode = CreateTestNode();
        _handler.NotifyInProgress(testNode, null);

        // Verify activity is tracked by completing the test and checking tags are set.
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);
        activity.Verify(a => a.SetTag("test.result", "passed"), Times.Once);
    }

    [TestMethod]
    public void NotifyInProgress_WhenStartActivityReturnsNull_DoesNotTrackActivity()
    {
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns((IPlatformActivity?)null);

        TestNode testNode = CreateTestNode();
        _handler.NotifyInProgress(testNode, null);

        // Should not throw when completing the test (no activity tracked).
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);
    }

    [TestMethod]
    public void HandleTestResult_WithPassedState_SetsPassedResultTag()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("test-1");
        TestNode testNode = CreateTestNode("test-1");

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, new PassedTestNodeStateProperty("test passed"));

        activity.Verify(a => a.SetTag("test.result", "passed"), Times.Once);
        activity.Verify(a => a.SetTag("test.result.explanation", "test passed"), Times.Once);
        activity.Verify(a => a.Dispose(), Times.Once);
    }

    [TestMethod]
    public void HandleTestResult_WithFailedState_SetsExceptionTags()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("test-2");
        TestNode testNode = CreateTestNode("test-2");
        InvalidOperationException exception = new("something went wrong");

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyFailed(testNode, new FailedTestNodeStateProperty(exception, "test failed"));

        activity.Verify(a => a.SetTag("test.result", "failed"), Times.Once);
        activity.Verify(a => a.SetTag("test.result.exception.type", typeof(InvalidOperationException).FullName), Times.Once);
        activity.Verify(a => a.SetTag("test.result.exception.message", "something went wrong"), Times.Once);
        activity.Verify(a => a.SetTag("test.result.exception.stacktrace", exception.StackTrace), Times.Once);
        activity.Verify(a => a.Dispose(), Times.Once);
    }

    [TestMethod]
    public void HandleTestResult_WithTimingProperty_RecordsDurationAndSetsTags()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("test-3");
        TimingInfo timing = new(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMilliseconds(150), TimeSpan.FromMilliseconds(150));
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test-3"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                PassedTestNodeStateProperty.CachedInstance,
                new TimingProperty(timing)),
        };

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(150d, _durationHistogram.LastRecordedValue);
        activity.Verify(a => a.SetTag("test.duration.ms", 150d), Times.Once);
    }

    [TestMethod]
    public void HandleTestResult_WithStdOutAndStdErr_SetsTags()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("test-4");
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test-4"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                PassedTestNodeStateProperty.CachedInstance,
                new StandardOutputProperty("hello"),
                new StandardErrorProperty("oops")),
        };

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        activity.Verify(a => a.SetTag("test.stdout", "hello"), Times.Once);
        activity.Verify(a => a.SetTag("test.stderr", "oops"), Times.Once);
    }

    [TestMethod]
    public void HandleTestResult_WithNoTrackedActivity_ReturnsEarly()
    {
        TestNode testNode = CreateTestNode("untracked");

        // Should not throw — no activity was started for this test node.
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        // Completed counter should still be incremented.
        Assert.AreEqual(1, _completedCounter.Value);
    }

    [TestMethod]
    public void HandleTestResult_WithSkippedState_SetsSkippedResultTag()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("test-5");
        TestNode testNode = CreateTestNode("test-5");

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifySkipped(testNode, SkippedTestNodeStateProperty.CachedInstance);

        activity.Verify(a => a.SetTag("test.result", "skipped"), Times.Once);
        activity.Verify(a => a.Dispose(), Times.Once);
    }

    [TestMethod]
    public void NotifyInProgress_WithParentUid_IncludesParentIdInTags()
    {
        IEnumerable<KeyValuePair<string, object?>>? capturedTags = null;
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Callback<string, IEnumerable<KeyValuePair<string, object?>>?, string?, DateTimeOffset>((_, tags, _, _) => capturedTags = tags)
            .Returns(new Mock<IPlatformActivity>().Object);

        TestNode testNode = CreateTestNode("child");
        _handler.NotifyInProgress(testNode, new TestNodeUid("parent"));

        Assert.IsNotNull(capturedTags);
        var tagList = capturedTags.ToList();
        Assert.IsTrue(tagList.Exists(t => t.Key == "test.parent.id" && (string?)t.Value == "parent"));
        Assert.IsTrue(tagList.Exists(t => t.Key == "test.name" && (string?)t.Value == "Test"));
        Assert.IsTrue(tagList.Exists(t => t.Key == "test.id" && (string?)t.Value == "child"));
    }

    [TestMethod]
    public void Dispose_DisposesOrphanedActivities()
    {
        Mock<IPlatformActivity> activity1 = SetupActivityForTestNode("orphan-1");
        Mock<IPlatformActivity> activity2 = SetupActivityForTestNode("orphan-2");

        _handler.NotifyInProgress(CreateTestNode("orphan-1"), null);
        _handler.NotifyInProgress(CreateTestNode("orphan-2"), null);

        _handler.Dispose();

        activity1.Verify(a => a.Dispose(), Times.Once);
        activity2.Verify(a => a.Dispose(), Times.Once);
    }

    [TestMethod]
    public void Dispose_CalledTwice_DoesNotDisposeActivitiesTwice()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("orphan");
        _handler.NotifyInProgress(CreateTestNode("orphan"), null);

        _handler.Dispose();
        _handler.Dispose();

        activity.Verify(a => a.Dispose(), Times.Once);
    }

    [TestMethod]
    public void HandleTestResult_WithFileArtifact_SetsArtifactTags()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("test-6");
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test-6"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                PassedTestNodeStateProperty.CachedInstance,
                new FileArtifactProperty(new FileInfo("test-output.log"), "Log")),
        };

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        activity.Verify(a => a.SetTag("test.artifact.file[0].path", It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void HandleTestResult_WithMetadataProperty_SetsMetadataTags()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("test-7");
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test-7"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                PassedTestNodeStateProperty.CachedInstance,
                new TestMetadataProperty("category", "unit")),
        };

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        activity.Verify(a => a.SetTag("test.metadataProperty.category", "unit"), Times.Once);
    }

    public void Dispose()
        => _handler.Dispose();

    private static TestNode CreateTestNode(string uid = "test-uid")
        => new()
        {
            Uid = new TestNodeUid(uid),
            DisplayName = "Test",
        };

    private Mock<IPlatformActivity> SetupActivityForTestNode(string testNodeUid)
    {
        Mock<IPlatformActivity> activity = new();
        activity.SetupGet(a => a.Id).Returns($"activity-{testNodeUid}");
        activity.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity.Object);
        _otelService.Setup(s => s.StartActivity(
            testNodeUid,
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns(activity.Object);

        return activity;
    }

    private sealed class FakeCounter<T> : ICounter<T>
        where T : struct
    {
        public T Value { get; private set; }

        public void Add(T delta)
            => Value = (T)(object)((int)(object)Value + (int)(object)delta);
    }

    private sealed class FakeHistogram<T> : IHistogram<T>
        where T : struct
    {
        public T? LastRecordedValue { get; private set; }

        public void Record(T value)
            => LastRecordedValue = value;
    }
}
