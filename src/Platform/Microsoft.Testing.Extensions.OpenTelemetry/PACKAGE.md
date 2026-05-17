# Microsoft.Testing.Extensions.OpenTelemetry

Microsoft.Testing.Extensions.OpenTelemetry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that instruments test execution with [OpenTelemetry](https://opentelemetry.io/)-compatible traces and metrics.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.OpenTelemetry` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.OpenTelemetry
```

## About

This package extends Microsoft.Testing.Platform with:

- **OpenTelemetry integration**: instruments test execution and produces OpenTelemetry-compatible traces and metrics that can be exported by user-configured exporters (for example, OTLP exporters)
- **Observability**: enables you to route test execution data, via your own OpenTelemetry exporter configuration, into observability backends (e.g. Jaeger, Prometheus, Grafana)
- **Standards-based**: leverages the OpenTelemetry .NET SDK so that data is sent only to the telemetry exporters and endpoints that you configure

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-open-telemetry>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
