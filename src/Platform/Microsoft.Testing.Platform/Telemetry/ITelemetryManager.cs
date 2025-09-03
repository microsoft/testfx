// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Defines a contract for managing telemetry providers within a diagnostics or monitoring system.
/// </summary>
/// <remarks>Implementations of this interface allow registration and management of OpenTelemetry providers,
/// enabling integration with distributed tracing and metrics collection frameworks. This interface is experimental and
/// may be subject to change.</remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITelemetryManager
{
    /// <summary>
    /// Registers an OpenTelemetry provider factory.
    /// </summary>
    /// <param name="openTelemetryProviderFactory">The OpenTelemetry provider factory.</param>
    void AddOpenTelemetryProvider(Func<IServiceProvider, IOpenTelemetryProvider> openTelemetryProviderFactory);
}

/// <summary>
/// Defines internal operations for managing telemetry collectors within the telemetry system.
/// </summary>
/// <remarks>This interface extends <see cref="ITelemetryManager"/> to provide additional functionality intended
/// for internal use. Members of this interface are not intended to be used directly by application code.</remarks>
internal interface IInternalTelemetryManager : ITelemetryManager
{
    void AddTelemetryCollectorProvider(Func<IServiceProvider, ITelemetryCollector> telemetryCollectorFactory);
}
