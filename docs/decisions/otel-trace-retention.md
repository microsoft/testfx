# Test-run trace retention: research and decisions

## Problem and contract

Retain the detailed OpenTelemetry activities already produced by repository test
hosts so later analysis can compare executions without losing hierarchy, fixture
steps, timing or machine provenance. This change does not add instrumentation,
aggregate durations, select an external backend, or implement weekly reporting.

The producer is `Microsoft.Testing.Extensions.OpenTelemetry` and MTP's existing
activity pipeline. The consumers are this repository's unit and acceptance test
executables, followed by the existing Azure Pipelines `TestResults_*` artifacts.
No shipping package, public API, protocol handshake, or release dependency changes.
The implementation targets `main`; no servicing change is currently required.

The repository-only exporter is enabled by `TESTFX_OTEL_TRACE_DIRECTORY`.
Without it, the existing instrumentation-only dogfooding remains unchanged.
`OTEL_SDK_DISABLED=true` disables this registration, including file export.
No `OTEL_EXPORTER_OTLP_ENDPOINT` is selected or contacted by this helper.
The existing Native AOT exclusion remains in place.

Each provider owns one collision-resistant UTF-8 JSONL file. MTP owns SDK-provider
disposal, while each repository entry point also owns a file-sink lifetime that
ends after application disposal. The sink is idempotently disposable because
normal provider shutdown can dispose it first. Completed spans are synchronously
written and flushed,
without an in-memory batch queue, sampling, or a full-run capture list.
Disposal appends a completion record. Failed tests retain their real exit code;
export I/O failures are reported to stderr and recorded as failures, not replaced
with successful telemetry. A crash or forced termination can leave an incomplete
last line and no completion record. Completed earlier records remain recoverable.

Preserve activity IDs/parents/links, exact UTC timestamps and 100 ns duration
ticks, source identity, status, events, and typed attributes. In particular,
`test.step.{id}.duration` and descriptions must survive. Canonical span elapsed
time, `test.case.duration_ms`, and reported result timings are different measures:
do not sum overlapping fixture intervals or interpret absent step timings as zero.
This trace-only sink does not retain metric histograms.

Resource provenance includes test module version/binary MVID, build/SDK/TFM,
MTP and OpenTelemetry versions,
host/OS/runtime/process architecture and CPU count, source revision/branch,
pipeline/job/agent/image identity, logical/module execution IDs and retry/job
attempts where supplied. Environment capture is allowlisted, not an environment
dump. Captured stdout/stderr attributes are omitted from this retention sink.
Names, IDs, exception details, paths and other attributes can still be sensitive;
the existing build-artifact access boundary applies.

## Research and experiments

### 2026-11-10

- Starting revision: `471a807cf8182e0bcd0d9967c8dbf002c04945d2`, clean worktree.
- Fifteen test `Program.cs` files register the OpenTelemetry source/meter without
  an exporter. `test/Directory.Build.targets` already supplies the extension
  reference. Collected activities therefore have no persistent destination.
- The extension's `OpenTelemetryProvider` owns both SDK providers, and MTP keeps
  them alive until process-shutdown disposal. Explicit application disposal is
  needed to finalize the export, including entry points that previously omitted
  `using`.
- `eng/pipelines/variables/test-env-vars.yml` is shared by the Windows, Linux and
  macOS test jobs. The Windows/non-Windows publishing templates already publish
  `artifacts/TestResults/<Configuration>` on failures and guard absent folders.
  Storing traces in its `OpenTelemetry` child requires no additional upload.
  The separate Windows app-model job already copies that whole results tree into
  `Windows_App_Model_Diagnostics_Attempt<job attempt>`; it also checks completion.
- The existing resource helper supplies module, host, OS, runtime and CI/source
  identity. Retry supplies `TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER`; retain it,
  the module execution ID and logical-run ID instead of guessing attempts.
- The internal TestContainer runner already supplies init/test/cleanup step
  attributes. Native MSTest is a different instrumentation path; this exporter
  must not manufacture missing phases.
- Baseline bootstrap initially failed because this machine's inherited PATH is
  over 21 KB, which `cmd.exe` ignores. A task-local, bounded PATH resolves command
  lookup without modifying the machine or other sessions.
