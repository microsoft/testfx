// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Exercises the real <see cref="OpenTelemetryPlatformService"/> against live <see cref="ActivityListener"/> and
/// <see cref="MeterListener"/> instances, covering activity semantics and service-level instrument registration.
/// </summary>
/// <remarks>
/// <see cref="Activity.Current"/> and the listener registries are process-global, so listeners only collect
/// activities and instruments created by this test instance (identified by a unique name prefix), and ambient
/// activity assertions are relative to the activity that was current when the test started.
/// </remarks>
[TestClass]
public sealed class OpenTelemetryPlatformServiceTests : IDisposable
{
    private static readonly FieldInfo ObservableInstrumentsField = typeof(OpenTelemetryPlatformService)
        .GetField("_observableInstruments", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly string _namePrefix = $"test-{Guid.NewGuid():N}-";
    private readonly List<Activity> _stoppedActivities = [];
    private readonly Activity? _ambientAtStart = Activity.Current;
    private readonly ActivityListener _listener;
    private readonly OpenTelemetryPlatformService _service = new();

    public OpenTelemetryPlatformServiceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryPlatformService.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName.StartsWith(_namePrefix, StringComparison.Ordinal))
                {
                    lock (_stoppedActivities)
                    {
                        _stoppedActivities.Add(activity);
                    }
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _service.Dispose();
    }

