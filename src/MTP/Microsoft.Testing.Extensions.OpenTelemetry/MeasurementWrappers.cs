// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

/// <summary>
/// Owns the instrument and the tag-forwarding logic shared by every wrapper that adapts a
/// <c>System.Diagnostics.Metrics</c> instrument onto the platform's dependency-free metric interfaces.
/// </summary>
/// <typeparam name="TInstrument">The wrapped instrument type.</typeparam>
/// <typeparam name="TValue">The type of the measurements the instrument records.</typeparam>
internal abstract class MeasurementWrapper<TInstrument, TValue>
    where TInstrument : Instrument<TValue>
    where TValue : struct
{
    protected MeasurementWrapper(TInstrument instrument)
        => Instrument = instrument
            ?? throw new ArgumentNullException(nameof(instrument));

    protected TInstrument Instrument { get; }

    /// <summary>
    /// Emits a measurement, taking the tag-less instrument overload when the caller supplied no tags so that the
    /// common untagged path never materializes an array.
    /// </summary>
    /// <param name="value">The measurement to emit.</param>
    /// <param name="tags">The attributes (dimensions) of the measurement, or <see langword="null"/> when untagged.</param>
    protected void Emit(TValue value, IEnumerable<KeyValuePair<string, object?>>? tags)
    {
        if (tags is null)
        {
            EmitCore(value);
            return;
        }

        EmitCore(value, MeasurementTags.ToArray(tags));
    }

    /// <summary>
    /// Forwards an untagged measurement to the wrapped instrument.
    /// </summary>
    /// <param name="value">The measurement to emit.</param>
    protected abstract void EmitCore(TValue value);

    /// <summary>
    /// Forwards a tagged measurement to the wrapped instrument.
    /// </summary>
    /// <param name="value">The measurement to emit.</param>
    /// <param name="tags">The attributes (dimensions) of the measurement.</param>
    protected abstract void EmitCore(TValue value, KeyValuePair<string, object?>[] tags);
}

/// <summary>
/// Adapts a <see cref="Counter{T}"/> onto <see cref="ICounter{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the measurements the counter records.</typeparam>
internal sealed class CounterWrapper<T>(Counter<T> counter) : MeasurementWrapper<Counter<T>, T>(counter), ICounter<T>
    where T : struct
{
    public void Add(T delta) => EmitCore(delta);

    public void Add(T delta, IEnumerable<KeyValuePair<string, object?>>? tags) => Emit(delta, tags);

    protected override void EmitCore(T value) => Instrument.Add(value);

    protected override void EmitCore(T value, KeyValuePair<string, object?>[] tags) => Instrument.Add(value, tags);
}

/// <summary>
/// Adapts an <see cref="UpDownCounter{T}"/> onto <see cref="IUpDownCounter{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the measurements the counter records.</typeparam>
internal sealed class UpDownCounterWrapper<T>(UpDownCounter<T> counter) : MeasurementWrapper<UpDownCounter<T>, T>(counter), IUpDownCounter<T>
    where T : struct
{
    public void Add(T delta, IEnumerable<KeyValuePair<string, object?>>? tags = null) => Emit(delta, tags);

    protected override void EmitCore(T value) => Instrument.Add(value);

    protected override void EmitCore(T value, KeyValuePair<string, object?>[] tags) => Instrument.Add(value, tags);
}

/// <summary>
/// Adapts a <see cref="Histogram{T}"/> onto <see cref="IHistogram{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the measurements the histogram records.</typeparam>
internal sealed class HistogramWrapper<T>(Histogram<T> histogram) : MeasurementWrapper<Histogram<T>, T>(histogram), IHistogram<T>
    where T : struct
{
    public void Record(T value) => EmitCore(value);

    public void Record(T value, IEnumerable<KeyValuePair<string, object?>>? tags) => Emit(value, tags);

    protected override void EmitCore(T value) => Instrument.Record(value);

    protected override void EmitCore(T value, KeyValuePair<string, object?>[] tags) => Instrument.Record(value, tags);
}
