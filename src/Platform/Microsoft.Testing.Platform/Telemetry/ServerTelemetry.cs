// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class ServerTelemetry(IServiceProvider services) : ITelemetryCollector
{
    private readonly IServiceProvider _services = services;

    public async Task LogEventAsync(string eventName, IDictionary<string, object> paramsMap)
    {
        var logMessage = new TelemetryEventArgs(eventName, paramsMap);
        await PushTelemetryToServerTestHostAsync(logMessage);
    }

    private async Task PushTelemetryToServerTestHostAsync(TelemetryEventArgs telemetryEvent)
    {
        IServerTestHost? server = _services.GetService<IServerTestHost>();
        if (server?.IsInitialized == true)
        {
            await server.SendTelemetryEventUpdateAsync(telemetryEvent);
        }
    }
}
