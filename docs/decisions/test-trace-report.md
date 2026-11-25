# Test trace report: research and decisions

## Problem and contract

Produce a useful first report from the traces already retained by TestFx before
adding native MSTest instrumentation or scheduling a weekly agent. The report
must identify actual fixture and test-occurrence costs, demonstrate whether the
existing detail is useful, and distinguish missing evidence from measured zero.

The producer is the repository-only `testfx-otel-jsonl` schema 1 exporter merged
in #12325. The consumer is an offline report utility and its generated report.
No shipping package, instrumentation, telemetry destination, CI permissions,
retention settings or scheduled workflow changes are in scope.

Input artifacts are untrusted data, never executable code. Bind observations to
their archive, module, framework, environment, CI run, source revision and attempt.
Validate archive completion and span identity before including an execution in
rankings. Controllers, informative/tool operations and nested hosts created by
tests are not independent test-suite executions. Preserve incomplete input as an
explicit coverage limitation rather than silently treating it as a healthy run.

Report elapsed test-occurrence time, shared fixture invocations and reported
TestContainer steps separately. Do not sum overlapping spans into run wall time,
subtract fixture/child durations to invent a native body-only timer, or equate
test-case span duration with CPU or reported per-result duration.

## Research and experiments

### 2026-11-11

- Implementation starts from `7d5d0508fd6984bf37d0eee47ba7dfc2443febbe`, the
  #12325 merge, fast-forwarded into this task's clean, isolated worktree.
- A bounded Azure build query found one completed #12325 PR build: `1673649`,
  source merge revision `3a1134ae9b2d32ee8a65b89075b130e8f7f7c195`, with PR
  head `daa3867dae22571095d307540db3e840c4cdb5d9`. One build cannot establish a
  longitudinal trend or distinguish runner load from a product regression.
- Downloaded its Windows Release, Windows Debug and Linux Release test artifacts
  using the existing bounded downloader. The direct file-container listing
  required sign-in; the public build-artifact ZIP endpoint worked. A `subPath`
  query did not narrow the ZIP. Read only trace members without extracting or
  executing other artifact content.
- The Linux archive has 29 trace files. It contains controller-only files,
  tools-only files, and executing workers; the extensions unit-test worker also
  contains nested hosts and an intentionally emitted synthetic test span.
  Counting all files, all `TestHost` spans or all `test.case.id` attributes would
  overstate suite coverage. Attribute/trace identity alone is insufficient:
  use the parent graph to isolate the outer host and its test frameworks.
- Existing fixture data already identifies individual acceptance fixture
  initialization intervals longer than 30 seconds. This is measured attribution,
  not proof that those fixtures regressed or are avoidably slow.
- Existing Python report/normalizer utilities under `.github/scripts` use the
  standard library and `unittest`. Reuse that execution model for ZIP streaming,
  typed JSON and large input; PowerShell performance/semantic parity for this
  processing has not been established. No new dependency is needed.
- Cross-checking the root CTRF reports found 137,818 published results, versus
  137,832 observed OTel occurrences. Inspection identified fourteen additional
  single-test subprocess observations: trace-bridge and isolated WinUI startup
  tests re-execute themselves. Some inherit the controlled-host protocol, so
  `HostType` alone cannot distinguish suite coverage from auxiliary processes.
  Keep both observations and the independent CTRF counts visible, rather than
  deleting genuine execution data or claiming fourteen additional suite tests.
- Portable and Windows-specific targets can export identical
  `TargetFrameworkAttribute` labels despite different test selections. Keep
  binary MVIDs separate in this snapshot; a stable full target-platform label
  is needed before constructing cross-build cohorts. No exporter change is
  included in this report-first work.

### 2026-11-25: CPU, memory and scheduling experiment

The follow-up request is to run experiments and assess whether machine-aware
scheduling belongs in MSTest or MTP. This is measurement and design research,
not authorization to change shipping scheduling defaults or introduce an API.

- CI snapshot: build `1699244`, source
  `a3bab4d2a755bcf1e9ccb602e5c404d2fc2d9960`. Its traces contain concurrency
  and logical-processor counts, not CPU utilization or memory measurements.
  Concurrent test spans alone cannot establish under- or oversubscription.
- Local experiment source: `394a1beb7ea17d8f51d1eed6c35a9c612f9f95d4`.
  The shared Windows machine has 32 logical processors and about 128 GiB RAM;
  it is not the four-core CI agent. Restrict only task-owned test processes
  to four logical processors; do not change machine-wide settings or touch
  other users' processes. Affinity is not exclusive CPU allocation.
