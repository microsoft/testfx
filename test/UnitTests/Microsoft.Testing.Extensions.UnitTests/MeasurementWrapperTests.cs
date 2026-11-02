// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;
using System.Globalization;

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Exercises the metric wrappers against a live <see cref="MeterListener"/>, which is the only way to observe both
/// the value and the tags that actually reach a <c>System.Diagnostics.Metrics</c> instrument.
/// </summary>
/// <remarks>
/// MSTest creates one instance per test method, so every test gets its own uniquely named <see cref="Meter"/> and
/// listener and can never observe measurements emitted by a concurrently running test.
/// </remarks>
[TestClass]
public sealed class MeasurementWrapperTests : IDisposable
{
    private readonly string _meterName = $"test-{Guid.NewGuid():N}";
    private readonly List<(string Instrument, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
    private readonly Meter _meter;
    private readonly MeterListener _listener;

    public MeasurementWrapperTests()
    {
        _meter = new Meter(_meterName);
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == _meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _meter.Dispose();
    }

    [TestMethod]
    public void CounterWrapper_AddWithoutTags_EmitsAnUntaggedMeasurement()
    {
        ICounter<long> counter = new CounterWrapper<long>(_meter.CreateCounter<long>("tests.run"));

        counter.Add(3);

        (string instrument, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual("tests.run", instrument);
        Assert.AreEqual(3d, value);
        Assert.IsEmpty(tags);
    }

    [TestMethod]
    public void CounterWrapper_AddWithNullTags_EmitsAnUntaggedMeasurement()
    {
        ICounter<long> counter = new CounterWrapper<long>(_meter.CreateCounter<long>("tests.run"));

        counter.Add(3, tags: null);

        (_, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual(3d, value);
        Assert.IsEmpty(tags);
    }

    [TestMethod]
    public void CounterWrapper_AddWithTags_ForwardsTheTags()
    {
        ICounter<long> counter = new CounterWrapper<long>(_meter.CreateCounter<long>("tests.run"));

        counter.Add(1, [new KeyValuePair<string, object?>("outcome", "passed")]);

        (_, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual(1d, value);
        AssertSingleTag(tags, "outcome", "passed");
    }

    [TestMethod]
    public void UpDownCounterWrapper_AddWithoutTags_EmitsAnUntaggedMeasurement()
    {
        IUpDownCounter<long> counter = new UpDownCounterWrapper<long>(_meter.CreateUpDownCounter<long>("tests.active"));

        counter.Add(-2);

        (string instrument, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual("tests.active", instrument);
        Assert.AreEqual(-2d, value);
        Assert.IsEmpty(tags);
    }

    [TestMethod]
    public void UpDownCounterWrapper_AddWithTags_ForwardsTheTags()
    {
        IUpDownCounter<long> counter = new UpDownCounterWrapper<long>(_meter.CreateUpDownCounter<long>("tests.active"));

        counter.Add(1, [new KeyValuePair<string, object?>("state", "running")]);

        (_, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual(1d, value);
        AssertSingleTag(tags, "state", "running");
    }

    [TestMethod]
    public void HistogramWrapper_RecordWithoutTags_EmitsAnUntaggedMeasurement()
    {
        IHistogram<double> histogram = new HistogramWrapper<double>(_meter.CreateHistogram<double>("tests.duration"));

        histogram.Record(1.5);

        (string instrument, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual("tests.duration", instrument);
        Assert.AreEqual(1.5, value);
        Assert.IsEmpty(tags);
    }

    [TestMethod]
    public void HistogramWrapper_RecordWithNullTags_EmitsAnUntaggedMeasurement()
    {
        IHistogram<double> histogram = new HistogramWrapper<double>(_meter.CreateHistogram<double>("tests.duration"));

        histogram.Record(1.5, tags: null);

        (_, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual(1.5, value);
        Assert.IsEmpty(tags);
    }

    [TestMethod]
    public void HistogramWrapper_RecordWithTags_ForwardsTheTags()
    {
        IHistogram<double> histogram = new HistogramWrapper<double>(_meter.CreateHistogram<double>("tests.duration"));

        histogram.Record(2.5, [new KeyValuePair<string, object?>("outcome", "failed")]);

        (_, double value, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.AreEqual(2.5, value);
        AssertSingleTag(tags, "outcome", "failed");
    }

    [TestMethod]
    public void Wrappers_WithAnEmptyTagCollection_EmitAnUntaggedMeasurement()
    {
        ICounter<long> counter = new CounterWrapper<long>(_meter.CreateCounter<long>("tests.run"));

        counter.Add(1, new List<KeyValuePair<string, object?>>());

        (_, _, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        Assert.IsEmpty(tags);
    }

    [TestMethod]
    public void Wrappers_WithALazyTagSequence_MaterializeItExactlyOnce()
    {
        ICounter<long> counter = new CounterWrapper<long>(_meter.CreateCounter<long>("tests.run"));
        int enumerations = 0;

        counter.Add(1, Lazy());

        (_, _, KeyValuePair<string, object?>[] tags) = SingleMeasurement();
        AssertSingleTag(tags, "outcome", "skipped");
        Assert.AreEqual(1, enumerations);

        IEnumerable<KeyValuePair<string, object?>> Lazy()
        {
            enumerations++;
            yield return new KeyValuePair<string, object?>("outcome", "skipped");
        }
    }

    [TestMethod]
    public void Wrappers_WithANullInstrument_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CounterWrapper<long>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpDownCounterWrapper<long>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HistogramWrapper<double>(null!));
    }

    [TestMethod]
    public void MeasurementTags_ToArray_ReusesTheCallerArrayAndNeverReturnsNull()
    {
        KeyValuePair<string, object?>[] source = [new KeyValuePair<string, object?>("outcome", "passed")];

        Assert.AreSame(source, MeasurementTags.ToArray(source));
        Assert.IsEmpty(MeasurementTags.ToArray(null));
        Assert.IsEmpty(MeasurementTags.ToArray(new List<KeyValuePair<string, object?>>()));
        AssertSingleTag(MeasurementTags.ToArray(new List<KeyValuePair<string, object?>>(source)), "outcome", "passed");
    }

    private void Capture<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        where T : struct
    {
        lock (_measurements)
        {
            _measurements.Add((instrument.Name, Convert.ToDouble(measurement, CultureInfo.InvariantCulture), tags.ToArray()));
        }
    }

    private (string Instrument, double Value, KeyValuePair<string, object?>[] Tags) SingleMeasurement()
    {
        lock (_measurements)
        {
            Assert.HasCount(1, _measurements);
            return _measurements[0];
        }
    }

    private static void AssertSingleTag(KeyValuePair<string, object?>[] tags, string key, object? value)
    {
        Assert.HasCount(1, tags);
        Assert.AreEqual(key, tags[0].Key);
        Assert.AreEqual(value, tags[0].Value);
    }
}
