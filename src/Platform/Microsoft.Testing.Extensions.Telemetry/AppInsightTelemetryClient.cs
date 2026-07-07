// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Testing.Extensions.Telemetry;

internal sealed class AppInsightTelemetryClient : ITelemetryClient
{
    // Note: The InstrumentationKey should match the one of dotnet cli.
    private const string InstrumentationKey = "74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
    private const string TelemetryServiceEndpoint = "https://dc.services.visualstudio.com/";

    private readonly TelemetryClient _telemetryClient;

    public AppInsightTelemetryClient(string? currentSessionId, string osVersion)
    {
        var config = TelemetryConfiguration.CreateDefault();
        config.ConnectionString = $"InstrumentationKey={InstrumentationKey};IngestionEndpoint={TelemetryServiceEndpoint}";
        _telemetryClient = new TelemetryClient(config);
        _telemetryClient.Context.Session.Id = currentSessionId;
        _telemetryClient.Context.Device.OperatingSystem = osVersion;
    }

    public void TrackEvent(string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics)
    {
        // Microsoft.ApplicationInsights 3.x (an OpenTelemetry shim) removed the metrics parameter
        // from TrackEvent. Tracking metrics separately via TrackMetric() would emit uncorrelated
        // metric instruments that are neither enriched with this client's context (session/OS) nor
        // tied back to the event, and would additionally require every metric key to satisfy the
        // OpenTelemetry instrument-name syntax (no spaces, must start with a letter).
        //
        // Instead we fold the numeric measurements into the event's properties as invariant-culture
        // strings. This keeps a single correlated, context-enriched customEvent and imposes no naming
        // restrictions on the keys. Consumers read these values back with todouble(customDimensions[...]).
        if (metrics.Count == 0)
        {
            _telemetryClient.TrackEvent(eventName, properties);
            return;
        }

        var combinedProperties = new Dictionary<string, string>(properties);
        foreach (KeyValuePair<string, double> metric in metrics)
        {
            combinedProperties[metric.Key] = metric.Value.ToString("R", CultureInfo.InvariantCulture);
        }

        _telemetryClient.TrackEvent(eventName, combinedProperties);
    }

    public void Flush()
        => _telemetryClient.Flush();
}