    [TestMethod]
    public void StartActivity_ByDefault_PublishesTheActivityAsCurrent()
    {
        using (_service.StartActivity(Name("ambient")))
        {
            Assert.IsNotNull(Activity.Current);
            Assert.AreEqual(Name("ambient"), Activity.Current.OperationName);
            Assert.IsTrue(_service.HasCurrentActivity);
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    [TestMethod]
    public void StartActivity_WithoutExplicitParent_InheritsTheAmbientActivity()
    {
        using IPlatformActivity? parent = _service.StartActivity(Name("parent"));
        Assert.IsNotNull(parent);

        using (IPlatformActivity? child = _service.StartActivity(Name("child")))
        {
            Assert.IsNotNull(child);
            Assert.AreEqual(parent.TraceId, child.TraceId);
        }

        Assert.AreEqual(parent.SpanId, Single().ParentSpanId.ToHexString());
    }

    [TestMethod]
    public void StartNonAmbientActivity_NeverBecomesCurrent()
    {
        // This is what keeps MSTest fixture spans out of the ExecutionContext that MSTest captures inside a
        // fixture and replays for the rest of the run.
        using (_service.StartNonAmbientActivity(Name("non-ambient")))
        {
            Assert.AreSame(_ambientAtStart, Activity.Current);
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);

        // The span is still timed and exported even though it was never current.
        Assert.AreEqual(Name("non-ambient"), Single().OperationName);
    }

    [TestMethod]
    public void StartNonAmbientActivity_DoesNotDisturbAnExistingAmbientActivity()
    {
        using (_service.StartActivity(Name("outer")))
        {
            Activity? ambient = Activity.Current;
            Assert.IsNotNull(ambient);

            using (IPlatformActivity? inner = _service.StartNonAmbientActivity(Name("inner")))
            {
                Assert.IsNotNull(inner);
                Assert.AreEqual(ambient.TraceId.ToHexString(), inner.TraceId);
                Assert.AreSame(ambient, Activity.Current);
            }

            Assert.AreEqual(ambient.SpanId, Single().ParentSpanId);

            // Stopping a non-ambient activity must not pop the ambient one.
            Assert.AreSame(ambient, Activity.Current);
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    [TestMethod]
    public void StartNonAmbientActivity_WithExplicitParentId_KeepsTheSameTrace()
    {
        using IPlatformActivity? parent = _service.StartActivity(Name("parent"));
        Assert.IsNotNull(parent);
        Assert.IsNotNull(parent.Id);

        using IPlatformActivity? child = _service.StartNonAmbientActivity(Name("child"), parentId: parent.Id);
        Assert.IsNotNull(child);
        Assert.AreEqual(parent.TraceId, child.TraceId);
        Assert.AreNotEqual(parent.SpanId, child.SpanId);
    }

    [TestMethod]
    public void SetStatus_MapsOntoActivityStatusCode()
    {
        using (IPlatformActivity? activity = _service.StartActivity(Name("status")))
        {
            Assert.IsNotNull(activity);
            activity.SetStatus(PlatformActivityStatusCode.Error, "it broke");
        }

        Activity stopped = Single();
        Assert.AreEqual(ActivityStatusCode.Error, stopped.Status);
        Assert.AreEqual("it broke", stopped.StatusDescription);
    }

    [TestMethod]
    public void RecordException_AddsTheConventionalExceptionEventAndFailsTheSpan()
    {
        InvalidOperationException exception = new("boom");

        using (IPlatformActivity? activity = _service.StartActivity(Name("exception")))
        {
            Assert.IsNotNull(activity);
            activity.RecordException(exception);
        }

        Activity stopped = Single();
        Assert.AreEqual(ActivityStatusCode.Error, stopped.Status);

        ActivityEvent exceptionEvent = stopped.Events.Single();
        Assert.AreEqual("exception", exceptionEvent.Name);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, GetTag(exceptionEvent, "exception.type"));
        Assert.AreEqual("boom", GetTag(exceptionEvent, "exception.message"));
        Assert.IsNotNull(GetTag(exceptionEvent, "exception.stacktrace"));
    }

    [TestMethod]
    public void RecordException_WithAdditionalTags_MergesThemOntoTheExceptionEvent()
    {
        InvalidOperationException exception = new("boom");

        using (IPlatformActivity? activity = _service.StartActivity(Name("exception-with-additional-tags")))
        {
            Assert.IsNotNull(activity);
            activity.RecordException(exception, [new("test.additional", "value")]);
        }

        Activity stopped = Single();
        Assert.AreEqual(ActivityStatusCode.Error, stopped.Status);
        ActivityEvent exceptionEvent = stopped.Events.Single();
        Assert.AreEqual("exception", exceptionEvent.Name);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, GetTag(exceptionEvent, "exception.type"));
        Assert.AreEqual(exception.Message, GetTag(exceptionEvent, "exception.message"));
        Assert.AreEqual(exception.ToString(), GetTag(exceptionEvent, "exception.stacktrace"));
        Assert.AreEqual("value", GetTag(exceptionEvent, "test.additional"));
    }

    [TestMethod]
    public void AddEvent_AddsANamedEventWithItsTags()
    {
        using (IPlatformActivity? activity = _service.StartActivity(Name("events")))
        {
            Assert.IsNotNull(activity);
            activity.AddEvent("hang.detected", [new("dump.path", "a.dmp")]);
        }

        ActivityEvent activityEvent = Single().Events.Single();
        Assert.AreEqual("hang.detected", activityEvent.Name);
        Assert.AreEqual("a.dmp", GetTag(activityEvent, "dump.path"));
    }

    [TestMethod]
    public void StartActivity_SetsTheTagsProvidedAtCreation()
    {
        using (_service.StartActivity(Name("tags"), tags: [new("test.case.name", "MyTest")]))
        {
        }

        Assert.AreEqual("MyTest", Single().GetTagItem("test.case.name"));
    }

    [TestMethod]
    public void SetTag_SetsTheTagAndReturnsTheSameActivityForChaining()
    {
        using (IPlatformActivity? activity = _service.StartActivity(Name("set-tag")))
        {
            Assert.IsNotNull(activity);
            Assert.AreSame(activity, activity.SetTag("test.case.name", "MyTest"));
        }

        Assert.AreEqual("MyTest", Single().GetTagItem("test.case.name"));
    }

    [TestMethod]
    public void TraceIdAndSpanId_WhenActivityIsNotW3CFormat_AreNull()
    {
        using Activity activity = new Activity(Name("hierarchical"))
            .SetIdFormat(ActivityIdFormat.Hierarchical)
            .Start();
        using IPlatformActivity wrapper = new ActivityWrapper(activity);

        Assert.IsNull(wrapper.TraceId);
        Assert.IsNull(wrapper.SpanId);
    }

    [TestMethod]
    public void TraceIdAndSpanId_WhenActivityIsW3CFormat_AreHexadecimalIdentifiers()
    {
        using IPlatformActivity? wrapper = _service.StartActivity(Name("w3c"));
        Assert.IsNotNull(wrapper);
        Assert.IsNotNull(Activity.Current);

        Assert.AreEqual(Activity.Current.TraceId.ToHexString(), wrapper.TraceId);
        Assert.AreEqual(Activity.Current.SpanId.ToHexString(), wrapper.SpanId);
    }

    [TestMethod]
    public void IsRecording_WhenListenerSamplesAllDataAndRecorded_IsTrue()
    {
        using IPlatformActivity? activity = _service.StartActivity(Name("recording"));
        Assert.IsNotNull(activity);

        Assert.IsTrue(activity.IsRecording);
    }

    [TestMethod]
    public void IsRecording_WhenNoListenerSamplesTheActivityForData_IsFalse()
    {
        string sourceName = Name("propagation-only");
        using ActivitySource source = new(sourceName);
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref _) => ActivitySamplingResult.PropagationData,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.PropagationData,
        };
        ActivitySource.AddActivityListener(listener);
        using Activity? activity = source.StartActivity(Name("not-recording"));
        Assert.IsNotNull(activity);
        using IPlatformActivity wrapper = new ActivityWrapper(activity);

        Assert.IsFalse(wrapper.IsRecording);
    }

