# CI diagnostic context: research and decisions

## Problem and contract

Keep fail-only console reporting quiet while making the existing Azure DevOps
summary useful for investigating a CI job. The observed Linux/macOS differences
in successful build 1655468 are platform-dependent skips, not evidence of a
regression. This work does not tune performance, change test selection, or alter
test counts, retry semantics, or failure exit codes.

The existing summary remains the owner of the bounded job-wide top ten. Timing
must identify the module, target framework, and outcome; a sum of test durations
must not be presented as job elapsed time. Skips should be counted by reported
reason and execution platform rather than listed individually. Issue references
are preserved from the framework's reason, not inferred. Missing historical
samples are not a performance baseline.

The producer is Microsoft.Testing.Extensions.AzureDevOpsReport; its fragments
are consumed by the same extension in the platform's artifact worker. Shared
fragment helpers also compile into Microsoft.Testing.Extensions.GitHubActionsReport.
Keep schema version 1 with additive, presence-aware optional metadata: old
fragments remain readable and missing metadata is visibly unavailable.
The SDK retains responsibility for the aggregate exit verdict and elapsed time.
Direct/older-SDK runs retain the existing per-module fallback. Cancellation and
partial-run handling remain owned by the existing platform/reporting lifecycle.

Acceptance-host diagnostics are repository tools, not a new shipping API.
Successful process statistics must name their host/module, framework, PID, and
execution versus artifact-worker role, and describe attempted launches accurately.
They must not add stderr statistics on failure, where collection prefers stderr.
The redundant local slowest-test consumer should defer to an enabled Azure DevOps
summary; its standalone fallback must retain failed-test timing.
The normal label is `acceptance-host`, which includes controllers and
orchestrators; command-line inspection cannot identify those internal subtypes
reliably. Only the exact leading artifact tool is labeled `artifact-worker`.

## Research and experiments

- 2026-10-30: inspected baseline
  `7dd2407848979d58f0e9b2bd75101a2dbfa7d1bc`. The summary already aggregates
  failures, slowest tests, coverage, dependencies, and available history.
  Aggregated failures unnecessarily repeat per-module slowest lists, and timing
  entries omit outcomes. `TestDurationTicks` is the sum of test durations.
- The acceptance hosts register `SlowestTestsConsumer`, which captures only
  passing tests and prints unqualified five-decimal seconds. Both hosts print
  `Process started: <count>` on successful exit, including artifact workers.
