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
    IActivity? TestFrameworkActivity { get; set; }

    IActivity? StartActivity([CallerMemberName] string name = "", IEnumerable<KeyValuePair<string, object?>>? tags = null, string? parentId = null, DateTimeOffset startTime = default);

    ICounter<T> CreateCounter<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct;

    IHistogram<T> CreateHistogram<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct;
}
