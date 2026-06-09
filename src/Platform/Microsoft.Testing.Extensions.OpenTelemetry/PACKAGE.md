# Microsoft.Testing.Extensions.OpenTelemetry

Microsoft.Testing.Extensions.OpenTelemetry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that instruments test execution with [OpenTelemetry](https://opentelemetry.io/)-compatible traces and metrics.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.OpenTelemetry` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.OpenTelemetry
```

## About

This package extends Microsoft.Testing.Platform with:

- **OpenTelemetry integration**: exposes the Microsoft Testing Platform activity source and meter (both named `Microsoft.Testing.Platform`) so test execution can be observed via the OpenTelemetry .NET SDK.
- **Lifecycle management**: ties the lifetime of a `TracerProvider` and `MeterProvider` to the test application, so they are disposed alongside the test host.
- **Observability**: lets you route test execution data, via your own OpenTelemetry exporter configuration, into observability backends (e.g. Jaeger, Prometheus, Grafana).
- **Standards-based**: leverages the OpenTelemetry .NET SDK so that data is sent only to the telemetry exporters and endpoints that you configure.

> Note: `AddOpenTelemetryProvider` does **not** register any instrumentation or exporter by default. To actually collect MTP telemetry you must, from the `withTracing` / `withMetrics` delegates:
>
> - call `AddTestingPlatformInstrumentation()` on both the `TracerProviderBuilder` and the `MeterProviderBuilder` to subscribe to the Microsoft Testing Platform source/meter, and
> - register at least one exporter (for example `AddOtlpExporter`, `AddConsoleExporter`, or a vendor-specific exporter).
>
> Without instrumentation, no MTP activities or metrics are collected; without an exporter, collected telemetry is not emitted anywhere.

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-open-telemetry>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