- Existing controls: MSTest `Workers=0` resolves to
  `Environment.ProcessorCount`; runsettings override assembly worker counts.
  `TestExecutionManager.ParallelExecution.cs` drains a queue using a fixed
  worker pool. `ResourceLockAttribute` coordinates named conflicts inside one
  process, not CPU shares, memory reservations or other test modules.
  `dotnet test` controls module concurrency separately from framework workers.
- Planned pilot: build the real MTP unit-test executable with the pinned SDK,
  run one warmup, then two mirrored rounds of 1/2/4/8 MSTest workers. Keep
  test selection, binary, runtime, CPU affinity and reporting constant.
  Capture parent exit, exact result identities/counts, wall time, process-tree
  CPU seconds, sampled private/working-set memory and whole-machine load.
  Flag short-lived children missed between samples and shared-machine noise.
  Do not extrapolate one unit module to acceptance tests that launch builds.
- Proposed ownership, not an implemented contract: the invocation orchestrator
  must own any cross-module CPU/memory budget; MSTest must own test/chunk
  admission and safety constraints. A local adaptive worker pool alone cannot
  coordinate independently launched modules. Begin with opt-in measurement
  and recommendations, not automatic default changes. A later scheduling
  protocol would need explicit unsupported/legacy fallback, cancellation,
  resource release after crashes, fairness and user-override semantics, plus
  aligned SDK/MTP/framework package-consuming end-to-end evidence.

Pilot setup and unsuccessful approaches:

- The initial PowerShell invocation used `-p:` for an MSBuild property, which
  binds ambiguously to the wrapper's parameters; `/p:` is required here.
- Restricting every project to `net10.0` exposed `NU1510` package-pruning
  diagnostics for the JUnit/CTRF reporters. `WarningsNotAsErrors=NU1510`
  did not fix the restore phase. The successful experiment-only build used
  `RestoreEnablePackagePruning=false`; no project or dependency was changed.
- The first sampler/launcher did not launch tests correctly: Windows command
  argument escaping was wrong, and polling performance-counter process rows
  was too slow to account reliably for short-lived children. Discard these
  preflights; zero CPU/memory in their output is invalid, not a measured zero.
- The replacement uses a task-owned Windows job object. A gated launcher is
  assigned before starting the test executable. The job enforces affinity
  `0xF` for descendants and accounts CPU time and peak private committed
  memory even for children that exit between samples. Closing the job
  terminates only its owned descendants. Working sets are sampled separately
  and can miss brief peaks or double-count shared pages. A process-exit race
  while reading affinity was fixed before accepting the preflight.
- The first accepted preflight executed 4,907 tests successfully, with two
  skipped, exit 0. Its parallelization banner confirmed four method-level
  workers. All sampled process affinities were `0xF`. Expected invalid-CLI
  output from nested unit-test probes is not an outer test-host failure.
- The first comparison stopped after its one-worker run despite identical
  counts and successful exits: one data-row display label embeds the current
  timestamp. Inspection found the same stable test ID and outcome. Compare
  stable row ID plus fully qualified method plus outcome, not the volatile
  display text; retain the raw TRX labels. The corrected grid restarts with
  the same workload and reporting, not a weakened test selection.

The pilot combines locally built MTP `3.0.0-dev` with the existing referenced
MSTest framework/adapter `5.0.0-preview.26574.9`, informational source
`eb362e89adf5d3e66e4d8aa22a707b0879f5d66e`, on .NET `10.0.15` using SDK
`12.0.100-alpha.1.26570.125`. It does not measure a new scheduler, a packed
acceptance runner, or a proposed SDK/MTP/framework integration.

Scheduling alternatives remain proposals, not implemented product behavior:

| Approach | Assessment |
| --- | --- |
| Keep explicit worker/module limits | Existing, deterministic and immediately usable; cannot automatically share spare capacity or predict memory requirements. |
| Add opt-in resource measurements and configuration recommendations | Preferred first product increment: explain actual CPU, memory, queue/lock waits and child-process cost without changing execution semantics. |
| Adapt only MSTest's local worker pool | Insufficient as a global solution: multiple modules can each independently consume the machine budget. CPU utilization also cannot distinguish I/O waits, contention and a genuine sequential tail on its own. |
| Cooperating invocation-wide budget with framework admission | Promising later design: the SDK/IDE/other orchestrator owns the budget; MTP transports compatible capabilities/leases; MSTest admits ready chunks while retaining its safety constraints. Requires a cross-component protocol and composed-product validation. |

