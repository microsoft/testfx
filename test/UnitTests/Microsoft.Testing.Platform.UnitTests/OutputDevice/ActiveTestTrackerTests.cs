// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class ActiveTestTrackerTests
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(60);

    [TestMethod]
    public void GetDueDiagnostics_UsesExponentialBackoff()
    {
        var clock = new FakeClock();
        var tracker = new ActiveTestTracker(SlowThreshold, clock.CreateStopwatch);
        tracker.Start("uid", "SlowTest");

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.IsEmpty(tracker.GetDueDiagnostics());

        clock.Advance(TimeSpan.FromSeconds(1));
        SlowTestDiagnostic[] firstDiagnostics = tracker.GetDueDiagnostics();
        Assert.HasCount(1, firstDiagnostics);
        SlowTestDiagnostic first = firstDiagnostics[0];
        Assert.AreEqual("uid", first.Uid.Value);
        Assert.AreEqual("SlowTest", first.DisplayName);
        Assert.AreEqual(TimeSpan.FromSeconds(60), first.Elapsed);

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.IsEmpty(tracker.GetDueDiagnostics());

        clock.Advance(TimeSpan.FromSeconds(1));
        SlowTestDiagnostic[] secondDiagnostics = tracker.GetDueDiagnostics();
        Assert.HasCount(1, secondDiagnostics);
        SlowTestDiagnostic second = secondDiagnostics[0];
        Assert.AreEqual(TimeSpan.FromSeconds(120), second.Elapsed);
    }

    [TestMethod]
    public void GetDueDiagnostics_WhenPollIsDelayed_ReportsOnceAndSkipsCatchUpThresholds()
    {
        var clock = new FakeClock();
        var tracker = new ActiveTestTracker(SlowThreshold, clock.CreateStopwatch);
        tracker.Start("uid", "SlowTest");

        clock.Advance(TimeSpan.FromSeconds(300));
        SlowTestDiagnostic[] diagnostics = tracker.GetDueDiagnostics();
        Assert.HasCount(1, diagnostics);
        SlowTestDiagnostic diagnostic = diagnostics[0];
        Assert.AreEqual(TimeSpan.FromSeconds(300), diagnostic.Elapsed);

        Assert.IsEmpty(tracker.GetDueDiagnostics());

        clock.Advance(TimeSpan.FromSeconds(179));
        Assert.IsEmpty(tracker.GetDueDiagnostics());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.HasCount(1, tracker.GetDueDiagnostics());
    }

    [TestMethod]
    public void Start_WhenUidIsAlreadyActive_DoesNotResetElapsedTimeOrBackoff()
    {
        var clock = new FakeClock();
        var tracker = new ActiveTestTracker(SlowThreshold, clock.CreateStopwatch);
        tracker.Start("uid", "OriginalName");

        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.HasCount(1, tracker.GetDueDiagnostics());

        tracker.Start("uid", "ReplacementName");
        clock.Advance(TimeSpan.FromSeconds(60));

        SlowTestDiagnostic[] diagnostics = tracker.GetDueDiagnostics();
        Assert.HasCount(1, diagnostics);
        SlowTestDiagnostic diagnostic = diagnostics[0];
        Assert.AreEqual("OriginalName", diagnostic.DisplayName);
        Assert.AreEqual(TimeSpan.FromSeconds(120), diagnostic.Elapsed);
    }

    [TestMethod]
    public void Complete_TracksSameNameTestsIndependentlyByUid()
    {
        var clock = new FakeClock();
        var tracker = new ActiveTestTracker(SlowThreshold, clock.CreateStopwatch);
        tracker.Start("uid-1", "SameName");
        tracker.Start("uid-2", "SameName");

        tracker.Complete("uid-1");
        clock.Advance(SlowThreshold);

        SlowTestDiagnostic[] diagnostics = tracker.GetDueDiagnostics();
        Assert.HasCount(1, diagnostics);
        SlowTestDiagnostic diagnostic = diagnostics[0];
        Assert.AreEqual("uid-2", diagnostic.Uid.Value);
        Assert.AreEqual("SameName", diagnostic.DisplayName);
    }

    [TestMethod]
    public void Start_WhenThresholdIsZero_DoesNotTrackTests()
    {
        var clock = new FakeClock();
        var tracker = new ActiveTestTracker(TimeSpan.Zero, clock.CreateStopwatch);

        tracker.Start("uid", "Test");
        clock.Advance(TimeSpan.FromDays(1));

        Assert.IsFalse(tracker.IsEnabled);
        Assert.IsEmpty(tracker.GetDueDiagnostics());
    }

    private sealed class FakeClock
    {
        private TimeSpan _now;

        public void Advance(TimeSpan duration) => _now += duration;

        public IStopwatch CreateStopwatch() => new FakeStopwatch(this, _now);

        private sealed class FakeStopwatch(FakeClock clock, TimeSpan start) : IStopwatch
        {
            public TimeSpan Elapsed => clock._now - start;

            public void Start()
            {
            }

            public void Stop()
            {
            }
        }
    }
}
