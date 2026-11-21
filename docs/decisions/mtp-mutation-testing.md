# Mutation testing of the Microsoft.Testing.Platform core assembly

## Problem and contract

The `platform-core` mutation-testing module in `eng/mutation-testing/modules.json`
covers about 650 source files and 53,000 lines but had not produced a usable
result. The monthly report in [#12166](https://github.com/microsoft/testfx/issues/12166)
reported no fresh measurement or actionable survivors. Its 2026-11-15 run
failed before mutation execution (`durationSeconds: 0`; build failed, tool and
mutation steps skipped). Issue [#11907](https://github.com/microsoft/testfx/issues/11907)
identified the missing core-assembly inventory; PR [#11912](https://github.com/microsoft/testfx/pull/11912)
onboarded the module.

This iteration measures selected `Microsoft.Testing.Platform` source slices,
adds focused unit tests for actionable survivors, and records the remaining
mutants and follow-up slices. The first pass also exposed a parser defect; no issue has been filed for it.
`ProcessHigherPrecedenceOperators` stopped after applying one pending
operator when `/` followed a mixed `|` and `&` expression. The fix removes that
unconditional `break`, restoring `&`-before-`|` precedence across tree-node
segments. Valid mixed expressions can now select different tests; malformed
expressions previously accepted because the operator stack was left partially
unprocessed are rejected.

The producer is Stryker.NET 5.0.0 against the `platform-core` module and its
configured MTP test assemblies. The consumers are maintainers interpreting
mutation results and applications using `--treenode-filter`. No package,
public API, Stryker configuration, or workflow change is made. The implementation
adds no Stryker suppressions. The shared CI configuration remains unchanged.

## Research and alternatives

- 2026-11-20: Reused the existing module manifest, generated solution, test
  runner, and shared `stryker-config.json`; no parallel mutation harness was
  created. The generated solution command is
  `python .github/scripts/mutation_testing.py create-solution --manifest eng/mutation-testing/modules.json --module platform-core --output MutationTesting.generated.slnx`.
- 2026-11-20: The baseline unit-test run executed 4,186 tests successfully
  (0 failed, 2 skipped). Stryker found 4,212 tests across both module test
  assemblies and created 19,664 project-wide mutants on each invocation.
- 2026-11-20: A whole-module invocation is too large for this iteration and
  exceeds the module's 360-minute CI timeout estimate. The B1 run tested 2,527
  mutants in 1 h 39 m 19 s; about 14.5 minutes were fixed invocation overhead
  and the measured mutation rate was about 29.8 mutants/minute.
- 2026-11-20: Rejected `test-case-filter` because it would omit mutants killed
  only by the heavy real-process tests. Higher concurrency was not a useful
  alternative on the already-loaded 32-logical-CPU machine. Stryker disable
  comments would hide mutants rather than preserve their disposition.
- 2026-11-20: Stryker ran from a task-owned plain copy outside the repository
  worktree, so tests could be authored against frozen production sources.
  Running a nested repository copy was rejected because ancestor analyzer
  configuration made it fail.
- 2026-11-20: The pilot and B1 slices used shared timeout defaults. B2a and the
  targeted verification use a local-only config that ignores `ConfigureAwait`
  and increases `additional-timeout` to 60 seconds and `timeout-ratio` to 4.0.
  This local setting is not proposed as a repository config change.

## Slices and results

The three completed Stryker slices cover the pilot, B1 and B2a source areas.
The score is slice-specific, not a whole-project score. "Tested" below is
Stryker's executed mutant count; the score denominator excludes pending and
runtime-error mutants.

| Slice | Stryker result | Test work |
| --- | --- | --- |
| Pilot: Capabilities and TestHost | 59 report rows; 39 scored (20 killed, 4 survived, 15 timeout); 1 runtime error, 1 compile error, 18 ignored; 89.74% score | T1 covered the request/filter and well-known-client survivors; parser regression rows exposed and fixed the parser defect. |
| B1: Builder, CommandLine, Configurations, Requests, TestFramework | 3,526 report rows; 2,527 tested, 2,482 scored (1,838 killed, 619 survived, 25 timeout); 3 pending, 42 runtime errors, 345 compile errors, 654 ignored; 75.06% score | T2–T5 added configuration, command-line, handler/builder and framework tests. |
| B2a: Services, Extensions, Logging, Telemetry, TestHost | 2,647 report rows; 1,493 tested, 1,427 scored (1,139 killed, 278 survived, 10 timeout); 2 pending, 64 runtime errors, 404 compile errors, 750 ignored; 80.52% score | T6a and T6b added service, logging, extension and telemetry tests. |

The survivor batches account for 716 surviving mutants and 27 timeout mutants:
T1 83, T2 91 + 5 timeouts, T3 69 + 4, T4 90 + 3, T5 114 + 5, T6a 144 + 10,
and T6b 125. Their mapping files record the proposed distinguishing tests and
dispositions. The targeted verification manifest selected surviving,
no-coverage and timeout candidates across these reports; it contained 761
entries before the whole-file parser selection. The executed verification and
its candidate/mapping reconciliation are recorded below; some T1 mapping rows
remain unmatched to the pilot report.

## Survivor triage

TEST dispositions identify a focused test intended to distinguish the mutant;
they are hypotheses until the corresponding mutation is executed. EQUIVALENT
and LOW-VALUE dispositions remain visible in the report and are not suppressed.
HANG-DETECTED is used only for timeout mutants whose altered loop or wait cannot
be allowed to run to completion. RuntimeError and Pending mutants are reported
separately because a host crash or incomplete execution is not a scored
survivor.

Timeouts are not necessarily genuine hangs. Stryker sometimes classifies
mutants as `Dubious` because the coverage relay does not receive an acknowledgement
from the heavy real-process tests; those tests are then assumed to cover every
mutant. A mutant can time out while running that broad set even when it would
survive. The pilot confirmed this: several timeout candidates in `ClientInfo`,
`SessionUid` and `TestHostManager` were ordinary survivors under the larger
timeout used in B2a. Timeout dispositions therefore require rechecking with the
larger timeout and the appropriate real tests.

The parser defect was not treated as an equivalent mutant. The same 14 new
syntax rows were run against the unfixed and fixed implementations. Before the
fix, 12 of 165 selected tree-filter tests failed for behavioral reasons; after
the fix all 165 passed. A broader full suite then passed 4,339 tests with 2
skipped. A separate differential probe compared 2,187,920 filters; its
independent reference grammar found 34,716 tree mismatches and 988 malformed
filters accepted before the fix, and none after it. The comparison also found
5,834 previously accepted inputs with independent structural defects; these
are rejected by the fixed parser. The 62 observed exception-type changes were
among inputs malformed both before and after.

The ten EQUIVALENT dispositions for
`AbortAtDeadlineExtension` were checked against its state machine. Completion
state transitions and deadline-claim release values are not observed after
the one-shot timer and `_handled` guard prevent a second handler pass. The
mutated fast-path checks are repeated under the lock and again when claiming
the deadline; skipping an early return can at most publish an unobservable
state change before the same guarded outcome. These are code-reading
dispositions, not mutation kills.

Linked source files are compiled into the unit-test assembly, so tests exercise
their local compiled copies rather than mutations in the shipping core
assembly. NoCoverage noise for those sources is not evidence that a shipping
mutation was tested. RuntimeError and Pending mutants are also excluded from
the score. The full module score remains unmeasured.

## Validation

### Executed unit-test evidence

The parser fix was tested on Windows x64, Debug, with the repository-pinned
SDK `12.0.100-alpha.1.26562.108`. The unfixed and fixed parser source hashes,
new test source hash, exact commands and red/green counts were captured in the
session evidence. The two commands were:

```powershell
.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Platform.UnitTests -c Debug --nologo -v:minimal
.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests -f net10.0 --no-build -c Debug -- --filter "FullyQualifiedName~TreeNodeFilter"
```

Red (actual pre-fix parser, tests retained): build exit 0; test exit 2;
165 selected, 12 failed, 153 passed, 0 skipped. The failures were 2 expected
`InvalidOperationException` assertions that did not throw and 10 expected
tree-filter results that differed. Green (fixed parser): build exit 0; the
same 165 selected and passed, with 0 failed and 0 skipped. The full suite on
that source state passed 4,339 with 2 skipped. The pre-fix parser source is
identified by Git blob `75c9b7c529eead18ff7360eb5d37b80ea02cad74`; the fixed
parser is `84940a3376882daa1f1b5dba404a0ba81e4b4fa3`. The unchanged regression
test source is `ac1d5e479b7072dba5a83cab5e4405d01dae665c`.

After merging the six mutation-test batches and the timer-test rendezvous
rework, the combined unit-test build succeeded for net462, net10.0 and net11.0
with 0 errors. The targeted
`AbortAtDeadlineExtensionDeadlineHandlingTests` run passed 18/18. The final
complete unit run was:

```powershell
.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests -f net10.0 -c Debug --
```

It passed with 4,841 passed, 0 failed and 2 skipped (4,843 total). Changed C#
file encoding validation passed for all 53 changed C# files (UTF-8 BOM);
`git diff --check` passed.

**Targeted Stryker verification:** completed, but this is not a whole-project
measurement. The Windows runner used Stryker.NET 5.0.0, `MutationTesting.generated.slnx`,
the `platform-core` module, `net10.0`, and the local verification config
generated from the 761-entry manifest. The config selected survivor,
no-coverage and timeout candidates with character-offset mutation spans;
`TreeNodeFilter.Parsing.cs` was selected as a whole file because the production
fix removed a line and shifted offsets. The wrapper invocation was
`run-stryker-slice2.ps1 -Name verify-final-20261121 -LocalConfig <session-scratch>\files\verify-test\stryker-config.verify.json -Repo Q:\src\copilot-comparisons\testfx-stryker-runner`.
It exited 0 after 50m50s. The report contains 5,751 rows: 4,060 Ignored,
673 CompileError, 737 Killed, 247 Survived, 25 Timeout, and 9 RuntimeError;
Stryker reported 75.52%. Treat that score as specific to this filtered report,
not the core project.

Reconciliation by source path, mutation, replacement, and location matched 760
of the 761 manifest candidates: 468 Killed, 239 Survived, 26 Ignored, 24
Timeout, and 3 RuntimeError. The unmatched candidate was the parser mutation
whose source file was selected whole after the fix. A separate reconciliation
of test-disposition mappings matched 442 rows: 380 Killed, 36 Survived, 21
Ignored, 4 Timeout, and 1 RuntimeError. Fifty-seven T1 mapping rows could not
be matched to the pilot report by mutant ID, so their disposition remains
unverified. The surviving and unverified candidates are not treated as
equivalent; further triage is required.

Stryker reported it could not restore its mutated test assembly during cleanup.
The original assembly was restored from Stryker's unchanged backup and the
restored and backup SHA-256 values matched
(`FD910472CCDC92263FA4F0D4C74B5587232FCA3AF546C4CFDEF1DD440ACA1F89`). A later
clone rebuild failed because `AddMSTest` was unavailable, so that rebuild is
not validation evidence. The passing unit run above is from the active worktree.

### Consumer and rendered-surface evidence

The bug is in the `TreeNodeFilter` parsing path used by
`ConsoleTestExecutionFilterFactory` for `--treenode-filter`; server-mode
requests also construct a `TreeNodeFilter`.

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Mixed precedence reaches the real CLI consumer | `ExecutionTests.Exec_WhenMixedPrecedenceFilterIsSpecified_OnlyMatchingTestIsRun` invokes discovery and execution on a generated app consuming the packed `Microsoft.Testing.Platform` package. Filter `A\|B&C/D` should select `Test1` at `/A/D` and exclude `Test2` at `/B/C/D`; discovery lists `Test1`, and execution reports the `Executed Test1` marker but not `Executed Test2`, with one passed result. | No layout change. The child host paths and package identity were checked against generated output and restore metadata; there is no changed rendered surface. |
| Existing simple filter remains usable | `ExecutionTests.Exec_WhenFilterIsSpecified_OnlyFilteredTestsAreRun` exercises `--treenode-filter` with `<whatever>` through the same packed host. | Existing test; no visual UI. |
| Parser regression sensitivity | `TreeNodeFilterSyntaxTests` tests malformed-filter rejection and mixed-operator matching. The same 165 selected tests failed against the pre-fix parser and passed with the fix. | Unit-level red/green evidence; packaged pre-fix acceptance comparison was not run. |

The final Release pack command succeeded:

```powershell
.\build.cmd -pack -c Release -bl
```

The focused acceptance command, with retained generated assets, was:

```powershell
$env:Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup = '0'
.\.dotnet\dotnet.exe run --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests -f net12.0 -c Release -- --filter-uid 16ca27a0-cb15-86f6-9c21-9ddd9c3674ed --filter-uid 10df907e-1d2a-84b5-8c64-35d2e0541b52 --filter-uid 13fe71d8-47ea-8972-8944-6cbbde972da6
```

It passed all three outer cases (net11.0, net10.0, net462): 3 passed, 0
failed, 0 skipped. The generated consumer resolved
`Microsoft.Testing.Platform/3.0.0-dev`; its restore SHA-512 matched the packed
`Microsoft.Testing.Platform.3.0.0-dev.nupkg` (SHA-256
`48502B1F32C1A72002B7B37A58201712F30C9DB1C9FCC6845DD646663C6936EC`). The
deployed net11.0, net10.0 and netstandard2.0 assemblies (the net462 asset)
matched their respective package DLL hashes. The outer test report recorded
the exact three test identities and outcomes. The parser's pre-fix behavior
was not repacked and run through this acceptance test, so packaged
red/green sensitivity remains unverified. The test class is already
`[DoNotParallelize]`; the acceptance host reports MethodLevel scheduling with
32 workers; `ExecutionTests` is marked `[DoNotParallelize]`, and each
child-process environment dictionary is per call.

### Test-value evidence

| Contract / test case | Counterfactual | Without behavior | Correct implementation | Evidence / gap |
| --- | --- | --- | --- | --- |
| Mixed `&`/`\|` tree-node filter precedence; `TreeNodeFilterSyntaxTests` | Actual pre-fix parser source (`75c9b7c529eead18ff7360eb5d37b80ea02cad74`), retaining the regression tests | Executed: 12 of 165 selected tests failed behaviorally | Executed: same 165 tests passed | Fixed parser source blob `84940a3376882daa1f1b5dba404a0ba81e4b4fa3`; test source blob `ac1d5e479b7072dba5a83cab5e4405d01dae665c`; exact commands above. |
| Packaged CLI consumer selects the correct identity/outcome | No pre-fix package comparison was run | Unverified | Executed: 3 target-framework cases passed; each discovered `Test1`, then ran exactly one passed result with `Executed Test1` and no `Executed Test2` | Package identity, restore hash and deployed DLL hashes were verified. The same acceptance test against the old parser remains a gap. |
| Mutation-driven coverage for the measured slices | Selected Stryker mutations in the targeted verification config | Executed: 737 mutants killed in the final filtered report; 247 survived, with 25 timeouts and 9 runtime errors | The full unit suite passed 4,841 tests and 2 were skipped | Stryker score 75.52% is partial. Candidate and mapping reconciliation above identifies unresolved survivors and a 57-row T1 mapping gap; this PR does not claim complete triage. |

## Remaining iterations

This is a partial measurement, not completion of the 19,664-mutant core
project. Suggested next slices are B2b (`TestHostControllers`,
`TestHostOrchestrator`, `DynamicExtensions`), then C (`Messages`, `Helpers`,
`IPC`), D (`Hosts`, `ServerMode`), and E (`OutputDevice`); small leftovers
include `Tools`, `AI`, `Resources`, and root `GlobalSuppressions.cs`. The
remaining areas need their own measured runs and survivor triage.

Follow-up infrastructure work could set an explicit `ignore-methods` entry
for `ConfigureAwait`, use larger mutation timeouts, and split `platform-core`
into directory-based invocations. These are recommendations only; this change
does not modify the shared workflow or configuration. The local run estimate
and machine contention make a full-project run in one invocation impractical.

## Open questions

- Which follow-up iteration will own B2b and the remaining C–E source areas?
- Should the shared workflow adopt the locally tested timeout and
  `ConfigureAwait` settings, and should it shard `platform-core` by directory?
- The parser behavior correction changes selection for valid mixed
  `&`/`|` filters and rejects some malformed filters accepted by the old
  parser. The unit regression evidence is strong; packaged acceptance evidence
  remains a follow-up.
