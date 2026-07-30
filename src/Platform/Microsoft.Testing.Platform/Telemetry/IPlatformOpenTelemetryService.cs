// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Defines platform-specific services for OpenTelemetry instrumentation, including activity management and metric
/// creation.
/// </summary>
/// <remarks>
/// This interface provides abstractions for starting and managing activities, as well as creating
/// counters and histograms for telemetry data collection. It is intended for internal use ONLY as the platform
/// should be dependency free and netstandard2.0 doesn't have the instrumentation APIs.
/// </remarks>
internal interface IPlatformOpenTelemetryService : IDisposable
{
    IPlatformActivity? TestFrameworkActivity { get; set; }

    /// <summary>
    /// Gets a value indicating whether an ambient (currently running) activity exists. Exposed as a boolean rather
    /// than as the activity itself so callers cannot accidentally dispose an activity they do not own.
    /// </summary>
    bool HasCurrentActivity { get; }

    /// <summary>
    /// Starts an activity.
    /// </summary>
    /// <param name="name">The span name.</param>
    /// <param name="tags">Attributes to set on the span at creation time.</param>
    /// <param name="parentId">An explicit parent. When <see langword="null"/> the ambient activity is used.</param>
    /// <param name="startTime">An explicit start time, for spans that cover work that already began.</param>
    /// <param name="kind">The span kind.</param>
    /// <param name="setAsCurrent">
    /// When <see langword="false"/>, the activity is created and timed but never becomes the ambient activity.
    /// <para>This matters for code that captures an <see cref="System.Threading.ExecutionContext"/> while the span is
    /// open: the ambient activity is an async-local, so it would be captured too and later restored - parenting
    /// unrelated, much later work to a span that has already ended. MSTest does exactly that when it propagates
    /// async-locals set by <c>AssemblyInitialize</c>/<c>ClassInitialize</c> to every subsequent test.</para>
    /// </param>
    IPlatformActivity? StartActivity([CallerMemberName] string name = "", IEnumerable<KeyValuePair<string, object?>>? tags = null, string? parentId = null, DateTimeOffset startTime = default, PlatformActivityKind kind = PlatformActivityKind.Internal, bool setAsCurrent = true);

    ICounter<T> CreateCounter<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct;

    IUpDownCounter<T> CreateUpDownCounter<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct;

    IHistogram<T> CreateHistogram<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct;

    /// <summary>
    /// Registers an asynchronous gauge that is polled by the metrics pipeline on every collection cycle.
    /// </summary>
    /// <typeparam name="T">The numeric type of the reported measurement.</typeparam>
    /// <param name="name">The instrument name.</param>
    /// <param name="observeValue">Callback invoked on every collection cycle to read the current value.</param>
    /// <param name="unit">The UCUM unit of the measurement.</param>
    /// <param name="description">A human readable description of the instrument.</param>
    void CreateObservableGauge<T>(string name, Func<T> observeValue, string? unit = null, string? description = null)
        where T : struct;
}
