# BuildXL build: research and decisions

## 2026-11-11: initialize the CI workspace change journal

[Azure build 1676185](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1676185)
reports DX7135 for the workspace's D-volume in both BuildXL configurations.
That run uses `d12b49e407477117dcdf6dbef1b1499988782529`, before the
read-only automatic filesystem-mode detection below. Neither revision provisions
the agent's journal, and the reported run already requests `-NoJournal`.
Disabling journal-backed optimizations is therefore not a substitute for
initializing the CI volume.

Initialize only the checkout volume in the BuildXL job, after checkout and
before restoring its action cache or invoking the driver. Derive the volume
from `BUILD_SOURCESDIRECTORY`, rather than assuming D:. An existing journal
must remain unchanged: Microsoft's [fsutil usn reference](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/fsutil-usn)
documents that `createjournal` also resizes existing journals. Query first;
create a missing journal with BuildXL's recommended 512 MiB maximum and
128 MiB allocation delta, then query again to verify activation.
Creation or verification failure must fail this prerequisite with the native
diagnostic and exit code, not silently continue with the same warning.
The [fsutil permission requirement](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/fsutil)
means the Windows CI agent must run elevated; do not launch an interactive UAC
prompt or change an agent-wide policy.

Keep the local driver read-only and preserve its explicit `-NoJournal` override
and full-hashing fallback. Do not change other volumes, disable sandbox checks,
or change the pinned BuildXL/SDK, native build, retry, cache isolation, or shipping
Arcade jobs. The journal belongs to the CI agent volume's lifetime, not a local
build invocation; do not delete it during job cleanup.

Extend `test_buildxl_pipeline.py` to check setup ordering and execute the actual
inline PowerShell against a child-process-local `fsutil.exe` fake. Cover an
existing journal, missing journal on different drive letters, creation failure,
and failed verification. The existing suite's 23 cases pass before this change.
Tests must never create or modify a real developer-volume journal. Hosted
activation, agent permissions, and disappearance of DX7135 require a run of the
changed pipeline; local mocked execution alone cannot establish those outcomes.

### Journal setup validation and test-value evidence

Tested source: `50b513022be7c356cb342866a3b755a4b18bf97d` plus the local
pipeline and regression-test changes. The executable-only `git diff --binary`
patch SHA-256 is
`ADA79F2F1A845DEFE6C7E2E563F7EFA15B3BB770C5EC0ECFC7F25CF05AF5A601`;
documentation is excluded to avoid a self-referential identity.
Runners: PowerShell 7.6.9 (`pwsh`) on Windows, Python 3.12.10, PyYAML 6.0.2.

| Contract | Retained regression |
| --- | --- |
| Setup runs after checkout, before cache restore and native build | `test_change_journal_is_initialized_before_cache_and_build` |
| Existing journals are queried without resizing | `test_existing_change_journal_is_preserved` |
| Missing journals are created and verified on D: and an alternate drive | `test_missing_change_journal_is_created_and_verified` |
| Creation errors block setup and retain the native diagnostic/code | `test_change_journal_creation_failure_blocks_build` |
| A successful create command alone is insufficient | `test_change_journal_verification_failure_blocks_build` |

For the isolated comparison, copy the base `azure-pipelines.yml` and
`eng\pipelines\jobs\buildxl.yml`, retaining the changed
`.github\scripts\test_buildxl_pipeline.py` at the same relative location.
`$comparison` below denotes that task-owned directory outside the checkout.
Replace only its job YAML with the fixed version for the second run;
the regression-test bytes and selection remain identical.

| Executed command / source state | Result |
| --- | --- |
| `python "$comparison\.github\scripts\test_buildxl_pipeline.py" -k change_journal` / base job YAML | Exit 1; five methods execute with six intended assertion failures, including both missing-journal subcases. No errors or skips; the absent setup issues no journal commands and incorrectly succeeds in failure scenarios. |
| Same command and retained tests / fixed job YAML | Exit 0; all five methods pass. |
| `python .github\scripts\test_buildxl_pipeline.py -k change_journal` / fixed worktree | Exit 0; all five methods pass. |
| `python .github\scripts\test_buildxl_pipeline.py` / fixed worktree | Exit 0; all 28 methods pass, with no errors, failures, or skips. This retains all 23 existing pipeline, collector, and read-only local-driver cases. |

This is **executed: detected** evidence for the CI setup contract, not proof of
real journal activation or a hosted performance improvement. Tests run serially
through `unittest`; each PowerShell child owns its environment and command fake.
No real `fsutil` command or volume mutation occurs. No shipping binaries,
packages, public APIs, or test-assembly scheduling change, so no product rebuild
or package validation is needed. The hosted-agent permission requirement and
absence of DX7135 remain unverified; no pipeline was queued or GitHub content
published as part of this local fix.

## 2026-11-11: correct the performance setup

The setup audit of [Azure build 1674906](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1674906)
confirmed a successful Release build with 226 cache misses, no hits, and no
uncacheable processes. The job had no persisted action cache, disabled managed
shared compilation, and scheduled eight processes on four logical CPUs. Its
32-minute engine execution averaged 100% CPU; graph construction took about
10 seconds. The matching MTP `net10.0` main compiler task took 160.5 seconds,
versus 23.4 seconds in normal CI. These observations identify setup differences,
not the isolated contribution of each difference.

