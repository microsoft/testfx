// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Services;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Extensions for adding AppInsights telemetry provider.
/// </summary>
public static class OpenTelemetryProviderExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing and metrics providers to the application builder, allowing customization of tracing
    /// and metrics configuration.
    /// </summary>
    /// <remarks>This method enables distributed tracing and metrics collection for the application. To
    /// customize telemetry behavior, provide configuration delegates for tracing and/or metrics. If no delegates are
    /// supplied, default OpenTelemetry settings are used.</remarks>
    /// <param name="builder">The application builder to which the OpenTelemetry providers will be added. Cannot be null.</param>
    /// <param name="withTracing">An optional delegate to configure the tracing provider. If null, default tracing configuration is applied.</param>
    /// <param name="withMetrics">An optional delegate to configure the metrics provider. If null, default metrics configuration is applied.</param>
    public static void AddOpenTelemetryProvider(this ITestApplicationBuilder builder, Action<TracerProviderBuilder>? withTracing = null, Action<MeterProviderBuilder>? withMetrics = null)
        => builder.Telemetry.AddOpenTelemetryProvider(serviceProvider =>
        {
            ((ServiceProvider)serviceProvider).AddService(new OpenTelemetryPlatformService());
            return new OpenTelemetryProvider(withTracing, withMetrics);
        });

    /// <summary>
    /// Enables instrumentation for the Microsoft Testing Platform by adding its activity source to the specified tracer
    /// provider builder.
    /// </summary>
    /// <remarks>Use this method to collect telemetry from components that emit activities under the
    /// "Microsoft.Testing.Platform" source. This is typically required to enable distributed tracing for operations
    /// performed by the Microsoft Testing Platform.</remarks>
    /// <param name="builder">The tracer provider builder to which the Microsoft Testing Platform activity source will be added.</param>
    /// <returns>The tracer provider builder with the Microsoft Testing Platform activity source configured for instrumentation.</returns>
    public static TracerProviderBuilder AddTestingPlatformInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource(OpenTelemetryPlatformService.ActivitySourceName);

    /// <summary>
    /// Adds instrumentation for the Microsoft Testing Platform to the specified <see cref="MeterProviderBuilder"/>
    /// instance.
    /// </summary>
    /// <remarks>Use this method to enable collection of metrics emitted by the Microsoft Testing Platform.
    /// This is typically required when monitoring or analyzing test execution within applications that utilize the
    /// platform.</remarks>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to which the Microsoft Testing Platform instrumentation will be added.</param>
    /// <returns>The same <see cref="MeterProviderBuilder"/> instance, configured to include metrics from the Microsoft Testing
    /// Platform.</returns>
    public static MeterProviderBuilder AddTestingPlatformInstrumentation(this MeterProviderBuilder builder)
        => builder.AddMeter(OpenTelemetryPlatformService.MeterName);
}
