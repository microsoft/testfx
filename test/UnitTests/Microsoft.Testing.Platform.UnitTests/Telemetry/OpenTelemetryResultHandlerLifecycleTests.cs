// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class OpenTelemetryResultHandlerLifecycleTests
{
    private readonly Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> _otelService = new();
    private readonly RecordingUpDownCounter _activeTestCases = new();
    private readonly RecordingHistogram _runDuration = new();

    public OpenTelemetryResultHandlerLifecycleTests()
    {
        _otelService.Setup(s => s.CreateCounter<int>("test.case.result.count", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(new Mock<ICounter<int>>().Object);
        _otelService.Setup(s => s.CreateUpDownCounter<int>("test.case.active", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(_activeTestCases);
        _otelService.Setup(s => s.CreateHistogram<double>("test.case.duration", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(new Mock<IHistogram<double>>().Object);
        _otelService.Setup(s => s.CreateHistogram<double>("test.run.duration", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(_runDuration);
    }

    [TestMethod]
    public void Constructor_InstrumentsCarryTheirDescriptions()
    {
        using OpenTelemetryResultHandler handler = new(_otelService.Object);

        Assert.AreSequenceEqual<(string Name, string? Description)>(
            [
                ("test.case.active", "Number of test cases currently running."),
                ("test.case.duration", "Duration of a single test case."),
                ("test.case.result.count", "Number of test cases, dimensioned by result status."),
                ("test.run.duration", "Duration of the whole test run."),
            ],
            _otelService.Invocations
                .Where(i => i.Method.Name.StartsWith("Create", StringComparison.Ordinal))
                .Select(i => (Name: (string)i.Arguments[0], Description: (string?)i.Arguments[2]))
                .OrderBy(i => i.Name, StringComparer.Ordinal));
    }

    [TestMethod]
    public void Dispose_WithInFlightTests_RemovesThemFromActiveCountInASingleUpdate()
    {
        OpenTelemetryResultHandler handler = new(_otelService.Object);
        handler.NotifyInProgress(CreateTestNode("one"), null);
        handler.NotifyInProgress(CreateTestNode("two"), null);
        _activeTestCases.Deltas.Clear();

        handler.Dispose();
        handler.Dispose();

        Assert.AreSequenceEqual([-2], _activeTestCases.Deltas);
    }

    [TestMethod]
    public void Dispose_WithoutInFlightTests_DoesNotTouchActiveCount()
    {
        OpenTelemetryResultHandler handler = new(_otelService.Object);

        handler.Dispose();

        Assert.IsEmpty(_activeTestCases.Deltas);
    }

    [TestMethod]
    public void Dispose_WithCanonicalActivitiesInFlight_RemovesThemFromActiveCount()
    {
        using TestExecutionActivityBroker broker = new(_otelService.Object);
        OpenTelemetryResultHandler handler = new(_otelService.Object);
        handler.NotifyInProgress(CreateCanonicalNode(broker, "canonical-1", out _), null);
        handler.NotifyInProgress(CreateCanonicalNode(broker, "canonical-2", out _), null);
        Assert.AreEqual(2, _activeTestCases.Value);

        handler.Dispose();

        Assert.AreEqual(0, _activeTestCases.Value);
        Assert.AreEqual(-2, _activeTestCases.Deltas[^1]);
    }

    [TestMethod]
    public void NotifyInProgress_CanonicalActivityAfterDispose_IsNotCounted()
    {
        using TestExecutionActivityBroker broker = new(_otelService.Object);
        OpenTelemetryResultHandler handler = new(_otelService.Object);
        handler.Dispose();

        handler.NotifyInProgress(CreateCanonicalNode(broker, "late", out _), null);

        Assert.AreEqual(0, _activeTestCases.Value);
        Assert.IsEmpty(_activeTestCases.Deltas);
    }

    [TestMethod]
    public void NotifyExecutionCompleted_CanonicalActivity_FinalizesReservationAtRecordedEnd()
    {
        Mock<IPlatformTestExecutionActivity> activity = SetupCanonicalActivity();
        using TestExecutionActivityBroker broker = new(_otelService.Object);
        using OpenTelemetryResultHandler handler = new(_otelService.Object);
        TestNode node = CreateCanonicalNode(broker, "completed", out TestExecutionActivityReservation reservation);
        handler.NotifyInProgress(node, null);
        reservation.Activate();
        DateTimeOffset end = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        reservation.RecordExecutionEnd(end);
        activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);

        handler.NotifyExecutionCompleted(node);

        activity.Verify(a => a.Stop(end), Times.Once);
    }

    [TestMethod]
    public void NotifyPassed_WithSeveralFileArtifacts_TagsEachWithItsOwnIndex()
    {
        Dictionary<string, object?> tags = [];
        Mock<IPlatformActivity> activity = new();
        activity.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>()))
            .Callback<string, object?>((key, value) => tags[key] = value)
            .Returns(activity.Object);
        _otelService.Setup(s => s.StartActivity(It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>()))
            .Returns(activity.Object);
        using OpenTelemetryResultHandler handler = new(_otelService.Object);
        FileInfo first = new("first-artifact.log");
        FileInfo second = new("second-artifact.log");
        TestNode node = CreateTestNode("artifacts");
        node.Properties.Add(new FileArtifactProperty(first, "First"));
        node.Properties.Add(new FileArtifactProperty(second, "Second"));

        handler.NotifyInProgress(node, null);
        handler.NotifyPassed(node, PassedTestNodeStateProperty.CachedInstance);

        // The property bag does not guarantee enumeration order, so only the set of indexed paths is asserted.
        Assert.AreSequenceEqual(
            new[] { first.FullName, second.FullName }.OrderBy(p => p, StringComparer.Ordinal),
            new[] { (string)tags["test.artifact.file[0].path"]!, (string)tags["test.artifact.file[1].path"]! }.OrderBy(p => p, StringComparer.Ordinal));
        Assert.IsFalse(tags.ContainsKey("test.artifact.file[2].path"));
        Assert.IsFalse(tags.ContainsKey("test.artifact.file[-1].path"));
    }

    [TestMethod]
    [DataRow(0, 0, "pass")]
    [DataRow(1, 0, "fail")]
    [DataRow(0, 2, "fail")]
    [DataRow(2, 2, "fail")]
    public void NotifyRunCompleted_RecordsVerdictFromFailuresAndExitCode(int failed, int exitCode, string expectedStatus)
    {
        using OpenTelemetryResultHandler handler = new(_otelService.Object);

        handler.NotifyRunCompleted(totalRanTests: 3, failedTests: failed, skippedTests: 0, exitCode: exitCode);

        Assert.AreSequenceEqual<KeyValuePair<string, object?>>(
            [
                new("test.run.result.status", expectedStatus),
                new("test.run.exit_code", exitCode),
            ],
            _runDuration.LastTags!);
    }

    private static TestNode CreateTestNode(string uid)
        => new() { Uid = uid, DisplayName = uid };

    private static TestNode CreateCanonicalNode(TestExecutionActivityBroker broker, string uid, out TestExecutionActivityReservation reservation)
    {
        TestNode node = CreateTestNode(uid);
        reservation = broker.Reserve(node)!;
        node.Properties.Add(new TestExecutionActivityProperty(reservation, isFinalResult: false));
        return node;
    }

    private Mock<IPlatformTestExecutionActivity> SetupCanonicalActivity()
    {
        Mock<IPlatformTestExecutionActivity> activity = new();
        activity.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity.Object);
        activity.Setup(a => a.SetStatus(It.IsAny<PlatformActivityStatusCode>(), It.IsAny<string?>())).Returns(activity.Object);
        _otelService.Setup(s => s.StartTestExecutionActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns(activity.Object);
        return activity;
    }

    private sealed class RecordingUpDownCounter : IUpDownCounter<int>
    {
        public List<int> Deltas { get; } = [];

        public int Value => Deltas.Sum();

        public void Add(int delta, IEnumerable<KeyValuePair<string, object?>>? tags = null)
            => Deltas.Add(delta);
    }

    private sealed class RecordingHistogram : IHistogram<double>
    {
        public IEnumerable<KeyValuePair<string, object?>>? LastTags { get; private set; }

        public void Record(double value)
        {
        }

        public void Record(double value, IEnumerable<KeyValuePair<string, object?>>? tags)
            => LastTags = tags;
    }
}
