# BuildXL build: research and decisions

## Problem and contract

Experiment with BuildXL scheduling and caching the existing TestFx MSBuild
projects. This is an opt-in Windows x64 development experiment, not a replacement
for Arcade, the existing Bazel experiment, or the shipping pipeline.

The initial workload is `Microsoft.Testing.Extensions.TrxReport` and its project
references, targeting `net10.0`. This exercises the platform, reporter, resource
generation, analyzers, and project-to-project dependencies without requiring
UWP, WinUI, browser, or WASI workloads. Windows-only scope and this initial
workload are reversible assumptions, not a request to drop other targets.

- Arcade's existing bootstrap owns the SDK pinned by `global.json`. NuGet
  restore precedes BuildXL and uses the same properties as compilation.
- BuildXL's native MSBuild resolver owns project scheduling and caching;
  wrapping an entire `build.cmd` invocation in one cached process is not the
  experiment.
- Experimental binaries, intermediates, engine state, cache, and logs must not
  overwrite normal Arcade outputs. Existing project definitions, analyzers,
  generated resources, and API validation remain authoritative.
- Failed restore, graph construction, sandbox validation, or compilation must
  fail the command. There is no silent fallback to the ordinary build.
- Builds are local-only. No remote cache, distributed execution, credentials,
  CI changes, package publication, or release-branch enablement is implied.
- Pack, signing, acceptance testing, the full solution/workload matrix, and
  non-Windows execution remain with the existing build until separately proven.

## Running the experiment

Run from the checkout root with **PowerShell 7** on Windows x64:

```powershell
pwsh -NoProfile -File .\eng\buildxl.ps1
```

On ReFS or a volume without a change journal, opt into full input hashing:

```powershell
pwsh -NoProfile -File .\eng\buildxl.ps1 -NoJournal
pwsh -NoProfile -File .\eng\buildxl\validate.ps1 -NoJournal
```

The first invocation downloads the checksum-pinned public BuildXL deployment
and uses Arcade's existing helper to acquire the exact repository SDK. It does
not install system tools or the full solution's optional workloads.

`-Project` accepts a checkout-local `.csproj`; `-Configuration Debug|Release`
and `-TargetFramework net10.0|net11.0|netstandard2.0` select the experiment's
global properties. Only the default `Debug/net10.0` TRX graph is validated so
far; other projects/frameworks are experiments, not promised coverage.
`-SkipRestore` is available only after a successful matching restore.

Binaries are in `artifacts\buildxl\bin`, intermediates in
`artifacts\obj\buildxl`, and the local action cache and engine logs are under
`artifacts\buildxl`. The pinned deployment is in `artifacts\buildxl-tools`.
Per-project MSBuild binlogs are under
`artifacts\buildxl\Out\Logs\MSBuild`; restore writes
`artifacts\buildxl\logs\restore.binlog`.
The validation script retains separate console logs, counters, and a parsed
seven-case `results.json` in a unique `artifacts\buildxl-validation` directory.
It intentionally exercises a failing build and deletes/retrieves one generated
reporter DLL; it does not edit product sources.

Do not run this backend, its validator, or another builder concurrently in
the **same checkout**. Different invocations share this checkout's BuildXL
outputs and cache. The source mount is not declared scrubbable, but BuildXL
does clean its own recorded generated outputs. No remote cache is configured.
The engine server and managed compiler server are disabled so compilation
does not depend on reusable background services.

`-NoJournal` still emits BuildXL's missing-journal warning. It disables
metadata/journal optimizations, **not** sandbox enforcement or content hashing.
Do not enable a shared volume's journal or suppress file-access violations just
to make this experiment pass.

## Comparing build performance

Run the same comparison used by the pipeline:

```powershell
pwsh -NoProfile -File .\eng\buildxl\benchmark.ps1 -SelfTest
pwsh -NoProfile -File .\eng\buildxl\benchmark.ps1 -Samples 3 -Jobs 8 -NoJournal
```

By default, results go into a new `artifacts\buildxl-perf\<run-id>` directory.
`-ResultsDirectory <new-absolute-directory>` selects a different evidence
directory; it must not exist. Binary/intermediate/action-cache directories use
unique `-OutputId` values under `artifacts\buildxl-runs` and
`artifacts\obj\buildxl-runs`, so the experiment neither cleans the regular build
nor borrows an old action cache. Generated benchmark input and import files
live in `artifacts\buildxl-perf-fixtures`, not the product source tree.