The requested correction preserves native project scheduling, analyzers,
sandbox/file-access enforcement, project frameworks, and blocking failures:

- Enable the resolver's supported managed shared compiler and its file-access
  logger, rather than manually breaking an arbitrary compiler out of the sandbox.
  Keep the MSBuild reference backend's compiler-reuse setting aligned.
- Default concurrency to the process's available logical CPU count, capped at
  the existing 64-worker limit; retain explicit `-Jobs` overrides.
- Restore and save the local action cache with Azure's branch-scoped `Cache@2`.
  Use toolchain/configuration-qualified, immutable run keys with a compatible
  restore prefix. Keep checkout cleaning, and do not cache binary/intermediate
  output directories, running compiler state, credentials, or the whole checkout.
  The engine's default cache root includes content, fingerprints, and serialized
  graph-cache metadata; BuildXL validates restored entries before reuse.
  Use a stable checkout-local
  NuGet path instead of embedding build/attempt IDs in tracked process inputs.
- Detect journal availability read-only. Use journal/content-table/hardlink
  optimizations when available, otherwise explicitly report the full-hashing
  fallback. Preserve `-NoJournal` as an explicit override. Never enable a volume
  journal or disable sandbox checks as part of this script.
- Retain .NET SDK MSBuild for the native resolver and the `-Engine MSBuild`
  comparison. Normal shipping CI must keep desktop MSBuild for classic UAP;
  changing that job merely to make its timing comparable would drop coverage.
  Record the effective compiler, concurrency, and filesystem policy, and use the
  matched reference command for engine-only comparisons.

The producer remains the checksum-pinned public BuildXL deployment and the
SDK pinned by `global.json`; the consumers are the existing local driver and
Windows Debug/Release jobs. A missing cache is a normal cold build, not a reason
to skip restore/build or fall back to another engine. Azure owns cache
authorization and branch isolation. Each action still validates its actual
inputs before reusing outputs; a pipeline cache hit is not itself an action hit.
Cancellation/failure ownership and the bounded native-build retry are unchanged.
No shipping package/API or release-branch behavior is changed by this experiment.

Validation extends the existing script suites: parsed pipeline cache
contracts, automatic/default and explicit settings, real shared-compiler
input invalidation (including preserved timestamps), unchanged action hits,
missing-output recovery, and failure propagation. Real native builds and the
SDK-matched reference must still produce equivalent assemblies. Hosted cache
restore/save and a hosted performance improvement remain unproven until the
changed pipeline runs; no new speedup is claimed from configuration alone.

The corrected defaults have completed a real Release `TestFx.slnx` build:

```powershell
pwsh -NoLogo -NoProfile -File .\eng\buildxl.ps1 `
    -Project TestFx.slnx -Configuration Release -OutputId r
