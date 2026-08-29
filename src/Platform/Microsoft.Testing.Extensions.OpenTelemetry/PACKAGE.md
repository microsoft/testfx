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
- **Semantic conventions**: where an OpenTelemetry convention exists it is used verbatim — `test.case.name`, `test.case.result.status` (upstream `pass`/`fail`), `test.suite.name`, `code.function.name`, `code.file.path`, `code.line.number`, `code.stacktrace`, `error.type`, plus an `exception` span event and an `Error` span status on failures. The pre-existing attribute and instrument names are still emitted by default so existing dashboards keep working; set `TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES=0` to drop them.
- **Platform extensions**: OpenTelemetry does not define any `test.*` **metrics** or test-case **span** conventions (as of semantic conventions 1.43.0), and `test.case.result.status` upstream only defines `pass` and `fail`. The instruments listed below, the additional result statuses (`skipped`, `error`, `timeout`, `cancelled`, `unknown`), `cicd.provider.name`, and the `test.case.*` attributes not listed above are therefore Microsoft.Testing.Platform extensions, deliberately placed in the namespace where an upstream definition would land.
- **Resource attributes**: `AddTestingPlatformResource()` describes *where* the run happened — test assembly, host, OS, runtime — and detects the CI provider, pipeline run, branch and commit (`cicd.*` / `vcs.*`) from GitHub Actions, Azure Pipelines, GitLab CI and Jenkins.
- **Turnkey configuration**: `AddOpenTelemetryProviderFromEnvironment()` wires instrumentation, resource and an OTLP exporter purely from the standard `OTEL_*` environment variables, so a run can be exported without writing configuration code.
- **Trace context propagation**: when the process that started the test run publishes a `TRACEPARENT` environment variable (CI runners, `dotnet test`, IDEs), the whole run nests under that trace instead of starting an orphan one.
- **Lifecycle management**: ties the lifetime of a `TracerProvider` and `MeterProvider` to the test application, so they are disposed alongside the test host.
- **Observability**: lets you route test execution data, via your own OpenTelemetry exporter configuration, into observability backends (e.g. Jaeger, Prometheus, Grafana).
- **Standards-based**: leverages the OpenTelemetry .NET SDK so that data is sent only to the telemetry exporters and endpoints that you configure.

> Note: `AddOpenTelemetryProvider` does **not** register any instrumentation or exporter by default. To actually collect MTP telemetry you must, from the `withTracing` / `withMetrics` delegates:
>
> - call `AddTestingPlatformInstrumentation()` on both the `TracerProviderBuilder` and the `MeterProviderBuilder` to subscribe to the Microsoft Testing Platform source/meter, and
> - register at least one exporter (for example `AddOtlpExporter`, `AddConsoleExporter`, or a vendor-specific exporter).
>
> Without instrumentation, no MTP activities or metrics are collected; without an exporter, collected telemetry is not emitted anywhere.
>
> Use `AddOpenTelemetryProviderFromEnvironment()` instead if you want all of that configured for you from the standard `OTEL_*` variables. It only installs the instrumentation when an exporter is actually configured (via `OTEL_TRACES_EXPORTER` / `OTEL_METRICS_EXPORTER` / `OTEL_EXPORTER_OTLP_ENDPOINT`) or when you pass a configuration delegate, so leaving it in `Program.cs` unconditionally costs nothing on machines that do not opt in.

## Emitted metrics

| Instrument | Type | Unit | Description |
| --- | --- | --- | --- |
| `test.case.duration` | Histogram | `s` | Duration of a single test case, dimensioned by `test.case.result.status` and `test.suite.name`. |
| `test.case.result.count` | Counter | `{test}` | Number of test cases, dimensioned by `test.case.result.status` and `test.suite.name`. |
| `test.case.active` | UpDownCounter | `{test}` | Test cases currently running. |
| `test.run.duration` | Histogram | `s` | Duration of the whole run, dimensioned by `test.run.result.status` and `test.run.exit_code`. |
| `test.case.retry.count` | Counter | `{test}` | Test cases scheduled for a retry attempt (requires `Microsoft.Testing.Extensions.Retry`). |