Any later requirements metadata should distinguish unspecified from an explicit
request, treat memory estimates as estimates rather than a hard isolation
guarantee, and account for child builds. Admission must preserve resource locks,
dependency ordering, cancellation, crash recovery and fairness. Unsupported
frameworks or legacy launchers need an explicit conservative fixed-limit
fallback; user-provided limits must remain authoritative. Do not prescribe a
new public attribute, handshake or default until those contracts and evidence
are established.

The optimization objective is successful-run throughput within CPU/memory and
correctness constraints, not 100% CPU usage. Equal division of core count by
module count is a baseline to measure, not a generally optimal worker count:
I/O-heavy work, shared locks, sequential tails and nested builds have different
demands. A profiler should explain queue/lock waits and the ready-work supply,
not assume every low-CPU interval can be repaired by adding workers.

#### Executed pilot and results

Successful build command (exit 0, zero warnings/errors):

```powershell
.\eng\build.ps1 -restore -build -c Release -msbuildEngine dotnet `
    -warnAsError $false `
    -projects test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj `
    /p:TargetFrameworks=net10.0 /p:RestoreEnablePackagePruning=false /m:4
```

The retained `measure-job-scheduling.ps1` helper launches
`artifacts\bin\Microsoft.Testing.Platform.UnitTests\Release\net10.0\Microsoft.Testing.Platform.UnitTests.exe`
directly, with `DOTNET_ROOT` pointing at this worktree's `.dotnet` directory.
Each run receives:

```text
--settings "<trial>\workers.runsettings" --report-trx --report-trx-filename results.trx --results-directory "<trial>" --timeout 5m
```

The generated runsettings specifies
`RunSettings/MSTest/Parallelize/Workers` and `Scope=MethodLevel`.
Full expanded commands, hashes, raw TRX, output and resource samples are retained
in the session artifacts, not published externally. The helper does not build
during timed runs. The job includes the launcher, controller, test host and all
descendants, not unrelated machine processes.

One four-worker warmup was excluded. The measured order was
`1, 2, 4, 8, 8, 4, 2, 1`; two observations per setting:

| Workers | Wall seconds, median (range) | Mean per-run test-tree CPU / four CPUs | Peak private committed memory, MiB (range) |
| --- | --- | --- | --- |
| 1 | 99.01 (95.13-102.89) | 14.23% | 204-267 |
| 2 | 55.10 (49.04-61.16) | 25.29% | 302-316 |
| 4 | 29.07 (28.97-29.17) | 45.06% | 232-291 |
| 8 | 31.91 (30.53-33.29) | 43.57% | 371-414 |

These are successful outer runs: **Passed: 4907, Failed: 0, Skipped: 2**,
exit 0 in all nine trials including warmup. The same 4,909 unique stable row
IDs, fully qualified methods and associated outcomes were independently
reconciled across runs. Banners confirm the requested method-level worker
counts, and every sampled process affinity is `0xF`. The two skipped tests
are the unauthorized-identity and AppContainer named-pipe connection cases;
they are not executed evidence for those behaviors.

Measured observations: parallelism substantially reduces this module's wall
time relative to one worker. Eight workers did not improve the observed median
over four, and both eight-worker runs used more peak private committed memory
than either measured four-worker run.

Limits on inference: whole-machine CPU varied from about 53% to 85% during
measured runs (about 90% in the excluded warmup). The machine is shared;
the four selected logical processors are not reserved. The sampler process
also accumulated 191.69 CPU seconds across monitoring and result processing,
outside the job budget. No uninstrumented control quantifies its timing
overhead. Consequently, 45% test-tree CPU is consumption, not proof that the
remaining 55% was available. This is not evidence that eight workers are
intrinsically slower, nor a CI saturation or memory-pressure diagnosis.
Private committed memory is not GC heap size or physical resident memory.
Module concurrency stayed at one; acceptance workloads and the interaction
between module and test parallelism were not measured.

Decision: retain scheduling defaults. The most useful first built-in capability
is opt-in MTP resource profiling with explicit child-process accounting,
effective CPU limits, memory semantics and queue/lock-wait context, followed by
recommendations rather than silent adaptation. A global budget, if justified,
must be owned by the invocation orchestrator and cooperatively consumed by
MSTest rather than independently reinvented in each module.

The next experiment is the real acceptance workload on dedicated CI agents:
vary module concurrency and per-module workers together, with the same packed
inputs, repeated/reversed order, resource collection and result-identity checks.
Do not enable downstream adaptation or change defaults based on this local pilot.

Retained session artifacts: `measure-job-scheduling.ps1`,
`summarize-scheduling.ps1`, `scheduling-provenance.json` and
`scheduling-worker-grid/` (all nine trials, `results.json`, `comparison.json`
and `measurement.json`). Binary SHA-256 identities:

| Binary | SHA-256 |
| --- | --- |
| MTP unit-test assembly | `2614CF3FC166D8A11B4CC41017E0DD72F79F8A7FC0867BA5B60562809932A065` |
| MTP under test | `74FCE49DDB3D9362D37B930E8367E095E04BDB49FA9C6D81B0EE46BAFD8F31B7` |
| Referenced MSTest adapter | `B3C39D6D11F92220C1EF74E3D088893D8DE3EF375B1E853D533FA3490C0A3F2A` |

No shipping code or tests changed, no new scheduling feature was implemented,
and no evidence was published to GitHub. Behavioral red/green comparisons and
rendered-product demonstrations are inapplicable to this measurement-only
follow-up; shipping compatibility and adaptive-scheduling integration remain
unvalidated proposals, not completed feature work.

### 2026-11-25: opt-in process resource metrics, first increment

The user requested implementation and a PR for iteration. The first increment
will instrument the MTP test-host process through its existing OpenTelemetry
extension. This is deliberately not automatic scheduling: one process cannot
observe or coordinate all independently launched modules. It is also not a
replacement for the previous Windows job-object pilot: process measurements
exclude children, machine-wide pressure and cross-module aggregation.

Contract: when `TESTINGPLATFORM_OTEL_PROCESS_METRICS=1` is set and the
application activates MTP diagnostics, register observable test-host CPU
time, working-set and private-memory metrics on the existing MTP meter, together
with the process-available logical CPU count. They are collected only by a
subscribed metrics provider/listener and exported only through the application's configured exporter. Unset means no
new instruments, preserving existing consumers. The values describe the
current process, not its process tree; CPU time is cumulative and must be
differenced over elapsed time. Memory readings are instantaneous, not peak
or machine-available memory. No new public .NET API, CLI option, custom
exporter, background sampling thread, or scheduler change is planned.

The existing OpenTelemetry extension owns its meter and lifetime; the
application or MTP owns the provider and exporter as before. No new
handshake is introduced. Older consumers and those that do not opt in retain
the current behavior. A later tree-wide or invocation-wide profiler needs a
separately specified orchestrator/SDK protocol, child ownership, sampling,
shutdown and unsupported-platform behavior and real composed-product proof.

The application-owned resource helpers do not necessarily add `process.pid` to
the provider resource. Giving every point a process ID and a unique diagnostics
instance ID prevents independently launched hosts with otherwise identical
resources, or a later host reusing the same PID, from becoming one metric
series. These identifiers increase cardinality once per host instance; they do
not aggregate children or create an invocation-wide identity.

| Requirement | Planned evidence | Surface |
| --- | --- | --- |
| Opt in and export the process metrics | Focused listener tests and packed OpenTelemetry consumer exercising the actual metrics provider. | Existing OpenTelemetry metric stream, no UI/layout change. |
| Disabled/default compatibility | Listener asserts no extra instruments without opt-in. | Existing provider behavior, including application-owned provider. |
| Correct scope and meaning | Assert metric names, types, units and independent basic bounds; document process-only semantics and exclusions. | Exported metric points and package readme. |

Expected package consumer: `Microsoft.Testing.Extensions.OpenTelemetry` and
the matching `Microsoft.Testing.Platform` package from this worktree's Release
shipping feed. The team owning this increment remains responsible for any
subsequent cross-package rollout and release-branch servicing decision; this
PR does not enable a downstream scheduler or claim tree-wide measurements.

#### Implementation and validation evidence

The implementation adds four observable instruments on the existing meter;
only an enabled listener samples them, and process handles are disposed after
each read. The meter remains available for the existing provider shutdown/flush
order. A process ID and a unique diagnostics-instance ID distinguish simultaneous
hosts and PID reuse. This does not add an exporter to TestFx's existing CI
trace JSONL: those reports still cannot answer the original CPU/memory question
until a metrics collection and reporting path is separately implemented.

The following Windows x64 checks used SDK `12.0.100-alpha.1.26570.125`, base
`394a1beb7ea17d8f51d1eed6c35a9c612f9f95d4` plus this change, and Release
outputs. The new instruments compiled for `netstandard2.0`, `net10.0`, and
`net11.0`. Linux and macOS measurement semantics have not been executed.

- A full `.\build.cmd -pack -c Release -msbuildEngine dotnet -binaryLog /m:4`
  failed: classic UWP projects require desktop MSBuild, which is not installed
  on this machine. Repeating with `/p:UwpMinimum=` built packages but failed
  the package-shape audit because the classic UWP assets were absent. Those
  incomplete MSTest packages were used only to satisfy the acceptance
  fixture's version discovery and source-generation build; they are not
  evidence of shipping MSTest package compatibility.
- The affected platform and OpenTelemetry projects were independently packed
  successfully, without that UWP override, with `.\build.cmd -pack -c Release
  -msbuildEngine dotnet '-warnAsError:$false' -binaryLog -projects <project>
  /m:4`. The two projects were
  `src\MTP\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj` and
  `src\MTP\Microsoft.Testing.Extensions.OpenTelemetry\Microsoft.Testing.Extensions.OpenTelemetry.csproj`.
  The resulting `3.0.0-dev` package SHA-256 values were respectively
  `D2D34CEEF3A35022F544B9F5B7CEC7E41879AB08B5D5E47BE6BD60F153CC1AC3` and
  `E665CACA1DA0A17694CE72DA08D4BAE95F640188C20634ABDC8B7031E633D495`.
  The generated consumer's assets file selected their `net11.0` runtime assets
  from an isolated acceptance package cache; SHA-256 hashes of the extracted
  package DLLs exactly matched the binaries loaded by the consumer.
- The `net10.0` unit host, with method-level parallelism and 32 workers, ran
  `--filter 'FullyQualifiedName~OpenTelemetryPlatformServiceTests|FullyQualifiedName~OpenTelemetryProviderExtensionsTests'`
  with **80 passed, zero failed/skipped**. The identical tests in an isolated
  comparison retained the constructor/API but disabled registration: the
  `ProcessMetrics_AreOptInAndObserveTheCurrentProcess (True)` case failed on
  the missing instrument names, with 79 passed, one failed, zero skipped.
  This is a behavior-removal test, not a pristine-base comparison.
- The `net12.0` acceptance host ran `--filter 'FullyQualifiedName~OpenTelemetryTests'`
  with **9 passed, zero failed/skipped**. Its generated application-owned OTel
  consumer used the `net11.0` shipping DLLs, explicitly tested unset, `0`, and
  `1`, and independently asserted instrument names, values, units and process
  identity. Its child deliberately publishes one passing and one failing test
  and returns exit 2; the outer acceptance process returns 0. With the identical
  acceptance tests and independently repacked behavior-removal variant, 8 passed
  and `ProcessMetrics_ExportOnlyWhenExplicitlyEnabled ("1",True)` failed: the
  child reported `Unexpected exported instruments` with all four process
  instruments absent. The disabled variant's package SHA-256 was
  `E1E7DD908E140810006CC845E6DE8256C58009B22BE0A955F5C9D23A2FAE0223`;
  its loaded extension DLL matched the package's `net11.0` DLL hash
  `59F922C060CDBD606A78560BC249E7F2C5E9B3256579DAA7AC3D538D1A71C79A`.
  Unchanged fixture prerequisites were copied from the original build; the two
  changed-boundary packages were built and packed independently in the
  comparison. This is a behavior-removal check, not a pristine-base comparison.
- A direct run of that generated host with
  `TESTINGPLATFORM_OTEL_PROCESS_METRICS=1`, `MTP_OTEL_EXECUTION_MODE=fallback`,
  and `OTEL_SDK_DISABLED=false` produced `logical_count=32 {cpu}`,
  `cpu.time=2.171875 s`, `memory.private=19513344 By`, and
  `memory.working_set=54554624 By`; its expected exit was 2. These are one
  process's instantaneous readings, not a CPU-rate or machine-saturation
  conclusion. Text is more useful than media for this nonvisual metric stream.

The existing assembly-wide parallel configuration remains unchanged. The unit
listener filters by its own meter instance, while the real SDK/provider tests
retain their existing assembly-level exclusion. Acceptance uses separate child
processes and explicit child environment overrides. CI execution, the full
classic-UWP pack, and a tree-wide/CI-reporting consumer remain unverified.

## Alternatives and decision

| Approach | Decision |
| --- | --- |
| Add a weekly agent immediately | Deferred: first demonstrate actual value and input coverage, without giving an agent responsibility for arithmetic or unproven regression claims. |
| Add body timing first | Deferred by the user; native fixture spans and TestContainer steps can already answer useful questions. |
| Compare raw Windows/Linux or Debug/Release durations as trends | Rejected: these are different environments, not repeated comparable observations. |
| Offline deterministic report from existing archives | Chosen: directly inspect measured costs, preserve provenance and uncertainty, and make the result reproducible without a service or scheduled job. |

The initial output is a descriptive snapshot, not an alerting system. Cohorts
remain separate by module, TFM, OS, configuration, architecture, runtime, SDK,
processor count, binary MVID and available image/collection metadata. MVIDs are
snapshot provenance, not stable cross-build cohort keys. Missing load, coverage
and parallelism metadata remains visible; environment labels do not establish
equal machine load. The HTML view puts ranked costs first and keeps the full
worker/environment tables in native expandable sections.

## Validation and demonstration

Executed on Windows with Python `3.12.10`; no packages were installed and no .NET
shipping code changed. The actual producer/consumer check reads the CI-exported
ZIP files, not a mock of the exporter.

| Requirement | Evidence |
| --- | --- |
| Attribute tests to their outer execution and count fixture invocations once | `test_outer_execution_owns_counts_and_overlapping_fixture_intervals` excludes two nested/probe tests, preserves two distinct outcomes, and independently expects 6,000 ms of fixture work but a 5,000 ms interval union. |
| Preserve exact timing units and distinguish duration statistics | `test_exact_timestamp_ticks_and_percentiles_preserve_units` checks 1,234,567 ticks, hand-calculated median/p95 and overlapping intervals. |
| Keep controllers, informative operations and copied archives out of execution totals | `test_zip_controller_informative_and_duplicate_files_are_not_extra_runs` reads a real ZIP with independent CTRF results and checks exact role/count associations. |
| Do not invent extra builds or merge platform variants | `test_configuration_cohorts_are_not_independent_build_samples` and `test_binary_variants_and_standalone_processes_do_not_merge_into_suite_coverage` keep one build distinct from multiple configurations, binary MVIDs and protocols. |
| Missing phases are unknown, not zero | `test_missing_fixture_and_step_timings_are_unknown_not_zero` requires null fixture timing, absent step observations and explicit presentation text. |
| Reject malformed/incomplete/incompatible evidence explicitly | `test_incomplete_incompatible_and_invalid_inputs_remain_explicit_gaps` covers missing completion, unsupported schema, wrong counts, export failure, duplicate/cyclic identity, inconsistent timestamps and invalid timing/identity/count types. |
| Bound inputs including rejected files | `test_input_budget_counts_rejected_archives_and_invalid_ctrf_fails_explicitly` verifies aggregate size enforcement and explicit invalid CTRF errors. |
| Retain result/name associations and escape report content | `test_rendered_tables_keep_identity_and_escape_untrusted_names` checks long and escaped labels, proper Markdown table shape and focusable horizontal scrolling. |
| Deliver the real CLI outputs and preserve partial-input failure status | `test_cli_writes_real_outputs_and_reports_partial_input_with_nonzero_exit` launches the actual script, reads JSON/HTML, and requires exit 2 with the rejected archive retained in coverage. |

Final command: `python .github\scripts\test_report_test_traces.py -v`.
Result: **10 tests passed, zero skipped, exit 0**. The first run exposed invalid
Markdown table spacing, which was fixed rather than weakening the assertion.

Generate the actual report from the retained evidence with:

```powershell
python .github\scripts\report_test_traces.py `
    --input C:\evidence\1673649\TestResults_Linux_Release_Attempt1.zip `
    --input C:\evidence\1673649\TestResults_Windows_Release_Attempt1.zip `
    --input C:\evidence\1673649\TestResults_Windows_Debug_Attempt1.zip `
    --output C:\evidence\report\testfx-trace-value