The shared driver also supports `-Engine MSBuild` for the reference build,
`-Jobs` for equal concurrency limits, `-AdditionalTargets` for a checkout-local
benchmark import, and `-PrepareOnly` for untimed tool/package acquisition.
Its default engine remains BuildXL. Do not compare the reference backend
against the shipping Arcade pipeline's complete pack/test job: this comparison
is deliberately limited to equivalent builds of the TRX project graph.

### Observed local results

Command executed from the checkout root:

```powershell
& .\eng\buildxl\benchmark.ps1 -Samples 3 -Jobs 8 -NoJournal `
    -ResultsDirectory (Join-Path $PWD 'artifacts\buildxl-perf\local-20261109-01')
```

It exited **0**, with **18 selected/executed/verified benchmark samples**: three
per engine/scenario. The order alternated MSBuild/BuildXL, BuildXL/MSBuild,
MSBuild/BuildXL. The host was Windows x64 `10.0.26300.0`, ReFS, 32 logical
processors, PowerShell `7.6.9`; both engines were capped at eight workers.
SDK and BuildXL versions match the original experiment above.

| Scenario | MSBuild median (observed range), seconds | BuildXL median (observed range), seconds | BuildXL elapsed increase |
| --- | ---: | ---: | ---: |
| Output-cold | 53.906 (53.865-61.192) | 70.400 (68.869-80.902) | 30.6% |
| Changed input | 55.537 (50.892-56.876) | 64.769 (63.027-74.125) | 16.6% |
| No-op | 6.861 (6.592-7.114) | 12.533 (10.082-13.731) | 82.7% |

BuildXL was **slower for this small local workload**; the result does not justify
replacing the default build. This is not a cache failure: every BuildXL cold
sample had six misses and zero hits, each changed-input sample had three misses
and three hits, and all no-op samples had six hits and zero misses.

All nine paired output inventories matched at **67 files**, and primary assembly
identities and the expected baseline/changed metadata marker were checked in
every sample. An untimed successful baseline build preceded each measured
changed-input or no-op run. Fresh output directories establish the output-cold
state without deleting any shared or earlier-run outputs.

Elapsed times include fresh PowerShell/driver startup, explicit restore, engine
initialization, build/cache lookup, and binary/console logging. Package/tool
downloads and scenario preparation are outside the timer; NuGet and OS caches
were not cleared. Both backends keep compiler analyzers enabled, use no reusable
engine/compiler server, and disable the same non-hermetic IDE-cache side effect.
BuildXL uses its real per-project binlogs and the reference uses a normal
MSBuild invocation's binlog; logging is enabled in both, not assumed free.

These are medians/ranges from three samples on **one machine, one small graph,
one SDK/BuildXL pair, and the ReFS full-hashing profile**. There is no independent
rerun or statistical-significance claim, and the measurements do not establish
full-solution, NTFS-journal, clean-network, packaging, testing, or whole-CI
performance. No part of the gap is attributed to a particular cause without
separate diagnostic evidence.

### Pipeline leg and trends

`azure-pipelines.yml` now imports
`eng/pipelines/jobs/buildxl-performance.yml` as the independent
**BuildXLPerformance** job. `EnableBuildXLPerformance` defaults to **true**;
queue-time `false` omits it. The job follows product-change classification and
runs on main CI, nightly, manual builds, and product-changing PRs; samples-only,
infrastructure-only, and XML-doc-only changes retain the existing skip behavior.
Unknown classification runs the comparison. `SkipTests` still controls the
existing product tests, not this build benchmark.

The Windows image, three paired samples, eight-worker limit, and `-NoJournal`
policy are explicit and match the local command. No remote/action cache is
restored, and each job has a fresh package cache primed outside measured work.
The job fails on build/output/cache-contract errors or an incomplete sample
matrix, **not** on a noisy percentage threshold.

The standalone job applies Arcade's existing public-project policy for
`Codeql.SkipTaskAutoInjection` and `skipComponentGovernanceDetection`. Without
those defaults, the CodeQL tracer injected into
[run 1672558](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1672558)
wrote `build-tracer.log` and `tags.db` outside BuildXL's declared outputs,
failing sandbox validation. Keep sandbox enforcement enabled; CodeQL scanning
remains in the official and dedicated CodeQL pipelines.

After success or failure, the job uploads the Markdown summary and retains
`results.json`, `timings.csv`, console logs, counters, tool/source/host
provenance, and binlogs in the `buildxl-performance-Attempt<attempt>` artifact.
Per-project logs replayed by a BuildXL cache hit are historical cached action
logs, not evidence that a compiler ran in that sample; the fresh process
counters distinguish those cases.

`PublishTestResults@2` sends the actual measured durations to Azure Test
Analytics with stable series
`TestFx.BuildPerformance.Trx.Debug.net10.Jobs8.FullHashing.v1` and 18 stable
names such as `BuildXL.Cold.Sample1` and `MSBuild.NoOp.Sample3`.
These are **newly executed benchmark cases, not product tests or coverage**.
`benchmark.junit.xml` preserves each sample's own elapsed duration; the JSON and
summary carry medians/ranges and signed savings. Incomplete runs publish a
failing `Benchmark.Completion` case rather than a green empty result.
Use the retained SDK/BuildXL/image metadata when interpreting trends across
toolchain or hosted-image changes.

Local YAML checks covered enabled/disabled expansion, the job's conditions,
publication-on-failure wiring, and its fresh-cache/no-token contract. All
existing affected-test rollout/gating self-tests still pass. The original
`BuildXLPerformance` check succeeded in
[Azure build 1671415](https://dev.azure.com/dnceng-public/cbb18261-c48f-4abb-8651-8cdcb5474649/_build/results?buildId=1671415)
for PR head `5c211fbaa7a9d5252248d6d35111bb925dea3712`. Live Test
Analytics/summary rendering has not been visually inspected, and this result
does not validate subsequent source revisions.

### Performance evidence and regression sensitivity

The local result directory above contains complete JSON, CSV, console/binlogs,
and JUnit. All **18 exported names and durations** were independently reconciled
against the 18 measured samples, and all nine paired inventories were checked.
The run's base is `19bfd517ba336e27bbeed4967fc5629e956ccc23`, with source-patch
identity `769F4BDD7BE6BBFBA9331A6A6DC51740B647323A676024F7A4DA30691A5E362A`.
Subsequent documentation does not change the measured executable inputs:

| Measured executable/configuration | SHA-256 |
| --- | --- |
| `eng/buildxl.ps1` | `3515FBC42FBFA2E1CC22F2DB7F6EE02AED6DF1D4079CB9C8FE83DCCD17440865` |
| `eng/buildxl/config.dsc` | `04FE9ECC6FD80B86171EAF21C8F692A98E6E83C09600A409B86810C22DD40FDF` |
| `eng/buildxl/benchmark.ps1` | `EF373F0B2F17A1795817EAC5EBEB52084B8C6F349DB9324ABADC917559E8E6E7` |
| `eng/buildxl/validate.ps1` | `B117B597572761C8F059376D486830E87AF05A26D46C2A7504D6A6340E001FD1` |
| `azure-pipelines.yml` | `96897A94587C06451B1D1F55458BF1C6BE97C61B730798F28855A4DECF26122E` |
| `eng/pipelines/jobs/buildxl-performance.yml` | `3900DBBFE5C4CDD07BF1C8CEF5C249DC6BBCA6204CCFDEFE01539953E0A1F481` |

**Test-value evidence:**

| Contract / case | Executed evidence |
| --- | --- |
| Correct statistics / `MedianAndObservedRange`, `EvenSampleMedian`, `InvalidTimingRejected` | Six benchmark self-checks pass with `-SelfTest`, exit 0. An isolated copy reporting the minimum as the median fails the retained `MedianAndObservedRange` assertion, exit 1; the unchanged script passes again. |
| Complete matching outputs / `MissingOutputRejected`, paired sample validation | The self-check rejects a missing output, and all 18 real samples verify inventories, identities, and changed-input markers. |
| Trend export / `CompleteBenchmarkExport`, `IncompleteRunExportFails` | JSON/JUnit self-checks pass; incomplete data exports a failing completion case. All actual XML durations match their measured JSON samples to the exported precision. |
| Reference failure and original driver contracts | `-Engine MSBuild` preserves a real `BUILDXLTEST001` failure as exit 1. The updated seven-case native smoke suite passes, including caller-state restoration, failure propagation, invalidation, cache reuse, and output recovery. |

The full smoke command was `& .\eng\buildxl\validate.ps1 -NoJournal` (exit 0,
seven selected/executed/passed, zero failures/skips), with report
`artifacts/buildxl-validation/ea2befe1240d4acb8cc0bdd517cf1e45/results.json`.
Self-check fixture cleanup initially hit Windows temporary-directory
permissions; the corrected harness uses and cleans its unique ignored
repository artifact directory instead. That failed setup attempt is not
counted as red-phase behavioral evidence.

## Research and experiments

### 2026-11-09: review correction for caller diagnostics

Review identified one additional environment side effect of the existing
Arcade bootstrap: `eng/common/tools.ps1` initializes `MSBUILDDEBUGPATH` when
it is absent. The driver did not include that variable in its restoration
snapshot, so direct invocation in an existing PowerShell session leaked the
checkout-specific diagnostic destination to later, unrelated builds.

The fix adds `MSBUILDDEBUGPATH` to the existing snapshot and `finally`
restoration rather than changing the shared Arcade helper or build behavior.
`CallerStateRestored` now explicitly starts with that variable absent and
checks it alongside the other caller state. Its cleanup preserves the original
value or absence even when the assertion fails.

**Test-value evidence:** the same retained test ran before and after the
driver fix with `& .\eng\buildxl\validate.ps1 -NoJournal -Case CallerStateRestored`.
The pre-fix run selected/executed one case and exited 1 with
`Caller environment variable MSBUILDDEBUGPATH changed from absent to present.`;
the native build itself exited 0. The corrected run selected/executed/passed
the same case and exited 0. The retained validator SHA-256 was
`9547EC0865F6C949B5E4085CAD17ADB1E42D0CAD7F656801DEA0C6287DB17E65`
in both runs. Driver hashes were:

| State | Driver SHA-256 |
| --- | --- |
| Pre-fix, PR head `5c211fbaa7a9d5252248d6d35111bb925dea3712` | `3515FBC42FBFA2E1CC22F2DB7F6EE02AED6DF1D4079CB9C8FE83DCCD17440865` |
| Corrected snapshot | `5FC6D69EE37C4F29D0DFD1817AF1559ADC24335DA0915F2DC3E98584F68AB28B` |

Red/green reports are retained locally as
`artifacts/buildxl-experiment/msbuild-debug-path-red.json` and
`msbuild-debug-path-green.json`. The complete
`& .\eng\buildxl\validate.ps1 -NoJournal` run also exited 0 with seven
selected/executed/passed cases and no failures/skips; its report is
`artifacts/buildxl-validation/700e8ee574754224849d7a5b2f59f232/results.json`.
`& .\eng\buildxl\benchmark.ps1 -SelfTest` exited 0 with all six self-checks
passing. The original 18-sample performance table remains bound to its
original measured hashes; it was not rerun or relabeled as the new revision.

The review suggestion to disable or make the performance job non-blocking
would change this PR's intended default-on tracking policy, not correct a
demonstrated defect. The default and queue-time opt-out remain unchanged;
timing variance does not fail the job, while build/output/report errors remain
visible.

CI triage for the original head found one unrelated Windows Debug failure:
`AnalyzerWaitCancellation [net472]` in the unchanged
`AnalyzerCompatibilityTests.WaitForExit_OwnedChildCancellation_CompletesAndReleasesItsWait`
failed `Assert.IsFalse(waiting.IsCompleted)` in Azure build 1671415,
log 65. That test and its product paths were not changed by this PR, so no
unrelated test fix or weakened assertion is included.

### 2026-11-09: matched local performance and pipeline leg

The follow-up request is to measure the local performance difference and enable
a separate pipeline leg for ongoing observation. The workload remains the
three-project TRX graph, **Debug/net10.0**, with analyzers and binary logging
enabled. Both engines use eight workers, fresh processes, no compiler server,
the exact pinned SDK, and the same explicit restore followed by Build.

The measured boundary includes PowerShell/driver startup, restore, graph or
MSBuild initialization, compilation/cache lookup, and log writes. Tool and
package acquisition is primed outside the timer. These are output-cold builds
with a warm NuGet/OS cache, not clean-machine or cold-network measurements.
Every pair gets fresh, separate binary/intermediate directories and an empty
BuildXL action cache, without deleting another run's files.

At least three alternating engine pairs will measure:

- **Cold:** fresh outputs/intermediates and empty action cache.
- **Changed:** an untimed successful baseline build, then a controlled assembly
  metadata source change in a generated, task-owned file compiled only into MTP.
- **NoOp:** restore the baseline marker and build it outside the timer, then
  measure the identical command with no input changes.

The marker is injected with a temporary MSBuild import shared by both engines,
not by editing repository product sources. Each sample must produce the exact
expected marker, complete matching output inventory, and primary assembly
identities. BuildXL no-op samples must report actual process cache hits.
Record all elapsed samples, medians/ranges, order, cache counters, source hashes,
SDK/BuildXL/host versions, and exact commands. Compare the same successful
workload, not a warm BuildXL action cache against an output-cold MSBuild build.

The existing Azure pipeline will gain a separate Windows job, enabled by
default with a queue-time opt-out. It will retain JSON/CSV/binlogs and a job
summary. Stable JUnit **benchmark-case** names and measured sample durations
will provide Azure Test Analytics duration history; these are not product unit
test executions. No percentage threshold is introduced before a reliable
machine/image baseline exists. The new job does not supply shipping outputs,
use remote action caching, or change ordinary test selection.

### 2026-11-09: integration research

Source state: TestFx `19bfd517ba336e27bbeed4967fc5629e956ccc23`.

The [BuildXL MSBuild frontend documentation][msbuild] confirms that the resolver
uses MSBuild's static project graph and schedules a process per project instance.
It has only been tested on Windows. The [resolver schema][schema] provides
`msBuildRuntime: "DotNetCore"`, explicit MSBuild and `dotnet` search locations,
entry points, global properties, output directories, and per-project binlogs.
The [example][example] restores dependencies before invoking BuildXL.

The [installation page][installation] says external users should build BuildXL
from source; there are no GitHub release assets. However, the repository's
[public bootstrap pin][bootstrap] identifies
`Microsoft.BuildXL.win-x64` version `0.1.0-20261030.6` on its
`BuildXL.External` Azure Artifacts feed. Feed discovery succeeds without
credentials; the subsequent experiments established package availability and
SDK compatibility.

The installed system SDKs do not satisfy TestFx's pinned .NET 12 engineering
SDK: `dotnet --version` fails with the repository's SDK-not-found message.
Arcade's `InitializeDotNetCli` successfully bootstrapped
`12.0.100-alpha.1.26556.115` into this worktree. An initial runtime probe also
installed the graph builder's default .NET `10.0.14` runtime locally. Inspection
of the deployed version's [graph-builder launch code][graph-builder] then
confirmed that explicit MSBuild search locations select the SDK's own
`MSBuild.runtimeconfig.json`; the wrapper therefore does not require that extra
runtime installation for compilation.

The public bootstrap package downloaded successfully and `bxl.exe /help:Verbose`
exited 0. Its SHA-256 is
`460832AD3CB34CB9D6B7DF3B5CF6059E172045206C48E6D48B9A3A401B0C5802`.
The engine identifies its source as
`aab9f814e31aa8a05607bbc4f051e44eb6e2afc7`.

The ordinary-MSBuild baseline compiled the three-project TRX workload
successfully with analyzers enabled and isolated binaries/intermediates
(`artifacts/buildxl-baseline/bin` and `artifacts/obj/buildxl-baseline`).
The first native BuildXL attempt restored successfully but failed DScript
configuration type checking (exit 1): output declarations require `Path`,
not `Directory`. This is a setup failure, not regression-sensitivity evidence.
The configuration now uses the deployed schema's path-valued declarations.
The next attempt rejected the backslash-relative DScript root (exit 1).
The resolver now receives the absolute checkout directory from the wrapper,
avoiding DScript path-literal escaping.

The third attempt loaded the configuration but failed the change-journal
prerequisite (exit 1, `DX0906`). The workspace is on ReFS. No volume settings
were changed. BuildXL's [engine contract][engine-configuration] explicitly
supports running without a journal, with file-content-table and change-tracker
optimizations disabled. The opt-in `-NoJournal` profile disables journal
verification/scanning, the file content table, and hardlinks. It must still
hash inputs and enforce sandbox/file-access violations; none of those checks
are disabled.

The wrapper copies its DScript configuration into `artifacts/buildxl` before
launch. This also keeps BuildXL's configuration-relative default output/log
directories inside ignored artifacts rather than under `eng`.

The fourth attempt constructed the .NET 12 MSBuild graph but could not schedule
outputs outside the configuration directory's default mount (exit -1).
The native resolver [always declares a catch-all shared output at its root][pip-constructor].
An explicit readable/writable, **non-scrubbable** checkout mount is therefore
required, along with a read-only mount for the effective NuGet package cache.
The unused in-box DScript SDK resolver is disabled.

The fifth attempt exposed a multi-targeting issue (exit -1): setting the global
`TargetFramework` left referenced projects' outer and inner builds in the graph,
including unwanted frameworks, and the native resolver assigned colliding log
paths to the outer build and the selected inner build. The experiment now
restricts **`TargetFrameworks`**, letting MSBuild's graph protocol distinguish
outer and inner builds and preserve fixed-target analyzer/tool projects.
Restore and the ordinary baseline must use this same property.

The sixth attempt scheduled and ran six project instances; compilation emitted
the expected assemblies and satellite resources. The build correctly failed
(exit 1) because the SDK's opportunistic language-service project-data writer
tried to update a donor-index lock in the **shared Git directory outside this
worktree**. The sandbox was not weakened and that directory was not allowlisted.
For this opt-in experiment only, `EnableProjectDataOnBuild=false` avoids that
non-hermetic IDE-cache side effect. Compiler analyzers, API checks, localization,
and binary generation remain enabled. Language-service project-data publication
is not part of this backend's contract. The matched baseline must use the same
switch.

Compilation receives an explicit tracked environment rather than all caller
variables. SDK paths and the effective package cache are included; unrelated
shell variables and feed credentials are not propagated to build processes.

The seventh attempt succeeded: all **six** outer/inner MSBuild process nodes
executed for the three-project graph, with no sandbox violations (exit 0).
An identical repeat reloaded the graph, reported **6/6 process cache hits** and
zero process cache misses, and exited 0. This establishes local cache reuse,
not a comparative speedup over ordinary MSBuild.

The matched baseline and BuildXL each produced **67 files**. Of these, **61
were byte-identical**. The six remaining files are the three primary assemblies
and their dependency copies; all three assembly identities match. Embedded-PDB
inspection confirms that generated-source document paths differ between the
isolated `buildxl-baseline` and `buildxl` intermediate directories. Binary
identity must not be claimed for these assemblies.

The smoke harness initially failed all six cases because a single PowerShell
switch string was splatted as characters. After making the argument list a
typed string array, four cases passed. The two remaining fixture checks exposed
an authoring error: bare MSBuild projects without the target protocol use their
default target, which was the first, empty `Restore` target. Giving those
fixtures explicit `DefaultTargets="Build"` made both focused checks pass.
These were harness/setup failures, not red-phase product evidence.

A final direct-invocation probe then found a real wrapper defect: PowerShell
converts an untyped null passed to `Environment.SetEnvironmentVariable` into an
empty string, changing previously absent caller variables into present-empty
values. The added `CallerStateRestored` regression executed a successful native
build and then failed specifically on the changed presence of
`DOTNET_INSTALL_DIR`. The fix passes `[NullString]::Value` for absent values.
The same retained case also checks the caller's working directory, tracked
environment values, and an existing cached-SDK sentinel.
After correcting environment absence, that same case exposed the second half
of restoration: `Get-Variable` returns a live `PSVariable`, whose value becomes
null when `Remove-Variable` runs. The wrapper now snapshots the **value** before
temporarily replacing the SDK cache. Both failures occurred after successful
native builds and are behavioral regression evidence, not setup failures.

## Alternatives and decision

| Approach | Decision |
| --- | --- |
| Keep only Arcade/MSBuild | Remains the default and shipping authority. |
| Cache the whole Arcade build as one process | Rejected: does not test BuildXL's project scheduling or dependency graph. |
| Translate projects into DScript compiler actions | Not attempted: duplicates framework, analyzer, resource, and package policy. |
| Use the native MSBuild resolver after restore | Selected for the bounded experiment; preserves existing project semantics and exposes real compatibility limits. |

No speedup or cache-correctness claim is established by configuration alone.
Successful equivalent outputs and actual BuildXL execution/cache statistics are
required before drawing conclusions.

## Validation and demonstration

| Requirement / risk | Executable evidence | Surface / gap |
| --- | --- | --- |
| Native resolver builds real projects | `NativeBuildProducesClosure`: six process nodes and the three expected assembly identities. | BuildXL console/log transcript; no TestFx UI/layout changes. |
| Existing build remains independent | Ordinary MSBuild builds the same workload into separate outputs; compare inventories and assembly identities. | 67 files in each layout; no full-binary-identity claim. |
| Cache reuse is real | `UnchangedBuildUsesCache`: six cache hits, zero misses. | Counters, not an inferred speedup. |
| Cache is not just leaving old files on disk | `MissingOutputRestoredFromCache`: delete the generated reporter DLL, rebuild, and require its exact SHA-256 with six cache hits. | No source or shared-cache deletion. |
| Changed inputs invalidate the cache | `ChangedInputInvalidatesCache`: change a real task-read input from `alpha` to `bravo`, preserve size and timestamp, require one cache miss and the new exact output. | Relevant to the full-hashing ReFS profile. |
| Failures remain failures | `InvalidProjectRejected` and `BuildFailurePropagates`: reject an absent project and execute the `BUILDXLTEST001` failing target. | The intentional child failure must remain exit 1. |
| Direct invocation preserves the caller | `CallerStateRestored`: start in `docs`, arrange absent variables and existing sentinels, run the real build, and require the exact original environment/SDK-cache state and directory. | Includes the observed null-to-empty restoration regression. |

Baseline and bootstrap logs are in ignored, task-owned artifact storage under
`artifacts/buildxl-experiment` and `artifacts/buildxl-tools`. Native BuildXL
compilation and local cache reuse have succeeded.

### Initial experiment executed results

The initial verified source state is the base commit above plus the three files
identified below. Documentation changes do not change these executable hashes.
Host: Windows x64, ReFS, PowerShell `7.6.9` on .NET `10.0.14`; build SDK
`12.0.100-alpha.1.26556.115`; BuildXL `0.1.0-20261030.6`.

| Executable input | SHA-256 |
| --- | --- |
| `eng/buildxl.ps1` | `DE5A35970534400B391A70E8862FDEEAF3EACA74961BEF2F6A48477AC109BBE2` |
| `eng/buildxl/config.dsc` | `88BDAB189306BDCBD5A55532351122421B45B47B6EB6635888942CAE4A4570E8` |
| `eng/buildxl/validate.ps1` | `404FDBC410E270088261179044A65428D4FD27CB33CF1BB93CE17E4E928E19E9` |

| Executed command | Result |
| --- | --- |
| `& .\eng\buildxl.ps1 -NoJournal` | Exit 0; final run: six process nodes, six cache hits, zero misses. |
| `& .\eng\buildxl\validate.ps1 -NoJournal` | Exit 0; seven selected/executed/passed cases, no failures or skips. |
| `& .\eng\buildxl\validate.ps1 -NoJournal -Case CallerStateRestored` | Exit 0 after the fixes; the same assertions previously failed after successful native builds. |
| `& .\eng\buildxl\validate.ps1 -NoJournal -Case BuildFailurePropagates` | Exit 0; the intentional native MSBuild/BuildXL failure remains child exit 1. |

The equivalent ordinary-MSBuild baseline was bootstrapped through
`eng/common/tools.ps1`, then executed with these arguments (exit 0):

```powershell
. .\eng\common\tools.ps1
$sdk = InitializeDotNetCli -install:$true -createSdkLocationFile:$false
$bin = Join-Path $PWD 'artifacts\buildxl-baseline\bin\'
$obj = Join-Path $PWD 'artifacts\obj\buildxl-baseline\'
& (Join-Path $sdk 'dotnet.exe') msbuild `
    .\src\Platform\Microsoft.Testing.Extensions.TrxReport\Microsoft.Testing.Extensions.TrxReport.csproj `
    /restore /t:Build /p:TargetFrameworks=net10.0 /p:Configuration=Debug `
    /p:EnableProjectDataOnBuild=false /p:UseSharedCompilation=false `
    "/p:ArtifactsBinDir=$bin" "/p:ArtifactsObjDir=$obj" /nr:false /nologo /v:minimal `
    "/bl:$PWD\artifacts\buildxl-experiment\baseline-no-shared-compiler.binlog"
```

