// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class PlatformActivityContext(string traceId, string spanId, bool isRecorded, string? traceState)
{
    public string TraceId { get; } = traceId;

    public string SpanId { get; } = spanId;

    public bool IsRecorded { get; } = isRecorded;

    public string? TraceState { get; } = traceState;
}