Metric dimensions are deliberately kept low-cardinality. Unbounded values such as the per-run test counts are set on
the root span (`test.run.total`, `test.run.failed`, `test.run.skipped`) rather than used as metric dimensions.
Span durations are reported in milliseconds under `test.case.duration_ms`, distinct from the seconds-valued
`test.case.duration` metric.

The legacy `tests.discovered` / `tests.started` / `tests.completed` / `tests.passed` / `tests.failed` / `tests.skipped` / `tests.unknown` counters and the `tests.duration` histogram (in milliseconds) are still emitted unless legacy attributes are disabled.

## Configuration

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `TRACEPARENT` / `TRACESTATE` | unset | W3C trace context to nest the run under. |
| `TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT` | `1` | Attach captured stdout/stderr to test spans. Set to `0` when the output can contain secrets. |
| `TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT` | `8192` | Maximum characters kept for a single string attribute. Applies to both the semantic-convention and the legacy attribute names. |
| `TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES` | `1` | Emit the pre-semantic-convention attribute and instrument names alongside the new ones. |
| `OTEL_SDK_DISABLED`, `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_TRACES_EXPORTER`, `OTEL_METRICS_EXPORTER` | unset | Standard OpenTelemetry variables honored by `AddOpenTelemetryProviderFromEnvironment`. |

## Data exported and controlling sensitive values

Telemetry is only ever sent to the exporters and endpoints **you** configure — nothing leaves the process unless you register an exporter (directly, or via `AddOpenTelemetryProviderFromEnvironment` and the `OTEL_*` variables). Once an exporter is configured, the following attributes can carry environment-specific or sensitive values, so review them against your exporter's destination:

| Attribute (and legacy twin) | Carries | Control |
| --- | --- | --- |
| `code.file.path` (`test.file.path`), `code.line.number` (`test.line.start`/`test.line.end`) | Absolute source file path of the test — reveals machine and repository layout. Exported verbatim (not truncated). | On by default whenever a test reports a file location. Disable the legacy twin with `TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES=0`. |
| `test.artifact.file[N].path` | Absolute path of each file artifact a test attaches (dumps, logs, screenshots). Exported verbatim (not truncated). | Emitted whenever a test produces file artifacts. |
| `test.output.stdout` / `test.output.stderr` (`test.stdout` / `test.stderr`) | Captured standard output and error of the test, which routinely contains secrets or environment data. | Off when `TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT=0`; truncated to `TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT`. |
| `code.stacktrace`, `test.case.result.explanation` (`test.result.explanation`), the `exception` span event (`exception.type` / `exception.message` / `exception.stacktrace`), the span status description, and the legacy `test.result.exception.type` / `test.result.exception.message` / `test.result.exception.stacktrace` | Exception message and stack-trace text. | Truncated to `TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT`. Disable the legacy twins with `TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES=0`. |
| `test.metadata.*` (`test.metadataProperty.*`) | Framework-supplied trait/metadata values, exported verbatim (not truncated). | Emitted whenever a test carries metadata. Disable the legacy twin with `TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES=0`. |
| Resource `vcs.repository.url.full`, plus the other resource attributes (`host.name`, `os.description`, and in CI the `cicd.*` / `vcs.*` pipeline, branch and commit — see *Resource attributes* above) | The machine, OS and CI provenance attached to every span and metric point. | User-info credentials in the repository URL (`https://user:token@host/...`) are stripped before export. |

Only the captured output, the result explanation and the exception message and stack trace are truncated to `TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT` (8192 characters by default); the file, artifact and metadata values above are exported verbatim, so truncation is a size guard rather than redaction. Set `TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT=0` on any job whose test output can contain secrets, and prefer sending telemetry to a backend you control.

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-open-telemetry>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
