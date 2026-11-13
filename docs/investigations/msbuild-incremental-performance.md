# MSBuild incremental-performance investigation

This branch preserves a reproduction for the MSBuild team, not a proposed change
to TestFx's build defaults. The product source is based on measured revision
`bacb2826dbf20a0192a6753a61a98743faf5d4f8`.

**Finding:** parallel worker processes and node reuse helped substantially;
experimental multithreading (`-mt`) did not improve on process-mode reuse.

## Reproduce on Windows

Use a dedicated checkout, PowerShell 7.4 or newer, and Git on PATH. Do not run
other builds in that checkout during the experiment.

```powershell
git clone --branch dev/amauryleve/msbuild-incremental-performance https://github.com/microsoft/testfx.git testfx-msbuild-investigation
Set-Location testfx-msbuild-investigation

# Prepare the pinned toolchain and all outputs; not a timing sample.
.\eng\build.ps1 -restore -build -msbuildEngine dotnet -projects .\TestFx.slnx -nodeReuse:$false -verbosity quiet /m:8 /mt:true /p:SkipUapTargetFramework=true /p:SkipRuntimesInstall=true

# Five configurations, two opposite-order blocks, three repetitions per block.
.\eng\measure-msbuild-incremental.ps1
```

The setup build must succeed before measuring. It restores the SDK from
`global.json` into `.dotnet` and prepares Debug outputs. Classic UWP is excluded
because it requires desktop MSBuild; modern Windows/WinUI targets remain.
Test-runtime provisioning is unnecessary for this build-only experiment.

If setup fails because `powershell` cannot be found, check the inherited PATH.
The original host's PATH exceeded cmd.exe's supported length. Start a shell with
a shorter process-local PATH that includes Git and Windows PowerShell; do not
change user-global settings. Benchmark children already use a short PATH.

For a shorter focused comparison:

```powershell
.\eng\measure-msbuild-incremental.ps1 -Cases processes-reuse,threads-reuse -Blocks 1 -Repetitions 1
```

`-Workers` changes the parallel budget (default eight); the serial control always
uses one worker. `-SkipBinaryLogs` omits the separate diagnostic captures.
`-OutputDirectory` changes the local evidence destination. No generated evidence
is automatically committed or uploaded.

## What the harness measures

The [harness](../../eng/measure-msbuild-incremental.ps1) invokes the repository's
`eng\build.ps1` using the pinned .NET MSBuild engine, `TestFx.slnx`, Debug, no
restore, and identical analyzer/diagnostic settings in every case. `-m` selects
the worker budget; `-mt` uses threads for parallel project execution rather than
ordinary worker processes. Incompatible tasks can use sidecar task hosts.

Each case/block starts a new salted node pool and receives one unmeasured warmup.
The timed sequence alternates unchanged builds with a timestamp-only touch of:

```text
src\Platform\Microsoft.Testing.Extensions.Configuration\MicrosoftExtensionsConfigurationSnapshotSource.cs
```

There is no source-content edit, API change, `Rebuild`, graph build, analyzer
disabling, shared cache purge, or change to global environment settings.
`MSBUILDUSESERVER=1` is held constant. Compiler/filesystem/package caches remain
warm across cases. The original source timestamp is restored in `finally`.

The default matrix runs 60 timed builds, ten warmups, and ten separate diagnostic
captures. Timed runs do not enable the binary logger. Wall time includes the
PowerShell wrapper's startup and completion.

The repository-local harness adds one-second descendant sampling to safely
identify its processes. It stops only verified descendants it launched, never
pre-existing servers or all processes sharing an SDK. This is a portability and
cleanup improvement over the original session harness; the historical numbers
below have not been relabeled as measurements of the revised harness.

## Results from 2026-11-06

Windows 11 Enterprise; 32 logical processors; 127.95 GiB RAM; shared host.
Pinned SDK `12.0.100-alpha.1.26509.101`, MSBuild `18.13.0.51001`
(`18.13.0-1.26509.101+907535df2`). Restore and initial output preparation were
excluded. The preparation build passed in 371.1 seconds, but was not a controlled
cold-build comparison.

Seconds, **median [minimum - maximum]**, six timed observations in every cell:

| Workers | `-mt` | `-nr` | Unchanged | Source timestamp touch |
|---|---|---|---|---|
| 1 | false | false | 42.079 [39.882 - 52.450] | 63.593 [61.330 - 82.539] |
| 8 | false | false | 25.476 [23.086 - 29.133] | 40.950 [35.969 - 66.877] |
| 8 | false | true | **18.648 [17.177 - 84.031]** | **34.815 [26.993 - 64.214]** |
| 8 | true | false | 34.599 [30.868 - 38.227] | 41.221 [31.976 - 56.484] |
| 8 | true | true | 24.920 [22.763 - 37.321] | 38.045 [29.089 - 49.069] |

At eight workers, reuse alone reduced the medians by **26.8% / 15.0%**.
Adding `-mt` to reuse made the medians **33.6% / 9.3% slower**.
The best configuration was **55.7% / 45.3% faster than the serial control**.

The [path-free CSV](msbuild-incremental-performance.csv) preserves all 60
observations, including the 84.031-second outlier. No samples were discarded.
The unchanged-build medians in the two blocks were:

| Case | Block 1 | Block 2 |
|---|---:|---:|
| Processes, reuse off | 25.697 | 24.757 |
| Processes, reuse on | 18.215 | 19.949 |
| Threads, reuse off | 34.337 | 36.191 |
| Threads, reuse on | 25.454 | 24.385 |

These descriptive results are from one shared host, not a tail-latency guarantee
or evidence about CI, restore-inclusive builds, other budgets, or desktop Visual
Studio MSBuild. Tests and packaging were not executed.

## Evidence and investigation leads

All 80 original builds succeeded. Native replay with the producing SDK verified
zero warnings/errors and identical compiler invocation counts across all five
configurations: one Csc call for unchanged builds, 24 after the timestamp touch.
The source hash and original timestamp were restored; the worktree stayed clean.

**Unchanged is not a pure no-op.** The WinUI target of `TestFramework.UnitTests`
recompiles because its generated manifest is newer than the intermediate DLL:

```text
Building target "CoreCompile" completely.
Input file "...artifacts\obj\TestFramework.UnitTests\Debug\net10.0-windows10.0.18362.0\win-x64\Manifests\app.manifest"
is newer than output file "...Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.dll".
```

This existing behavior was not repaired or excluded from the benchmark.

Representative snapshots confirmed one central server and seven mode-1 worker
processes with ordinary reuse, versus one central server and sixteen mode-2 task
hosts with `-mt`. All 24 timed reuse-enabled builds retained at least one MSBuild
PID; the 36 timed reuse-disabled builds left none.

Task-host fallback is a lead, not an isolated root-cause conclusion. Separate
reuse-enabled diagnostic samples recorded 171 project-data slice writes taking
276 ms aggregate in process mode versus 11,208 ms in thread mode; 87
GenerateResource calls took 369 versus 7,081 ms. These inclusive/overlapping
durations and instrumentation overhead must not be summed into wall time or
treated as CPU utilization.

The .NET build wrapper already enables `-m`, local node reuse, and `-mt`.
Windows defaults to Visual Studio MSBuild, for which the wrapper omits `-mt`.
Keep these engine paths distinct when investigating.

## Test-value evidence

The product source is unchanged from the measured base revision above. The
reproduction package was validated on 2026-11-09, separately from the historical
performance measurements:

```powershell
.\eng\measure-msbuild-incremental.ps1 -Cases processes-reuse,threads-reuse -Blocks 1 -Repetitions 1
.\eng\measure-msbuild-incremental.ps1 -Cases processes-no-reuse,threads-no-reuse -Workers 2 -Blocks 1 -Repetitions 1 -SkipBinaryLogs
.\eng\measure-msbuild-incremental.ps1 -Cases threads-reuse -Blocks 1 -Repetitions 1 -SkipBinaryLogs
```

Results: passed, respectively ten, six, and three builds; all exited zero. The
last run revalidated the final PID/creation-time retention refinement. The tested
Windows working copy (CRLF line endings) has SHA-256
`F0093972A479993BCA6C0F222D7612969CDF6E6E02D8D9FF1D1452CCD0726503`.
Git stores the identical script with LF line endings; its canonical blob SHA-256
is `A2FF5EA082444956BFC8619648A09A2BE832FCE94F020E018901250A4337F412`.
Remote readback confirmed that only the line endings differ.

| Contract / case | Counterfactual | Without behavior | Correct implementation | Evidence / gap |
|---|---|---|---|---|
| Benchmark workload / four parallel configurations | Original session harness, preservation comparison | Existing evidence: original 80 builds passed | Executed: 16 broader smoke builds passed | Four new native replays matched one/24 Csc calls, zero warnings/errors; all 60 historical CSV rows match the originals |
| Timestamp restoration and owned-process cleanup / final thread-reuse sequence | No faulty cleanup injected | Unverified | Executed: three final builds passed, timestamp/hash restored, no owned processes remained | Actual retained PIDs and creation times verified; no paired fault-injection or cancellation claim |

Zero workers, empty selections, duplicate selections, and mixed-case duplicates
were also rejected before building. These are executed preservation/smoke
checks, not a full automated regression suite or a new performance baseline.

## Local diagnostic artifacts

Runs create a unique folder under `artifacts\msbuild-incremental` with
`runs.csv`, `summary.csv`, `metadata.json`, `processes.json`, console logs, and
separate `diagnostic-*.binlog` captures. Failed builds abort the comparison;
inspect their console logs rather than using them as timing samples.

Replay with the same SDK that produced the log:

```powershell
.\.dotnet\dotnet.exe msbuild 'artifacts\msbuild-incremental\<run>\diagnostic-processes-reuse-no-change.binlog' -noconlog -fl '-flp:logfile=artifacts\msbuild-incremental\<run>\replay.log;verbosity=diagnostic;PerformanceSummary'
```

The original binlog MCP reader reported unknown new-format events, so compiler
counts and invalidation were confirmed using native replay. A reader warning is
not a warning from the recorded build.

Raw binlogs, process dumps, environment metadata, and console logs are deliberately
not checked in: they can contain paths, credentials, or imported source.
`ProjectImports=None` reduces embedded source but does not redact secrets.
Review/redact artifacts before sharing them privately with the MSBuild team.

References: [MSBuild switches](https://learn.microsoft.com/visualstudio/msbuild/msbuild-command-line-reference),
[repository defaults](../../eng/common/tools.ps1), and
[`global.json`](../../global.json).
