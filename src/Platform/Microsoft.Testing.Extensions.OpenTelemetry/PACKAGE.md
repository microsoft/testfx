# Microsoft.Testing.Extensions.OpenTelemetry

Microsoft.Testing.Extensions.OpenTelemetry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that exports test telemetry data using the [OpenTelemetry](https://opentelemetry.io/) standard.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.OpenTelemetry` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.OpenTelemetry) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.OpenTelemetry
```

## About

This package extends Microsoft.Testing.Platform with:

- **OpenTelemetry exporter**: exports test execution data (traces, metrics) using the OpenTelemetry protocol (OTLP)
- **Observability**: integrate test execution data into your existing observability stack (e.g. Jaeger, Prometheus, Grafana)
- **Standards-based**: leverages the OpenTelemetry .NET SDK for broad compatibility with telemetry backends
- **Your data, your infrastructure**: telemetry data is sent exclusively to the OTLP endpoint you configure — no data is sent to Microsoft

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
