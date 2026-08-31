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
    private readonly FakeCounter<int> _discoveredCounter = new();
    private readonly FakeCounter<int> _startedCounter = new();
    private readonly FakeCounter<int> _completedCounter = new();
    private readonly FakeCounter<int> _passedCounter = new();
    private readonly FakeCounter<int> _failedCounter = new();
    private readonly FakeCounter<int> _skippedCounter = new();
    private readonly FakeCounter<int> _unknownCounter = new();
    private readonly FakeCounter<int> _testCaseResultCounter = new();
    private readonly FakeUpDownCounter<int> _activeTestCases = new();
    private readonly FakeHistogram<double> _durationHistogram = new();
    private readonly FakeHistogram<double> _testCaseDurationHistogram = new();
    private readonly FakeHistogram<double> _testRunDurationHistogram = new();
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

        _otelService.Setup(s => s.CreateCounter<int>("test.case.result.count", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(_testCaseResultCounter);
        _otelService.Setup(s => s.CreateUpDownCounter<int>("test.case.active", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(_activeTestCases);
        _otelService.Setup(s => s.CreateHistogram<double>("test.case.duration", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(_testCaseDurationHistogram);
        _otelService.Setup(s => s.CreateHistogram<double>("test.run.duration", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(_testRunDurationHistogram);

        _handler = new OpenTelemetryResultHandler(_otelService.Object);
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

        // Should not throw when completing the test (no activity tracked), and the result must still be counted
        // so that a metrics-only configuration keeps working.
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(1, _testCaseResultCounter.Value);
        Assert.AreEqual(0, _activeTestCases.Value);
    }

    [TestMethod]
    public void NotifyExecutionCompleted_DisposesActivityWithoutRecordingOutcome()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("dropped-test");
        TestNode testNode = CreateTestNode("dropped-test");
        _handler.NotifyInProgress(testNode, null);

        _handler.NotifyExecutionCompleted(testNode);

        Assert.AreEqual(1, _completedCounter.Value);
        Assert.AreEqual(0, _passedCounter.Value);
        Assert.AreEqual(0, _failedCounter.Value);
        Assert.AreEqual(0, _skippedCounter.Value);
        activity.Verify(a => a.SetTag("test.result", It.IsAny<object?>()), Times.Never);
        activity.Verify(a => a.Dispose(), Times.Once);
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
        Mock<IPlatformActivity> activity1 = new();
        Mock<IPlatformActivity> activity2 = new();
        activity1.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity1.Object);
        activity2.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity2.Object);
        _otelService.SetupSequence(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Returns(activity1.Object)
            .Returns(activity2.Object);

        _handler.NotifyInProgress(CreateTestNode("orphan-1"), null);
        _handler.NotifyInProgress(CreateTestNode("orphan-2"), null);
        Assert.AreEqual(2, _activeTestCases.Value);

        _handler.Dispose();

        Assert.AreEqual(0, _activeTestCases.Value);
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

    [TestMethod]
    public void NotifyInProgress_WithDuplicateUid_DoesNotThrow()
    {
        // Regression test for https://github.com/microsoft/testfx/issues/7442.
        // Some test frameworks (e.g. NUnit with [Values("one", "one")] or MSTest with folded
        // parameterized tests) can emit multiple test nodes that share the same Uid. Starting
        // activities for them used to throw because the underlying dictionary did not tolerate
        // duplicate keys.
        Mock<IPlatformActivity> activity1 = new();
        Mock<IPlatformActivity> activity2 = new();
        _otelService.SetupSequence(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Returns(activity1.Object)
            .Returns(activity2.Object);

        TestNode testNode = CreateTestNode("duplicate-uid");

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyInProgress(testNode, null);

        Assert.AreEqual(2, _startedCounter.Value);
    }

    [TestMethod]
    public void HandleTestResult_WithDuplicateUid_PairsActivitiesInFifoOrder()
    {
        // Regression test for https://github.com/microsoft/testfx/issues/7442.
        // When two in-flight activities share the same Uid, results must be paired with them
        // in FIFO order and each activity must be disposed exactly once.
        Mock<IPlatformActivity> activity1 = new();
        activity1.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity1.Object);
        Mock<IPlatformActivity> activity2 = new();
        activity2.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity2.Object);

        _otelService.SetupSequence(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Returns(activity1.Object)
            .Returns(activity2.Object);

        TestNode testNode = CreateTestNode("duplicate-uid");

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);
        _handler.NotifyFailed(testNode, new FailedTestNodeStateProperty());

        activity1.Verify(a => a.SetTag("test.result", "passed"), Times.Once);
        activity1.Verify(a => a.SetTag("test.result", "failed"), Times.Never);
        activity1.Verify(a => a.Dispose(), Times.Once);

        activity2.Verify(a => a.SetTag("test.result", "failed"), Times.Once);
        activity2.Verify(a => a.SetTag("test.result", "passed"), Times.Never);
        activity2.Verify(a => a.Dispose(), Times.Once);

        Assert.AreEqual(2, _completedCounter.Value);
    }

    [TestMethod]
    public void Dispose_WithMultipleActivitiesSharingUid_DisposesAllOfThem()
    {
        // Regression test for https://github.com/microsoft/testfx/issues/7442.
        // Orphaned activities sharing the same Uid must all be disposed.
        Mock<IPlatformActivity> activity1 = new();
        Mock<IPlatformActivity> activity2 = new();
        _otelService.SetupSequence(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Returns(activity1.Object)
            .Returns(activity2.Object);

        TestNode testNode = CreateTestNode("orphan-duplicate-uid");
        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyInProgress(testNode, null);

        _handler.Dispose();

        activity1.Verify(a => a.Dispose(), Times.Once);
        activity2.Verify(a => a.Dispose(), Times.Once);
    }

    public void Dispose()
        => _handler.Dispose();

    [TestMethod]
    public void HandleTestResult_WithPassedState_SetsSemanticConventionStatusAndCounts()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("semconv-passed");
        TestNode testNode = CreateTestNode("semconv-passed");

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        // The upstream test.case.result.status enum is "pass"/"fail"; the legacy attribute keeps "passed"/"failed".
        activity.Verify(a => a.SetTag("test.case.result.status", "pass"), Times.Once);
        activity.Verify(a => a.SetTag("test.result", "passed"), Times.Once);
        activity.Verify(a => a.SetStatus(PlatformActivityStatusCode.Ok, It.IsAny<string?>()), Times.Once);
        Assert.AreEqual(1, _testCaseResultCounter.Value);
        Assert.IsNotNull(_testCaseResultCounter.LastTags);
        Assert.Contains(t => t.Key == "test.case.result.status" && (string?)t.Value == "pass", _testCaseResultCounter.LastTags);
    }

    [TestMethod]
    public void HandleTestResult_WithFailedState_RecordsExceptionAndErrorAttributes()
    {
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("semconv-failed");
        string? eventName = null;
        IReadOnlyList<KeyValuePair<string, object?>>? exceptionEventTags = null;
        activity.Setup(a => a.AddEvent(It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<DateTimeOffset>()))
            .Callback<string, IEnumerable<KeyValuePair<string, object?>>?, DateTimeOffset>((name, tags, _) =>
            {
                eventName = name;
                exceptionEventTags = tags?.ToList();
            })
            .Returns(activity.Object);
        TestNode testNode = CreateTestNode("semconv-failed");
        InvalidOperationException exception = new("boom");

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyFailed(testNode, new FailedTestNodeStateProperty(exception, "test failed"));

        activity.Verify(a => a.RecordException(exception, It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>()), Times.Never);
        Assert.AreEqual("exception", eventName);
        Assert.IsNotNull(exceptionEventTags);
        Assert.Contains(t => t.Key == "exception.type" && (string?)t.Value == typeof(InvalidOperationException).FullName, exceptionEventTags);
        Assert.Contains(t => t.Key == "exception.message" && (string?)t.Value == "boom", exceptionEventTags);
        Assert.Contains(t => t.Key == "exception.stacktrace" && (string?)t.Value == exception.ToString(), exceptionEventTags);
        activity.Verify(a => a.SetStatus(PlatformActivityStatusCode.Error, "boom"), Times.Once);
        activity.Verify(a => a.SetTag("error.type", typeof(InvalidOperationException).FullName), Times.Once);

        // error.message is deprecated upstream and NOT RECOMMENDED on spans; the message travels on the
        // exception event and on the legacy attribute instead.
        activity.Verify(a => a.SetTag("error.message", It.IsAny<object?>()), Times.Never);
        activity.Verify(a => a.SetTag("test.result.exception.message", "boom"), Times.Once);
        Assert.Contains(t => t.Key == "test.case.result.status" && (string?)t.Value == "fail", _testCaseResultCounter.LastTags!);
    }

    [TestMethod]
    public void HandleTestResult_WithTimingProperty_RecordsSecondsOnSemanticConventionHistogram()
    {
        SetupActivityForTestNode("semconv-duration");
        TimingInfo timing = new(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), TimeSpan.FromSeconds(2));
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("semconv-duration"),
            DisplayName = "Test",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, new TimingProperty(timing)),
        };

        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        // OpenTelemetry requires durations in seconds, while the legacy instrument stays in milliseconds.
        Assert.AreEqual(2d, _testCaseDurationHistogram.LastRecordedValue);
        Assert.AreEqual(2000d, _durationHistogram.LastRecordedValue);
    }

    [TestMethod]
    public void HandleTestResult_WithoutTrackedActivity_StillRecordsDurationAndCount()
    {
        TimingInfo timing = new(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(1), TimeSpan.FromSeconds(1));
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("no-activity"),
            DisplayName = "Test",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, new TimingProperty(timing)),
        };

        // No NotifyInProgress: frameworks that only publish final results must still produce latency data.
        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(1d, _testCaseDurationHistogram.LastRecordedValue);
        Assert.AreEqual(1, _testCaseResultCounter.Value);
    }

    [TestMethod]
    public void NotifyInProgress_EmitsSemanticConventionAttributes()
    {
        IEnumerable<KeyValuePair<string, object?>>? capturedTags = null;
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Callback<string, IEnumerable<KeyValuePair<string, object?>>?, string?, DateTimeOffset>((_, tags, _, _) => capturedTags = tags?.ToList())
            .Returns(new Mock<IPlatformActivity>().Object);

        _handler.NotifyInProgress(CreateTestNode("semconv-tags"), new TestNodeUid("parent"));

        Assert.IsNotNull(capturedTags);
        var tagList = capturedTags.ToList();
        Assert.IsTrue(tagList.Exists(t => t.Key == "test.case.name" && (string?)t.Value == "Test"));
        Assert.IsTrue(tagList.Exists(t => t.Key == "test.case.id" && (string?)t.Value == "semconv-tags"));
        Assert.IsTrue(tagList.Exists(t => t.Key == "test.case.parent.id" && (string?)t.Value == "parent"));
    }

    [TestMethod]
    public void NotifyRunCompleted_RecordsRunDurationWithVerdict()
    {
        Mock<IPlatformActivity> runActivity = new();
        _handler.NotifyRunCompleted(totalRanTests: 10, failedTests: 2, skippedTests: 1, exitCode: (int)ExitCode.AtLeastOneTestFailed, runActivity.Object);

        Assert.IsNotNull(_testRunDurationHistogram.LastRecordedValue);
        Assert.IsNotNull(_testRunDurationHistogram.LastTags);

        // Only bounded dimensions belong on the histogram: the raw counts would create a new time series per
        // distinct value.
        Assert.Contains(t => t.Key == "test.run.result.status" && (string?)t.Value == "fail", _testRunDurationHistogram.LastTags);
        Assert.Contains(t => t.Key == "test.run.exit_code" && (int?)t.Value == (int)ExitCode.AtLeastOneTestFailed, _testRunDurationHistogram.LastTags);
        Assert.DoesNotContain(t => t.Key == "test.run.total", _testRunDurationHistogram.LastTags);

        // The unbounded counts go on the span instead, where cardinality is free.
        runActivity.Verify(a => a.SetTag("test.run.total", 10), Times.Once);
        runActivity.Verify(a => a.SetTag("test.run.failed", 2), Times.Once);
        runActivity.Verify(a => a.SetTag("test.run.skipped", 1), Times.Once);
    }

    [TestMethod]
    public void NotifyRunCompleted_WithNonSuccessExitCodeAndNoFailedTests_RecordsFailedVerdict()
    {
        _handler.NotifyRunCompleted(totalRanTests: 0, failedTests: 0, skippedTests: 0, exitCode: (int)ExitCode.ZeroTests);

        Assert.IsNotNull(_testRunDurationHistogram.LastTags);
        Assert.Contains(t => t.Key == "test.run.result.status" && (string?)t.Value == "fail", _testRunDurationHistogram.LastTags);
        Assert.Contains(t => t.Key == "test.run.exit_code" && (int?)t.Value == (int)ExitCode.ZeroTests, _testRunDurationHistogram.LastTags);
    }

    [TestMethod]
    public void ActiveTestCases_GoesBackToZeroWhenTestsComplete()
    {
        SetupActivityForTestNode("active");
        TestNode testNode = CreateTestNode("active");

        _handler.NotifyInProgress(testNode, null);
        Assert.AreEqual(1, _activeTestCases.Value);

        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);
        Assert.AreEqual(0, _activeTestCases.Value);
    }

    [TestMethod]
    public void ActiveTestCases_GoesBackToZero_WhenNoTracerIsListening()
    {
        // StartActivity returns null whenever nothing subscribes to the activity source, which is the normal state
        // for a metrics-only configuration. The in-flight bookkeeping must not depend on a span existing.
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns((IPlatformActivity?)null);

        TestNode testNode = CreateTestNode("no-tracer");
        _handler.NotifyInProgress(testNode, null);
        Assert.AreEqual(1, _activeTestCases.Value);

        _handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(0, _activeTestCases.Value);
        Assert.AreEqual(1, _testCaseResultCounter.Value);
    }

    [TestMethod]
    public void ActiveTestCases_GoesBackToZero_WhenExecutionCompletesWithoutTracer()
    {
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns((IPlatformActivity?)null);

        TestNode testNode = CreateTestNode("no-tracer-completed");
        _handler.NotifyInProgress(testNode, null);
        _handler.NotifyExecutionCompleted(testNode);

        Assert.AreEqual(0, _activeTestCases.Value);
    }

    [TestMethod]
    public void NotifyInProgress_AfterDispose_ClosesTheSpanAndDoesNotTrackIt()
    {
        // A cancelled run skips the message-bus drain, so a consumer can still publish results while the host is
        // disposing us. That must not throw or leave a span open.
        Mock<IPlatformActivity> activity = SetupActivityForTestNode("late");
        _handler.Dispose();

        _handler.NotifyInProgress(CreateTestNode("late"), null);

        activity.Verify(a => a.Dispose(), Times.Once);
        Assert.AreEqual(0, _activeTestCases.Value);
    }

    [TestMethod]
    public void Dispose_WhileResultsAreStillArriving_BalancesActiveCountAndDoesNotThrow()
    {
        SetupActivityForTestNode("racing");
        for (int i = 0; i < 200; i++)
        {
            _handler.NotifyInProgress(CreateTestNode($"racing-{i}"), null);
        }

        // Concurrently completing results while disposing used to throw "collection was modified" out of the
        // telemetry path during shutdown.
        Task publisher = Task.Factory.StartNew(
            () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    _handler.NotifyPassed(CreateTestNode($"racing-{i}"), PassedTestNodeStateProperty.CachedInstance);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        _handler.Dispose();
        publisher.GetAwaiter().GetResult();

        // Every in-flight entry is decremented exactly once, whether it was completed by the publisher or drained
        // by Dispose, so the gauge must settle at zero no matter how the two interleave.
        Assert.AreEqual(0, _activeTestCases.Value);
    }

    [TestMethod]
    public void NotifyRunCompleted_CalledTwice_RecordsRunDurationOnce()
    {
        _handler.NotifyRunCompleted(totalRanTests: 1, failedTests: 0, skippedTests: 0, exitCode: 0);
        _testRunDurationHistogram.Reset();

        _handler.NotifyRunCompleted(totalRanTests: 1, failedTests: 0, skippedTests: 0, exitCode: 0);

        Assert.IsNull(_testRunDurationHistogram.LastRecordedValue);
    }

    [TestMethod]
    public void NotifyInProgress_EmitsFullyQualifiedCodeFunctionName()
    {
        IEnumerable<KeyValuePair<string, object?>>? capturedTags = CaptureInProgressTags(
            new TestMethodIdentifierProperty("MyAssembly", "My.Namespace", "MyClass", "MyMethod", 0, [], "void"));

        Assert.IsNotNull(capturedTags);
        Assert.Contains(t => t.Key == "code.function.name" && (string?)t.Value == "My.Namespace.MyClass.MyMethod", capturedTags);

        // code.namespace is deprecated upstream and must not be emitted.
        Assert.DoesNotContain(t => t.Key == "code.namespace", capturedTags);
    }

    [TestMethod]
    [DataRow("")]
    // Roslyn's INamespaceSymbol.ToDisplayString() returns this for the global namespace, so a framework that
    // does not first check IsGlobalNamespace hands us the sentinel rather than an empty string.
    [DataRow("<global namespace>")]
    public void NotifyInProgress_ForTheGlobalNamespace_DoesNotEmitALeadingDot(string @namespace)
    {
        IEnumerable<KeyValuePair<string, object?>>? capturedTags = CaptureInProgressTags(
            new TestMethodIdentifierProperty("MyAssembly", @namespace, "MyClass", "MyMethod", 0, [], "void"));

        Assert.IsNotNull(capturedTags);
        Assert.Contains(t => t.Key == "code.function.name" && (string?)t.Value == "MyClass.MyMethod", capturedTags);
    }

    private IEnumerable<KeyValuePair<string, object?>>? CaptureInProgressTags(TestMethodIdentifierProperty identifierProperty)
        => CaptureInProgressTags(new PropertyBag(identifierProperty));

    [TestMethod]
    public void NotifyInProgress_WithDuplicateIdentifierProperty_Throws()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => CaptureInProgressTags(new PropertyBag(
            new TestMethodIdentifierProperty("MyAssembly", "My.Namespace", "MyClass", "MyMethod", 0, [], "void"),
            new TestMethodIdentifierProperty("MyAssembly", "My.Namespace", "MyClass", "MyOtherMethod", 0, [], "void"))));

        Assert.AreEqual($"Found multiple properties of type '{typeof(TestMethodIdentifierProperty)}'.", exception.Message);
    }

    [TestMethod]
    public void NotifyInProgress_WithDuplicateFileLocationProperty_Throws()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => CaptureInProgressTags(new PropertyBag(
            new TestFileLocationProperty("first.cs", new LinePositionSpan(new LinePosition(1, 0), new LinePosition(2, 0))),
            new TestFileLocationProperty("second.cs", new LinePositionSpan(new LinePosition(3, 0), new LinePosition(4, 0))))));

        Assert.AreEqual($"Found multiple properties of type '{typeof(TestFileLocationProperty)}'.", exception.Message);
    }

    private IEnumerable<KeyValuePair<string, object?>>? CaptureInProgressTags(PropertyBag properties)
    {
        IEnumerable<KeyValuePair<string, object?>>? capturedTags = null;
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Callback<string, IEnumerable<KeyValuePair<string, object?>>?, string?, DateTimeOffset>((_, tags, _, _) => capturedTags = tags?.ToList())
            .Returns(new Mock<IPlatformActivity>().Object);

        _handler.NotifyInProgress(
            new TestNode
            {
                Uid = new TestNodeUid("fqn"),
                DisplayName = "Test",
                Properties = properties,
            },
            null);

        return capturedTags;
    }

    private static TestNode CreateTestNode(string uid = "test-uid")
        => new()
        {
            Uid = new TestNodeUid(uid),
            DisplayName = "Test",
        };

    private static Exception CreateExceptionWithStackTrace(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private Mock<IPlatformActivity> SetupActivityForTestNode(string testNodeUid)
    {
        Mock<IPlatformActivity> activity = new();
        activity.SetupGet(a => a.Id).Returns($"activity-{testNodeUid}");
        activity.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity.Object);
        _otelService.Setup(s => s.StartActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns(activity.Object);

        return activity;
    }

    [TestMethod]
    public void HandleTestResult_TruncatesExceptionEventAndStatus()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT")).Returns("10");
        using OpenTelemetryResultHandler handler = new(_otelService.Object, PlatformOpenTelemetryOptions.FromEnvironment(environment.Object));

        Mock<IPlatformActivity> activity = SetupActivityForTestNode("truncated-exception");
        IReadOnlyList<KeyValuePair<string, object?>>? exceptionEventTags = null;
        activity.Setup(a => a.AddEvent(It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<DateTimeOffset>()))
            .Callback<string, IEnumerable<KeyValuePair<string, object?>>?, DateTimeOffset>((_, tags, _) => exceptionEventTags = tags?.ToList())
            .Returns(activity.Object);
        Exception exception = CreateExceptionWithStackTrace(new string('m', 5000));
        TestNode testNode = CreateTestNode("truncated-exception");

        handler.NotifyInProgress(testNode, null);
        handler.NotifyFailed(testNode, new FailedTestNodeStateProperty(exception));

        string expectedMessage = new string('m', 10) + "…";
        string expectedStackTrace = exception.ToString().Substring(0, 10) + "…";
        Assert.IsNotNull(exceptionEventTags);
        Assert.Contains(t => t.Key == "exception.message" && (string?)t.Value == expectedMessage, exceptionEventTags);
        Assert.Contains(t => t.Key == "exception.stacktrace" && (string?)t.Value == expectedStackTrace, exceptionEventTags);
        activity.Verify(a => a.SetStatus(PlatformActivityStatusCode.Error, expectedMessage), Times.Once);
        activity.Verify(a => a.RecordException(exception, It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>()), Times.Never);
    }

    [TestMethod]
    public void HandleTestResult_TruncatesLegacyAttributesToo()
    {
        // Legacy attributes are on by default, so leaving them untruncated would defeat the size limit entirely.
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT")).Returns("10");
        using OpenTelemetryResultHandler handler = new(_otelService.Object, PlatformOpenTelemetryOptions.FromEnvironment(environment.Object));

        Mock<IPlatformActivity> activity = SetupActivityForTestNode("truncation");
        string longOutput = new('x', 5000);
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("truncation"),
            DisplayName = "Test",
            Properties = new PropertyBag(
                PassedTestNodeStateProperty.CachedInstance,
                new StandardOutputProperty(longOutput),
                new StandardErrorProperty(longOutput)),
        };

        handler.NotifyInProgress(testNode, null);
        handler.NotifyPassed(testNode, new PassedTestNodeStateProperty(new string('e', 5000)));

        string expected = new string('x', 10) + "…";
        activity.Verify(a => a.SetTag("test.output.stdout", expected), Times.Once);
        activity.Verify(a => a.SetTag("test.stdout", expected), Times.Once);
        activity.Verify(a => a.SetTag("test.output.stderr", expected), Times.Once);
        activity.Verify(a => a.SetTag("test.stderr", expected), Times.Once);

        string expectedExplanation = new string('e', 10) + "…";
        activity.Verify(a => a.SetTag("test.case.result.explanation", expectedExplanation), Times.Once);
        activity.Verify(a => a.SetTag("test.result.explanation", expectedExplanation), Times.Once);
    }

    private sealed class FakeCounter<T> : ICounter<T>
        where T : struct
    {
        public T Value { get; private set; }

        public void Add(T delta)
            => Value = (T)(object)((int)(object)Value + (int)(object)delta);

        public void Add(T delta, IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            LastTags = tags;
            Add(delta);
        }

        public IEnumerable<KeyValuePair<string, object?>>? LastTags { get; private set; }
    }

    private sealed class FakeUpDownCounter<T> : IUpDownCounter<T>
        where T : struct
    {
        public T Value { get; private set; }

        public void Add(T delta, IEnumerable<KeyValuePair<string, object?>>? tags = null)
            => Value = (T)(object)((int)(object)Value + (int)(object)delta);
    }

    private sealed class FakeHistogram<T> : IHistogram<T>
        where T : struct
    {
        public T? LastRecordedValue { get; private set; }

        public IEnumerable<KeyValuePair<string, object?>>? LastTags { get; private set; }

        public void Record(T value)
            => LastRecordedValue = value;

        public void Record(T value, IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            LastTags = tags;
            LastRecordedValue = value;
        }

        public void Reset()
        {
            LastRecordedValue = null;
            LastTags = null;
        }
    }
}