```

Exit 0; all 67 solution project paths reconcile to 226 process-instance binlogs,
with 226 cache misses, no hits, and no failed processes. The effective default
was 32 workers on this 32-CPU Windows host. Read-only probing reported the
unavailable Q-volume journal and selected full hashing without an explicit
`-NoJournal` switch. The native compiler binlog records
`UseSharedCompilation=True` and `CompilerServer: server - server processed
compilation`, including the resolver's compiler-access logger.
The same selector on the available C-volume journal selects `Journal`, while
the explicit override selects `FullHashing`. No volume settings were changed.
This local coverage result is not a comparison with the four-CPU hosted run.

Repeating that full-solution command exited 0 with **226 hits, zero misses**.
The engine's execution phase was 22.8 seconds; this is one unchanged local
cache check, not a three-sample timing baseline or a hosted-CI speedup.
A separate fresh-output recovery probe saved only the default cache directory,
removed its task-owned binary/intermediate/object directories, restored the
snapshot, and rebuilt the TRX graph. All six actions were cache hits, all 67
output files returned, and the three expected assembly identities matched.
This tests actual persisted-cache recovery rather than leaving old DLLs on disk.
The SDK-matched `-Engine MSBuild` TRX build also exited 0 with the same 67-file
inventory and three assembly identities. Metadata confirms identical SDK,
MSBuild/compiler paths, shared compilation, CPU limit, framework/configuration,
and resolved filesystem mode in the two backends. Embedded source/output paths
can differ, so this is not a byte-identical-binary claim.

### Correction validation and test-value evidence

Source state is base `cb020393f8c050fd794f575ca69be4ddf673d2ea` plus the
uncommitted setup correction. The actual producer versions remain SDK
`12.0.100-alpha.1.26556.115` and BuildXL `0.1.0-20261030.6`.
The retained executable-only patch SHA-256 is
`08617CAEE31D6053CEABEB0B0323B29CAB8132E7F2EA47D40AB568EDACBCB3CB`;
it includes the driver, resolver, benchmark, validator, pipeline job, and Python
regressions, but excludes documentation so this identity is not self-referential.

| Requirement | Evidence |
| --- | --- |
| Reusable, qualified, branch-scoped cache snapshots | `test_action_cache_uses_qualified_immutable_key_and_compatible_restore_prefix`, `test_action_cache_restores_only_actions_between_checkout_and_native_build`, and `test_action_cache_hit_never_skips_native_build`; real saved-cache recovery restores six actions and 67 files without old binaries/intermediates. |
| Stable package paths and native auto-settings | `test_checkout_and_nuget_cache_are_job_isolated` and `test_native_build_targets_full_solution_and_preserves_exit_code`. |
| CPU-sized defaults, explicit worker limits, and SDK-matched backends | `SettingsAndProvenance` checks default and explicit 1/64 workers, existing pinned compiler files, equal backend profiles, and prepare-only behavior. The real SDK reference also produces equivalent outputs. |
| Shared compilation still tracks source content | `SharedCompilerTracksChangedInputs` compiles assembly version `1.0.0.0`, changes the source to `2.0.0.0` with the original timestamp, requires a cache miss and the new version, then requires an unchanged cache hit. SDK analyzers and shared compilation remain enabled. |
| Read-only journal detection and explicit fallback | `test_journal_mode_checks_every_relevant_volume_read_only`, `test_missing_journal_falls_back_with_diagnostics`, and `test_no_journal_explicitly_forces_full_hashing`. These execute the actual helper with an isolated fake journal command. Available/unavailable real local volumes were also checked read-only. |
| Preserve framework selection, errors, caller state, and output recovery | All nine pre-existing native cases remain, including `SolutionBuildPreservesProjectFrameworks`, `BuildFailurePropagates`, `CallerStateRestored`, `UnchangedBuildUsesCache`, and `MissingOutputRestoredFromCache`. |

Executed commands:

| Command | Result |
| --- | --- |
| `python .github\scripts\test_buildxl_pipeline.py` | Exit 0; 23 executed/passed, zero failures/errors/skips. All 17 original case identities remain. |
| `pwsh -NoLogo -NoProfile -File .\eng\buildxl\validate.ps1` | Exit 0; all 11 named cases executed/passed. The parsed `results.json` reconciles 19 child invocations; the two deliberately failing children retain exit 1 and every other child exits 0. |
| `pwsh -NoLogo -NoProfile -File .\eng\buildxl\benchmark.ps1 -SelfTest` | Exit 0; all six existing statistics/output/export self-checks pass. |
| Full-solution Release command above, repeated unchanged | Both exit 0; cold 226 misses, unchanged 226 hits/zero misses. |
| `pwsh -NoLogo -NoProfile -File .\eng\buildxl.ps1 -Project src\Platform\Microsoft.Testing.Extensions.TrxReport\Microsoft.Testing.Extensions.TrxReport.csproj -TargetFramework net10.0 -Engine MSBuild -OutputId m` | Exit 0; settings, 67 output paths, and three primary assembly identities match the native backend. |

Native regression results are retained in
`artifacts/buildxl-validation/16c0d77f5fb2468686bc17bdd79d7676/results.json`.
The generated C# fixture's UTF-8 BOM was independently checked.
The tests remain serial: child journal mocks cannot alter real volumes,
collector trees are per-test, and the compiler fixture precedes the shared TRX
closure/cache checks. No test scheduling or shipping package layout changed.

The same retained assertions also ran against isolated counterfactuals, without
changing the real worktree or its generated outputs:

| Contract / retained test | Without the correction | Correct controls |
| --- | --- | --- |
| Old pipeline / cache-key, package-path, native-command tests | Actual base job YAML: all three methods execute and fail their intended assertions; zero errors/skips. | Five selected methods pass before and after the pipeline/journal counterfactuals. |
| Missing-journal fallback / `test_missing_journal_falls_back_with_diagnostics` | A buildable helper mutation ignoring failed queries produces three expected assertion failures across its three subcases; zero errors/skips. | Same method passes before/after. |
| Explicit override / `test_no_journal_explicitly_forces_full_hashing` | Ignoring `-NoJournal` produces one expected assertion failure, not a launch/setup error. | Same method passes before/after. |
| CPU default / `SettingsAndProvenance` | Restore only the fixed eight-worker default in a comparison clone outside the repository: one case executes and fails `Expected <32>, got <8>.`; its child preparation exits 0. | The same case passes before and after both settings counterfactuals. |
| Shared compiler / `SettingsAndProvenance` | Restore disabled compilation policy and matching metadata in that clone: one case executes and fails `Expected <True>, got <False>.`; child preparation exits 0. | The same case passes before/after. The real shared-compiler invalidation case separately passes. |

These are **executed: detected** assertions. The settings/helper mutations are
targeted counterfactuals, not claims that the new API shape existed at the base
revision. The comparison clone shared only the existing pinned SDK/tool
deployment, performed no native compilation, and was removed along with its
borrowed tool junction. Pipeline-cache service transfer, hosted performance,
and native journal-optimized execution on this journal-less checkout remain
unverified. Do not equate the local 226-hit result with a hosted cache hit rate.

## 2026-11-10: build the repository instead of benchmarking in CI

The requested outcome is a real BuildXL repository build, with performance
analysis deferred until useful build data exists. The three-project,
18-sample comparison does not provide that coverage.

The entry point is the existing Windows solution, `TestFx.slnx`,
rather than a second manually maintained project list. Preserve each project's
declared target frameworks, project references, analyzers, generators, and build
checks. Include product, test, utility, and sample projects selected by that
solution; do not execute tests, pack, sign, or replace the shipping Arcade jobs.
The solution remains the maintained project inventory; do not add a separate
manually curated project allowlist.

The native resolver uses .NET MSBuild. The existing
`SkipUapTargetFramework=true` escape hatch excludes classic `uap10.0.16299`,
which requires desktop MSBuild through MSBuild.Sdk.Extras. This is a declared
toolchain limitation, not permission to force every project to `net10.0` or
silently drop other failing projects. Modern Windows targets remain in scope.
Native Debug and Release verification below covers all 67 solution projects and
226 project instances per configuration.

The producer is the checksum-pinned BuildXL deployment plus Arcade's pinned
SDK; the consumer is a separate Windows Azure Pipelines job. Restore must
complete with the same global properties before the native resolver schedules
`Build`. Restore, graph, sandbox, and compilation failures fail the job without
falling back to MSBuild. Cancellation remains owned by the pipeline task and
its child process tree. Outputs/cache stay checkout-local and isolated from
shipping outputs. Do not introduce remote caches or credentials.

The job runs one repository build per Debug/Release configuration, streams
its real output, and retains
source/toolchain provenance, engine counters/execution logs, and per-project
binlogs on success or failure. Remove benchmark repetitions, synthetic source
markers, and benchmark-as-test publication from CI. Keep the explicit local
comparison command available as a separate experiment.

| Requirement / risk | Automated evidence | Rendered check / gap |
| --- | --- | --- |
| Build the solution rather than the TRX graph | `DefaultBuildSelectsRepositorySolution`; real native Debug/Release `TestFx.slnx` builds, each with 226 executed processes and all 67 project paths reconciled to the solution. | Native console output inspected; a hosted run of the changed YAML has not been queued. |
| Preserve project-specific framework declarations | A two-project solution fixture with distinct framework declarations and independently expected outputs. | Nonvisual build contract. |
| Keep native execution and failures authoritative | Existing native closure, failure propagation, invalidation, cache reuse, and output recovery cases. | Retain real child-process diagnostics, not only a timing summary. |
| Keep CI build-only and retain useful evidence | Sixteen parsed-YAML/real-collector cases; both full native builds' project logs retained and verified byte-for-byte. | Azure job/artifact UI requires a run of the changed YAML. |

Research: `eng/Build.props` selects `TestFx.slnx` for normal Windows builds;
`Directory.Build.props` documents the classic-UAP/.NET-MSBuild incompatibility.
BuildXL's `MsBuildWorkspaceResolver.TryRetrieveParsingEntryPoint` accepts
explicit relative entry points without restricting them to its automatic
`.proj`/`.sln` discovery extensions. Solution-format compatibility must still
be exercised with the pinned deployment.

Alternatives: retaining the benchmark does not meet the requested coverage;
forcing one TFM would discard supported framework/platform branches; wrapping
`build.cmd` in a single cached process would not use native project scheduling.
Start with the existing solution and only narrow scope for demonstrated,
documented incompatibilities.

## Original experiment: problem and contract

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

Run from the checkout root with **PowerShell 7** on Windows x64. The default
builds `TestFx.slnx` using each project's declared frameworks:

```powershell
pwsh -NoProfile -File .\eng\buildxl.ps1
```

The driver checks the source, SDK, package, and output volumes read-only. When
their journals are available, it uses journal-backed input tracking, the file
content table, and hardlinks. On ReFS or another volume without an accessible
journal, it reports the reason and automatically uses full input hashing.
Force that compatibility profile explicitly with:

```powershell
pwsh -NoProfile -File .\eng\buildxl.ps1 -NoJournal
pwsh -NoProfile -File .\eng\buildxl\validate.ps1 -NoJournal
```

The first invocation downloads the checksum-pinned public BuildXL deployment
and uses Arcade's existing helper to acquire the exact repository SDK. It does
not install system tools or the full solution's optional workloads.

`-Project` accepts a checkout-local `.csproj`, `.fsproj`, `.sln`, `.slnx`, or
`.slnf`; `-Configuration Debug|Release` selects the build configuration.
Without `-TargetFramework`, no global framework override is applied.
`-TargetFramework net10.0|net11.0|netstandard2.0` remains available for an
explicitly constrained project experiment:

```powershell
pwsh -NoProfile -File .\eng\buildxl.ps1 `
    -Project src\Platform\Microsoft.Testing.Extensions.TrxReport\Microsoft.Testing.Extensions.TrxReport.csproj `
    -TargetFramework net10.0 -NoJournal