- A baseline build with a global `TargetFrameworks=net10.0` override produced
  NU1510 package-pruning errors in existing multi-targeted reporting projects.
  This was validation setup, not evidence against the new feature. Switching to
  the bootstrapped SDK's ordinary `dotnet build -f <tfm>` path preserved restore's
  multi-target graph. Final builds use `-warnAsError` with no warning exception.
- An initial focused run executed 21 new cases: 19 passed and two test-harness
  assertions failed. Reading an open writer's file on Windows requires the
  reader to share existing write access; the test now explicitly opens with
  `FileShare.ReadWrite`. Parallel test-result order is not contractual, so the
  identity oracle compares sorted exact names while retaining unique-ID and
  per-result outcome assertions.
- Assembly versions alone were unsuitable provenance: local MTP uses
  `42.42.42.42`, and the OpenTelemetry SDK keeps assembly identity `1.0.0.0`.
  The archive therefore records informational versions, the module MVID, and
  build-time SDK metadata. Actual archives were checked against `global.json`
  and central package versions.
- The actual tested SDK is `12.0.100-alpha.1.26556.115`; OpenTelemetry is
  `1.19.1` and the existing MSTest framework/adapter packages resolve to
  `5.0.0-preview.26559.7`. No package references or versions changed.
- A blanket `-f net10.0` entry-point build correctly rejected acceptance projects
  with NETSDK1005: they target `net12.0`. Entry-point verification now reads the
  evaluated `TargetFramework`/`TargetFrameworks` and builds a supported target,
  rather than overriding the project contract.
- Direct test execution produces separate controller and worker archives.
  Discovery invocations can also produce archives. Consumers must read each
  archive's actual resource and activity data; file counts or a containing
  directory alone are not test counts, TFM identity, or successful executions.
- Real `net12.0` acceptance startup exposed a gap in the initial ownership
  assumption: `--help` exited 0 but left a header and `TestHostBuilder` span
  without completion. `TestHostBuilder.Modes.cs` selects
  `InformativeCommandLineHost`, whose `Dispose` only disposes the push protocol,
  not the SDK provider (`CommandLine/InformativeCommandLineTestHost.cs:18`).
  The verifier correctly rejected that archive. The repository entry points
  now explicitly own the sink after application disposal; real `--help` and
  `--info` cases protect this path. This avoids changing shipped MTP lifecycle
  code. A ProcessExit hook was rejected because it can falsely certify an
  abruptly exited run that still has active work.

## Alternatives and decision

| Approach | Decision |
| --- | --- |
| Keep instrumentation with no exporter | Cannot support retrospective execution analysis; no retained traces. |
| Configure the existing OTLP exporter and deploy a local collector | Standard transport, but adds a downloaded binary, service lifecycle, ports, platform-specific setup and another failure boundary before any backend has been chosen. Not implemented. |
| Console exporter redirected into CI logs | Easier initially, but interleaved logs are not a durable structured trace contract and require another exporter dependency. Not implemented. |
| Repository-scoped JSONL file exporter | Chosen: no new package/service/network destination; preserves current SDK activities and uses existing CI artifact publication. |

JSONL is a documented, versioned **TestFx archive format**, not an OTLP HTTP
request or a Jaeger/Chrome trace file. It is intended for offline analysis and
streaming readers. An eventual collector/backend choice can add a converter or
replace this repository-only sink without expanding a shipped API.

Synchronous flushing trades some run overhead and artifact size for deterministic
delivery of every completed span and recoverable partial files. Its cost must not
be confused with an improvement or regression in test-body duration. Resource
metadata identifies this collection policy. Reconsider batching/compression only
with measured overhead and explicit dropped-span/crash-recovery semantics.

Retention is **the build's existing Azure Pipelines retention policy**. No number
of days is declared in this repository, and this change neither invents a period
nor retains builds indefinitely. A project owner must confirm that policy covers
the desired weekly comparison window before relying on long-term availability.
Local files remain until their owner cleans the chosen directory.

## Validation and demonstration

The affected surface is a machine-readable artifact, not a rendered report.
Screenshots are not applicable. Executed evidence on Windows:

| Requirement / risk | Automated evidence | Gap / interpretation |
| --- | --- | --- |
| Preserve identity, hierarchy, exact timing, fixture steps, events and links | `Export_PreservesHierarchyTimingStepsAndFailureDetails` checks known parent/link IDs, exact UTC instants and 1,234,567 duration ticks, step/result timing kept separately, failure status, exception and typed values. | Preserves source timing semantics; does not invent instrumentation. |
| Concurrency, unique files and completion | `Export_ConcurrentBatchesProduceCompleteNoninterleavedRecords` joins 64 concurrent exports; `FileExporters_SharedDirectoryNeverOverwriteAndReleaseFiles` reads flushed spans before disposal and opens both files exclusively after disposal. | No batch queue or background exporter worker. Hard-kill durability beyond completed lines is not claimed. |
| Opt-in, disabled/default, informative and zero-result behavior | `RepositoryTestHost_PreservesResultsAndPersistsOnlyWhenEnabled` launches the actual built host in six modes. Enabled mode preserves seven distinct named passing results; disabled/no-directory modes write no file; empty selection retains exit 8 and an Error run span with no test results. Help/info retain only the informative builder operation, with no test identities or run verdict. | Archive completion is explicitly distinct from test-session execution/success; no result events are invented. |
| Output policy and provenance | `Export_OmitsCapturedOutputWithoutLosingOtherAttributes` and `GetExecutionAttributes_AllowlistPreservesProvenanceAndDoesNotCaptureSecrets`; self-host cases assert CI/commit/job/retry/logical/module identity association. | Other metadata, names, paths and exceptions can still contain sensitive data. |
| Export failures and empty/disposal paths | `Export_WriteFailureIsReportedAndCompletionCannotClaimSuccess` (write/flush), `Dispose_WriteFailureReportsErrorWithoutClaimingCompletion`, `Dispose_EmptyExportWritesExactlyOneCompletion`, `Constructor_InvalidDirectoryFailsExplicitly`. | File initialization errors propagate; later I/O failures log to stderr and cannot claim a complete archive. |
| Existing CI publication and completion checks | Shared variables enable the sink. Existing Windows/Linux/macOS and app-model artifact paths include the trace directory. The verifier accepted valid archives, rejected partial JSON, wrong span counts and explicit exporter failure, warned on failed-job partials/empty selection, and respected explicit disablement. | Static pipeline wiring plus local script execution is not a live Azure upload or retention-policy inspection. |
| Internal TestContainer fixture data | `AssertTests.InstanceShouldCacheAssertInstance` was independently discovered and executed; exactly one retained result had `test.step.init.duration`, `test.step.test.duration`, and `test.step.cleanup.duration`. | No phase counts or zero durations inferred for native MSTest. |

### Revision and execution identities

Base: `471a807cf8182e0bcd0d9967c8dbf002c04945d2`.
The implementation handoff identifies the final commit. Behavior-bearing source
SHA-256 identities for the runs below:

| File | SHA-256 |
| --- | --- |
| `test/Utilities/TestFxOpenTelemetry.cs` | `7694EA319ED103A386AD59508DD800DA206C3708482811EA4890664973FAF501` |
| `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/TestFxOpenTelemetryTests.cs` | `F5DBD1DE65FF8184E5EF4BE0B6FDC7CE71640D7B1E8028BE7486C4D4F9F12A87` |

| Selection / configuration | Planned / discovered / executed | Passed / failed / skipped | Exit |
| --- | --- | --- | --- |
| Existing telemetry baseline, Debug `net10.0` | 43 / 43 / 43 | 43 / 0 / 0 | 0 |
| New + existing telemetry, Debug `net10.0` | 66 / 66 / 66 | 66 / 0 / 0 | 0 |
| New + existing telemetry, Debug `net11.0` | 66 / 66 / 66 | 66 / 0 / 0 | 0 |
| New + existing telemetry, Debug `net462` | 60 / 60 / 60 | 60 / 0 / 0 | 0 |
| Internal TestContainer representative, Debug `net10.0` | 1 / 1 / 1 | 1 / 0 / 0 | 0 |

