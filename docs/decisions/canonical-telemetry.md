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

### 2026-11-10: Release unit build and cross-framework execution

Implementation source: `006fa932f29de2841189f3e8c9a462409d8667e1`.
The reserved single-node command exited 0 with no warnings or errors:

```powershell
.\build.cmd -c Release -projects test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj /m:1 /nr:false /p:UseSharedCompilation=false /bl:artifacts\log\Release\telemetry-unit-release-1.binlog
```

It built core .NET 10/11/.NET Standard 2.0 and unit .NET 10/11/.NET Framework
4.6.2 assets. The preceding attempt to forward a `{}` binlog name through
`build.cmd` failed before compilation because its PowerShell wrapper split that
token; the successful command used a checked, unique explicit filename instead.
That logger-argument failure is not test evidence.

The four-class telemetry/lifecycle selection used this exact filter:

```text
FullyQualifiedName~OpenTelemetryResultHandlerTests|FullyQualifiedName~TestHostTestFrameworkInvokerTests|FullyQualifiedName~TestExecutionActivityBrokerTests|FullyQualifiedName~TestExecutionTests
```

For each of `net10.0`, `net11.0`, and `net462`, the compiled Release apphost was
invoked first with `--list-tests json --filter $filter`, then with:

```powershell
& $testHostPath --filter $filter --report-trx --report-trx-filename telemetry-contract.trx --report-junit --report-junit-filename telemetry-contract.xml --report-ctrf --report-ctrf-filename telemetry-contract.ctrf.json --results-directory $results --show-test-results all --progress off --ansi off
```

Here `$testHostPath` was
`artifacts\bin\Microsoft.Testing.Platform.UnitTests\Release\<TFM>\Microsoft.Testing.Platform.UnitTests.exe`
and `$results` was the absolute worktree path
`artifacts\TestResults\Release\telemetry-contract-<TFM>`.
Each run exited 0: **109 planned, selected, executed, and passed; 0 failed or
skipped**. Separate structured discovery supplied each expected-name plan.
`verify_execution.py` verified each fresh TRX against that plan and process
exit; all JUnit and CTRF documents also parsed with 109 reported tests.

The 109 cases comprise 55 result-handler cases, 21 activity-broker cases,
24 execution cases, and 9 invoker cases. They include the new canonical-only
instrument/attribute assertions, retired-setting cases, exact duration scaling,
capture opt-out/truncation, retry association, compatible service fallback,
and cancellation/drain/disposal fault observation. This is source-backed
runtime evidence, including the .NET Standard asset through .NET Framework,
not yet packed-consumer proof.

A wider .NET 11 selection adding `TestApplicationResultTests` executed all
168 cases: 167 passed, one failed because
`ConsumeAsync_ExecutionCompleted_ClosesOnlyOldestActivityWithoutOutcome`
still expected the intentionally removed `test.result=passed` alias. Its mock
showed the correct `test.case.result.status=pass` on the second activity.
The coupled positive assertion and the first activity's no-outcome assertion
were migrated to the canonical key without weakening the oldest-activity
association check.
The corrected owner was subsequently rebuilt with `BuildProjectReferences=false`;
all **168 cases passed on each of .NET 10, .NET 11, and .NET Framework 4.6.2**
(504 executed/passed, no failures or skips). Structured discovery, the TRX
execution gate, and JUnit/CTRF parsing verified every run.

### 2026-11-10: shipping packages and consumers

The complete source-product pack at
`8d006ba6208f2d0b35a92d45824faa3d9286ec81` exited 0 with no warnings/errors.
Default desktop-engine/UAP assets and all NuGet content/metadata guards were
retained:

```powershell
.\build.cmd -pack -c Release /m:2 /nr:false /p:UseSharedCompilation=false /p:Projects=Q:\dev\copilot-worktrees\testfx\evangelink-literate-guide\src\*\*\*.csproj /bl:artifacts\log\Release\telemetry-source-pack-1.binlog
```

The native `/p:Projects` glob avoids the wrapper's unescaped expanded
semicolon-list problem. No mirrored build files, package guards, or supported
asset sets were changed to obtain a successful pack.

Affected utility/test owners were built individually with the repository script,
`-c Release`, `/m:1 /nr:false /p:UseSharedCompilation=false`,
`/p:BuildProjectReferences=false`, and separate owner binlogs. All passed with
no warnings/errors. The adapter consumer compiled for all five declared targets.
The actual acceptance hosts target `net12.0`; generated external-framework
consumers target `net11.0`, and the native MSTest consumer runs `net10.0` and
`net11.0`.

