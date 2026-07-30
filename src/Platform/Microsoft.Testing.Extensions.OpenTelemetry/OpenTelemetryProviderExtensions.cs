// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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
        => builder
            .AddMeter(OpenTelemetryPlatformService.MeterName)

            // Default OpenTelemetry histogram buckets top out at 10s and are tuned for HTTP latency. Test durations
            // span microseconds to minutes, so without explicit buckets almost every measurement lands in the last
            // bucket and percentiles become meaningless.
            .AddView(
                TestingPlatformSemanticConventions.Metrics.TestCaseDuration,
                new ExplicitBucketHistogramConfiguration { Boundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 300] })
            .AddView(
                TestingPlatformSemanticConventions.Metrics.TestRunDuration,
                new ExplicitBucketHistogramConfiguration { Boundaries = [1, 5, 10, 30, 60, 120, 300, 600, 1800, 3600] });

    /// <summary>
    /// Adds the Microsoft Testing Platform resource attributes (test assembly, host, OS, runtime and the detected
    /// CI provider, pipeline and commit) to the resource of a tracer, meter or logger provider.
    /// </summary>
    /// <remarks>Resource attributes are attached once to every span and metric point exported by the provider, which
    /// is what lets you slice a dashboard by branch, pipeline or machine without adding those values to every span.
    /// <para>The CI attributes follow the OpenTelemetry <c>cicd.*</c> and <c>vcs.*</c> conventions and are detected
    /// from GitHub Actions, Azure Pipelines, GitLab CI and Jenkins environment variables.</para></remarks>
    /// <param name="builder">The resource builder to enrich.</param>
    /// <returns>The same <see cref="ResourceBuilder"/> instance.</returns>
    public static ResourceBuilder AddTestingPlatformResource(this ResourceBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));

        return builder
            .AddService(
                serviceName: TestingPlatformResourceDetector.GetServiceName(),
                serviceVersion: TestingPlatformResourceDetector.GetServiceVersion(),
                autoGenerateServiceInstanceId: true)
            .AddAttributes(TestingPlatformResourceDetector.GetResourceAttributes());
    }

    /// <summary>
    /// Registers OpenTelemetry tracing and metrics providers configured entirely from the standard <c>OTEL_*</c>
    /// environment variables, so a test run can be exported to an observability backend without any code change.
    /// </summary>
    /// <remarks>
    /// This is the "turnkey" counterpart of
    /// <see cref="AddOpenTelemetryProvider(ITestApplicationBuilder, Action{TracerProviderBuilder}?, Action{MeterProviderBuilder}?)"/>:
    /// it registers the Microsoft Testing Platform instrumentation, the platform resource attributes, and an exporter
    /// selected from the environment.
    /// <list type="bullet">
    /// <item><description><c>OTEL_SDK_DISABLED=true</c> turns everything off.</description></item>
    /// <item><description><c>OTEL_TRACES_EXPORTER</c> / <c>OTEL_METRICS_EXPORTER</c> select the exporter
    /// (<c>otlp</c> or <c>none</c>). When unset, <c>otlp</c> is used if
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, otherwise nothing is exported.</description></item>
    /// <item><description><c>OTEL_SERVICE_NAME</c> overrides the service name, which otherwise defaults to the test
    /// assembly name.</description></item>
    /// </list>
    /// The optional delegates run last, so callers can still add their own sources, instrumentation or exporters.
    /// </remarks>
    /// <param name="builder">The application builder to which the OpenTelemetry providers will be added.</param>
    /// <param name="configureTracing">An optional delegate applied after the environment-driven tracing configuration.</param>
    /// <param name="configureMetrics">An optional delegate applied after the environment-driven metrics configuration.</param>
    public static void AddOpenTelemetryProviderFromEnvironment(this ITestApplicationBuilder builder, Action<TracerProviderBuilder>? configureTracing = null, Action<MeterProviderBuilder>? configureMetrics = null)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));

        if (IsTrue(Environment.GetEnvironmentVariable(OpenTelemetryEnvironmentVariables.SdkDisabled)))
        {
            return;
        }

        builder.AddOpenTelemetryProvider(
            tracing =>
            {
                tracing
                    .AddTestingPlatformInstrumentation()
                    .ConfigureResource(resource => resource.AddTestingPlatformResource());

                if (UseOtlpExporter(OpenTelemetryEnvironmentVariables.TracesExporter))
                {
                    tracing.AddOtlpExporter();
                }

                configureTracing?.Invoke(tracing);
            },
            metrics =>
            {
                metrics
                    .AddTestingPlatformInstrumentation()
                    .ConfigureResource(resource => resource.AddTestingPlatformResource());

                if (UseOtlpExporter(OpenTelemetryEnvironmentVariables.MetricsExporter))
                {
                    metrics.AddOtlpExporter();
                }

                configureMetrics?.Invoke(metrics);
            });
    }

    private static bool UseOtlpExporter(string environmentVariableName)
    {
        string? configured = Environment.GetEnvironmentVariable(environmentVariableName);

        // Mirror the behavior of the OpenTelemetry auto-instrumentation: an endpoint alone is enough to opt in.
        return OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace(configured)
            ? !OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OpenTelemetryEnvironmentVariables.ExporterOtlpEndpoint))
            : configured.Trim().Equals("otlp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrue(string? value)
        => value is "1" or "true" or "True" or "TRUE";
}
