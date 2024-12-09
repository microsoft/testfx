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

    private readonly TelemetryConfiguration _config;
    private readonly TelemetryClient _telemetryClient;

    public AppInsightTelemetryClient(string? currentSessionId, string osVersion)
    {
        _config = TelemetryConfiguration.CreateDefault();
        _config.ConnectionString = $"InstrumentationKey={InstrumentationKey};IngestionEndpoint={TelemetryServiceEndpoint}";
        _telemetryClient = new TelemetryClient(_config);
        _telemetryClient.Context.Session.Id = currentSessionId;
        _telemetryClient.Context.Device.OperatingSystem = osVersion;
    }

    public void TrackEvent(string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics)
    {
        _telemetryClient.TrackEvent(eventName, properties, metrics);
        _telemetryClient.Flush();
    }
}
