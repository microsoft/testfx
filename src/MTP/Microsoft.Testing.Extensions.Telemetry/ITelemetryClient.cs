// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Telemetry;

internal interface ITelemetryClient
{
    void TrackEvent(string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics);

    /// <summary>
    /// Forces any buffered telemetry events to be sent to the ingestion endpoint. This is intended
    /// to be called once during shutdown rather than per-event: per-event flushing serializes the
    /// ingest loop on the network round-trip and can block subsequent events from being processed
    /// within the shutdown timeout.
    /// </summary>
    void Flush();
}
