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
- 2026-10-30, review comment 4423698557: the standalone consumer currently
  suppresses timings whenever `--report-azdo-summary` is present, but
  `AzureDevOpsSummaryReporter` writes no summary unless `TF_BUILD` equals `true`
  using `StringComparison.OrdinalIgnoreCase`. Match both gates before suppressing
  the fallback. Preserve the public constructor and inject the raw environment
  value through an internal overload for constructor tests; this avoids adding
  a service dependency or mutating process-wide environment variables in the
  method-parallel unit suite. Keep reporting, counters, and exit policies unchanged.

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

### Review correction: summary-option fallback

Review comment 4423698557 was accepted against
`5e8d17e9263e6abe947a58b87017d155a44c60d6`. The local timing consumer now
suppresses output only when the summary option is set and `TF_BUILD` equals
`true` ordinally ignoring case, matching the summary reporter. Its public
constructor signatures and the parameterless always-enabled behavior are unchanged.

`CommandLineConstructor_DisablesOnlyWhenAzureDevOpsSummaryIsActive` now has
eleven deterministic cases: `true`, `True`, and `TRUE` enable suppression;
missing, false, empty, invalid, and padded values retain local timings; an
absent summary option retains timings both inside and outside Azure DevOps.
The tests inject the raw value, never change the process environment, and run
with the existing method-level parallelization (32 workers on this host).

| State / target | Executed | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: | ---: |
| Original tests, net8.0 | 20 | 20 | 0 | 0 | 0 |
| Corrected tests and helper, net8.0 | 29 | 29 | 0 | 0 | 0 |
| Corrected tests and helper, net462 | 29 | 29 | 0 | 0 | 0 |
| Isolated option-only suppression, net8.0 | 29 | 24 | 5 | 0 | 2 |
| Same isolated comparison with the gate restored, net8.0 | 29 | 29 | 0 | 0 | 0 |

This is a buildable behavior-removal comparison, not a pristine baseline:
the injection overload stays present while only the environment gate is removed.
The five failures are the missing, false, empty, invalid, and padded cases,
each asserting enabled `true` but observing `false`. The final test source is
identical in both variants, SHA-256
`F02A6F5B338A38DBD92141BC41889A0983845C860315E505D26792D3DFA1439E`.
The corrected and counterfactual helper hashes are respectively
`39C34F1BE612647E91843CE02E9155BC601F9E2C33C2C1E650E0FA8CED81273B` and
`9171E10F65DD13B3FB91210A9D1D3DC75AE8367FECE5183A295D595F62B0BEB8`.

Validation uses the pinned SDK, Debug configuration, and process-scoped
`TEMP`/`TMP` under `artifacts\tmp\timing-review-4423698557`. The unit project builds
with `/warnaserror` for net8.0, net9.0, net10.0, net11.0, and net462; the owning
test-infrastructure project builds for net12.0. Representative exact commands:

```powershell
.\.dotnet\dotnet.exe msbuild .\test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj /t:Build /p:Configuration=Debug /p:UseSharedCompilation=false /nr:false /v:minimal /warnaserror '-bl:artifacts\timing-review-4423698557\all-target-build-{}.binlog'
.\.dotnet\dotnet.exe .\artifacts\bin\Microsoft.Testing.Platform.UnitTests\Debug\net8.0\Microsoft.Testing.Platform.UnitTests.dll --filter 'FullyQualifiedName~Microsoft.Testing.Platform.UnitTests.TestInfrastructure.SlowestTestsConsumerTests' --report-trx --report-trx-filename green.trx --results-directory .\artifacts\timing-review-4423698557\green --progress off --show-test-results all
.\artifacts\bin\Microsoft.Testing.Platform.UnitTests\Debug\net462\Microsoft.Testing.Platform.UnitTests.exe --filter 'FullyQualifiedName~Microsoft.Testing.Platform.UnitTests.TestInfrastructure.SlowestTestsConsumerTests' --report-trx --report-trx-filename net462.trx --results-directory .\artifacts\timing-review-4423698557\net462 --progress off --show-test-results all
```

All three commands exit 0. Logs, parsed TRX results, hashes, and comparison
sources are retained under the task-owned ignored
`artifacts\timing-review-4423698557` directory. The isolated comparison needed
explicit output overrides and the hash-verified existing runtime dependency
layout; its initial incomplete-layout launch failure is not red evidence.
No shipping package, reporter lifecycle, result counter, or exit policy changes,
so this non-shipping helper correction does not require repacking products.

