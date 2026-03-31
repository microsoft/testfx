// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal interface ITelemetryManager
{
    void AddTelemetryCollectorProvider(Func<IServiceProvider, ITelemetryCollector> telemetryCollectorFactory);

    void AddOpenTelemetryProvider(Func<IServiceProvider, IOpenTelemetryProvider> openTelemetryProviderFactory);
}