Final output comparison: identical 67-file inventories, matching primary
assembly identities, and 61 byte-identical files; the six assembly copies
identified above are not byte-identical. The recorded result is
`artifacts/buildxl-experiment/final-output-comparison.json`. No comparative
wall-time benchmark was performed.

The complete final smoke report is
`artifacts/buildxl-validation/a3ab05026d4f4420b2f8e572395eb694/results.json`;
it records the seven exact case names, command exits, and executable hashes.
The directories and logs are local evidence, **not published artifacts or CI
results**. The commands and concise observed results are preserved here so
another checkout can reproduce them without those local files.

### Test-value evidence

| Contract / retained test | Counterfactual / pre-fix state | Without correct behavior | Correct implementation |
| --- | --- | --- | --- |
| Failure aggregation / `BuildFailurePropagates` | An isolated script copy returns zero after the real engine exits 1. | **Executed: detected.** One selected/executed case fails `Expected <1>, got <0>.`; validator exit 1. | Same final validator: one selected/executed/passed case, exit 0; real failing child remains exit 1. |
| Caller state / `CallerStateRestored` | Pre-fix environment restoration converts absence to empty; then a live `PSVariable` loses the saved SDK value. | **Executed: detected.** Native builds exit 0, but the outer case fails on variable presence, then the SDK sentinel. Each run selects/executes one case and exits 1. | One focused case and the complete seven-case suite pass after both fixes; all original assertions remain. |
| Cache data and output recovery / `ChangedInputInvalidatesCache`, `UnchangedBuildUsesCache`, `MissingOutputRestoredFromCache` | Controlled input-content change with the original timestamp, and deletion of one generated DLL. | Wrong stale/missing outcomes are rejected by exact output, hash, and counter assertions. No modified BuildXL cache implementation was run. | **Executed correctness probes:** new input gives one cache miss and `bravo`; unchanged/recovery builds give six hits, and recovery restores the exact DLL hash. |