## Open questions

This is a `main`-branch reporting improvement. No capability bit or downstream
feature flag is enabled here. Per-module fallback and real TRX-worker diagnostics
are validated. The originally missing SDK contract was subsequently implemented
and exercised in the real composed runs below; rollout still requires that
companion SDK implementation. Servicing branches and other operating systems
have not been validated; no backport is claimed. Exact package snapshots are
retained for reproducibility.

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

## Real required-worker composition

The final same-SDK comparisons use the frozen, committed
`dotnet/sdk` build `6fc4e43c66eb03fb2383ab25a809ff7f4dda81aa`
(parent `f5838f51c497a70fb60f3ed7e89102ca293b6838`), version
`12.0.100-dev`, through its managed CLI and actual shipping layout. Its managed
CLI SHA-256 is
`852D141F067C6E2021F194A76DEFFFA67868E6B21F9F6565EB8064286C8354CF`.
The kit was used read-only. Independent package caches and byte comparisons
verify each packed DLL against both executed modules and its resolved cache.

The deterministic Alpha/Beta fixtures each publish sixteen results. Reported
durations are fixture inputs, not a benchmark. An identical observation-only
hook in both variants copies the actual SDK-produced worker manifest before
starting the platform tool; it does not supply or alter the manifest,
capability negotiation, policy, results, or post-processing.

| Comparison | Verified result |
| --- | --- |
| Original `7dd2407848979d58f0e9b2bd75101a2dbfa7d1bc` versus implementation `4d10341bf973cb94adcdab8e150f18431b07c04f` | One actual job-wide Azure artifact in each run; 32 reported, 25 passed, 1 failed, 6 skipped, two modules, parent/manifest exit 2. Timing rows decrease from 20 to 10; failed timing and grouped skip context remain. |
| Policy baseline `0269e3cb4724521dfde20cdd10ea9ea8a87f57cf` versus `4d10341bf973cb94adcdab8e150f18431b07c04f`, `--ignore-exit-code 2` | Both retain 32/25/1/6 and parent/manifest exit 0. Azure keeps its failed test indication; GitHub keeps its accepted global indication and failed module detail. The corrected Azure artifact, GitHub artifact, and job summary explicitly show accepted runner policy and exit 0. |
| Same ignored failure with `--report-gh-step-summary on-failure` | Job-summary visibility, standalone artifacts, raw failure details, and counts remain intact in both variants. No failure is rewritten or hidden. |
| Successful candidate with `on-failure` | 32 reported, 26 passed, 6 skipped, parent/manifest exit 0; standalone artifacts exist and the job-summary file is absent. |
| Zero-result candidate | Zero reported results, two modules, parent/manifest exit 8; required artifacts are still produced. This is a product-policy check, not regression-test execution evidence. |
| Candidate stopped at one failure after Alpha completes | Parent/manifest exit 13, `maximumFailedTests` truncation, two-module denominator and one completed module's two provider fragments. Both real aggregates and the job summary retain partial-data warnings. |

The initial concurrent stop-policy probe terminated both producers before any
fragments were retained, so it produced no worker artifact and is not partial
rendering proof. The final partial probe uses
`--max-parallel-test-modules 1` solely to arrange a completed module before the
stop; repository test scheduling is unchanged.

These runs close the earlier global-aggregation evidence gap. Earlier
renderer-only and unsupported-SDK captures remain labeled as such rather than
being retroactively presented as composed execution.

## Latest-base reconciliation

Merged `origin/main` at `a0fcc2da5d7911dae128795184f99165f3332cae` in merge
commit `72a3bc87add2b08c153e8eb56ad2a3631170e33c`, preserving published history.
The single textual conflict was the GitHub reporter's internal API baseline;
the resolution retains both the skip-reason APIs and main's display-label
overload. The incoming data-row snapshot was reconciled with the separate
runner-policy line without removing its data-row assertions.

The merged tree passes 298 reporter/policy/display-name cases and 38
serializer/acceptance-diagnostic cases. The three shipping packages were
repacked from the merge commit into a new immutable feed; nuspec source stamps,
resolved cache DLLs, and both actual module outputs were compared byte-for-byte.
The same capable SDK reran normal failure and ignored-failure aggregation:
32/25/1/6 and a two-module denominator are preserved, parent/manifest exits are
2 and 0 respectively, the Azure global timing bound is ten, and both providers'
standalone artifacts and existing on-failure detail remain correct. Earlier
frozen package snapshots remain unchanged.
