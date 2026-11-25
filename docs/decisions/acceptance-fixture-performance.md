# Acceptance fixture performance: experiments and decisions

## Problem and contract

Investigate and experimentally improve the cost of generating and building MSTest
and Microsoft.Testing.Platform acceptance assets. Preserve the exact test,
target-framework, metadata-mode and package coverage. Do not substitute mocks,
project references to product source, or stale packages for packed consumer tests.

Generated projects and mutable outputs remain fixture-owned. NuGet payloads may
be reused within a run, but must not survive a repack under the same package
version. Build failures, cancellation and nonzero child test exits must remain
observable. Existing source-generation compile coverage is intentional, including
when a class executes only its reflection variant.

The starting revision is `a3bab4d2a755bcf1e9ccb602e5c404d2fc2d9960`.
This experiment owns this worktree's outputs and scratch artifacts only.
The environment is a shared Windows machine; no shared caches or build servers
will be cleared or stopped.

## Research and experiments

### 2026-11-25: source inventory and existing evidence

Both acceptance suites share `AcceptanceTestBase<TFixture>` class initialization
and `TestAssetFixtureBase` generation/building. The source inventory finds 79
shared-base fixture declarations in MSTest and 74 in MTP. The base eagerly builds
reflection and source generation, with optional additional metadata modes.

