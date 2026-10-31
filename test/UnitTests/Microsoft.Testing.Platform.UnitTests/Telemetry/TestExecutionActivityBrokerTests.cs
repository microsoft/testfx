// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestExecutionActivityBrokerTests
{
    private readonly Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> _service = new();
    private readonly Mock<IPlatformTestExecutionActivity> _activity = new();

    public TestExecutionActivityBrokerTests()
    {
        _activity.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(_activity.Object);
        _activity.Setup(a => a.SetStatus(It.IsAny<PlatformActivityStatusCode>(), It.IsAny<string?>())).Returns(_activity.Object);
        _activity.Setup(a => a.AddEvent(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<DateTimeOffset>())).Returns(_activity.Object);
        _service.Setup(s => s.StartTestExecutionActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns(_activity.Object);
    }

    [TestMethod]
    public void FinalResultBeforeExecutionEnd_StopsAtExecutionEnd()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        string? aggregateResult = null;

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, result, _, _) => aggregateResult = result));
        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);
        Assert.IsNull(aggregateResult);

        DateTimeOffset executionEnd = DateTimeOffset.UtcNow;
        reservation.RecordExecutionEnd(executionEnd);

        Assert.AreEqual(TestingPlatformSemanticConventions.TestResultStatus.Pass, aggregateResult);
        _activity.Verify(a => a.Stop(executionEnd), Times.Once);
    }

    [TestMethod]
    public void ExecutionEndBeforeFinalResult_StopsAfterEnrichment()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        DateTimeOffset executionEnd = DateTimeOffset.UtcNow;
        reservation.RecordExecutionEnd(executionEnd);
        bool enriched = false;

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, _, _, _) =>
            {
                _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);
                enriched = true;
            }));

        Assert.IsTrue(enriched);
        _activity.Verify(a => a.Stop(executionEnd), Times.Once);
    }

    [TestMethod]
    public void MultipleResults_AggregatesWorstAuthoritativeResult()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        reservation.RecordExecutionEnd(DateTimeOffset.UtcNow);
        string? aggregateResult = null;

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Fail,
            contributesToAggregate: true,
            isFinalResult: false,
            (_, result, _, _) => aggregateResult = result));
        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, _, _, _) => Assert.Fail("The lower-priority passing result must not provide final enrichment.")));

        Assert.AreEqual(TestingPlatformSemanticConventions.TestResultStatus.Fail, aggregateResult);
        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [TestMethod]
    public void SupersededRetryResult_DoesNotDecideAggregateStatus()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        reservation.RecordExecutionEnd(DateTimeOffset.UtcNow);
        string? aggregateResult = null;

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Fail,
            contributesToAggregate: false,
            isFinalResult: false,
            (_, result, _, _) => Assert.IsNull(result)));
        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, result, _, _) => aggregateResult = result));

        Assert.AreEqual(TestingPlatformSemanticConventions.TestResultStatus.Pass, aggregateResult);
    }

    [TestMethod]
    public void DuplicateFinalResult_DoesNotStopTwice()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        reservation.RecordExecutionEnd(DateTimeOffset.UtcNow);

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, _, _, _) => { }));
        Assert.IsFalse(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Fail,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, _, _, _) => Assert.Fail("A finalized reservation must reject late enrichment.")));

        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [TestMethod]
    public void RepeatedExecutionEnd_UsesFirstTimestamp()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        DateTimeOffset firstEnd = DateTimeOffset.UtcNow;
        reservation.RecordExecutionEnd(firstEnd);
        reservation.RecordExecutionEnd(firstEnd.AddMinutes(1));

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, _, _, _) => { }));

        _activity.Verify(a => a.Stop(firstEnd), Times.Once);
    }

    [TestMethod]
    public void ExecutionEndBeforeActivation_IsClampedToActivityStart()
    {
        DateTimeOffset? activityStart = null;
        _service.Setup(s => s.StartTestExecutionActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>()))
            .Callback<string, IEnumerable<KeyValuePair<string, object?>>?, string?, DateTimeOffset>(
                (_, _, _, startTime) => activityStart = startTime)
            .Returns(_activity.Object);
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = ReserveWithoutActivation(broker);
        DateTimeOffset capturedEnd = DateTimeOffset.UtcNow;
        reservation.Activate();
        reservation.RecordExecutionEnd(capturedEnd);
        DateTimeOffset? enrichedEnd = null;
        TimeSpan? enrichedDuration = null;

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Skipped,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, _, endTime, duration) =>
            {
                enrichedEnd = endTime;
                enrichedDuration = duration;
            }));

        Assert.IsNotNull(activityStart);
        Assert.AreEqual(activityStart, enrichedEnd);
        Assert.AreEqual(TimeSpan.Zero, enrichedDuration);
        _activity.Verify(a => a.Stop(activityStart.Value), Times.Once);
    }

    [TestMethod]
    public void Dispose_StopsAbandonedActivityOnceAtRecordedExecutionEnd()
    {
        TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        DateTimeOffset executionEnd = DateTimeOffset.UtcNow;
        reservation.RecordExecutionEnd(executionEnd);

        broker.Dispose();
        broker.Dispose();

        _activity.Verify(a => a.Stop(executionEnd), Times.Once);
    }

    [TestMethod]
    public void Dispose_DuringEnrichment_DefersStopUntilCallbackReturns()
    {
        TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        DateTimeOffset executionEnd = DateTimeOffset.UtcNow;
        reservation.RecordExecutionEnd(executionEnd);

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (_, _, _, _) =>
            {
                broker.Dispose();
                _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);
            }));

        _activity.Verify(a => a.Stop(executionEnd), Times.Once);
    }

    [TestMethod]
    public void SampledOutActivity_StillUsesCanonicalReservation()
    {
        _service.Setup(s => s.StartTestExecutionActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns((IPlatformTestExecutionActivity?)null);
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        reservation.RecordExecutionEnd(DateTimeOffset.UtcNow);
        bool enriched = false;

        Assert.IsTrue(reservation.ProcessResult(
            TestingPlatformSemanticConventions.TestResultStatus.Pass,
            contributesToAggregate: true,
            isFinalResult: true,
            (activity, _, _, _) =>
            {
                Assert.IsNull(activity);
                enriched = true;
            }));

        Assert.IsTrue(enriched);
    }

    [TestMethod]
    public void Enter_UsesActivityScopeWithoutStopping()
    {
        Mock<IDisposable> scope = new();
        _activity.Setup(a => a.Enter()).Returns(scope.Object);
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);

        IDisposable? actualScope = reservation.Enter();

        Assert.AreSame(scope.Object, actualScope);
        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [TestMethod]
    public void Reserve_DoesNotStartActivityUntilActivated()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = ReserveWithoutActivation(broker);

        _service.Verify(
            s => s.StartTestExecutionActivity(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>()),
            Times.Never);

        reservation.Activate();
        reservation.Activate();

        _service.Verify(
            s => s.StartTestExecutionActivity(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>()),
            Times.Once);
    }

    private TestExecutionActivityBroker CreateBroker()
        => new(_service.Object, PlatformOpenTelemetryOptions.Default);

    private static TestExecutionActivityReservation Reserve(TestExecutionActivityBroker broker)
    {
        TestExecutionActivityReservation reservation = ReserveWithoutActivation(broker);
        reservation.Activate();
        return reservation;
    }

    private static TestExecutionActivityReservation ReserveWithoutActivation(TestExecutionActivityBroker broker)
        => broker.Reserve(new TestNode
        {
            Uid = new TestNodeUid(Guid.NewGuid().ToString()),
            DisplayName = "Test",
        })!;

    [TestMethod]
    public void Activate_ThrowingFactoryIsRethrownAndReservationIsPermanentlyRejected()
    {
        InvalidOperationException expected = new("activity rejected");
        _service.Setup(s => s.StartTestExecutionActivity(
            It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>()))
            .Throws(expected);
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = ReserveWithoutActivation(broker);

        Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(reservation.Activate));
        reservation.Activate();
        reservation.RecordExecutionEnd(DateTimeOffset.UtcNow);
        Assert.IsNull(reservation.Enter());
        Assert.IsFalse(reservation.ProcessResult("pass", true, true, (_, _, _, _) => Assert.Fail("Rejected activation must not accept results.")));
        _service.Verify(
            s => s.StartTestExecutionActivity(It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>()),
            Times.Once);
        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [TestMethod]
    public void Activate_ReentrantDisposeSealsAndStopsReturnedActivityExactlyOnce()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        _service.Setup(s => s.StartTestExecutionActivity(
            It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>()))
            .Returns(() =>
            {
                broker.Dispose();
                return _activity.Object;
            });
        TestExecutionActivityReservation reservation = ReserveWithoutActivation(broker);

        reservation.Activate();
        reservation.Activate();
        broker.Dispose();

        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
        Assert.IsFalse(reservation.ProcessResult("fail", true, true, (_, _, _, _) => Assert.Fail("Disposed activity cannot be enriched.")));
        Assert.IsNull(reservation.Enter());
        Assert.IsNull(broker.Reserve(new TestNode { Uid = "late", DisplayName = "Late case" }));
    }

    [TestMethod]
    public void CompleteWithoutResult_FinalizesOnlyAfterExecutionEndAndRejectsLateUpdates()
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        reservation.CompleteWithoutResult();
        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);
        DateTimeOffset end = DateTimeOffset.UtcNow;

        reservation.RecordExecutionEnd(end);
        reservation.CompleteWithoutResult();
        reservation.RecordExecutionEnd(end.AddHours(1));

        _activity.Verify(a => a.Stop(end), Times.Once);
        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
        Assert.IsFalse(reservation.ProcessResult("error", true, true, (_, _, _, _) => Assert.Fail("Finalized reservation cannot accept results.")));
    }

    [TestMethod]
    [DataRow("error", "timeout", "error")]
    [DataRow("timeout", "fail", "timeout")]
    [DataRow("fail", "cancelled", "fail")]
    [DataRow("cancelled", "pass", "cancelled")]
    [DataRow("pass", "skipped", "pass")]
    [DataRow("skipped", "unknown", "skipped")]
    public void ProcessResult_PreservesWorstStatusAcrossEveryPriority(string higher, string lower, string expected)
    {
        using TestExecutionActivityBroker broker = CreateBroker();
        TestExecutionActivityReservation reservation = Reserve(broker);
        reservation.RecordExecutionEnd(DateTimeOffset.UtcNow);
        string? actual = null;
        Assert.IsTrue(reservation.ProcessResult(lower, true, false, (_, result, _, _) => actual = result));
        Assert.IsTrue(reservation.ProcessResult(higher, true, false, (_, result, _, _) => actual = result));
        Assert.IsTrue(reservation.ProcessResult(lower, true, true, (_, _, _, _) => Assert.Fail("A lower priority result cannot replace the winning enrichment.")));

        Assert.AreEqual(expected, actual);
        _activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
    }
}