```

Both restore and compilation set `SkipUapTargetFramework=true`: classic UAP
requires desktop MSBuild, while the resolver uses .NET MSBuild. This does not
exclude the modern Windows TFMs. The build remains experimental; the executed
coverage and any additional limitations are recorded below rather than inferred
from the solution's existence.
`-SkipRestore` is available only after a successful matching restore.

Binaries are in `artifacts\buildxl\bin`, intermediates in
`artifacts\obj\buildxl`, and the local action cache and engine logs are under
`artifacts\buildxl`. The pinned deployment is in `artifacts\buildxl-tools`.
Concurrency defaults to the available logical CPU count (at most 64); `-Jobs`
overrides that limit for controlled experiments. Per-project MSBuild binlogs are under
`artifacts\buildxl\Out\Logs\MSBuild`; restore writes
`artifacts\buildxl\logs\restore.binlog`. `toolchain.json` records the source
revision, SDK/BuildXL versions, configuration, framework policy, concurrency,
compiler-reuse policy, SDK MSBuild/compiler paths, and the resolved filesystem mode.
The validation script retains separate console logs, counters, and a parsed
per-case `results.json` in a unique `artifacts\buildxl-validation` directory.
It intentionally exercises a failing build and deletes/retrieves one generated
reporter DLL; it does not edit product sources. Its cache/closure checks still
select the small TRX graph explicitly, while its solution fixture checks distinct
project-framework declarations and its default-selection case checks
`TestFx.slnx`.

Do not run this backend, its validator, or another builder concurrently in
the **same checkout**. Different invocations share this checkout's BuildXL
outputs and cache. The source mount is not declared scrubbable, but BuildXL
does clean its own recorded generated outputs. The local driver does not configure
a remote cache backend. CI restores/saves this local cache through Azure's
branch-scoped pipeline cache; it never reuses shipping outputs.
The engine server remains disabled. Managed compiler reuse is enabled through
the native resolver's supported compiler-access logger, retaining input tracking
and sandbox enforcement rather than manually exempting compiler accesses.

Keep the checkout and any `-OutputId` short when building Windows app-model
projects. The Windows SDK's native PRI tool can exceed its path limit after
adding temporary suffixes, even when the `.pri` output path itself fits.
The CI job uses the short default layout; local full-solution verification uses
one-character `d`/`r` output IDs rather than benchmark-style descriptive IDs.

`-NoJournal` still emits BuildXL's missing-journal warning. It disables
metadata/journal optimizations, **not** sandbox enforcement or content hashing.
Do not enable a shared volume's journal or suppress file-access violations just
to make this experiment pass.

## Comparing build performance

The comparison is now a **local-only experiment**, not the CI workload:

```powershell
pwsh -NoProfile -File .\eng\buildxl\benchmark.ps1 -SelfTest
pwsh -NoProfile -File .\eng\buildxl\benchmark.ps1 -Samples 3
```

By default, results go into a new `artifacts\buildxl-perf\<run-id>` directory.
`-ResultsDirectory <new-absolute-directory>` selects a different evidence
directory; it must not exist. Binary/intermediate/action-cache directories use
unique `-OutputId` values under `artifacts\buildxl-runs` and
`artifacts\obj\buildxl-runs`, so the experiment neither cleans the regular build
nor borrows an old action cache. Generated benchmark input and import files
live in `artifacts\buildxl-perf-fixtures`, not the product source tree.

The benchmark explicitly selects the TRX project and `net10.0`; it does not
inherit the driver's full-solution default. The shared driver also supports
`-Engine MSBuild` for the reference build,
`-Jobs` for equal concurrency limits, `-AdditionalTargets` for a checkout-local
benchmark import, and `-PrepareOnly` for untimed tool/package acquisition.
Its default engine remains BuildXL. Do not compare the reference backend
against the shipping Arcade pipeline's complete pack/test job: this comparison
is deliberately limited to equivalent builds of the TRX project graph.
Both backends use the pinned SDK's MSBuild/Roslyn and shared compilation, unlike
normal Windows shipping CI's desktop MSBuild. For a full-solution SDK-matched
reference build, invoke the driver with `-Engine MSBuild -Project TestFx.slnx`;
keep configuration, `-Jobs`, source/output starting state, and restore policy
matched. That command alone is not a repeated performance baseline.
New benchmark results use the `SharedCompilation.v2` trend series and record
the resolved filesystem profile. The historical measurements below used
compiler reuse disabled and are not measurements of the corrected defaults.

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

### Repository pipeline build and retained data

`azure-pipelines.yml` imports `eng/pipelines/jobs/buildxl.yml` as the independent
**BuildXL** job with **Debug** and **Release** matrix entries.
`EnableBuildXL` defaults to **true**; queue-time `false` omits both entries.
This replaces the previous `EnableBuildXLPerformance` queue-time parameter.
The existing trusted path classification and unknown-classification fallback
are preserved. The gate now accepts **product or sample changes**, because
samples are part of the solution; infrastructure-only and XML-doc-only changes
retain their exclusions. `SkipTests` still controls product tests, not these
build-only jobs.

Each entry invokes `eng/buildxl.ps1 -Project TestFx.slnx` with its configuration,
CPU-sized concurrency, tracked shared compilation, and automatic filesystem
profile selection. There is one normal restore/build, no warm-up,
synthetic source marker, benchmark repetition, or comparison threshold.
A failed task can retry the same native build once to recover from the observed
Windows child-process teardown race; a second failure still fails the job.
The native engine's stdout/stderr reaches the Azure task directly instead of
being redirected to a benchmark sample log. Tests are compiled as part of the
solution; they are not executed or represented as synthetic benchmark tests.
The ordinary Arcade build/pack/test jobs remain the shipping authority.

The job retains `toolchain.json`, the consumed `config.dsc`, engine `logs`
(including counters and execution logs), and `Out/Logs/MSBuild` project binlogs
in **buildxl-Debug-Attempt&lt;attempt&gt;** or
**buildxl-Release-Attempt&lt;attempt&gt;**. Collection runs after success or
failure, tolerates absent/partial output trees, and never includes the local
action cache or binary tree in the log artifact. Publication requires actual
evidence to exist.
These raw build artifacts support later performance analysis without claiming
matched workloads or a speedup now.

The standalone job preserves Arcade's public-project
`Codeql.SkipTaskAutoInjection` and `skipComponentGovernanceDetection` policy.
The earlier injected tracer wrote `build-tracer.log` and `tags.db` outside
BuildXL's declared outputs; sandbox enforcement remains enabled.
CodeQL scanning remains in the official and dedicated CodeQL pipelines.
No BuildXL remote backend or custom cache credentials are configured.
`Cache@2` separately persists `artifacts/buildxl/cache` with Azure's native
branch/pipeline isolation. Keys bind the OS, configuration, SDK/toolchain files,
and driver/resolver imports; a build/attempt suffix creates a new immutable
snapshot while a compatible prefix restores the latest prior one.
`NUGET_PACKAGES` is the stable checkout-local `.packages` path, so build IDs
do not salt tracked package paths. A clean checkout prevents leftover local
state from bypassing the pipeline cache's authorization scope. Cache restoration
never skips restore or the native engine, and actual engine counters remain the
authority for action reuse. Version metadata and other legitimate changed inputs
can still cause misses even after a pipeline cache restore.

### Previous benchmark pipeline and trends (historical)

Before the repository-build change, `azure-pipelines.yml` imported
`eng/pipelines/jobs/buildxl-performance.yml` as the independent
**BuildXLPerformance** job. `EnableBuildXLPerformance` defaulted to **true**;
queue-time `false` omitted it. The job followed product-change classification and
ran on main CI, nightly, manual builds, and product-changing PRs; samples-only,
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

After success or failure, that job uploaded the Markdown summary and retained
`results.json`, `timings.csv`, console logs, counters, tool/source/host
provenance, and binlogs in the `buildxl-performance-Attempt<attempt>` artifact.
Per-project logs replayed by a BuildXL cache hit are historical cached action
logs, not evidence that a compiler ran in that sample; the fresh process
counters distinguish those cases.

Its `PublishTestResults@2` step sent the actual measured durations to Azure Test
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

### 2026-11-11: hosted child-process cleanup failure

[Azure build 1674906](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1674906)
tested merge `e31e4478f393a724d4f93d7ad98991f575b1e4b1` for PR head
`5c5201ff95c01ab7b0ed9f756f02ebc613bbdad9`. The full BuildXL Release job
passed. Debug's WinUI framework compilation exited 0 and produced the assembly,
but BuildXL rejected a surviving `C:\Windows\System32\getmac.exe` child.
The Debug graph therefore finished with 165 successful, one failed, and
60 skipped processes, rather than a successful full build.

The pinned native resolver sets a fixed 500 ms nested-process termination
timeout in `PipConstructor.TryConfigureProcessBuilder`. Its implementation does
not consume the similarly named generic resolver timeout setting, so adding
that setting here would not fix this deployment. Do not allowlist `getmac`,
break it away from the sandbox, suppress the error, or disable build tooling.
The chosen bounded recovery is one Azure task retry of the same native build.
Successful cached projects can be recovered; the failed project must complete
normally. Both attempts retain failure semantics, and a second failure still
fails the job. This is a retry for an observed teardown race, not a benchmark
sample or a claim that the upstream process-lifetime issue is fixed.

The ordinary Windows/Linux jobs failed in `TestFxOpenTelemetryTests`, which
was added on the base side of the merge and is unchanged by this PR. Its child
host receives unsupported `--no-ansi` arguments. Those independent product-test
failures must not be hidden or patched into this build-infrastructure change.
The GitHub analysis upload and review activation also hit installation API
quota limits. A manual retry of the dynamic code-quality run was rejected by
GitHub; a pushed correction will start fresh checks instead.

Local regression coverage checks the bounded retry and preserved final exit
code in the parsed pipeline, alongside the existing native-command and
failure-publication checks. Actual automatic retry behavior remains a hosted
CI validation gap until the changed task runs.

**Test-value evidence:** against base
`5c5201ff95c01ab7b0ed9f756f02ebc613bbdad9`, the retained command
`python .github\scripts\test_buildxl_pipeline.py
BuildXLPipelineTests.test_native_build_retry_is_bounded_and_preserves_failure`
executed one case and exited 1 at `1 != 0` without the retry setting.
After adding the setting, `python .github\scripts\test_buildxl_pipeline.py`
exited 0 with all 17 cases executed/passed and no failures/errors/skips.
This proves the pipeline contract, not execution of Azure's task-retry engine.
The YAML SHA-256 is
`C719704B9CF8D6558C1DE98FB3A4453704B7FFD57AE2527E2F11C4012BEA08D4`;
the retained test SHA-256 is
`CCA770D92B47F67D45ACE75BBF3E4C26D3D57A698578EE7DCFB3DF78FB4F110F`.

### 2026-11-10: native full-solution integration

Base source: `129aff748a337473ebd9a2b9417a6779cd6619d3`, with the
repository-build changes recorded in this section. The original, unchanged
TRX driver completed a fresh native baseline with:

```powershell
& .\eng\buildxl.ps1 -OutputId repo-baseline-d9ab2acb -NoJournal
```

Exit 0; six process instances executed with no cache hits. The pinned SDK was
initially absent (`dotnet --version` reported the repository's SDK-not-found
message); the existing driver bootstrapped it through Arcade and verified the
pinned BuildXL package.

The first expanded invocation was:

```powershell
& .\eng\buildxl.ps1 -Configuration Debug -OutputId repository-debug-d9ab2acb -NoJournal
```

Restore and solution graph construction succeeded, producing 226 project
instances. Execution failed with `MSB4252` when references requested
`GetTargetFrameworks` using a different configuration/platform identity.
The failed project binlogs and the [native pip constructor][pip-constructor]
show why: BuildXL deliberately omits `CurrentSolutionConfigurationContents`
from project command lines. The SDK's `AssignProjectConfiguration` otherwise
unsets configuration/platform for unmatched solution references.
Both restore and execution now set
`ShouldUnsetParentConfigurationAndPlatform=false`, preserving the explicitly
selected configuration. Empty optional import properties are also omitted
rather than serialized as an empty quoted MSBuild argument.

The next expanded invocation, with output ID
`repository-debug-2-d9ab2acb`, reached compilation across the graph and exposed
a second real integration issue in the WinUI TFM. The Microsoft.Windows.SDK
MRT targets call `GetPackagingOutputs` on project references during `Build`,
but do not declare that edge to MSBuild's static target protocol. The new
`eng/buildxl/msbuild.targets` import adds that target dependency only to the
WinUI inner build. `GetPackagingOutputs` already exists, including an empty
implementation for ordinary SDK projects. This keeps the Windows target and
its resource work instead of disabling it or allowing untracked nested builds.
The same import is supplied to restore, reference MSBuild, and BuildXL.

Initial focused checks also caught two setup errors: DScript configuration
must remain a single `config(...)` expression, and bare solution fixtures need
`DefaultTargets="Build"` rather than accidentally selecting their first
`Restore` target. Neither failure is counted as behavioral sensitivity.
After those corrections, `DefaultBuildSelectsRepositorySolution`,
`SolutionBuildPreservesProjectFrameworks`, `InvalidProjectRejected`, and
`BuildFailurePropagates` all passed (four selected/executed, exit 0).
The solution fixture now also asserts the explicit Release configuration and
the reference-configuration policy.

The first protocol-import attempt used `CustomBeforeMicrosoftCommonTargets`.
A focused native framework build exposed why that hook is unsuitable here:
Arcade chained it a second time (`MSB4011`), and the WinUI package explicitly
rejected the overridden MSIX initialization hook. The import now uses the SDK's
independent `CustomBeforeDirectoryBuildTargets` extension point. This preserves
Arcade/MSIX initialization instead of suppressing either diagnostic.

With the bridge in place, the focused `TestFramework.Extensions.csproj` graph
completed all 17 native processes successfully, including modern UWP and WinUI.
The next full-solution attempt completed 223 of 226 processes, exposing two
additional integration/environment constraints rather than a reason to drop
projects:

- The F# SDK selects its `dotnet`-hosted compiler only when `DOTNET_HOST_PATH`
  is available. The resolver's explicit environment omitted it, so F# looked
  for the nonexistent SDK `FSharp\fsc.exe` (`MSB6004`). The tracked environment
  now supplies the exact pinned SDK's `dotnet.exe` to both graph construction
  and project execution.
- The long local output ID made the WinUI unit project's PRI output path
  256 characters before the native tool's `.intermediate` suffix. Its binlog
  records `PRI175`/`PRI222`, with no C# compilation failure. Subsequent
  verification uses short isolated output IDs, retaining the WinUI framework
  and its resource generation rather than disabling either.

The final Debug command succeeded:

```powershell
pwsh -NoLogo -NoProfile -File .\eng\buildxl.ps1 -Project TestFx.slnx `
    -Configuration Debug -Jobs 8 -OutputId d -NoJournal
```