The first generated exporter fixture failed compilation because its two
snapshot records were accidentally nested in the exporter rather than visible
to the verifier. Their scope was corrected without changing any assertion.
That setup failure is not counted as regression sensitivity. Fixed retained
test patch SHA-256:
`4FEB587EAE7E232157C4B9A1139AF5181806252806873C899B353BB066155DF1`.
The same fixed test source and byte-identical outer assertion assembly were
used for every paired run; the latter's SHA-256 is
`5A4937AEC817021541C9BDFE3E8353B0BFAF7026A2AEFE4D1597392B89022A69`.

The package-consuming exporter selection is:

```text
FullyQualifiedName~Microsoft.Testing.Platform.Acceptance.IntegrationTests.OpenTelemetryTests
```

It selects these two methods, each with the retired setting unset, `"0"`, and
`"1"`:

- `HostApplicationBuilderProviders_ConsumeMtpDiagnosticsAndPreserveApplicationResource`
- `ExternalFramework_PublicCanonicalExecutionApi_ProducesOneIsolatedSpanPerExecution`

The direct Release acceptance apphost used `--filter $filter --report-trx`
with named result files/directories, `--show-test-results all --progress off
--ansi off`. Independent `--list-tests json` discovery supplied the six-name
execution plan; stderr diagnostics were captured separately from JSON stdout.
Both the initial corrected green run and the final post-counterfactual replay
exited 0 with **6 planned/selected/executed/passed, 0 failed/skipped**, verified
by `verify_execution.py`. Each intentionally mixed pass/fail child exited 2;
the outer assertions require that exit and then verify the exported data.

`MSTestSdk_ApplicationOwnedOpenTelemetry_UsesOneCanonicalTestActivity`
also passed both .NET 10/11 rows: **2 executed/passed, 0 failed/skipped,
outer exit 0**. Its existing fixture additionally builds a source-generation
variant; only the two actual reflection executions are credited here.

Existing real-SDK provider/listener/wrapper/environment tests passed
**95/95 on each of net10.0, net11.0, net462, and net472** (380 executions).
Their exact selection was
`FullyQualifiedName~OpenTelemetryProviderExtensionsTests|FullyQualifiedName~OpenTelemetryPlatformServiceTests|FullyQualifiedName~MeasurementWrapperTests|FullyQualifiedName~OpenTelemetryEnvironmentVariablesTests`.
Independent discovery and the fresh-TRX gate verified all four runs.

### Executed paired sensitivity and package provenance

The isolated comparison starts at pristine production
`c46cfa7394e7be10c4057d3ba8d1d3b698fe242e`, retains the fixed test source,
and runs the byte-identical green outer assertion assembly from inside that
checkout. `RootFinder` resolves its package feeds from the copied assembly's
location. Only the affected core is rebuilt/repacked; unchanged peer packages
are fixed to their byte-identical green artifacts. This is an affected-core
counterfactual with fixed peer packages, not a claim that the entire baseline
repository was repacked.

Both core variants were packed with:

```powershell
.\build.cmd -pack -c Release /m:2 /nr:false /p:UseSharedCompilation=false /p:Projects=Q:\dev\copilot-telemetry-counterfactual-c13fcd30\src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj /bl:artifacts\log\Release\<variant>-core-pack-1.binlog
```

The actual variant binlogs are `telemetry-baseline-core-pack-1.binlog` and
`telemetry-wrong-unit-core-pack-1.binlog`; both commands exited 0 with no
warnings/errors and package guards intact.

| State / contract | Executed result with the same six exporter cases |
| --- | --- |
| Correct canonical-only core | 6 passed, outer exit 0, including the final replay after both comparisons. |
| Pristine baseline core restores legacy output | 4 failed at the intended forbidden `SessionUid` attribute on `CreateTestFrameworkSession` (both modes, unset/`"1"`); both `"0"` cases passed. Outer exit 2, no skips/setup failures. |
| Baseline plus one compiling wrong-unit mutation | 6 failed, outer exit 2. In particular both `"0"` cases reached the duration assertion and reported `count=1, duration=1250, samples=1` instead of 1.25 seconds. The other four still detected baseline legacy output. |

The wrong-unit mutation changes only the canonical histogram's `TotalSeconds`
to `TotalMilliseconds`; production patch SHA-256:
`6291EAD4FBE0C602F78DD4B14F428EAFB79257FA9B4C38B4CE9AAD686E4E0130`.
It was removed after the experiment. No counterfactual production change is
present in the delivered worktree.

Fresh, marked, run-owned caches selected through
`TESTFX_COLLECTION_NUGET_CACHE` separate green, baseline, and wrong-unit
packages despite their identical `3.0.0-dev` version. Generated `NuGet.config`
and `project.assets.json` identify the intended feed, primary cache, package
ID/version, and `lib/net11.0` runtime asset. The machine's additional Visual
Studio fallback folder is recorded, not modified. The primary cached nupkg,
cached DLL, every child's self-reported loaded DLL hash, and the physical loaded
DLL all match the corresponding freshly packed nupkg payload:

| Variant | Core nupkg SHA-256 | Executed net11.0 core DLL SHA-256 |
| --- | --- | --- |
| Green | `6B69635C0AA3D4AE96C5B5CE99CA45CBE94DD4E7B662CAFC15C7B126D03B3EF3` | `4712FA34954F619D68FEE700C351C74C470E45CA9C9047223EEF5C7CAAC84905` |
| Baseline | `EF41412996AB094FD6217E9E2197257808157D35B25441DF1B5992F5E56E879D` | `DDEA8DBCF2983F6A07BCBA558D45A4D4E80B079A3D334F9722E1DFF69998BB39` |
| Wrong unit | `35260E72983A6794A395F324D024D6A5C6AC3A3F69B13E6A32C032E5A393BDD0` | `86C692BEE861EBA27A65B166DF7712568B7D242894ADC6423899AD3AD048FEDD` |

Native MSTest's .NET 10/11 core DLLs and effective package cache were checked
against the same shipping package as well. The .NET 10 payload SHA-256 is
`930A832018AB91CE2E9433A240DA0528536B5CF1B1ADE1D5F3B26BF4A3DA7DE6`.
Logs, binlogs, raw TRX, discovery plans, and the detailed provenance JSON are
retained in task-owned storage; the tables and failure excerpts above are the
portable reviewer evidence. No live telemetry or credentials were published.

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

Executed requirement coverage:

| Requirement / risk | Automated evidence | Consumed surface |
| --- | --- | --- |
| Canonical-only attributes and instruments | Passed: `Constructor_CreatesOnlyCanonicalInstrumentsWithUnits`, `ExtensionTags_ContainOnlyCanonicalIdentity`, `HandleTestResult_EmitsOnlyCanonicalAttributes`, and both package-consuming `OpenTelemetryTests` methods with the retired variable unset, `0`, and `1`. | Real in-process SDK export batches; no external endpoint. |
| Exact values, identities, and units | Passed: `HandleTestResult_WithTimingProperty_RecordsSecondsOnSemanticConventionHistogram`; packaged checks distinguish `FirstCases.First` / pass / 1250 ms from `SecondCases.Second` / fail / 750 ms, require 1.25 s / 0.75 s histogram samples and exact suite/status dimensions, and check the failure's own exception event. | Exported activity tags/events and metric points, not only console markers. |
| Lifecycle and unrelated behavior | Passed: `CompleteScope_DrainAndDisposeFailuresPreservePrimaryAndSecondaryExceptions`, execution/broker tests, retry/active-count tests, provider tests, and `MSTestSdk_ApplicationOwnedOpenTelemetry_UsesOneCanonicalTestActivity`. | Source-backed unit execution and native MSTest packaged topology. |
| Regression sensitivity | Executed: identical exporter assertions detect restored baseline legacy output and the compiling wrong-unit mutation; final correct implementation passes again. | Separate outputs, exact package/DLL hashes, fresh per-variant consumer caches. |

Both relevant test assemblies use method-level parallelism with CPU-count
workers. Result-handler tests use instance-owned mocks, not global listeners.
Packaged consumers execute in separate processes with their own providers.
Each process explicitly enables capture and sampling, sets the attribute size
limit, and enables the SDK so inherited CI settings cannot change the probe's
expected output. The output-capture opt-out is checked separately by
`HandleTestResult_WithOutputCaptureDisabled_OmitsOutputWithoutSuppressingResult`.
Existing listener filtering/resource declarations remain in place; no assembly
scheduling change or global environment mutation was introduced.

This is nonvisual telemetry. Exported names, values, units, and associations are
the consumed contract, so an accessible text transcript is more useful than a
screenshot. External dashboards are unavailable and outside the authorized
scope; their queries/rendering and migration are not validated here.

## Rollout boundaries and remaining gaps

External dashboards must migrate before adopting MTP 3; no dashboard readiness
is inferred from local tests. This is a main/MTP 3 change, not a 2.x servicing
backport. The telemetry PR owner is responsible for the core/extension package
validation and migration documentation; downstream dashboard owners retain
their own rollout responsibility.

Full CI completion for the final published head is not inferred from these local
runs. Individual legacy-name removals were not each subjected to a separate
mutation campaign; the complete canonical-only export shape is asserted in
green, and the legacy cohort is detected at its first forbidden attribute in
the baseline comparison. The owned comparison source/package was restored and
no task-owned build/compiler process remained. Named caches, comparison checkout,
and evidence files are deliberately retained for reproducibility; shared/global
caches and other sessions were untouched.
