// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class NopTelemetryService : ITelemetryCollector
{
    private readonly bool _enabled;

    public NopTelemetryService(bool enabled)
    {
        _enabled = enabled;
    }

    public Task LogEventAsync(string eventName, IDictionary<string, object> paramsMap)
        => !_enabled ? throw new InvalidOperationException("Unexpected call, telemetry is disabled") : Task.CompletedTask;
}