Exit 0; **67 distinct projects, 226 process instances, 226 cache misses, zero
cache hits, zero failed/skipped processes**. This includes F#, modern Windows
resource generation, product libraries, analyzers, test projects, and samples.
The actual YAML collector then retained **1,159 files**, including **226
per-project-instance binlogs**, `BuildXL.xlg`, `DirectedGraph`, `PipGraph`,
counters, and source/toolchain metadata. This is native execution and real
consumer-side log collection, not an empty graph or synthetic benchmark report.

The matching Release command also succeeded:

```powershell
pwsh -NoLogo -NoProfile -File .\eng\buildxl.ps1 -Project TestFx.slnx `
    -Configuration Release -Jobs 8 -OutputId r -NoJournal
```

Exit 0; **226 executed processes, 226 misses, zero hits, failures, or skips**.
Its actual YAML collector also retained 226 per-project-instance binlogs and the
engine graph/counters. Both cold builds used SDK
`12.0.100-alpha.1.26556.115`, BuildXL `0.1.0-20261030.6`, eight workers, and
full hashing. These are build-coverage results, not a performance comparison.

The final complete command `pwsh -NoLogo -NoProfile -File
.\eng\buildxl\validate.ps1 -NoJournal` exited 0 with **nine
selected/executed/passed, zero failed/skipped**. Its parsed JSON confirms all
named cases and records the explicit TRX project/framework arguments for
`CallerStateRestored`. An intermediate harness-only attempt incorrectly used a
string array to splat named arguments into an in-process PowerShell script;
explicit named invocation corrected the binding error. That failed setup is
not counted as sensitivity evidence. Final validator SHA-256:
`6797D2B0E23E1C08BA6333B350464D3B99AB817BC6F64B0AC25D3A65BFB866FC`.

Collect each configuration's evidence before starting the next graph in the
same checkout. The native resolver's catch-all shared output can scrub recorded
generated files from a previous graph even when it used another `OutputId`.
CI matrix entries have separate checkouts; local comparison/verification
retains logs before switching configurations.
After the smoke suite, untimed native cache-recovery runs materialized each
configuration again (226 cache hits, zero misses, exit 0). All **1,130
project-log files per configuration** matched the originally collected files
byte-for-byte, including all 226 binlogs. These cached logs are historical
action logs, not additional compiler executions. The final Release binaries
and WinUI unit-test PRI remain materialized; isolated counterfactual checkouts
and their tool links were removed without changing shared tools.

The reproducible executable-input snapshot includes the changed/new scripts,
target bridge, pipeline YAML, tests, base revision, and removed-path manifest.
It is retained locally as `buildxl-executable-inputs-final.zip`, SHA-256
`242604D32545C044989A7BBAE3C293A25D173580ED1F8257E45B8A0BED8D4496`;
`buildxl-source-identity.json` SHA-256 is
`F02F67A0A7F703D597A14B994031F9E777C1D759B715C374591A6C61D952EF8C`.
Publication and hosted execution were not requested and have not been performed.

#### Repository-build test-value evidence

The retained `SolutionBuildPreservesProjectFrameworks` case ran in an isolated
comparison checkout outside the repository tree, sharing only the pinned SDK
and verified tool deployment read-only. It executed a real two-project native
solution build with distinct framework declarations and Release configuration.
The same validator and assertions were retained in all three runs:

```powershell
pwsh -NoLogo -NoProfile -File "$comparison\eng\buildxl\validate.ps1" `
    -NoJournal -Case SolutionBuildPreservesProjectFrameworks
```

