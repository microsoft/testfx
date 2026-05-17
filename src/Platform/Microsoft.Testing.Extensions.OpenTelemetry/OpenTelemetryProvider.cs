// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Telemetry;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class OpenTelemetryProvider : IOpenTelemetryProvider
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;

    public OpenTelemetryProvider(Action<TracerProviderBuilder>? withTracing = null, Action<MeterProviderBuilder>? withMetrics = null)
    {
        TracerProviderBuilder tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();
        withTracing?.Invoke(tracerProviderBuilder);
        _tracerProvider = tracerProviderBuilder.Build();

        MeterProviderBuilder meterProviderBuilder = Sdk.CreateMeterProviderBuilder();
        withMetrics?.Invoke(meterProviderBuilder);
        _meterProvider = meterProviderBuilder.Build();
    }

    public void Dispose()
    {
        _tracerProvider.Dispose();
        _meterProvider.Dispose();
    }
}
