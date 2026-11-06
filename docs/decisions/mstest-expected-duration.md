# MSTest expected duration: research and decisions

## Problem and contract

The original per-assembly slowest-test request in [#3495](https://github.com/microsoft/testfx/issues/3495)
was delivered by [#9894](https://github.com/microsoft/testfx/pull/9894).
The separate [soft-budget feature](https://github.com/microsoft/testfx/issues/12245)
expresses an author's intended upper bound without requiring CI history.
The user requested issue triage and implementation for MSTest, not a new
cross-framework platform contract.

The initial API is `[ExpectedDuration(milliseconds)]`, a sealed, non-inherited,
method-only attribute with one positive integer upper bound, exposed through
`Milliseconds`. Invalid bounds throw `ArgumentOutOfRangeException`, following
the existing `RetryAttribute` constructor-validation convention.

The completed-result contract is:

- Unannotated tests retain their existing behavior.
- Compare each final, passed or failed result's existing `TestResult.Duration`
  with the budget. Equality is within budget; only a strictly greater duration
  produces a diagnostic.
- Report a warning containing the fully qualified method identity, the result's
  distinct display label when present, actual milliseconds, and the budget.
  Preserve the same warning in that result's test-context output for reports.
- Do not change outcomes, counters, result identities, duration values, exceptions,
  retry decisions, exit codes, cancellation, or timeout behavior. Skipped,
  inconclusive, timed-out, aborted, and infrastructure-error results do not receive
  an additional duration-budget warning.
- Use the existing result-duration boundary, not a second stopwatch. Ordinary
  execution includes per-test global/instance initialization, construction, body,
  cleanup, and disposal; assembly/class initialization and cleanup are outside
  this boundary. Data-driven execution applies the budget independently to each
  reported row. Custom test-method executors' reported durations are respected.
- Evaluate only the final retry attempt. Do not accumulate superseded attempts or
  backoff delays into the final attempt's budget.

MSTest owns the attribute and comparison. Its existing adapter message logger
owns warning delivery to VSTest and MTP; existing result output carries the
diagnostic into reports. There is no new MTP outcome, property, extension hook,
handshake, service, network dependency, or CLI option. Older adapters may ignore
the new attribute; using it requires aligned framework and adapter packages.

Budgets are opt-in, generous diagnostic expectations, not portable microbenchmarks.
Machine load, debugging, cold starts, and runtime differences remain relevant.
Beyond the explicitly requested issue triage, no release-branch backport, PR,
package release, or evidence upload is requested. This session owns the initial
implementation and its package-consuming validation.

## Research and experiments

### 2026-11-06: current implementation and delivery

- Baseline: `c67c555ad2e1e44683f7e492b735d39400d8af9b`, initially clean.
- Read the original issue and related progress/history proposals. Verified that
  #9894 merged and the current terminal reporter implements the requested list.
  Closed #3495 as completed and created native Feature issue #12245, preserving
  its template; read back both bodies and the original issue's completed state.
- `TestMethodInfo.InvokeAsync` assigns the measured duration after normal per-test
  cleanup. `TestMethodRunner` resolves data-row display names; `UnitTestRunner`
  subsequently combines retry attempts and identifies superseded results.
- `ITestContext.DisplayMessage` forwards through the existing adapter logger.
  `MSTestFrameworkHandle.SendMessage` maps warning messages to MTP's existing
  `WarningMessageOutputDeviceData`; VSTest uses its existing warning logger.
- `TestResult.TestContextMessages` already becomes per-result output in the
  adapter. Appending the warning there avoids a new result or protocol API and
  preserves row association in generated reports.
- `ReflectHelper.GetFirstAttributeOrDefault` uses the shared attribute cache.
  Reuse it instead of introducing another reflection cache.
- `IProgressEnricher` is only mentioned as future work in
  `SharedExtensionHelpers/SlowTestReporterBase.cs`; it is not an implemented
  contract to build upon.
- Started a focused baseline build through the repository build script, which
  bootstraps the pinned SDK without installing unrelated Windows SDK components:
  `.\eng\build.ps1 -restore -build -projects test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -c Release -bl /p:TargetFrameworks=net10.0`.
  The command failed because the global `TargetFrameworks` override also
  retargeted compiler generators and triggered framework-specific pruning
  diagnostics. This is not behavioral red-phase evidence.
- A second attempt used `TargetFramework=net10.0` and the pinned dotnet MSBuild
  engine, but its restore still inherited the target restriction and left the
  compiler generator without its `netstandard2.0` assets.
- Restoring the project graph without either target-framework override succeeded
  with zero warnings/errors. Subsequent targeted compilation uses `-build`
  without `-restore`, with `TargetFramework=net10.0`; every dependency retains its
  declared restore framework graph. No unrelated package or analyzer changes
  were needed.
- The source generator's `AttributeMaterializationHelper` reconstructs public
  attributes generically and honors `AttributeUsage.Inherited`; a special
  expected-duration generator path is unnecessary. Packaged execution remains
  the required proof.
- The first implementation invoked reporting at the end of
  `TestMethodRunner.ExecuteAsync`. A subsequent call-chain check exposed an
  incorrect assumption: `UnitTestRunner` invokes that method independently for
  every retry, and only `CombineRetryAttempts` marks superseded results. The
  initial placement warned about attempts that later recovered: all six packaged
  acceptance cases detected two unwanted 250-ms retry warnings.
  Keep the comparison on the existing method runner, expose its reporting step
  internally, and invoke it only after retry combination in `UnitTestRunner`,
  before assembly/class cleanup. Add real retry-execution coverage, not only
  pre-tagged result tests. Earlier package/demo runs are not final retry evidence;
  rebuild/repack both production states after correcting this placement.

## Alternatives and decision

| Alternative | Decision and reason |
| --- | --- |
| Existing top-N list or live slow-test notice | Retain unchanged. Ranking and in-progress diagnostics do not express a completed test's intentional budget. |
| Existing Azure DevOps duration history | Retain unchanged. Useful adaptive diagnostics, but unavailable locally or without sufficient history. |
| `[Timeout]` or performance assertions | Keep separate. They deliberately affect cancellation/failure semantics rather than issuing a soft diagnostic. |
| External configuration or custom extension | Viable for experiments, but requires matching/configuration conventions and separates the expectation from the test. |
| One MSTest attribute plus existing warning/output channels | Chosen: minimal public API, local/first-run usefulness, no new platform contract, and existing host/report transport. |
| New warning outcome, platform metadata, general extension hook, or duration ranges | Deferred. No concrete cross-framework consumer requires those contracts for this feature. |

Reconsider platform metadata only when a specific consumer needs machine-readable
cross-framework budgets or annotations that existing diagnostic output cannot
provide. That would require a separate compatibility and transport contract.

## Validation and demonstration

Tests must use independent, deterministic reported durations for threshold
boundaries and custom-executor cases rather than scheduling-sensitive sleeps.
Package-consuming tests must still invoke the real MSTest runner and execute test
bodies, preserving result identities, row labels, outcomes, and existing output.

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Over-budget result warns but remains passing | `Mtp_ExpectedDuration_PreservesWarningsAndResultAssociations` and `Vstest_ExpectedDuration_PreservesWarningsAndResultAssociations`: passing-only child exits 0 and retains its Passed result and diagnostic output. | Actual packed .NET 10 terminal demo, ANSI on/off, 80 columns by 24 rows, long label with `<`, `&`, and quotes; all four cases remain passing. |
| Below/equal boundary and unannotated compatibility | `ExecuteExpectedDuration_UsesStrictReportedDurationBoundary` checks one tick below/equal/above both 1-ms and 100-ms budgets; `ExecuteExpectedDuration_WithoutAttributeDoesNotWarnOrChangeOutput` protects opt-in behavior. Packed folded/unfolded rows independently cover the same boundary. | The actual demo's 500-ms, exactly-1000-ms, and unannotated-1500-ms results have no warning. |
| Outcomes, exclusions, and final retries | `ExecuteExpectedDuration_PreservesOutcomesExceptionsAndAuthoredOutput`, `ExecuteExpectedDuration_DoesNotWarnForSkippedTimeoutOrInfrastructureResults`, and `ExecuteExpectedDuration_DoesNotWarnForSupersededAttempts`; real retries finish at 50 ms or 150.125 ms after failed 250-ms attempts. All final GREEN. | Only final over-budget Passed/Failed results warn. Existing timeout/cancellation diagnostics remain authoritative; no new timeout mechanism is implemented. |
| Data-row identity and output association | Both packed host tests assert exact method/name/outcome tuples, distinct execution IDs, three unfolded IDs versus one folded ID, and each row's exact TRX context output. Mixed children retain 10 Passed, 1 Failed, and 2 NotExecuted leaf rows. | Long/escaped demo labels remain readable in rendered terminal text; no new report or IDE layout is implemented. |
| Reflection/source-generated consumers | All six host/mode cases execute: Reflection, SourceGeneration (Rooting), and AotSourceGeneration (ReflectionFree). Consumers use .NET 11 and C# 12; the outer acceptance host uses .NET 12. | ReflectionFree metadata is not a Native AOT publish. Hosted IDE/CI rendering and legacy SDK warning transport are not newly claimed capabilities. |
| Behavioral sensitivity | Identical tests and API retained in an outside-repository comparison; replacing the configured comparison maximum with `TimeSpan.FromTicks(long.MaxValue)` yields four intended adapter assertion failures and six intended packaged assertion failures. | Both full shipping packs succeed before execution; compile/setup failures are excluded from RED proof. |

### Executed commands and results

SDK: `12.0.100-alpha.1.26509.101`, Windows/x64, Release. The ordinary shipping
build covers the declared targets, including .NET Framework and UWP/WinUI;
runtime proofs here cover .NET 10 and .NET 11. `$S` below denotes task-owned
artifact storage. Each test selection was independently discovered and its
executed identities reconciled against fresh TRX, with no skipped outer tests.

| Command | Observed result |
| --- | --- |
| `.\build.cmd -pack -c Release -bl /bl:artifacts\log\Release\duration-pack-after-retry-fix.binlog` | Exit 0; zero warnings/errors; shipping package-content and metadata validation passed. |
| `.\.dotnet\dotnet.exe test --project test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj -c Release -f net10.0 --no-restore -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$S\final-green-framework" "-bl:$S\final-framework-green.binlog" --treenode-filter "/*/*/ExpectedDurationAttributeTests/*" --show-test-results all` | Exit 0; 1 selected/executed/passed. |
| `.\.dotnet\dotnet.exe test --project test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -c Release -f net10.0 --no-restore -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$S\final-green-adapter" "-bl:$S\final-adapter-green.binlog" --treenode-filter "/*/*/TestMethodRunnerTests/ExecuteExpectedDuration_*" --show-test-results all` | Exit 0; 6 selected/executed/passed. |
| `.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -f net12.0 --no-restore -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$S\final-green-acceptance" "-bl:$S\final-acceptance-green.binlog" --filter "FullyQualifiedName~MSTest.Acceptance.IntegrationTests.ExpectedDurationTests" --show-test-results all` | Exit 0; all 6 selected host/metadata cases executed and passed. Each passing-only child exits 0; mixed MTP children exit 2 and mixed VSTest children exit 1 for the intentional assertion failure. |
| `.\.dotnet\dotnet.exe test --project test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -c Release -f net10.0 --no-restore -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$S\runner-preservation" "-bl:$S\runner-preservation-green.binlog" --treenode-filter "/*/*/*Test*RunnerTests/*" --show-test-results all` | Exit 0; 42 TestMethodRunnerTests and 32 UnitTestRunnerTests passed, including the 6 new adapter cases. This adds 68 existing cases, not another 74 unique cases. |
| `python .github\scripts\test_check_localization.py` and `python .github\scripts\check_localization.py` | Exits 0; 41 guard tests pass and all 364 catalogs validate. |

The framework case
`Constructor_NonPositiveMilliseconds_ThrowsArgumentOutOfRangeException` protects
invalid bounds. `ExecuteExpectedDuration_FormatsFractionalMillisecondsWithCurrentCulture`
also verifies exact French decimal formatting. All three final module reports
and the 74-case preservation report pass the existing `verify_execution.py`
execution gate.

Acceptance runs explicitly set `MSTEST_ACCEPTANCE_SKIP_SOURCEGEN=0` and retain
generated assets for provenance. A new, marked, run-owned collection cache avoids
same-version package reuse. The effective `NuGet.config`, three metadata-mode
asset graphs, cached package metadata, child-recorded assembly locations, and
payload hashes bind the consumers to their corresponding freshly packed bits.
Neither compiler success nor discovery alone is counted as runtime evidence.

### Test-value evidence

Base commit: `c67c555ad2e1e44683f7e492b735d39400d8af9b`, plus the feature/test
changes in this delivery. The retained regression files are byte-identical across
the two production states. No assertions, filters, or data were weakened.

| Contract / test case | Counterfactual | Without behavior | Correct implementation |
| --- | --- | --- | --- |
| Configured comparison, preserved results/output, culture, final attempts: the six `ExecuteExpectedDuration_*` cases | Only replace the configured maximum with `TimeSpan.FromTicks(long.MaxValue)`; keep the API, orchestration, tests, and existing warning/report channels. | Executed: 6 total, 4 failed at missing-warning assertions, 2 unchanged negative cases passed, 0 skipped; exit 2. | Executed: all 6 passed, exit 0. |
| Real MTP/VSTest warning and report transport: both `*_ExpectedDuration_PreservesWarningsAndResultAssociations` methods in all three modes | Same behavior removal, separately rebuilt and repacked from the same full shipping entry point. | Executed: all 6 outer cases fail at the intended per-result output assertion, not setup or compilation. The passing child still executes `body:over-pass` and exits 0, but its warning is absent; outer exit 2. | Executed: all 6 outer cases pass, exit 0; original final outcomes, identities, output, and child exits remain correct. |
| Real retry finality | Earlier prototype emitted inside each `TestMethodRunner.ExecuteAsync`, before retry combination. | Executed: all 6 packaged cases detected two extra warnings from superseded 250-ms attempts. | Executed: all 6 pass after reporting moves after `CombineRetryAttempts`. |

Representative RED assertion:

```text
expected: TestContext Messages:
          body:over-pass

          Test 'DurationFeature.DurationTests.OverBudgetPass' took 150.125 ms, exceeding its expected maximum duration of 100 ms.
actual:   TestContext Messages:
          body:over-pass
```

Final package SHA-256 identities:

| State / package | SHA-256 |
| --- | --- |
| Correct MSTest.TestAdapter.5.0.0-dev | `35BD992F3FC05CD8032C00ED4A2D8A7099564260D8CA3F32F36AAB7004BE7CF6` |
| Disabled-behavior MSTest.TestAdapter.5.0.0-dev | `55937448B4AC3A078A6CB45D45532B6CFE3992E7B8C32DEBB7E0A745BF9A3815` |
| Correct MSTest.TestFramework.5.0.0-dev | `9B4467F98010AD8C49F5B76A117D2E537A1FF0BC738C8157992C643A81F6E9FB` |
| Comparison MSTest.TestFramework.5.0.0-dev | `AC14691B0385BC67C660D1D3B4FD7B7391208CB76D8BED75B4E6CEE3E2215284` |

The comparison patch SHA-256 is
`29CF4ED2CF320D262A730102A3C4BDD3824F05BF6787295397BDD0DF848675BA`.
The shared corrected `UnitTestRunner.RunSingleTest.cs` SHA-256 is
`393821CC7C3C25BBA9613DC2CA31F8F8DD16B88683559DA40B3465ECBE247BAC`.
Both states use MSTest 5.0.0-dev and MTP 3.0.0-dev from their own physical feeds.

Unsuccessful comparison setup is retained but excluded from RED proof:
local-clone origin metadata initially lacked PackageProjectUrl; standalone
project packing then encountered UWP-reference/package-layout constraints.
The same complete shipping entry point used for GREEN succeeds in the comparison.
An initial comparison test run also reused an older outer test binary after file
copying; explicit `--no-incremental` test builds corrected that before the
behavioral assertions above were recorded. No product or test assertion changes
were made to work around those setup failures.

### Rendered demonstration and CI scope

An actual package-consuming .NET 10 app executed four real bodies with
deterministic reported durations. The 1500-ms annotated result warns against a
1000-ms budget; 500-ms, equal-1000-ms, and unannotated-1500-ms results do not.
TRX preserves the matching labels, durations, Passed outcomes, body markers, and
warning association; exit 0, four passed, none failed/skipped. Consumed assembly
hashes match the freshly packed payloads.

The terminal canvas was inspected at 80 columns by 24 rows in ANSI and non-ANSI
modes using a long label containing markup characters and quotes. Rendered text
retains the complete label/warning and four passing results. The canvas cannot
resize its actual viewport through console-buffer APIs: a 120-column buffer
experiment did not resize the 80-column surface and was restored. Additional
viewport sizes, pixel/color screenshots, hosted CI UI, and IDE UI remain explicit
unverified surfaces; readable terminal output is the retained demonstration.

The owning projects are included in the existing full Release solution test
commands in `eng/pipelines/steps/test-windows-configuration-tests.yml` and
`test-non-windows.yml`, under their existing `SkipTests`/job gates. No dedicated
category or new test-registration list is needed. CI has not been run for this
local delivery; local execution is not CI verification.

Temporary logs, binlogs, package provenance, preserved comparison packages, and
test-pipeline state remain in task-owned artifact storage, not source control.
This record preserves the contract, exact reproductions, counts, identities,
and assertion excerpts without publishing private machine paths or raw logs.

## Open questions

- No blocking implementation decision remains for the bounded MSTest feature.
- Existing warning transport remains subject to each host/SDK's capabilities;
  no new IDE annotation or legacy SDK support is claimed.
- New structured cross-framework annotations, Native AOT publishing, extra
  rendered surfaces, rollout/backports, and release validation require separate
  scope if requested.
