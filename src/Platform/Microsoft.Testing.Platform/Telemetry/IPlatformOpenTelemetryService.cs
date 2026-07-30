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
    /// Gets the ambient (innermost currently running) activity, if any. Frameworks and extensions use this to
    /// parent their own spans without having to thread an activity through their call stack.
    /// </summary>
    IPlatformActivity? CurrentActivity { get; }

    IPlatformActivity? StartActivity([CallerMemberName] string name = "", IEnumerable<KeyValuePair<string, object?>>? tags = null, string? parentId = null, DateTimeOffset startTime = default, PlatformActivityKind kind = PlatformActivityKind.Internal);

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
