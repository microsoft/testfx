// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class ServerTelemetry(IServerTestHost serverTestHost) : ITelemetryCollector
{
    private readonly IServerTestHost _serverTestHost = serverTestHost;

    public async Task LogEventAsync(string eventName, IDictionary<string, object> paramsMap)
    {
        TelemetryEventArgs logMessage = new(eventName, paramsMap);
        await PushTelemetryToServerTestHostAsync(logMessage);
    }

    private async Task PushTelemetryToServerTestHostAsync(TelemetryEventArgs telemetryEvent)
        => await _serverTestHost.SendTelemetryEventUpdateAsync(telemetryEvent);
}