The six self-host process cases are compiled only for modern .NET, hence the
66/60 difference. MSTest scheduling was unchanged: MethodLevel with CPU-count
workers (32 here). The internal TestContainer runner remains sequential.
Expected names came from source/data rows plus the independently executed
43-case baseline, then were reconciled with separate discovery. The existing
`verify_execution.py` gate parsed each final TRX, checked freshness against its
preceding build binlog, and confirmed every planned result and zero skips.

Tests use per-test directories, a unique filtered activity source, immutable
test-owned resources, and child-only environment overrides. The sink serializes
its own file's writes; collision-resistant filenames isolate processes sharing
the CI results directory.
No process-global environment mutation or assembly scheduling change was added.
Concurrent exports are joined before disposal. Child process cancellation kills
only the owned process tree and joins exit plus both redirected streams using
a separate cleanup bound before disposal.

### Reproduction commands and retained evidence

Bootstrap with `.\build.cmd` (or `.\eng\build.ps1 -restore -build` when the cmd
wrapper cannot resolve PowerShell). Subsequent commands use that worktree's
`.dotnet`, not another checkout. From a fresh PowerShell process in the repository
root, the final runtime selection was:

```powershell
$project = 'test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj'
$filter = 'FullyQualifiedName~TestFxOpenTelemetryTests|FullyQualifiedName~OpenTelemetryProviderExtensionsTests'
$env:DOTNET_ROOT = Join-Path $PWD '.dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
foreach ($tfm in @('net10.0', 'net11.0', 'net462')) {
    $supported = if ($tfm -eq 'net11.0') { 'net11.0' } else { 'net10.0' }
    & .\.dotnet\dotnet.exe build $project -c Debug -f $tfm "-p:SupportedNetFrameworks=$supported" -p:SkipUapTargetFramework=true -warnAsError "-bl:artifacts\log\Debug\otel-delivery-$tfm-build.binlog" -v:quiet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $exe = ".\artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\$tfm\Microsoft.Testing.Extensions.UnitTests.exe"
    $env:TESTFX_OTEL_TRACE_DIRECTORY = Join-Path $PWD "artifacts\TestResults\Debug\otel-delivery-$tfm-discovery\OpenTelemetry"
    & $exe --list-tests --filter $filter
    if ($LASTEXITCODE -ne 0) { throw 'Discovery failed.' }
    $results = "artifacts\TestResults\Debug\otel-delivery-$tfm"
    $env:TESTFX_OTEL_TRACE_DIRECTORY = Join-Path $PWD "$results\OpenTelemetry"
    & $exe --filter $filter --report-trx --report-trx-filename telemetry.trx --results-directory $results
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & .\eng\verify-test-traces.ps1 -TraceDirectory $env:TESTFX_OTEL_TRACE_DIRECTORY
}
```

Each final build above exited 0 with zero warnings/errors; each execution and
archive-verification command exited 0. TRX and archives are under the indicated
results directory; independent discovery logs and binlogs are under
`artifacts/log/Debug/otel-delivery-<tfm>-*`. These local files were not uploaded.
The table and assertion details above are retained reviewer-readable evidence,
not a claim that local artifact paths are publicly accessible.

The internal-runner command used
`Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.exe --treenode-filter "/*/*/AssertTests/InstanceShouldCacheAssertInstance"`
for separate discovery (`--list-tests`) and execution, with `--report-trx` and
the results directory `artifacts\TestResults\Debug\otel-delivery-internal`.
Its archive completion check passed. The test-host's copied platform DLL matched
the owning source project's built DLL byte-for-byte; the resolved SDK package
was `OpenTelemetry/1.19.1`, not another checkout's output.

No shipping package layout changed, so these checks build and execute the real
repository test-host consumers; they do not substitute a mock exporter or
claim packed-consumer acceptance execution. All 15 modified entry points
compiled with zero warnings/errors. The acceptance
hosts target `net12.0`; both real executables were then launched with each of
`--help` and `--info` (four commands, all exits 0). Each produced a complete
one-span informative archive, not a test execution. Those archives are under
`artifacts\TestResults\Debug\otel-delivery-acceptance\<module>\<help or info>`.
No package-consuming acceptance cases were run without packing.

