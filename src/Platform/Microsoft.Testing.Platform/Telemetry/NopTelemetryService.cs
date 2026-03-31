// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class NopTelemetryService(bool enabled) : ITelemetryCollector
{
    private readonly bool _enabled = enabled;

    public Task LogEventAsync(string eventName, IDictionary<string, object> paramsMap, CancellationToken cancellationToken)
        => !_enabled ? throw new InvalidOperationException(PlatformResources.UnexpectedCallTelemetryIsDisabledErrorMessage) : Task.CompletedTask;
}