    [TestMethod]
    public void Dispose_ForANonAmbientActivity_RestoresThePreviousAmbientActivity()
    {
        Activity nonAmbientActivity = new Activity(Name("non-ambient")).Start();
        Activity.Current = nonAmbientActivity.Parent;
        using Activity ambientActivity = new Activity(Name("ambient")).Start();

        IPlatformActivity wrapper = WrapNonAmbient(nonAmbientActivity);
        wrapper.Dispose();
        nonAmbientActivity.Dispose();

        Assert.AreSame(ambientActivity, Activity.Current);
    }

    [TestMethod]
    public void CreateCounter_ForwardsMetadataAndEmitsMeasurements()
    {
        string instrumentName = Name("counter");
        using MetricCapture capture = new(instrumentName);

        ICounter<long> counter = _service.CreateCounter<long>(
            instrumentName,
            unit: "{test}",
            description: "Completed tests",
            tags: [new("scope", "run")]);
        counter.Add(7, [new("outcome", "passed")]);

        PublishedInstrument instrument = capture.SingleInstrument();
        Assert.AreEqual(OpenTelemetryPlatformService.MeterName, instrument.MeterName);
        Assert.AreEqual(instrumentName, instrument.Name);
        Assert.AreEqual("{test}", instrument.Unit);
        Assert.AreEqual("Completed tests", instrument.Description);
        AssertSingleTag(instrument.Tags, "scope", "run");

        Measurement measurement = capture.SingleMeasurement();
        Assert.AreEqual(instrumentName, measurement.InstrumentName);
        Assert.AreEqual(7d, measurement.Value);
        AssertSingleTag(measurement.Tags, "outcome", "passed");
    }

    [TestMethod]
    public void CreateUpDownCounter_ForwardsMetadataAndEmitsMeasurements()
    {
        string instrumentName = Name("up-down-counter");
        using MetricCapture capture = new(instrumentName);

        IUpDownCounter<long> counter = _service.CreateUpDownCounter<long>(
            instrumentName,
            unit: "{test}",
            description: "Active tests",
            tags: [new("scope", "run")]);
        counter.Add(-2, [new("state", "completed")]);

        PublishedInstrument instrument = capture.SingleInstrument();
        Assert.AreEqual(OpenTelemetryPlatformService.MeterName, instrument.MeterName);
        Assert.AreEqual(instrumentName, instrument.Name);
        Assert.AreEqual("{test}", instrument.Unit);
        Assert.AreEqual("Active tests", instrument.Description);
        AssertSingleTag(instrument.Tags, "scope", "run");

        Measurement measurement = capture.SingleMeasurement();
        Assert.AreEqual(instrumentName, measurement.InstrumentName);
        Assert.AreEqual(-2d, measurement.Value);
        AssertSingleTag(measurement.Tags, "state", "completed");
    }