| State | Driver SHA-256 | Executed result |
| --- | --- | --- |
| Correct framework-default behavior | `8D15E08592842A41F36F6FC1A24C237501E0835CCA095F0E3976BE8714E64AB5` | One selected/executed/passed, exit 0. |
| Counterfactual: restore a global `net10.0` default | `7669B8951D3DB457C7F33883D087983817FE19DC00331CE2F0B048C70C4263AC` | One selected/executed/failed, exit 1, at the exact output assertion. |
| Correct behavior restored | `8D15E08592842A41F36F6FC1A24C237501E0835CCA095F0E3976BE8714E64AB5` | The same case passed again, exit 0. |

Validator SHA-256 in every state:
`6EECE745377E28B9AD42A44E82935BEF5BAD9CF7010C540D8F4D850A25085B72`.
Configuration SHA-256:
`2DE6E407C2F5FD90D8648C3F7CF4872D046280F864E43206A7D6F8B8CB00C04E`.
The complete green/red/green comparison was repeated after the F# host
environment change, with the same results and unchanged driver/validator hashes.
The later caller-state argument-recording correction changes the complete
validator hash, but an AST/text comparison confirms this framework case and
all reached helpers are byte-identical to the paired runs. The final complete
smoke suite also exercises that corrected command reporting.
All child builds succeeded and scheduled two native processes, including the
counterfactual. The failing assertion expected both `net462` and `net10.0`
from the first project's declared frameworks, but observed only `net10.0`;
this is **executed: detected**, not a compilation/setup failure or zero-work
success. Green/red/green results, counters, console logs, and the exact mutated
driver are retained in the session's `buildxl-framework-evidence` artifacts.