The exit-code counterfactual uses the same final validator hash above. Its
script-copy hash is
`3B1245E1A7246EAD0AB8A33FF565FDC9A607C5C8A65C42C3E21E6A882EC444B6`.
The support patch points the external driver copy at the unchanged tooling and
configuration, and redirects binary/intermediate/cache outputs into separate
`buildxl-counterfactual` roots. Product sources and the original driver are
unchanged. The red/green report directories are
`artifacts/buildxl-validation/ad0386f7bc0b4d2e95fc63098d6637a3` and
`artifacts/buildxl-validation/eec1da192d8c48a8ac8f53c3a27ab8f2`.

Caller-state red reports are retained as
`artifacts/buildxl-experiment/caller-state-red.json` and
`caller-sdk-cache-red.json`. The pre-fix driver hashes are, respectively,
`28C1599F9037FE62B2E142EAA97ED0D5D163992FD7C1B3DC3612B0348D57A4D8` and
`21C0300D95EC7EE00CADA4794D4DFB7517E738741B87D81C365E4BA0CF265E2A`.
The validator's cleanup snapshot was hardened along with the second fix; the
tested assertions and selection did not change.

Parallel-safety scope is a standalone, sequential PowerShell smoke runner, not
a changed MSTest assembly. Fixtures/evidence use a unique run directory;
build outputs are shared only within the explicitly non-concurrent checkout
contract. Caller state is restored in `finally`, child processes are joined,
and no worktree build processes remain. There are no TestFx UI/layout changes;
the real BuildXL console transcripts are the demonstration.