    [TestMethod]
    public void CreateHistogram_ForwardsMetadataAndEmitsMeasurements()
    {
        string instrumentName = Name("histogram");
        using MetricCapture capture = new(instrumentName);

        IHistogram<double> histogram = _service.CreateHistogram<double>(
            instrumentName,
            unit: "ms",
            description: "Test duration",
            tags: [new("scope", "test")]);
        histogram.Record(12.5, [new("outcome", "failed")]);

        PublishedInstrument instrument = capture.SingleInstrument();
        Assert.AreEqual(OpenTelemetryPlatformService.MeterName, instrument.MeterName);
        Assert.AreEqual(instrumentName, instrument.Name);
        Assert.AreEqual("ms", instrument.Unit);
        Assert.AreEqual("Test duration", instrument.Description);
        AssertSingleTag(instrument.Tags, "scope", "test");

        Measurement measurement = capture.SingleMeasurement();
        Assert.AreEqual(instrumentName, measurement.InstrumentName);
        Assert.AreEqual(12.5, measurement.Value);
        AssertSingleTag(measurement.Tags, "outcome", "failed");
    }

    [TestMethod]
    public void CreateObservableGauge_RootsTheInstrumentUntilDisposalAndObservesItsValue()
    {
        string instrumentName = Name("observable-gauge");
        using MetricCapture capture = new(instrumentName);
        int callbackCount = 0;

        _service.CreateObservableGauge(
            instrumentName,
            () =>
            {
                callbackCount++;
                return 42L;
            },
            unit: "{test}",
            description: "Queued tests");

        PublishedInstrument instrument = capture.SingleInstrument();
        Assert.AreEqual(OpenTelemetryPlatformService.MeterName, instrument.MeterName);
        Assert.AreEqual(instrumentName, instrument.Name);
        Assert.AreEqual("{test}", instrument.Unit);
        Assert.AreEqual("Queued tests", instrument.Description);
        Assert.IsEmpty(instrument.Tags);

        IReadOnlyCollection<object> observableInstruments = GetObservableInstruments();
        Assert.HasCount(1, observableInstruments);
        Assert.IsInstanceOfType<ObservableGauge<long>>(observableInstruments.Single());
        capture.RecordObservableInstruments();

        Assert.AreEqual(1, callbackCount);
        Measurement measurement = capture.SingleMeasurement();
        Assert.AreEqual(instrumentName, measurement.InstrumentName);
        Assert.AreEqual(42d, measurement.Value);
        Assert.IsEmpty(measurement.Tags);

        _service.Dispose();
        Assert.IsEmpty(GetObservableInstruments());
    }

