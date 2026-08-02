// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal interface IPlatformActivity : IDisposable
{
    string? Id { get; }

    /// <summary>
    /// Gets the W3C trace id of the activity, or <see langword="null"/> when unavailable.
    /// </summary>
    string? TraceId { get; }

    /// <summary>
    /// Gets the W3C span id of the activity, or <see langword="null"/> when unavailable.
    /// </summary>
    string? SpanId { get; }

    /// <summary>
    /// Gets a value indicating whether the activity is being recorded. Callers should use this to skip building
    /// expensive tag payloads (large stdout/stderr buffers, stack traces, ...) when nothing is listening.
    /// </summary>
    bool IsRecording { get; }

    IPlatformActivity SetTag(string key, object? value);

    /// <summary>
    /// Sets the status of the activity, which observability backends use to flag failures.
    /// </summary>
    IPlatformActivity SetStatus(PlatformActivityStatusCode statusCode, string? description = null);

    /// <summary>
    /// Adds a timestamped event to the activity.
    /// </summary>
    IPlatformActivity AddEvent(string name, IEnumerable<KeyValuePair<string, object?>>? tags = null, DateTimeOffset timestamp = default);

    /// <summary>
    /// Records an exception following the OpenTelemetry <c>exception</c> event convention and marks the activity
    /// as failed.
    /// </summary>
    IPlatformActivity RecordException(Exception exception, IEnumerable<KeyValuePair<string, object?>>? additionalTags = null);
}