Existing [profiling results](../affected-test-selection.md#profiling-results)
measured 8.957–10.173 seconds of initialization in a two-test control. Blind
whole-class partitioning did not give reliable wall-time gains, and disabling
shared compilation was slower in the recorded controls. These are historical
results, not measurements of the current revision.

The prior discovery-order experiment reduced repeated fixture setup during
affected-test collection, but its full-run record explicitly describes locally
versioned packages. Do not assume the currently pinned public package includes
that fix without inspecting its resolved implementation.

### Planned controlled experiments

1. Bootstrap the pinned SDK and pack current products. Record failures and
   provenance before measuring consumers.
2. Measure real class-fixture initialization and independent project builds with
   fresh outputs and a consistent run-owned package cache.
3. Compare four sequential project builds with one aggregate invocation, retaining
   the same project sources, properties, frameworks, warnings and binlogs.
   Alternate at least three samples per arm; reconcile required outputs and
   execute the same acceptance cases before/after any selected change.
4. Record neutral/rejected results as well as improvements. Keep broader fixture
   caching and collector lifecycle changes separate from this bounded experiment.

### 2026-11-25: baseline execution and harness corrections

- `.\build.cmd -c Release -pack -bl /p:FastAcceptanceTest=true
  /p:OfficialBuildId=20261125.91` succeeded. It installed SDK
  `12.0.100-alpha.1.26570.125` in the worktree and built/packed the full solution.
  The outer acceptance host targets `net12.0`; generated assets use `net11.0`
  and `net462` with this existing fast-acceptance setting.
- An initial discovery command using `--list-tests --filter ...` failed, exit 1:
  the pinned CLI consumes the next token as the list format. Repeating with
  `--list-tests text --filter ...` succeeded and independently discovered all ten
  `ParallelExecutionTests` cases.
- The unmodified fixture's focused acceptance run executed all ten planned cases:
  ten passed, zero failed/skipped, outer exit 0. The execution gate checked the
  exact source-derived names, both TFMs, fresh TRX timestamps and counters.
  Intentionally failing child test applications are asserted by passing outer
  tests. Assembly method-level CPU-count parallelization remained enabled.
- The first measurement harness launch failed before creating measurement
  samples because three fixture source fields are static readonly rather than
  compile-time constants. `GetRawConstantValue()` was replaced by `GetValue(null)`
  on the actual fields. This is a harness failure, not an optimization result
  or regression-detection proof. The original console log is retained.
- The corrected harness rebuilt with zero warnings/errors. Its first, untimed
  package-priming set generated four real assets and built all eight required
  project/TFM outputs. Timed comparisons use fresh generated directories and
  the same now-populated, experiment-owned package cache.

The measurement captures generation separately from restore/build. Both build
arms include implicit restore, warnings-as-errors, the infrastructure's binlog
logging and its existing multithreaded MSBuild policy. The aggregate arm changes
only the grouping of the same four projects into a generated `.slnx`. A concurrent
process arm is measured separately rather than conflated with aggregation.

## Alternatives and decision

### 2026-11-25: select one aggregate build for ParallelExecutionTests

Three interleaved samples per arm used the actual four fixture sources, fresh
outputs, the same warmed experiment-owned cache and `net11.0;net462`.
Every arm built all eight required outputs successfully.

| Build arm | Samples, seconds | Median, seconds | Range, seconds | Top-level build processes |
| --- | --- | ---: | --- | ---: |
| Sequential | 68.9012566, 30.0283905, 33.3061799 | 33.306 | 30.028–68.901 | 4 |
| One solution | 7.1139588, 8.0814678, 12.4138289 | 8.081 | 7.114–12.414 | 1 |
| Concurrent processes | 6.6173967, 6.7187540, 11.5310437 | 6.719 | 6.617–11.531 | 4 |

The solution arm reduces the median build interval by **75.7%** for this
four-project fixture workload. Each paired trial is faster, but the sequential
outlier and small sample on a shared 32-logical-processor Windows machine limit
precision. This is not a full-suite or hosted-CI percentage.

Generation takes 0.007–0.010 seconds after priming: file creation is not the
dominant cost in this fixture. The actual unmodified fixture's separate three
initialization samples are 59.240, 25.969 and 23.368 seconds (median 25.969).
These are a different workload from the build-only arm, not additional samples
for its median.

Binlog inspection confirms the solution still executes eight `Csc` tasks,
eight `TestingPlatformEntryPointTask` tasks and four
`SetGeneratedAppConfigMetadata` tasks. No compiler-output or completed-copy
double writes were observed. The representative solution build records 6.3
seconds of MSBuild wall time; task totals overlap and are not additive wall time.
One slow sequential project's two-TFM build spends 4.986 seconds cumulatively
in entry-point tasks, 4.133 seconds in generated-app-config metadata, 2.604 seconds
in restore and 1.716 seconds in compilation. This supports amortizing repeated
build/task setup and allowing independent projects to overlap; it does not
attribute the entire gain to NuGet or Roslyn.

**Decision:** generate the same four independent projects, then build a
fixture-owned `.slnx` once. Retain the existing build options, package/cache
policy, framework matrix, cancellation token and output paths. Preserve assembly
method-level CPU-count parallelization and every acceptance assertion.

The concurrent-process arm is slightly faster locally, but consumes four
top-level build slots per initializing fixture. The solution arm obtains a
large gain with one existing `DotnetCli`/`CommandLine` admission slot and fewer
top-level processes, without raising any global limit. No new generic build
helper, cache, feature flag or public API is necessary.

The two shared-base `HelpInfoTests` fixtures were also initialized three times
each, retaining reflection and source-generation builds: MSTest 39.811, 24.746,
30.690 seconds; MTP 13.874, 42.057, 16.054 seconds. They confirm substantial
initialization cost and variance, not a measured optimization for those classes.
Removing their source-generation checks is not selected.

Shared immutable package caching and reduced intermediate-framework matrices
already exist. Enabling persistent mutable caches, raising worker counts and
disabling compiler sharing are not justified by these experiments.

### Parallel-safety boundary

The changed fixture owns one unique temporary directory, four independent
project/TFM output sets and its generated solution until class cleanup.
Initialization awaits the complete aggregate build before any test sees an
asset. Test methods do not mutate the solution or project inputs. The existing
run-owned package cache is shared through NuGet's synchronization, unchanged.
No lock, resource declaration or assembly scheduling configuration changes.
The relevant runtime readers are the five `ParallelExecutionTests` methods;
their host and probe paths remain identical. No cross-fixture output sharing
or new process-global state is introduced.

## Validation and demonstration

### 2026-11-25: validate the selected implementation

Repacked the full Release solution after the fixture edit, retaining the same SDK,
package versions and fast framework setting. Build/pack and the rebuilt measurement
harness both succeeded with zero warnings/errors. Each real fixture measurement
used a new generated directory; a new experiment-owned cache was primed once.

| Actual fixture initialization | Untimed prime, seconds | Timed samples, seconds | Median, seconds | Build processes per initialization |
| --- | ---: | --- | ---: | ---: |
| Original sequential fixture | 39.9042932 | 59.2404418, 25.9685910, 23.3683858 | 25.9685910 | 4 |
| Implemented solution fixture | 27.3868415 | 10.9071912, 13.6277459, 12.4097892 | 12.4097892 | 1 |

The actual initialization median decreased **52.2%** locally. Unlike the
interleaved build-only experiment, these before/after fixture samples were
collected in separate processes/time blocks, so they are supporting evidence,
not a precise causal estimate. Each initialization located the same eight hosts.

The complete instrumented, fast-matrix focused acceptance command took
44.122 seconds before and 43.890 seconds after. Both independently discovered and
executed the same ten cases, all passed, zero failed/skipped, exit 0. The fixture
and child-host launch attempts decreased from 16 to 13 (four builds to one,
plus twelve unchanged child executions). **This one end-to-end pair is effectively
neutral**, not evidence of a 52% suite speedup. Child execution, coverage/reporting,
machine load and contention can dominate the outer interval.

Then repacked with `FastAcceptanceTest=false`, built all twelve project/TFM
outputs, and independently discovered/executed all fifteen cases across
`net11.0`, `net10.0` and `net462`: fifteen passed, zero failed/skipped, exit 0.
That run took 28.435 seconds with nineteen launches (one build plus eighteen
child executions). It has no matching full-matrix baseline and is correctness
evidence only.

The unchanged five methods run for each selected framework:

| Existing case | Preserved contract |
| --- | --- |
| `AllMethodsShouldRunInParallel` | Method-level overlap and intentionally nonzero child exit. |
| `AllClassesShouldRunInParallel` | Class-level overlap and intentionally nonzero child exit. |
| `NothingShouldRunInParallel` | Runsettings-driven serialization and intentionally nonzero child exit. |
| `DoNotParallelizeTestRunsAfterAllParallelizableTests` | Deferred test observes all prior completions; child exit zero. |
| `ParallelizationBannerIsHiddenWithMinimalOutput` | Normal/minimal banner behavior and both child exits zero. |

The existing execution gate verified source/data-derived exact names,
fresh outer TRX start times, planned/selected/executed counts and exits.
Assembly `MethodLevel`, `Workers=0` scheduling was retained. No assertion,
metadata-mode coverage, exclusion or resource-lock declaration changed.
This is **green-before/green-after test-value evidence**, not a bug-fix red phase.

The implemented fixture's fast acceptance build binlog records eight `Csc`
tasks, eight entry-point tasks, four generated-app-config metadata tasks and
one restore task, with zero errors/warnings. No recorded compiler/Copy double
writes or shared completed-Copy destinations were found. That observation
does not rule out unrecorded external/custom writers.

### Input and shipping-package provenance

The only compiled source change is fixture orchestration in
`ParallelExecutionTests.cs`; shipping product sources are unchanged.
The validated fixture's Git blob is
`929860128d2fffd00b2cda87960a344976ae6ef1`.
Both fast packs and the later full-matrix pack use MSTest
`5.0.0-preview.26575.91` and MTP `3.0.0-preview.26575.91`.
The relevant package bytes are unchanged across repacks:

| Package | SHA-256 |
| --- | --- |
| `Microsoft.Testing.Platform.3.0.0-preview.26575.91.nupkg` | `CF152243F90B7FE3FA212F004A103CD919655DC89B97A8B0B47748D753658AB8` |
| `MSTest.SourceGeneration.5.0.0-preview.26575.91.nupkg` | `EC18F164F75064D017F0F6245055F71FE693507067564FF2B5CD4BF1E272460D` |
| `MSTest.TestAdapter.5.0.0-preview.26575.91.nupkg` | `3807F037961085F7716E9429A66211235E3010326A8D903A646B5208A30F53EE` |
| `MSTest.TestFramework.5.0.0-preview.26575.91.nupkg` | `3F5A29296C7492F7FCC5650D4F144675F1C460B25591C2D332423DD29604DB9F` |

Inspected generated `obj/project.assets.json` files to establish framework assets
and effective package folders, not just feed configuration. The actual optimized
harness cache's adapter, framework and platform `.nupkg` files are byte-identical
to the current Shipping feed packages. Inner hosts are under the fixture-owned
`<project>/bin/Release/<tfm>` directories; outer hosts and the harness are under
`artifacts/bin/.../Release/net12.0`. No product-source project references replace
the inner package-consuming projects. All 104 framework/platform DLL copies in
the six implemented fixture directories match the appropriate current package
entries by SHA-256 (MTP selects `netstandard2.0` for `net462`). An initial payload
checker treated `net462/win-x86` as a TFM-only key and failed to find a ZIP entry;
splitting RID-qualified asset keys and comparing each distinct TFM passed.

Compared generated `.cs`, `.csproj` and `.runsettings` inputs across all six
implemented fixture directories (24 project sets). Fast inputs are byte-identical
to the sequential control. Full-matrix inputs differ only by the intentional
`net10.0` expansion in `<TargetFrameworks>`. An initial byte-equality check
incorrectly included the full matrix and failed on that expected project-file
difference; inspecting the diff and repeating a matrix-aware comparison passed.
This was a provenance-check correction, not a product failure.

### Reproduction and retained artifacts

[The reproduction appendix](acceptance-fixture-performance-reproduction.md)
preserves the measurement harness source, build commands, sampling order,
selection and execution gate. It is an experimental scratch program, not
additional shipping tooling or a CI benchmark.

The local evidence root is `artifacts/fixture-performance/`. It contains
`baseline`, `experiment-1` (failed harness launch), `experiment-2` (interleaved
arms and HelpInfo samples), `actual-baseline`, `actual-optimized`,
`optimized-tests`, `full-matrix-tests`, package hashes and generated provenance.
Each timed harness run writes `measurements.json` and `provenance.json`;
build-only arms also write build logs and generated-asset manifests.
Outer acceptance runs retain discovery/execution logs, expected names, UTC start,
execution-gate output and TRX. Generated inputs and unique inner binlogs are
retained under `artifacts/tmp/Release/testsuite`; full pack binlogs are under
`artifacts/log/Release`.
Raw diagnostics remain task-owned local artifacts rather than publicly uploaded
machine paths; the committed records preserve the results and reproducible method.
No shared cache/server was cleared or stopped, and environment overrides were
process-local. An internal setup-cost optimization has no changed visual surface;
the timings, process counts and unchanged test identities are its demonstration.

## CI versus local end-to-end time

### 2026-11-25: attribute the hosted-CI gap

Hosted evidence came from completed dnceng-public build 1699206 (definition 209),
cross-checked against build 1699115. Local evidence came from one isolated run on
a shared 32-logical-processor Windows machine.
Build 1699206 tested source revision
`94a19aec18007665ffede752aefb5ff7e5d0ee45` from PR #12631's merge ref, not this
PR's current head.

| Measurement | Hosted CI (Windows Release) | Local |
| --- | --- | --- |
| MSTest acceptance wall time | 70m 38s (13:53:38–15:04:17) | 30m 49s |
| MSTest acceptance tests | 1,259 (1,205 passed, 54 skipped) | 1,576 (1,506 passed, 15 failed, 55 not executed) |
| Concurrent work in the same `dotnet test` | ~45 module/TFM runs (64,534 tests, 71m 10s) | none (acceptance module only) |
| Coverage, trace diagnostics, crash/hang dumps | on | on (same `test/Directory.Build.*` defaults) |

Local command, with `PATH`/`DOTNET_ROOT` set to the repo-local `.dotnet` and
freshly packed packages:

```powershell
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests -c Release --no-build -f net12.0 --results-directory artifacts\tmp\local-full-mstest-acceptance -- --report-trx --minimum-expected-tests 1
```

It exited with code 2. The 15 failures were confined to `ShippingController_*`,
`ExplicitConsumer_*` and `TestDataSourceFromAppConfig`; they were not
investigated, and they do not shorten the run. Summed per-test durations were
325 minutes, showing roughly 10.5x effective concurrency inside a 31-minute
wall time. The local tree was newer than the CI commit and ran **more** tests,
yet took less than half the hosted wall time.

Hosted job-level facts for build 1699206:

- Windows Release took 102.7 minutes. That included a 16.4-minute build and a
  73.5-minute "Test affected changes" step (74.3 minutes in build 1699115).
  Linux Release took 50.7 minutes (36.4-minute test step) and Windows Debug
  took 36.5 minutes (8.8-minute test step).
- The test step runs a single `dotnet test --solution TestFx.slnx -c Release
  --no-build --affected-tests`
  (`eng/pipelines/steps/test-affected-tests.yml`). Unit tests for several TFMs,
  both acceptance suites and the playgrounds all run in that one invocation.
- The cache-seed step imported 46 maps from build 1695348 in 101 seconds, but
  modules still logged `Falling back to all tests (planned count unavailable):
  No successful Azure DevOps collect artifact contains 'affected-tests/v2/...'`.
  The imported artifact therefore did not provide the requested map objects
  for this run, and affected-test selection removed no work. The exact reason
  those object names were unavailable remains unresolved.
- MSTest acceptance started first and was the critical path. MTP acceptance
  (920 passed, 34 skipped) ran 14:12:57–14:58:10 and finished six minutes
  earlier.
- During MSTest acceptance's first ~20 minutes, CPU-heavy unit modules ran on
  the same agent. Examples: `MSTest.Analyzers.UnitTests` took about 4–4.6
  minutes for each of 3 TFMs, and `Microsoft.Testing.Platform.UnitTests` took
  about 1 minute per TFM. MTP acceptance overlapped the remainder. The log has
  41 `[slow] still running` warnings, for example `AnalyzersTests.MSTEST0001`.
- Fixture builds are admitted up to `Environment.ProcessorCount`
  (`DotnetCli`). Two acceptance suites share that agent's cores with each
  other and with the unit modules. No hosted log prints the agent's core
  count, so the hosted capacity is unknown.

Attribution: the evidence supports a contention and capacity explanation, not
a fixture-specific regression. Diagnostics and coverage are configured the
same on both sides, and `DotnetCli` strips the coverage profiler from child
fixture builds. The hosted run did extra work beyond the local acceptance
module, and the affected-test fallback ran the full suite. The available data
cannot separate the contribution of the unknown hosted core count from that of
concurrent modules. Per-test hosted durations require authenticated artifact
or test-result access that the anonymous REST endpoints did not provide, so
uniform versus concentrated slowdown is unmeasured.

Candidate levers, ordered by expected value:

1. Run the acceptance projects in a separate job or agent from the unit-test
   modules. Alternatively, shard MSTest acceptance, the 70-minute critical
   path, across agents. **Not attempted:** no matched hosted CI A/B run yet
   measures whether the extra job capacity reduces wall time enough to justify
   the extra build/agent cost.
2. Resolve why the 46 imported maps did not satisfy the requested object names.
   The earlier local provider check showed an imported map can be consumed
   when its identity matches, but does not explain this hosted run's misses.
3. This PR now logs `.NET Environment.ProcessorCount` immediately before the
   affected-test invocation. The metric is a diagnostic only; it has no
   runtime speedup, and no hosted run of this change is available yet.
4. Measure the cost of the TF_BUILD-only AzDO history reporters and of trace
   diagnostics before changing them. Neither is shown to be a cause.

Validation for the added capacity line: the PowerShell expression printed
`ProcessorCount=32` locally, and
`pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests true -EnableNonWindowsAffectedTests false`
exited 0 with 92 pipeline-configuration checks passed. This does not substitute
for an Azure Pipelines run; the hosted value and any performance effect remain
unmeasured.

The fixture aggregation in this change reduces work under either explanation.
Pipeline restructuring is a separate change, and it needs hosted A/B runs with
matched commits before any speedup is claimed.

## Remaining limits

Local measurements cannot establish hosted-CI or full-suite speedup. The full
local MSTest acceptance run exited with 15 uninvestigated failures; a passing
full run and a full local MTP acceptance run were not established. Linux/macOS
and other release branches were not executed. This test-only optimization needs
no downstream package rollout, temporary flag or product servicing/backport;
existing acceptance CI can exercise the changed fixture, but no exact-head
hosted execution is claimed here.

Active-build cancellation and injected build failures were not separately
executed. Existing `DotnetCli` failure/retry handling and cancellation-token
forwarding remain unchanged; fixture publication still awaits successful build
completion. The passing multi-project/TFM runs and intentionally failing test
children are not substitutes for injected build-failure/cancellation proof.

The MTP HelpInfo fixture was measured, but no general shared-base improvement
was established. Reflection/source-generation sharing needs a separate experiment
that preserves both compile variants. Cross-process mutable fixture caching and
collector changes are untried alternatives; the public collector's historical
optimization status remains unverified. Do not extrapolate this four-project
result to single-project fixtures.
