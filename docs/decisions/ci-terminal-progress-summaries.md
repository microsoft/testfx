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

Implementation targets `main` only. This batch excludes backports, downstream
SDK-pin updates, and feature-gate rollout. A future downstream pin or backport is
separate work requiring its own release-branch validation; existing SDK releases
are not fixed by this PR.

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
- 2026-10-30: The coordinator finalized main-only delivery for this batch.
  Backports, downstream SDK-pin updates, and feature-gate rollout are out of
  scope rather than pending decisions for this PR.
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
- 2026-10-30: The full multi-TFM build/pack succeeded with zero warnings/errors.
  All 32 shipping packages identify implementation commit
  `f4d8d35bf45ce0ece75a66e1b197c96684767e48`. With the task-local
  `VersionSuffix=preview.26530.102`, the SDK's independently generated JUnit
  default remains `1.0.0-dev`; a producer-side private-property override did not
  change that evaluated value. The SDK validation consumer must use the supported
  `MicrosoftTestingExtensionsJUnitReportVersion=1.0.0-preview.26530.102` override
  to consume the same-source JUnit package. No package/source fix outside this
  reporter change was made.

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

### Revision-bound regression sensitivity

The correct implementation is
`f4d8d35bf45ce0ece75a66e1b197c96684767e48`. The isolated comparison retains
both final test files against baseline production, adding only an ignored optional
fourth renderer-constructor parameter so the new aggregation test can compile.
This is a buildable targeted counterfactual, not an unmodified baseline run.
Its retained-test/support patch SHA-256 is
`164ED76C41CAD0FD7B5BEB1A8B3DBAFBFC180836C3A872B39A4BD162CD642352`.

The identical filter in each state was:

```text
FullyQualifiedName~VisibleSilence_|FullyQualifiedName~NonCursorSummary_|FullyQualifiedName~NonCursorProgress_
```

Both runners used
`dotnet <built-test-dll> --filter <filter> --report-trx --results-directory <isolated-results> --no-ansi --progress off`.
The comparison build used the repository-pinned dotnet with
`build <comparison-test-project> -c Debug -p:TargetFramework=net8.0 -p:TargetFrameworks=net8.0 -bl:<isolated-binlog> -v:minimal`.

| Contract / test case | Without behavior | Correct implementation |
| --- | --- | --- |
| `VisibleSilence_HiddenCompletions_EmitAtDefaultBoundary` | Assertion: expected one progress line, actual zero. | Passed; 59/60-second boundary and repeat interval. |
| `VisibleSilence_VisibleWriteDelaysHeartbeatButNotCompletionAge`, `VisibleSilence_ProgressMessage_PostponesHeartbeat` | Premature heartbeat/count assertions failed. | Both passed. |
| `VisibleSilence_NoCompletionsAndRestart_DoNotInventProgress` | Exact-output assertion failed. | Passed. |
| `VisibleSilence_CompletedModules_RemainInCumulativeCounts`, `NonCursorProgress_CompletedWorkerSlot_IsIncludedInRunTotals` | Lost completed-module totals; reporter wiring printed one instead of three completed tests. | Both passed. |
| `NonCursorSummary_FailuresPrecedeArtifactsAndSuccessfulRecapsAreNotRepeated` (two modes) | Both exact-output assertions failed. | NoAnsi and SimpleAnsi passed. |
| `NonCursorSummary_AllPassingModules_KeepOnlyAggregateAfterCompletion`, `NonCursorSummary_SuppressedVerdict_KeepsFailureRecapsAndArtifacts` | Both exact-output assertions failed. | Both passed. |

Red: 10 selected/executed, 10 assertion failures, zero skipped, process exit 2.
Green: the same 10 selected/executed passed, zero skipped, process exit 0.
The source-declared expected names, individual TRX results, test-file hashes, and
green execution gate were checked. Build failures, skips, and zero-test runs were
not counted as sensitivity evidence. The nested comparison emitted duplicate
ancestor `.globalconfig` warnings, but compiled and executed; the real candidate
build had zero warnings.

### Real packed-host demonstration

An external net8.0 MSTest host ran eight sequential data rows, each awaiting 750ms,
against each packed core. Both hosts used MSTest framework/adapter
`4.6.0-preview.26529.1` and TrxReport `2.6.0-preview.26529.1`; only the compared core
package changed. This isolates the reporter behavior. The separately frozen
same-source package set below is for native SDK composition.

The exact arguments were
`--filter FullyQualifiedName~HiddenPass --no-ansi --progress on --show-test-results failed --no-banner --report-trx --results-directory <isolated-results>`,
with `MTP_PROGRESS_SILENCE_SECONDS=1` and `MTP_PROGRESS_SLOW_TEST_SECONDS=0`.
The one-second override accelerates the demonstration, not the production default.

Before: eight executed/passed, exit 0, duration `6s 379ms`, **zero progress lines**.
After: the same eight executed/passed, exit 0, duration `6s 332ms`, and these
captured progress lines:

```text
running... 1 completed, 0 failed, active modules: 1 | last completion: 0s ago | active: HiddenPass (2)
running... 2 completed, 0 failed, active modules: 1 | last completion: 0s ago | active: HiddenPass (3)
running... 3 completed, 0 failed, active modules: 1 | last completion: 0s ago | active: HiddenPass (4)
running... 5 completed, 0 failed, active modules: 1 | last completion: 0s ago | active: HiddenPass (6)
running... 6 completed, 0 failed, active modules: 1 | last completion: 0s ago | active: HiddenPass (7)
running... 7 completed, 0 failed, active modules: 1 | last completion: 0s ago | active: HiddenPass (8)
```

Additional real consumer runs retained eight passing rows and no heartbeat for
`--progress off` and for the zero heartbeat threshold. An intentionally failing
test retained its full diagnostic and exit 2; an empty selection retained exit 8.
Those expected child failures are not counted as green test cases.

The comparison packages' embedded repository commits, `project.assets.json`,
effective caches, and the DLLs in the launched host directories were verified:

| Artifact | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| Net8-only comparison nupkg | `E923C274656C1AF40AE9236F7712D537C88C99FC4C5DEDA07277746BD11483FB` | `FD65003E40C1C20E055388C35205CB0081603759A3EFA25E41F9FFCAECE368F8` |
| Consumed core DLL | `59590E8F40E00587F38916EBE98A3E5B8B9B5F15CC1E9973F7FBD5A759A490D1` | `D0A562846536D9FE8E9456E3785FAF566165F6896720993605F81B07CBC519D5` |

The authorized PR preserves complete before/after progress transcripts, the
consumer fixture/build commands, and final-summary transcripts captured from
the same reporter harness in both states. Text is preferable to a screenshot
for this CLI output; the harness captures are explicitly not a native SDK run.

### Aligned package handoff

The full source build/pack command was
`.\eng\build.ps1 -restore -build -pack -projects 'src\**\*.csproj' -c Debug -bl /p:VersionSuffix=preview.26530.102 /p:SkipRuntimesInstall=true`;
it completed with exit 0, zero warnings/errors, and successful package
content/metadata verification. The pinned runtime needed by focused tests was
already installed. The immutable 32-package feed was handed to the SDK owner
with a SHA-256 manifest, not published to a package registry.

MSTest packages use `4.6.0-preview.26530.102`, MTP and its version-aligned
extensions use `2.6.0-preview.26530.102`, and JUnit uses
`1.0.0-preview.26530.102` with the explicit consumer property above. Every
package's repository commit is the implementation revision. The full MTP nupkg
hash is `AD327786AD823B6E7424DE97FC308368F14032A6DFF8226F3FF06D07B11B3DBE`;
its net8 core payload exactly matches the captured after-consumer DLL.

The owning unit-test assembly uses MSTest method-level parallelism with one
worker per processor. New renderer tests use per-test clocks and consoles, not
shared environment/console mutation; reporter refresher threads must be joined
before assertions or cleanup. Packed consumers use task-owned directories/caches.

### Native SDK composed-product evidence

On 2026-10-30, the SDK owner published the composed validation in
[dotnet/sdk#56851](https://github.com/dotnet/sdk/pull/56851) at
`f5838f51c497a70fb60f3ed7e89102ca293b6838`. The published source states,
commands, captured output, package manifest identity, and results were read back
for this record; these commands were executed by the SDK owner, not rerun by this
TestFx session.

The comparison uses baseline SDK CLI binaries from
`c781c3cda4cf87f94291d04223e9146664d4f5f5` and final managed/Native AOT binaries
from the SDK revision above in the built Windows x64 redist layout
(`12.0.100-dev`). Both states use the frozen 32-package TestFx feed from
`f4d8d35bf45ce0ece75a66e1b197c96684767e48`, genuine net9.0 MSTest applications,
and .NET 9.0.0. The native invocation explicitly reports
`AOT test tier: TestModules.`.

In the real native multi-module hidden-pass capture, one module finishes ten
tests while the other continues completing hidden passing tests. The baseline
SDK remains silent; the final SDK emits the following captured line at
`61.7903221` seconds, including the finished module's ten results:

```text
running... 18 completed, 0 failed, active modules: 1 | last completion: 4s ago
```

The final total remains 20. The compact verdict/counts precede artifact paths,
already-printed successful modules are not repeated, and explicit progress
suppression is preserved. The linked SDK evidence records 17 composed cases,
50 passing composed checks, and 246 passing focused SDK tests with no failures
or skips. Its scenarios also cover diagnostic workers, actual map collection,
crash recovery, worker degradation, cancellation, and zero-result exit behavior.

This closes the previously missing reporter-to-native-SDK evidence for these
main-branch Windows x64 revisions. It does **not** prove required cross-module
Azure DevOps/GitHub summary publication: the SDK PR deliberately does not
advertise handshake field 19 (`RequiredPostProcessingSupported`). Required
singleton processing (field 18) and the authoritative final-policy `runSummary`
handoff remain a separate dependent SDK implementation/validation task. Ordinary
TRX/CTRF/JUnit merging must not be presented as that aggregate proof.

## Open questions

- The reporter's native SDK boundary has composed evidence at the pinned
  revisions above. Required cross-module CI-summary aggregation remains outside
  that proof and belongs to the separate dependent SDK work.
- Backports, downstream SDK-pin updates, and feature-gate rollout are outside
  this main-only batch. Future work in those areas needs separate release-branch
  validation; no existing SDK release or required global CI-summary completion
  is claimed.
