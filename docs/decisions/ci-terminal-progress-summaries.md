# CI terminal progress and summaries: research and decisions

## Problem and contract

The public TestFx [build 1655468](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1655468)
at `b3b88187058a2f024d9d9981d143172953d05fd2` had long quiet test
intervals despite continuing test completions. Its Debug step also repeated
successful module results in the final summary and placed artifact paths before
the run verdict. The implementation baseline is
`7dd2407848979d58f0e9b2bd75101a2dbfa7d1bc`.

The non-cursor reporter must distinguish **visible output** from **test
completion**. After 60 seconds without visible reporter output it should report
the real cumulative completed/failed counts, active module count, and age of the
last completion (or explicitly state that no test has completed). Emitting a
heartbeat is not evidence that a test completed. Completed modules must remain
in the aggregate when their rendering slots are reused.

Non-cursor final summaries should lead with the verdict and counts, then
failures, then optional details and artifact paths. Successful module results
already printed at completion should not be repeated; when the caller suppresses
those completion lines, their final module results remain available. Full test
diagnostics, process stdout/stderr, and artifact paths remain available.

Cursor-capable interactive rendering, result visibility selection, cancellation,
retry accounting, zero-test policy, and the distinction between failed tests and
host/handshake errors are unchanged. `--progress off` and
`MTP_PROGRESS_SILENCE_SECONDS=0` still disable heartbeat reporting; the existing
slow-test threshold/backoff is independent. No public API or wire-protocol change
is needed.

### Producer-consumer boundary

TestFx owns `Microsoft.Testing.Platform` and the terminal source under
`src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal`.
`dotnet/sdk` vendors that source into the outer `dotnet test` process.
The host and outer CLI are separately built/shipped consumers: an in-repository
source-linked test cannot establish that the outer SDK console changed.

The SDK integration owner is responsible for porting the exact source revision,
aligning its explicit heartbeat default, and exercising the built SDK with packed
test hosts. Existing handshakes, intentionally absent handshakes, legacy fallback,
parent/child exit semantics, and multi-module aggregation must not change.
Reporter timers are owned by the existing progress terminal, stopped/joined on
completion/disposal, and reset for subsequent runs.

Implementation targets `main`. No release-branch backport or downstream feature
enablement is authorized by this change alone; the coordinating CI-output owner
retains rollout and servicing responsibility.

## Research and experiments

- 2026-10-30: `SilenceDrivenHeartbeatRenderer.OnTestCompleted` resets the same
  clock used by heartbeat emission, including when result visibility hides the
  completed test. Its totals scan only active worker slots, which
  `TestProgressStateAwareTerminal.RemoveWorker` clears.
- 2026-10-30: `TerminalTestReporter.Summary` prints all artifacts before the
  verdict, and all module results again before the aggregate counts.
  `ErroredAssemblies` and `Handshake` already retain complete actionable
  diagnostics; reuse them rather than introducing a second reporting pipeline.
- 2026-10-30: The working tree and refreshed `origin/main` both resolve to the
  baseline above; no open terminal-reporter PR overlaps this work.
- 2026-10-30: Initial build launches could not find Windows PowerShell because
  the inherited PATH exceeded `cmd.exe`'s limit. A short process-local PATH
  containing the pinned dotnet, Windows PowerShell, System32, Git, and the active
  PowerShell installation allows the unmodified repository build/pack scripts
  and package verifier to succeed. No global setting or build source changed.
- 2026-10-30: A first external-consumer build put its isolated NuGet cache under
  the project directory; SDK default items then exposed other-TFM adapter
  assemblies as reference candidates. Moving the task-owned cache outside the
  consumer project fixed the incompatible-reference build without changing
  package content or target frameworks. Neither setup failure is regression
  evidence.

## Alternatives and decision

- **Keep completion-driven silence:** lowest code churn, but hidden successful
  tests can suppress visible progress indefinitely. Rejected.
- **Unconditional periodic output:** solves silence but duplicates useful
  visible diagnostics during active logging. Prefer a visible-output silence
  deadline, independent of the completion-age clock.
- **Count only active worker slots:** cheap, but totals decrease as modules
  finish. Read cumulative reporter-owned assembly state when a heartbeat is due.
- **Suppress successful modules and artifact paths entirely:** quieter but loses
  diagnostic/navigation information. Instead remove duplicate success recaps and
  put complete artifact detail after the verdict and failures.
- **Change every terminal mode:** unnecessary compatibility cost. Preserve
  cursor-capable output and apply the summary ordering to non-cursor modes.

No new option or general progress abstraction is planned. The existing threshold
override, renderer, summary sections, and per-run state supply the needed seams.

## Validation and demonstration

The pinned SDK is `12.0.100-alpha.1.26509.101`, with .NET `8.0.32` for the
focused Debug/net8.0 executions.

- Unchanged baseline: 361 reporter/output-device cases passed, none skipped.
- Implementation: 370 cases passed, none skipped, including exact-output
  failure-first summaries, completed worker-slot aggregation, 59/60-second
  boundaries with hidden results, visible-output suppression, completion-age
  preservation, and session reset. Existing cursor snapshots stayed green.
- The standalone terminal-contract consumer builds with zero warnings/errors.
- The owning project's `UpdateXlf` generated all 13 catalogs; the localization
  guard validated all 364 repository catalogs.
- The repository build script packed and validated
  `Microsoft.Testing.Platform.2.6.0-preview.26530.102.nupkg`; the isolated baseline
  package is `2.6.0-preview.26530.101`. These are task-local comparison versions,
  not published package versions.

The same behavioral tests will be retained against baseline production behavior
in the isolated comparison. Packed-host before/after captures and assertion-level
red/green results will be added after execution; they are not yet claimed here.

The owning unit-test assembly uses MSTest method-level parallelism with one
worker per processor. New renderer tests use per-test clocks and consoles, not
shared environment/console mutation; reporter refresher threads must be joined
before assertions or cleanup. Packed consumers use task-owned directories/caches.

Executed evidence and the exact implementation revision will be added after the
comparison runs. Native SDK composed-product evidence is owned by the coordinated
SDK PR and remains separate from TestFx-local proof.

## Open questions

- Exact package/source pair and native SDK validation results are pending.
- Supported release-branch servicing is a coordinator decision after composed
  validation; no claim of cross-repository completion is made here.