**Cleanup limitation:** Windows denied deletion of two SDK-installer temporary
download archives. No permissions were changed or shared processes terminated.
Their exact paths and sizes are recorded in the local
`artifacts/buildxl-experiment/cleanup.json`; the installed SDK, tool deployment,
cache, and evidence are intentionally retained for repeat experiments.

## Open questions

- The default journal-enabled NTFS profile has not been exercised on this ReFS
  worktree; the full-hashing profile is the local proof.
- The native resolver's catch-all output declaration and SDK project-data
  side effect need reconsideration before wider adoption.
- Full-solution, other project/framework combinations, packaging, product tests,
  cancellation, cross-machine reuse, and remote/distributed caching are outside
  this initial proof. The independent CI benchmark passed for the original PR
  head; live UI checks, later revisions, downstream rollout, and
  servicing/backport decisions remain unverified. Arcade remains responsible
  for shipping and release validation.

[msbuild]: https://github.com/microsoft/BuildXL/blob/7c7fa36383a4a4ccc8e3cba45a4c7f486e2856e5/Documentation/Wiki/Frontends/MSBuild/Main.md
[schema]: https://github.com/microsoft/BuildXL/blob/7c7fa36383a4a4ccc8e3cba45a4c7f486e2856e5/Public/Sdk/Public/Prelude/Prelude.Configuration.Resolvers.dsc
[example]: https://github.com/microsoft/BuildXL/blob/7c7fa36383a4a4ccc8e3cba45a4c7f486e2856e5/Examples/MsBuildHelloWorld/HelloWorldCS/README.md
[installation]: https://github.com/microsoft/BuildXL/blob/7c7fa36383a4a4ccc8e3cba45a4c7f486e2856e5/Documentation/Wiki/Installation.md
[bootstrap]: https://github.com/microsoft/BuildXL/blob/7c7fa36383a4a4ccc8e3cba45a4c7f486e2856e5/Shared/Scripts/BuildXLLkgVersionPublic.cmd
[graph-builder]: https://github.com/microsoft/BuildXL/blob/aab9f814e31aa8a05607bbc4f051e44eb6e2afc7/Public/Src/FrontEnd/MsBuild/MsBuildWorkspaceResolver.cs
[engine-configuration]: https://github.com/microsoft/BuildXL/blob/aab9f814e31aa8a05607bbc4f051e44eb6e2afc7/Public/Src/Utilities/Configuration/IEngineConfiguration.cs
[pip-constructor]: https://github.com/microsoft/BuildXL/blob/aab9f814e31aa8a05607bbc4f051e44eb6e2afc7/Public/Src/FrontEnd/MsBuild/PipConstructor.cs