The YAML contract suite,
`python .github\scripts\test_buildxl_pipeline.py`, passed all 14 cases (exit 0).
Its collector cases execute the actual YAML PowerShell against complete,
partial/failure, absent, and cache-only trees, asserting exact retained paths,
bytes, and evidence markers. An isolated YAML snapshot changing only the native
command's project from `TestFx.slnx` back to the TRX project failed
`test_native_build_targets_full_solution_and_preserves_exit_code` at the
expected command assertion: 13 passed, one failed, exit 1. The unchanged real
YAML passed the same 14 tests again, exit 0. Source YAML and test hashes were
unchanged across that comparison. No hosted pipeline run or artifact
publication is claimed by these local checks.

The collector iteration had **15** passing cases, including
`test_collector_repeated_collection_does_not_nest_log_directories`.
It executes collection twice into the same staging tree. An isolated
counterfactual restoring `Copy-Item -Destination $destination` passed the
first collection but failed the second at the exact file-map assertion:
`logs/logs/...` and `Out/Logs/MSBuild/MSBuild/...` were incorrectly nested.
Both PowerShell processes exited 0 and reported evidence; this was an output
layout failure, not setup failure. The corrected destination-parent copy passed
all 15 retained cases again, exit 0. CI staging is also configuration- and
attempt-specific to avoid borrowing a previous retry's evidence.

The final suite has **16** passing cases (exit 0), adding
`test_buildxl_job_gate_includes_product_or_sample_changes_and_fails_open`.
It protects the exact product-or-sample gate and infrastructure/XML exclusions;
the old product-only condition would silently omit the newly included F#
sample on sample-only changes. All three isolated counterfactuals were rerun
against the final YAML/test bytes: product-only gate, TRX-only project, and
nested repeated-copy destination each produced 15 passes and one intended
assertion failure (exit 1), with no setup errors/skips. The final real suite
passed all 16 cases again. Final test SHA-256:
`1F7507CE678B4BBDDC895F26FF0617789AFF5F08A783BB23DB050EB093A65ADC`;
job YAML SHA-256:
`F15F508DA17829ADAE4D23B7461C0570C24D72CC558BA6E372C0D9DD24FEDC18`.

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