- Existing PR [#12139](https://github.com/microsoft/testfx/pull/12139) changes the
  shared display-name formatter and GitHub Actions rendering. This change avoids
  editing that formatter or those renderers; shared schema changes must retain
  the GitHub Actions consumer's behavior.
- Baseline toolchain bootstrap installed the repository-pinned
  `12.0.100-alpha.1.26509.101` SDK. The batch wrapper/runtime installer could not
  resolve `powershell` in this host environment. Invoking `eng/build.ps1`
  bootstrapped the SDK; subsequent explicit SDK MSBuild commands and the
  repository's PowerShell runtime installer avoid that shell lookup defect.
  This is tooling setup, not regression evidence.
- The native pinned SDK selected the existing per-module fallback in both
  variants. It did not negotiate required artifact post-processing. Therefore a
  native run is evidence for the fallback and exit/count preservation, not proof
  that this SDK executed the aggregate worker.
- The frozen main SDK at
  `4622ed3b6b3d58465613c5ac0a11243d40f61107` was then tested through both solution
  and direct-module entry points. Both retain per-module Azure DevOps summaries.
  Its owner confirmed that reply field 19 (`RequiredPostProcessingSupported`),
  mandatory one-input processing, and authoritative `runSummary` are absent.
  This is an existing SDK contract gap, not permission to force a capability
  bit. A separate dependent SDK change must implement and validate that contract.
- The same frozen SDK genuinely merged two TRX files from the actual acceptance
  hosts, exercising their artifact-worker entry point. The zero-result scenario
  retained exit 8 in both variants: the old worker printed `Process started: 0`;
  the updated worker prints its module, framework, PID, and `artifact-worker`
  role. Zero-result runs are observable process/artifact evidence, not executed
  regression-test evidence.
- Captured the original packed aggregate renderer on fixed two-module records
  before implementation: it rendered twenty timing rows (ten repeated under the
  failed module and ten globally), no outcomes, and no skip-reason context.
  The updated renderer produces ten rows, includes the failed timing, and reports
  grouped skips with platform/module identity. This renderer experiment supplies
  synthetic record input; it is not an SDK or performance benchmark.
- The source-linked standalone timing fixture published a 20-second failed
  result and passing results of 12 and 1.25 seconds. Before: the failure was
  absent and passing times had five decimal places. After: the failure ranks
  first, every result names its outcome, and the heading identifies the module
  and target framework.
- A new seven-argument internal `TestRecord` constructor carries skip reasons.
  The redundant optional default on the six-argument overload was removed to
  satisfy RS0060; its existing five-argument overload preserves omitted-argument
  call behavior. Both linked consumers' internal API baselines are updated.

## Alternatives and decision

- No change leaves ambiguous timing and process statistics.
- Enabling per-module top-ten console output across the matrix adds noise and
  duplicates the existing job-wide aggregate. Reject that approach.
- Reuse the current summary and optional fragment metadata. Bound skip groups
  and timing entries, make omitted/missing information explicit, and retain
  the standalone fallback for local acceptance runs.
- Do not infer platform regressions or historical baselines from different
  operating-system test totals.

## Validation and demonstration

Before/after captures use real packed reporters and the native pinned SDK, with
two deterministic framework fixtures publishing known durations/outcomes.
These fixtures exercise reporting, not test or machine performance. Capture
package hashes and resolved consumer assets separately for each variant.
Keep raw logs/binlogs and temporary fixtures in session artifacts; include
portable transcripts, exact commands, counts/exits, and revision-bound
red/green assertions in the PR.

Executed native-SDK fixtures on Windows/x64 with packed platform and Azure DevOps
reporting version `2.6.0-dev`, targeting `net8.0`:

| Variant / mode | Reported tests | Passed | Failed | Skipped | Parent exit |
| --- | ---: | ---: | ---: | ---: | ---: |
| Original / intentional failure in Beta | 32 | 25 | 1 | 6 | 2 |
| Updated / same intentional failure | 32 | 25 | 1 | 6 | 2 |
| Updated / all runnable tests pass | 32 | 26 | 0 | 6 | 0 |
| Updated / zero-result selection | 0 | 0 | 0 | 0 | 8 |

These are actual product outcomes, not outer regression-test counts. Zero
selection proves the expected product exit only; it is not test-execution proof.
Separate before/after NuGet caches prevent reuse of equal-version stale packages.
The updated reporter DLL hash matches the `.nupkg` entry, resolved cache, and
consumer output; host tracing confirms that output DLL is loaded.

### Regression sensitivity

The same tests and assertions are retained in an isolated comparison based on
`7dd2407848979d58f0e9b2bd75101a2dbfa7d1bc`. This is a **buildable
counterfactual**, not an unmodified checkout: additive model/constructor and
metadata-construction support stays present so the new malformed-fragment
fixtures can execute. Rendering, lifecycle capture, and reader validation use
their original behavior. The local timing consumer restores passed-only,
five-decimal output and ignores summary mode; process formatting restores the
unqualified count.
The retained counterfactual patch (including identical tests and support code)
has SHA-256
`E53787B6BD22500CA24A69FFD4BE37A0B64A3558459A77BC246350DE9102D9E9`.

| Contract / representative test | Without behavior | Correct implementation |
| --- | --- | --- |
| Timing, skip context, history, metadata guards: `BuildAggregateMarkdown_RendersOneGlobalTopTenWithOutcomesAndElapsedTime`, `SessionFinishing_GroupsSkipExplanationsWithoutListingSkippedNamesAsync`, `ReadAndAggregate_InvalidDiagnosticMetadataIsRejectedByBothProvidersAsync` | 263 executed; 35 intended assertion failures; exit 2 | Same 263 passed; exit 0 |
| Standalone timing and process context: `ConsumeAsync_RecordsEveryTerminalOutcome`, `CommandLineConstructor_DisablesOnlyWhenAzureDevOpsSummaryIsSet`, `FormatProcessSummary_AttributesModuleFrameworkProcessAndRole` | 30 executed; 23 intended assertion failures; exit 2 | Same 30 passed on .NET 8 and .NET Framework 4.6.2; exit 0 |
| Packed producer: `PackedStandalone_ReportsOutcomesSkipReasonsAndSummedTestTime`, `FixtureSdkWithoutRequiredArtifactSupport_PreservesPerModuleFallback` | Both outer tests reach the intended Markdown equality failure; child exits remain 2 | Both outer tests passed; child exits remain 2 |

No cases were skipped in these regression selections. The original reporter
selection passed 215 existing cases before the new regression expectations.
The full declared extension and MTP unit target frameworks compile with warnings
treated as errors, and both acceptance entry points compile.

The first counterfactual attempt removed metadata construction too broadly,
which prevented malformed-fragment test setup; that attempt is not red proof.
The final comparison retains that construction. Copies preserve timestamps, so
comparison source changes are rebuilt with `Rebuild`, not trusted to an
incremental build. Missing comparison SDK layout and Windows temporary-directory
cleanup failures were corrected as environment setup, never counted as
behavioral failures. Tests use task-owned temporary storage and preserve the
existing method-level/CPU-count scheduling.

## Open questions

This is a `main`-branch reporting improvement. No capability bit or downstream
feature flag is enabled here. Per-module fallback and real TRX-worker diagnostics
are validated, but job-wide Azure DevOps aggregation remains blocked by the
separate SDK contract work described above. Servicing branches and other
operating systems have not been validated; no backport is claimed. This PR's
owner retains the exact package snapshots for the coordinator's composed run.

## Authoritative policy follow-up

The required-contract SDK composition exposed a previously hidden distinction:
an invocation can retain genuinely failed tests while `--ignore-exit-code 2`
makes its authoritative final runner exit zero. The existing Azure regression
deliberately requires a failed test-result indicator in this situation; preserve
it rather than making failed tests appear green. GitHub's aggregate heading
already follows the runner exit and is not being changed.

Render the authoritative runner-policy outcome and exit separately, including
zero, in both aggregate reports. Keep raw counts, failure details, and partial
warnings. Do not invent a policy verdict when authority is absent.

The GitHub option documentation (`GitHubActionsResources.resx` and `PACKAGE.md`)
says `on-failure` writes to `GITHUB_STEP_SUMMARY` only when the test invocation
fails. The final integration decision preserves the existing visibility of failed
tests and partial data even when runner policy accepts the invocation. This
supersedes an intermediate proposal to make the writer gate exit-code-only.
Standalone artifact generation remains separate, and both forms retain raw
failure details and partial markers. Add explicit runner-policy context rather
than suppressing producer fragments, summaries, or test outcomes.

Freeze the original three-package feed at
`0269e3cb4724521dfde20cdd10ea9ea8a87f57cf`, add focused authority/legacy/partial
and writer-versus-artifact cases, and compare the old and corrected packages
through the same real capable SDK. The coordinator retains ownership of the SDK
contract; this follow-up stays in the independent reporting PR.

The focused `CiRunSummaryPolicyTests` selection contains twelve cases. With
the final tests retained against the isolated pre-correction revision, seven
fail at missing runner-policy assertions and five legacy characterization cases
pass. The corrected implementation passes all twelve on net8.0 and net462, and
the combined reporter selection passes 275 cases. The original Azure test
requiring a failed test-result indication with an accepted runner exit remains
unchanged and passes. No on-failure writer gate, fragment capture, or raw outcome
calculation was modified.