```

Executed with the same filenames under this session's task-owned `trace-report`
artifact directory: **exit 0**, 271 complete archives, no rejected archives,
114 executing processes and 137,832 attributed occurrences. The 100 root CTRF
reports independently contain 137,818 results: 137,054 passed and 764 skipped.
All worker executed/skipped counts reconcile with their corresponding outer
host. The fourteen additional occurrences were traced to self-executing
trace-bridge and isolated-startup tests, not reported as additional suite tests.
There are 30,780 occurrences with reported steps (22.3%). There is no macOS
artifact in this selected build.

The report already exposes useful investigation targets without new
instrumentation. Windows Release `ClassInitialize` takes 149.70 seconds for
`DynamicExtensionIsolationTests`, 119.52 seconds for
`AssemblyFixtureProviderNativeAotTests`, and 114.70 seconds for `AbortionTests`.
At least one fixture is active during 2,052.42 seconds of the 4,184.99-second
MSTest acceptance worker, and 1,647.95 seconds of the 3,431.89-second MTP
acceptance worker. These overlapping intervals are **not exclusive overhead,
avoidable cost, a critical path or evidence of regression**.

The HTML report was inspected in the browser at 1280x721 and in a real
480x760 iframe viewport. At the constrained width, the first layout wrapped
names excessively; the final layout keeps readable 900-pixel tables inside
focusable horizontal-scroll regions. Document width remains 465 pixels at
the 480-pixel viewport, and all columns remain reachable by scrolling.
Expandable worker/step/cohort sections open and close correctly.
Separate, explicitly synthetic presentation fixtures exercise long labels,
literal `<script>` text, a Unicode label and the empty/partial-input state;
names remain associated with their outcomes, no script element is created,
rows do not overlap, and missing/error evidence stays visible. These fixtures
are presentation checks, not measured CI results.

The focused tests use Python `unittest` with per-test temporary directories and
joined, bounded child processes. They do not mutate environment variables,
install listeners, change .NET test scheduling, or share output paths between
test methods. No MSTest assembly or cross-test resource declaration changes.

### Test-value evidence

Base: `7d5d0508fd6984bf37d0eee47ba7dfc2443febbe`. The final local commit binds
these source hashes; the report JSON SHA-256 is
`0C898A58E38522224C4299FD99C82BBA5C6407911B406908F11589C09380A15E`.

| Source | SHA-256 |
| --- | --- |
| `.github/scripts/report_test_traces.py` | `B3D207CD223175CC2F9CA0AA7C1E7726739E554E4E5BC720DA3ECB88F8FD5B1E` |
| `.github/scripts/test_report_test_traces.py` | `2F8DCD767366B83670F7C50DE0F9117B577313C0B536BE8EB2F66623CCB8032E` |

The same ten tests and assertions were retained in two isolated session-artifact
directories. Only the indicated production expression changed. Both execute
normally and fail at the intended behavioral assertions; neither comparison
relies on a syntax, import, setup or launch failure.

| Contract / counterfactual | Without behavior | Correct implementation |
| --- | --- | --- |
| Remove framework ownership filtering, counting all `test.case.id` spans | 10 executed, five assertion failures, exit 1; expected two occurrences but observed four. | 10 passed, exit 0. |
| Replace the fixture interval union with a sum | 10 executed, one assertion failure, exit 1; expected 5,000 ms but observed 6,000 ms. | 10 passed, exit 0. |

Comparison command for each variant:
`python C:\evidence\counterfactual\<variant>\test_report_test_traces.py -v`.
The copies were run in this session's artifact directory with byte-identical
test files. The unchanged repository implementation was rerun afterward:
ten passed, zero skips, exit 0.

| Variant | Mutated source SHA-256 | One-hunk patch SHA-256 |
| --- | --- | --- |
| `ownership` | `3713368A578A1FADC26DFE5D6A184F5422CB70A75F334F0ADB79BC321AB83001` | `3C1144E42B73202A12E2FDA1545D00646899932EF8C159EF3D2DC85940A30B02` |
| `overlap` | `8D1FF8A9EFB7295DEA75AA41619FFA62C818EA142F06D3D4EFC7F7346D9DF2CB` | `F26AD9BBFFD3B20A2B304ED88854374C94D5A408E3BF63A39043C632D85DDEB0` |

Exact source/artifact identities, downloaded ZIP hashes and sizes are in the
task artifact `trace-report/1673649/input-manifest.json`; test logs and comparison
patches are retained beside the report. Nothing was published to GitHub or to
an external telemetry service.

### Review follow-up: input layouts, partial ZIP data and literal labels

The requested review batch is scoped to three concrete report-consumer defects,
without changing telemetry production or the report's intended UX:

- Root CTRF selection must mean the same thing for wrapped/unwrapped ZIPs and
  their extracted directory equivalents. A one-level child or merged report is
  not an independent root report.
- A CRC failure in one JSONL ZIP member must name that member in rejected
  coverage while preserving healthy observations and the existing partial-report
  exit code.
- Artifact-derived labels and provenance must remain literal in Markdown as
  well as HTML, including introductions and rejection diagnostics. Escaping
  must not change raw JSON identities or the report's intentional formatting.

The pre-fix report from `d004eedc201e17e7a73dc3113a11f1d8534ad0d3` provides
the real-input baseline: 271 complete archives, 114 executing processes,
137,832 attributed occurrences and 137,818 published CTRF results. Its JSON
SHA-256 is recorded above. Two parallel workers own the ingestion and
presentation corrections; the parent reviewed their combined diff and owns
validation and publication.

All three comments were accepted. ZIP and directory processing now share a
single common-wrapper rule and select CTRF reports only directly under that
artifact root. Member-level `BadZipFile` errors use the existing rejected-input
path. Dynamic presentation values are escaped before composition for each
output context, leaving JSON identities and intentional headings/emphasis
unchanged.

| Comment / requirement | Exact regression evidence |
| --- | --- |
| Comment 4536620612: equivalent artifact roots | `test_ctrf_coverage_uses_same_artifact_root_in_zip_and_directory_layouts` expects two root reports with three associated outcomes in four layouts, excluding distinct child and merged reports. The pre-fix unwrapped ZIP inflates the counts, and the wrapped directory misses them. |
| Comment 4536620700: recover healthy members after a CRC error | `test_cli_retains_healthy_members_and_names_crc_damaged_trace` changes a real stored member's trailing JSON whitespace without changing its recorded CRC. The healthy member and all three report files survive; the exact damaged member is rejected and CLI exit remains 2. |
| Comment 4536620757: preserve literal Markdown/HTML labels | `test_rendered_tables_keep_identity_and_escape_untrusted_names`, `test_introduction_escapes_dynamic_labels_before_intentional_formatting`, and `test_rejected_inputs_render_literal_source_and_reason` cover markup delimiters, introduction provenance, literal `**`, diagnostic labels, unchanged JSON and intended report formatting. |

Parent integration corrected the CRC test's Markdown diagnostic expectation to
the newly escaped literal filename, while retaining the exact raw identity in
JSON and HTML. An initial combined run also encountered the existing 15-second
child-process timeout under machine load. The three CLI launches now share a
named 60-second startup/I/O/teardown bound; this is not a performance assertion,
and no result assertion was removed.

Final validation on Python 3.12.10:

```powershell
python -B .github\scripts\test_report_test_traces.py -v
```

Result: **14 executed, 14 passed, zero skipped, exit 0**. The parent reran the
same five regression methods against an isolated copy of the exact
`d004eedc201e17e7a73dc3113a11f1d8534ad0d3` production file, then the corrected
file. Test bytes and assertions were identical:

| State | Executed methods | Result |
| --- | --- | --- |
| Pre-fix source | 5 | Eight intended assertion failures across layout, CRC output and literal-rendering contracts; zero setup errors/skips, exit 1. |
| Corrected source | 5 | Five passed, zero skipped, exit 0. |
| Combined final suite | 14 | Fourteen passed, zero skipped, exit 0. |

The paired selector is:

```powershell
python -B .github\scripts\test_report_test_traces.py `
    ReportTestTracesTests.test_ctrf_coverage_uses_same_artifact_root_in_zip_and_directory_layouts `
    ReportTestTracesTests.test_cli_retains_healthy_members_and_names_crc_damaged_trace `
    ReportTestTracesTests.test_rendered_tables_keep_identity_and_escape_untrusted_names `
    ReportTestTracesTests.test_introduction_escapes_dynamic_labels_before_intentional_formatting `
    ReportTestTracesTests.test_rejected_inputs_render_literal_source_and_reason -v
```