    [TestMethod]
    public void StartNonAmbientActivity_WithRootTraceState_StampsTheActivity()
    {
        Activity? ambient = Activity.Current;
        try
        {
            Activity.Current = null;
            _service.RootTraceState = "vendor=state";

            using IPlatformActivity? activity = _service.StartNonAmbientActivity(Name("root-trace-state"));
            Assert.IsNotNull(activity);
            Assert.IsNull(Activity.Current);
        }
        finally
        {
            Activity.Current = ambient;
        }

        Assert.AreEqual("vendor=state", Single().TraceStateString);
        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    [TestMethod]
    public void StartActivity_WithRootTraceState_StampsTheActivity()
    {
        Activity? ambient = Activity.Current;
        try
        {
            Activity.Current = null;
            _service.RootTraceState = "vendor=state";

            using IPlatformActivity? activity = _service.StartActivity(Name("root-trace-state"));
            Assert.IsNotNull(activity);
            Assert.IsNotNull(Activity.Current);
            Assert.AreEqual(Name("root-trace-state"), Activity.Current.OperationName);
            Assert.AreEqual("vendor=state", Activity.Current.TraceStateString);
        }
        finally
        {
            Activity.Current = ambient;
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    [TestMethod]
    public void StartActivity_WithInheritedTraceState_DoesNotOverwriteIt()
    {
        _service.RootTraceState = "root=state";

        using (Activity parent = new Activity(Name("external-parent")).SetIdFormat(ActivityIdFormat.W3C))
        {
            parent.TraceStateString = "parent=state";
            parent.Start();

            using IPlatformActivity? child = _service.StartActivity(Name("child"));
            Assert.IsNotNull(child);
            Assert.AreEqual(parent.TraceId.ToString(), child.TraceId);
            Assert.IsNotNull(Activity.Current);
            Assert.AreEqual(Name("child"), Activity.Current.OperationName);
            Assert.AreEqual("parent=state", Activity.Current.TraceStateString);
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    [TestMethod]
    public void TestFrameworkActivity_RoundTripsTheAssignedActivity()
    {
        using IPlatformActivity? activity = _service.StartActivity(Name("test-framework"));
        Assert.IsNotNull(activity);

        _service.TestFrameworkActivity = activity;
        Assert.AreSame(activity, _service.TestFrameworkActivity);

        _service.TestFrameworkActivity = null;
        Assert.IsNull(_service.TestFrameworkActivity);
    }

    [TestMethod]
    public void Dispose_PreventsNewActivitiesFromBeingCreated()
    {
        _service.Dispose();

        Assert.IsNull(_service.StartActivity(Name("after-dispose")));
        Assert.IsNull(_service.StartNonAmbientActivity(Name("non-ambient-after-dispose")));
        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    private static void AssertSingleTag(KeyValuePair<string, object?>[] tags, string key, object? value)
    {
        Assert.HasCount(1, tags);
        Assert.AreEqual(key, tags[0].Key);
        Assert.AreEqual(value, tags[0].Value);
    }

    private static object? GetTag(ActivityEvent activityEvent, string key)
        => activityEvent.Tags
            .Where(tag => tag.Key == key)
            .Select(tag => tag.Value)
            .FirstOrDefault();

    private IReadOnlyCollection<object> GetObservableInstruments()
        => (IReadOnlyCollection<object>)ObservableInstrumentsField.GetValue(_service)!;

    private string Name(string name) => _namePrefix + name;

    private static IPlatformActivity WrapNonAmbient(Activity activity) => new ActivityWrapper(activity, isAmbient: false);

    private Activity Single()
    {
        lock (_stoppedActivities)
        {
            return _stoppedActivities.Single();
        }
    }

    private sealed record PublishedInstrument(
        string MeterName,
        string Name,
        string? Unit,
        string? Description,
        KeyValuePair<string, object?>[] Tags);

    private sealed record Measurement(
        string InstrumentName,
        double Value,
        KeyValuePair<string, object?>[] Tags);

    private sealed class MetricCapture : IDisposable
    {
        private readonly List<PublishedInstrument> _instruments = [];
        private readonly List<Measurement> _measurements = [];
        private readonly MeterListener _listener;

        public MetricCapture(string instrumentName)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name != OpenTelemetryPlatformService.MeterName
                        || instrument.Name != instrumentName)
                    {
                        return;
                    }

                    lock (_instruments)
                    {
                        _instruments.Add(new(
                            instrument.Meter.Name,
                            instrument.Name,
                            instrument.Unit,
                            instrument.Description,
                            instrument.Tags?.ToArray() ?? []));
                    }

                    listener.EnableMeasurementEvents(instrument);
                },
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();

        public void RecordObservableInstruments() => _listener.RecordObservableInstruments();

        public PublishedInstrument SingleInstrument()
        {
            lock (_instruments)
            {
                Assert.HasCount(1, _instruments);
                return _instruments[0];
            }
        }

        public Measurement SingleMeasurement()
        {
            lock (_instruments)
            {
                Assert.HasCount(1, _measurements);
                return _measurements[0];
            }
        }

        private void Capture<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            where T : struct
        {
            lock (_instruments)
            {
                _measurements.Add(new(
                    instrument.Name,
                    Convert.ToDouble(measurement, CultureInfo.InvariantCulture),
                    tags.ToArray()));
            }
        }
    }
}