### Test-value evidence

Two independent comparisons use their own `ArtifactsBinDir` and
`ArtifactsObjDir` under `artifacts/bin/otel-counterfactual/<variant>` and
`artifacts/obj/otel-counterfactual/variants/<variant>`. A test-only MSBuild import
replaces only the linked exporter source before `CoreCompile`; the original source and tests stay
unchanged. The emitted compiler-input manifest verifies the replacement.
All dependencies are rebuilt into that isolated output layout.

Both preserve API shape and execute the same 66-case selector and assertions
used by the final correct implementation. Both rebuilt with zero warnings/errors.

| Contract / counterfactual | Without behavior | Correct implementation | Distinguishing evidence |
| --- | --- | --- | --- |
| Persistence: serialize but return before writing `recordType: span` records. | Executed: 66 tests, 56 passed, 10 intended assertion failures, zero skips; exit 2. | Executed: 66 passed, zero skips; exit 0. | Expected four records versus two, 66 concurrent records versus two, and actual spans versus declared counts. The CI verifier also detects the missing span lines. |
| Informative-host lifetime: return `null` instead of the sink from `AddTestFxOpenTelemetry`, restoring sole reliance on SDK disposal. | Executed: 66 tests, 64 passed, precisely `help` and `info` failed at the missing-completion assertion, zero skips; exit 2. | Executed: both cases pass, complete archives contain only informative operations with no test results/run verdict. | This restores the actual pre-fix short-circuit defect, not a compiler or setup failure. |

| Variant | Source SHA-256 | One-hunk patch SHA-256 |
| --- | --- | --- |
| `spans` | `250178F098E27A076F2D5CDBE91A9808EA9E5A884C168B1341BC3331115FF0BD` | `9A474A12BDC4C745F4066FFEF2BD4D5DB1DCBF2CE4BA1FE54DC939D733BB582F` |
| `lifetime` | `D8A601AF696FD54464C36AD611DBF8070B6B5E6E9F208B8E2BEB40B3D75BFA04` | `0E0D192556FBC940438A20596A36159C451A85809C9E0B04AE78F5383BB2720C` |

The replacement target runs before `CoreCompile`, removes
`$(RepoRoot)test\Utilities\TestFxOpenTelemetry.cs`, and includes the path supplied
by `TestFxTraceCounterfactualSource`. The exact comparison build shape was:

```powershell
& .\.dotnet\dotnet.exe build $project -c Debug -f net10.0 -p:SupportedNetFrameworks=net10.0 -p:SkipUapTargetFramework=true -warnAsError "-p:ArtifactsBinDir=$root\artifacts\bin\otel-counterfactual\$variant\" "-p:ArtifactsObjDir=$root\artifacts\obj\otel-counterfactual\variants\$variant\" "-p:CustomAfterMicrosoftCommonTargets=$root\artifacts\obj\otel-counterfactual\use-counterfactual.targets" "-p:TestFxTraceCounterfactualSource=$root\artifacts\obj\otel-counterfactual\source\$variant\TestFxOpenTelemetry.cs" "-bl:artifacts\log\Debug\otel-counterfactual-$variant-build.binlog" -v:quiet
```

Each variant's real executable then ran with the identical `$filter` above,
`--report-trx --report-trx-filename counterfactual.trx`, and
`--results-directory artifacts\TestResults\Debug\otel-counterfactual-<variant>`.
Normal sources/test hashes were checked unchanged after the experiment, followed
by the final green runtime matrix and real acceptance startup checks.

## Open questions

- The project owner must confirm the server-side artifact retention period and
  any policy for keeping selected trend baselines longer. The coordinated
  telemetry work owns that confirmation and the CI rollout through merge.
- Actual Linux/macOS jobs and Azure artifact publication need CI execution;
  local Windows evidence cannot establish them.
- This is collection, not a measured zero-overhead tracing implementation.
  No CPU-utilization telemetry, long-run overhead benchmark, or physical
  forced-termination/cooperative-cancellation experiment was performed.
- Finer-grained native MSTest instrumentation and weekly analysis remain owned
  by separate work; this change only preserves activities that already exist.