The red command uses the same test file in the task-owned
`trace-report/review-batch/baseline` directory. Relevant source identities:

| File/state | SHA-256 |
| --- | --- |
| Exact pre-fix production snapshot | `4124A9005969CC2AA48E0902A0E7F0B808DC1E2D3DDA8B8A54A03827FF1ACC41` |
| Corrected `report_test_traces.py` | `9BDD1035CEC2833A085EC58DF005170BF7D5ECD66E60D1CB48E2331C1F95064C` |
| Final regression test file, identical in both states | `B765809691840C7F6CBFA437D97B49BC456FEF627D2A663374AE377B3AA109C8` |

The real three-artifact CLI command above was rerun with the combined fixes:
exit 0, no rejected archives, and **byte-equivalent analytical JSON** with the
same SHA-256, counts, identities and largest fixture timings. Corrected HTML
and rendered Markdown retain the measured table values; escaping changes
presentation serialization, not analytical data. Focused literal-label/empty
rendering was inspected locally at 1280/480-pixel widths, including disclosures
and keyboard scrolling. Hosted GitHub page CSS remains an explicit gap.

## Open questions

- No multi-week history, effective server-side retention period, CPU/load
  measurements or native body-only spans have been established.
- This first sample is a PR build, not a representative history of full main
  runs. The report must not extrapolate its coverage to unobserved platforms or
  claim missing modules/tests were skipped by the product.
