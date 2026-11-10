# Canonical OpenTelemetry output in MTP 3

## Problem and contract

MTP 3 removes the duplicate legacy telemetry named in
[tracking Task #12314](https://github.com/microsoft/testfx/issues/12314).
This is an intentional observable telemetry break, not a retirement of all
MTP 2.x extension compatibility. Dashboard migration is the consumer's
responsibility; this change does not access or modify external dashboards.

The `Microsoft.Testing.Platform` activity source and meter keep their names.
Canonical attributes, result associations, exception events, activity parenting,
execution links, retry accounting, cancellation/fault observation, output
controls, and supported target frameworks remain unchanged. No public API,
exporter, endpoint, provider-ownership, or protocol change is required.

The producer is the core platform's result handler and framework/extension
invokers. Consumers include the OpenTelemetry extension's standalone and
application-owned providers, native MSTest execution activities, fallback
framework result activities, and user-configured exporters and queries.
The host still owns its test application; application-owned providers retain
their own flush/disposal lifetime. No telemetry-specific handshake exists.
Telemetry does not change the test application's exit code.

## Migration map

Names below describe platform-produced output. Applications remain free to
produce their own attributes and instruments.

### Activity attributes

| Removed name | Canonical replacement | Value / unit migration |
| --- | --- | --- |
| `test.name` | `test.case.name` | Same framework display name. |
| `test.id` | `test.case.id` | Same framework UID string. |
| `test.parent.id` | `test.case.parent.id` | Same parent UID, when present. |
| `test.method`, `test.class`, `test.namespace` | `code.function.name`; `test.suite.name` | Function name is fully qualified (`namespace.type.method`), omitting the global-namespace segment; suite is the original type name. No separate namespace attribute is emitted. |
| `test.assembly` | `test.assembly.name` | Same assembly full name. |
| `test.file.path` | `code.file.path` | Same source path. |
| `test.line.start` | `code.line.number` | Same reported start line, without rebasing. |
| `test.line.end` | None | End-line detail is no longer exported. |
| `test.result` | `test.case.result.status` | `passed` becomes `pass`; `failed` becomes `fail`; `skipped`, `error`, `timeout`, `cancelled`, and `unknown` retain their spellings. |
| `test.result.explanation` | `test.case.result.explanation` | Same explanation and configured truncation. |
| `test.result.exception.type` | `error.type`; `exception` event's `exception.type` | Same exception full type name. |
| `test.result.exception.message` | `exception` event's `exception.message` | Same truncated exception message; no `error.message` span attribute. |
| `test.result.exception.stacktrace` | `code.stacktrace`; `exception` event's `exception.stacktrace` | `code.stacktrace` retains the truncated `Exception.StackTrace`; the event retains truncated `Exception.ToString()`, including exception type/message. |
| `test.result.timeout.ms` | `test.case.timeout` | Milliseconds, unchanged. |
| `test.duration.ms` | `test.case.duration_ms` | Milliseconds, unchanged. Do not confuse this span attribute with the seconds-valued metric. |
| `test.stdout`, `test.stderr` | `test.output.stdout`, `test.output.stderr` | Same captured output, capture opt-out, and truncation. |
| `test.metadataProperty.<key>` | `test.metadata.<key>` | Same framework metadata key/value. |
| `test.step<id>.duration.ms` | `test.step.<id>.duration` | Milliseconds, unchanged. |
| `test.step<id>.description` | `test.step.<id>.description` | Same framework description. |
| `Extension.UID` | `test.extension.uid` | Same extension UID. |
| `Extension.Version` | `test.extension.version` | Same extension version. |
| `Extension.DisplayName` | `test.extension.display_name` | Same extension display name. |
| `Extension.Description` | None | Extension descriptions are no longer duplicated onto activities. |
| `SessionUid` | `test.session.id` | Session UID string rather than the legacy UID object. |
| `RequestType` | `test.run.request_type` | Same request CLR type name. |

### Metrics

| Removed instrument | Canonical replacement | Value / unit migration |
| --- | --- | --- |
| `tests.duration` | `test.case.duration` | Histogram: divide old millisecond samples by 1000 to obtain seconds (`s`); e.g. `2500 ms` becomes `2.5 s`. Dimensions are `test.case.result.status` and `test.suite.name`. Convert dashboards' axes, thresholds, histogram boundaries, and stored-history queries as well as names; do not combine differently scaled historical series. |
| `tests.passed` | `test.case.result.count` filtered to `pass` | Counter in `{test}`, also dimensioned by suite. |
| `tests.failed` | `test.case.result.count` filtered to `fail`, `error`, `timeout`, or `cancelled` | The old aggregate failure counter covered all four outcomes. |
| `tests.skipped` | `test.case.result.count` filtered to `skipped` | Counter in `{test}`, also dimensioned by suite. |
| `tests.completed` | `test.case.result.count` | Sum terminal result counts across statuses/suites. Not an exact event-for-event replacement: execution-completed notifications without a result no longer increment a completion counter. Superseded retry results remain excluded from aggregates. |
| `tests.started` | No cumulative-start replacement | `test.case.active` is an UpDownCounter in `{test}` for currently running cases, not a monotonic started total. |
| `tests.discovered` | None | No discovery-count instrument is emitted. |
| `tests.unknown` | None | No counter for unrecognized state notifications is emitted; do not reinterpret those notifications as terminal results. |

The legacy instruments did not declare units. `tests.duration` nevertheless
recorded milliseconds; this was a value contract, not a unit-metadata label.
`test.run.duration` remains a seconds histogram with `test.run.result.status`
and `test.run.exit_code`. `test.case.retry.count`, supplied by the Retry
extension, remains unchanged. A dashboard already consuming canonical output
must not add old and new series together.

### Configuration and compatibility

`TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES` is retired and ignored, including
when set to `1`: legacy output cannot be re-enabled in MTP 3. Remove it from CI
configuration after migrating queries. Capture and truncation still use
`TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT` and
`TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT`.

Shipped API history is preserved. Intentional removal of internal telemetry
constants/helpers is recorded with `*REMOVED*` entries in the unshipped API
baseline. Independently compiled friend assemblies that directly bind removed
internal telemetry helpers must be rebuilt; this is not a blanket requirement
to retire otherwise compatible older extensions.

## Research and experiments

### 2026-11-10: source and consumer inventory

Starting source: `c46cfa7394e7be10c4057d3ba8d1d3b698fe242e`.
The older reviewed source was
`471a807cf8182e0bcd0d9967c8dbf002c04945d2`; implementation is based on the
current source, not a reconstruction of that review.

Inspection confirmed duplicate emission in `TestingPlatformSemanticConventions`,
`ExtensionHelper`, `PlatformOpenTelemetryOptions`, all result-handler partials,
and `TestHostTestFrameworkInvoker`. `CommonHost` and the invoker publish a
process-global legacy-output flag. `TestApplicationResult` also sends two
notifications whose only purpose is updating legacy counters.

Canonical consumers already exist:

- `OpenTelemetryResultHandlerTests` covers canonical result counts, seconds
  metrics, millisecond span durations, exception events, retry association,
  active-case balancing, and shutdown.
- `OpenTelemetryTests` runs a package-consuming external framework with an
  application-owned tracer/meter provider in fallback and canonical modes.
- `OpenTelemetryActivityTopologyTests` consumes `MSTest.Sdk` packages on .NET
  10/11 and verifies native execution, parallel identity, folded/unfolded rows,
  retries, skip outcomes, and producer timestamps.
- The OpenTelemetry extension and `samples/public/MTPOTel` subscribe to the
  unchanged source/meter, rather than relying on legacy names.

The canonical duration histogram already records `TotalSeconds`; removing
`tests.duration` must not replace it with the legacy millisecond value.

Application-owned activities in the existing sample and acceptance asset use
their own `test.name` / `test.id` tags. Those are not platform-produced aliases
and are deliberately unchanged. Negative-name assertions therefore filter to
the `Microsoft.Testing.Platform` source rather than banning unrelated
application telemetry in the process.

### 2026-11-10: baseline and implementation

The repository bootstrap installed the pinned SDK
`12.0.100-alpha.1.26556.115` and the pinned .NET 10/11 runtimes into the
worktree-local toolchain. The existing Windows SDK was already installed.

`.\build.cmd -projects test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -bl`
exited 0 before production edits. It built the platform's .NET 10, .NET 11,
and .NET Standard 2.0 assets and the unit host's .NET 10, .NET 11, and
.NET Framework 4.6.2 targets.

The native MTP command
`.\.dotnet\dotnet.exe test --project test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Debug -f net10.0 --no-build --filter 'FullyQualifiedName~OpenTelemetryResultHandlerTests|FullyQualifiedName~TestHostTestFrameworkInvokerTests|FullyQualifiedName~TestExecutionTests'`
(with the repository's TRX/report properties and a unique binlog/results path)
exited 0: **84 executed, 84 passed, 0 failed, 0 skipped**, confirmed in the
outer TRX. This is preservation evidence, not proof of the new negative-name
assertions.

The first edited rebuild exited 1 solely because IDE0022 required an
expression-bodied instrument-contract test. That test was corrected without
changing its assertions. Further heavy validation was deferred while another
coordinated build owned the shared host's memory-constrained validation slot;
this is scheduling, not a product test result.

## Alternatives and decision

Keeping duplicate emission is incompatible with the authorized MTP 3 removal
and retains extra series and process-global control state. Merely changing the
legacy flag's default would leave a compatibility mode, legacy instruments, and
cross-host global state behind. Neither alternative is being implemented.

Remove legacy emission and its now-unused plumbing, retaining the existing
canonical mapping and lifecycle code. Do not invent replacements for end-line,
extension-description, discovery, or unknown-notification data. Do not rename
canonical millisecond span attributes into seconds metrics. External dashboard
readiness remains a rollout responsibility, not a claim made by this PR.

## Validation and demonstration

Planned evidence (updated with actual results before publication):

| Requirement / risk | Automated evidence | Consumed surface |
| --- | --- | --- |
| Canonical-only attributes and instruments | `Constructor_CreatesOnlyCanonicalInstrumentsWithUnits`, `ExtensionTags_ContainOnlyCanonicalIdentity`, `HandleTestResult_EmitsOnlyCanonicalAttributes`, and both package-consuming `OpenTelemetryTests` methods with the retired variable unset, `0`, and `1`. | In-process OpenTelemetry SDK export batches; no external endpoint. |
| Exact values, identities, and units | `HandleTestResult_WithTimingProperty_RecordsSecondsOnSemanticConventionHistogram`; packaged export checks distinguish `FirstCases.First` / pass / 1250 ms from `SecondCases.Second` / fail / 750 ms, require 1.25 s / 0.75 s histogram samples and their exact suite/status dimensions, and check the failure's own exception event. | Exported activity tags/events and metric points, not only console markers. |
| Lifecycle and unrelated behavior | Existing `CompleteScope_DrainAndDisposeFailuresPreservePrimaryAndSecondaryExceptions`, execution/broker tests, result-handler retry/active-count tests, OpenTelemetry provider tests, and `MSTestSdk_ApplicationOwnedOpenTelemetry_UsesOneCanonicalTestActivity`. | Source-backed unit execution and native MSTest packaged topology. |
| Regression sensitivity | Same assertions against isolated old-output behavior; a compiling wrong-unit mutation if needed to prove conversion sensitivity. | Separate outputs and fresh generated-consumer caches. |

Both relevant test assemblies use method-level parallelism with CPU-count
workers. Result-handler tests use instance-owned mocks, not global listeners.
Packaged consumers execute in separate processes with their own providers.
Each process explicitly enables capture and sampling, sets the attribute size
limit, and enables the SDK so inherited CI settings cannot change the probe's
expected output. The output-capture opt-out is checked separately by
`HandleTestResult_WithOutputCaptureDisabled_OmitsOutputWithoutSuppressingResult`.
Existing listener filtering/resource declarations remain in place; no assembly
scheduling change or global environment mutation is planned.

This is nonvisual telemetry. Exported names, values, units, and associations are
the consumed contract, so an accessible text transcript is more useful than a
screenshot. External dashboards are unavailable and outside the authorized
scope; their queries/rendering and migration are not validated here.

## Open questions and rollout

External dashboards must migrate before adopting MTP 3; no dashboard readiness
is inferred from local tests. This is a main/MTP 3 change, not a 2.x servicing
backport. The telemetry PR owner is responsible for the core/extension package
validation and migration documentation; downstream dashboard owners retain
their own rollout responsibility.
