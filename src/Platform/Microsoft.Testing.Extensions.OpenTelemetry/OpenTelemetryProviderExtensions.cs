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
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class OpenTelemetryProviderExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing and metrics providers whose lifetime is managed by the Microsoft Testing Platform.
    /// </summary>
    /// <remarks>The providers are created with empty configuration. Callers are responsible for wiring everything
    /// the providers should observe and export, including:
    /// <list type="bullet">
    /// <item><description>The Microsoft Testing Platform instrumentation, via
    /// <see cref="AddTestingPlatformInstrumentation(TracerProviderBuilder)"/> and
    /// <see cref="AddTestingPlatformInstrumentation(MeterProviderBuilder)"/>. Without these, no MTP activities or
    /// metrics are collected even if exporters are registered.</description></item>
    /// <item><description>Any additional activity sources, meters, or instrumentation libraries the caller wants.</description></item>
    /// <item><description>At least one exporter (for example, <c>AddOtlpExporter</c> or <c>AddConsoleExporter</c>);
    /// without an exporter, collected telemetry is not emitted anywhere.</description></item>
    /// </list>
    /// No defaults are applied — this method does not pick instrumentation or exporters on the caller's behalf because
    /// the right choice depends on the target observability backend.</remarks>
    /// <param name="builder">The application builder to which the OpenTelemetry providers will be added. Cannot be null.</param>
    /// <param name="withTracing">An optional delegate to configure the tracing provider (sources, instrumentation, exporters).</param>
    /// <param name="withMetrics">An optional delegate to configure the metrics provider (meters, instrumentation, exporters).</param>
    public static void AddOpenTelemetryProvider(this ITestApplicationBuilder builder, Action<TracerProviderBuilder>? withTracing = null, Action<MeterProviderBuilder>? withMetrics = null)
        => ((TestApplicationBuilder)builder).Telemetry.AddOpenTelemetryProvider(serviceProvider =>
        {
            ((ServiceProvider)serviceProvider).AddService(new OpenTelemetryPlatformService());
            return new OpenTelemetryProvider(withTracing, withMetrics);
        });

    /// <summary>
    /// Enables instrumentation for the Microsoft Testing Platform by adding its activity source to the specified tracer
    /// provider builder.
    /// </summary>
    /// <remarks>Call this method from the <c>withTracing</c> delegate of
    /// <see cref="AddOpenTelemetryProvider(ITestApplicationBuilder, Action{TracerProviderBuilder}?, Action{MeterProviderBuilder}?)"/>,
    /// or on any <see cref="TracerProviderBuilder"/> configured outside that helper, to collect activities emitted under
    /// the <c>Microsoft.Testing.Platform</c> source.</remarks>
    /// <param name="builder">The tracer provider builder to which the Microsoft Testing Platform activity source will be added.</param>
    /// <returns>The tracer provider builder with the Microsoft Testing Platform activity source configured for instrumentation.</returns>
    public static TracerProviderBuilder AddTestingPlatformInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource(OpenTelemetryPlatformService.ActivitySourceName);

    /// <summary>
    /// Adds instrumentation for the Microsoft Testing Platform to the specified <see cref="MeterProviderBuilder"/>
    /// instance.
    /// </summary>
    /// <remarks>Call this method from the <c>withMetrics</c> delegate of
    /// <see cref="AddOpenTelemetryProvider(ITestApplicationBuilder, Action{TracerProviderBuilder}?, Action{MeterProviderBuilder}?)"/>,
    /// or on any <see cref="MeterProviderBuilder"/> configured outside that helper, to collect metrics emitted under
    /// the <c>Microsoft.Testing.Platform</c> meter.</remarks>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to which the Microsoft Testing Platform instrumentation will be added.</param>
    /// <returns>The same <see cref="MeterProviderBuilder"/> instance, configured to include metrics from the Microsoft Testing
    /// Platform.</returns>
    public static MeterProviderBuilder AddTestingPlatformInstrumentation(this MeterProviderBuilder builder)
        => builder.AddMeter(OpenTelemetryPlatformService.MeterName);
}
