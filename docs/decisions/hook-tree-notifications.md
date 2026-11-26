# Hook tree notifications: research and decisions

## Problem and contract

Experiment with reporting MSTest class/assembly cleanup failures as non-test
nodes in the native Microsoft.Testing.Platform (MTP) execution tree, while
preserving a flat projection for clients that do not declare tree support.
MSTest owns surfacing the cleanup failure; test authors should not need to
manufacture a failing test to make teardown errors visible.
Existing test identities and outcomes must not change in the tree-aware view;
a cleanup failure must fail the run without changing test outcomes or the
canonical test count. Flat clients and test-only report formats may render a
fixture failure as a separate action/result row so the failure is not lost.
For a long-lived server-mode host, the failed request is conveyed through its
test-node update; an orderly server shutdown still exits successfully. A
standalone runner reports the fixture failure with
`ExitCode.AtLeastOneTestFailed`.

The first experiment is limited to MSTest class/assembly cleanup failures in
the native MTP path and MTP's JSON-RPC client boundary. It does not change
class/assembly initialization semantics, per-test hooks, the VSTest adapter,
Visual Studio or VS Code Test Explorer, or dotnet/sdk. Existing MTP reporters
and report formats consume the canonical event stream; flat formats may
represent a stateful fixture group as a result row, while MTP's process and
server-mode test statistics must not count it as a test.

Producer projects are `MSTest.TestAdapter` (package
`MSTest.TestAdapter` 5.0.0-dev) and
`Microsoft.Testing.Platform` (package `Microsoft.Testing.Platform`
3.0.0-dev), built together from this repository with the pinned SDK
`12.0.100-alpha.1.26570.125`. The packed TRX consumer also uses
`Microsoft.Testing.Extensions.TrxReport` 3.0.0-dev and
`MSTest.TestFramework` 5.0.0-dev. The adapter project targets the repository's
supported frameworks, with Windows-specific UWP/WinUI targets; MTP targets
those frameworks plus `netstandard2.0`. This prototype is branch-local; no
release branch or downstream rollout is selected.

MSTest is the producer of test and fixture identities and outcomes. MTP owns
the node model, result accounting, protocol projection, and built-in reporter
compatibility. A client opts into hierarchy support with an explicit
capability; an absent capability is intentionally equivalent to unsupported.
The legacy projection removes parent links and omits stateless group nodes;
stateful fixture groups are projected as flat action nodes with their identity,
display name, and outcome retained. This can appear as a test-like row to an
old client, but avoids losing a failure it cannot display as a group.

## Research and experiments

### 2026-11-25 — existing contract

- Issue [#4087](https://github.com/microsoft/testfx/issues/4087) requests hook
  diagnostics without dummy tests, count skew, or false failures.
- Related issues [#12469](https://github.com/microsoft/testfx/issues/12469)
  and [#7237](https://github.com/microsoft/testfx/issues/7237) retain the
  needs for individually visible non-test failures and describe MSTest's
  existing last-test cleanup attribution.
- `TestNodeUpdateMessage` already carries `ParentTestNodeUid`.
  `SerializerUtilities.TestNodeSerializers` currently emits `node-type: group`
  only when no state is present, and emits `action` for every stateful node.
- At the start of the experiment, `TestApplicationResult` and
  `PerRequestServerDataConsumer` counted every stateful result as a test. The
  native MSTest adapter publishes test nodes from `MtpUnitTestElementSink` and
  `MtpTestResultRecorder`; cleanup failures arrive as `TestResult` instances
  from the execution engine.
- `ClientCapabilities` already models optional declarations such as
  `ShowMessage`, which is precedent for capability negotiation. The server
  protocol already serializes the parent UID.
- Stateless node groups and stateful action results are consumed by the
  terminal reporter, report generators, `dotnet test` IPC, retry and
  max-failed-test extensions, and the JSON-RPC server consumer. The POC must
  preserve flat result visibility while keeping fixture failures out of
  canonical test counts and failure-budget accounting.
- The repository was clean at the start of the experiment. The prototype now
  adds fixture classification, canonical group nodes, a capability-gated
  legacy projection, and separate TRX rows; it remains uncommitted and
  unshipped.

## Alternatives and decision

- **Keep the current flat results:** no compatibility risk, but cleanup errors
  remain attached to or represented as tests and cannot be shown in their
  owning scope.
- **Send tree nodes without a compatibility projection:** simplest producer
  change, but old clients may misinterpret group outcomes or lose the parent
  relationship.
- **Chosen experiment: produce one canonical tree and project at the client
  boundary.** Tree-aware clients receive groups and parent links; clients that
  do not declare support receive a flat projection. An explicit group marker is
  required for stateful fixture nodes because the current JSON-RPC serializer
  infers `group` only from a missing state. The client capability remains
  presence-aware; absent or false means legacy projection.
- Keep cleanup classification internal to MSTest's neutral result model. The
  result crosses .NET Framework AppDomain boundaries, so any added marker must
  be optional when deserializing older results. Setup failures remain in their
  existing per-test shape in this experiment rather than changing user-visible
  skip/failure semantics without dedicated evidence.

The decision is deliberately reversible and limited to the native MSTest/MTP
path. It must not be described as shipped IDE support: the IDE clients and
dotnet test SDK live outside this repository. Reconsider the fallback if a
real client cannot display the flat action projection without changing test
selection, retry, or result identity.

## Validation and demonstration

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Tree-aware clients receive hierarchy for class and assembly cleanup | `ClassCleanupFailure_PreservesTestOutcomeAndProjectsForClientCapability (True)` and `AssemblyCleanupFailure_PreservesTestOutcomeAndProjectsForClientCapability (True)` passed. Both preserve the test outcome and link the failed cleanup node to its group. | Test Explorer (VS and VS Code) is external and unavailable here; actual IDE rendering remains unverified. |
| Clients without declared hierarchy support receive the legacy projection | Both class- and assembly-cleanup cases passed with capability `(null)` and `(False)`. They assert a flat failed action with no parent links or stateless group nodes. | No old IDE client is included; payload-level compatibility only. |
| Flat TRX retains distinct class and assembly fixture failures | `ClassCleanupFailure_ProducesSeparateFailedTrxResult` and `AssemblyCleanupFailure_ProducesSeparateFailedTrxResult` passed against the packed consumer. Each verifies the test and cleanup rows, identities, outcomes, counters, and child exit code `AtLeastOneTestFailed` (2). | TRX is flat, so the fixture row is a separate result rather than a hierarchy node. Its visual presentation is not verified. |
| Fixture groups do not corrupt canonical test counts or retry/failure budgets | `ConsumeAsync_FailedFixtureGroup_IsIgnoredByRetryTracking` and `ConsumeAsync_FailedFixtureGroup_DoesNotSpendFailureBudget` each passed 1/1. With the respective fixture-group guard removed, each failed at its intended assertion; the original guards were restored and the tests passed again. | Full unit-test projects were not run; focused regressions only. |

Validation ran on Windows x64, Release, outer acceptance TFM `net12.0`, and
generated consumer TFM `net11.0`, using repository SDK
`12.0.100-alpha.1.26570.125`. The source is based on
`a3bab4d2a755bcf1e9ccb602e5c404d2fc2d9960` plus uncommitted changes. The
current source/test content manifest and package provenance are retained under
`artifacts\TestResults\Release\hook-tree-final-e2e-20261125-rerun`.
The 37-file source/test content manifest digest is
`748FE329EE8383523D1604E78863FBF9971AF7658A3267BD8BCDE114BD557FA4`;
the exact file hashes are in `source-test-manifest-current.json`.

- `.\build.cmd -pack -c Release -bl` — succeeded. The package archives used
  were `MSTest.TestAdapter` 5.0.0-dev
  (`78A15DD47F882B78B4FF1537A40D931DAE9BE7783C053A627AF1AB2B10094D4C`),
  `MSTest.TestFramework` 5.0.0-dev
  (`AF2870B7FC7BC7D6EA4731F098496C9811F32828641AC9FDD4C0F0894FEAFCC2`),
  `Microsoft.Testing.Platform` 3.0.0-dev
  (`F51F5949E414EC45E3234059D85E078339516EC4D2DEA66AAA3EBFDD5DCDB716`),
  and `Microsoft.Testing.Extensions.TrxReport` 3.0.0-dev
  (`3F17E6BB7AF24F769F64105EA6D3FE7407D3B98ED3152FCD40E3513E869A588F`).
- `.\.dotnet\dotnet.exe test test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -f net12.0 --no-build --no-restore --filter 'FullyQualifiedName~CleanupFailure'`
  — passed, 13 total, 13 passed, 0 failed, 0 skipped. The eight class/assembly
  capability and TRX cases are all included; the wider filter also includes
  other cleanup-failure acceptance tests. The execution log is
  `artifacts\TestResults\Release\hook-tree-final-e2e-command\execution.log`.
- `.\.dotnet\dotnet.exe test test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj -c Release -f net11.0 --no-build --no-restore --filter 'FullyQualifiedName~ConsumeAsync_FailedFixtureGroup_IsIgnoredByRetryTracking'`
  and `.\.dotnet\dotnet.exe test test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Release -f net11.0 --no-build --no-restore --filter 'FullyQualifiedName~ConsumeAsync_FailedFixtureGroup_DoesNotSpendFailureBudget'`
  — each passed 1/1. Their logs and the red/green counterfactual logs are in
  `artifacts\TestResults\Release\hook-tree-final-e2e-command` and
  `artifacts\TestResults\Release\hook-tree-fixture-guards-counterfactual`.
- Package provenance verified 8 generated `net11.0` consumer outputs against
  the shipping archives: all 48 selected runtime DLL payloads match byte for
  byte. The report is
  `artifacts\TestResults\Release\hook-tree-final-e2e-20261125-rerun\verified-package-provenance-current.json`.

The display-name experiment was reverted. Tests expecting a stable group label
first failed 6/6 against the original cleanup-specific labels. With the
temporary stable-label implementation, the six protocol capability cases
passed, but both TRX cases failed because TRX `UnitTestResult.testName` comes
from `TestNode.DisplayName`, while `TrxTestDefinitionName` only controls the
definition name. The original cleanup-specific display names are retained.
Whether clients tolerate multiple updates for one group UID with differing
display names remains an unverified consumer-contract question, not a proven
defect. Trial logs are under
`artifacts\TestResults\Release\hook-tree-display-name-red` and
`artifacts\TestResults\Release\hook-tree-display-name-green`.

### Test-value evidence

The relevant contract is that a class-cleanup failure has an identity distinct
from the test it follows, while the capability projection and TRX row preserve
the failure without corrupting the test result.

| Contract / test case | Counterfactual | Without behavior | Correct implementation | Evidence / gap |
| --- | --- | --- | --- | --- |
| Distinct cleanup identity and client projection; three capability data rows | On base `a3bab4d2a755bcf1e9ccb602e5c404d2fc2d9960` plus the feature changes, set `isFixtureCleanup = false` in `MtpTestResultRecorder.cs`. `behavior-removal.patch` SHA-256: `A15D2277E341330054F414BF964B52FCEA8536BBB60ABD5C807662915DEBFB9B`. | Executed: detected. All 3 rows failed at `Assert.AreNotEqual(testResult.Uid, cleanupFailure.Uid)` because both UIDs matched. | Executed: all 3 rows passed. | The Release repack used `.\build.cmd -pack -c Release -bl`; the acceptance project rebuilt successfully. The exact execution command is in `acceptance\counterfactual-execution-command.txt`; four generated `net11.0` consumers resolved 6 packages each (24 checks) and 16 output payload hashes matched their packed entries. The outer red TRX has 4 executed, 3 failed, 1 passed, 0 skipped, process exit 2. Later projection assertions are not reached in the red rows. |
| Separate flat TRX cleanup row | Same behavior-removal mutation. | Executed: survived; the TRX case passed, showing this TRX projection does not depend on the recorder classification. | Executed: passed; child TRX has one passed test and one failed `[CleanupTests ClassCleanup]` row, and exits 2. | This is an independent flat-report contract, not evidence that the mutation is detected by the TRX case. |
| Retry and max-failed budgets ignore fixture groups | Remove the respective `isFixtureCleanup` guards in `RetryDataConsumer` and `AbortForMaxFailedTestsExtension`. | Executed: `ConsumeAsync_FailedFixtureGroup_IsIgnoredByRetryTracking` failed because the fixture was counted as a test; `ConsumeAsync_FailedFixtureGroup_DoesNotSpendFailureBudget` failed because the failure budget was consumed. Each red test ran 1/1 and exited 2. | Executed: both tests passed 1/1 after restoring the guards. | Logs for both red and green runs are retained in `artifacts\TestResults\Release\hook-tree-fixture-guards-counterfactual`. |

The initial acceptance test iterations found and fixed a missing MSTest import
in the generated consumer, then corrected the expected cleanup message and TRX
display name. The earlier discovery attempts also used unsupported VSTest
`--filter` and an incomplete verifier invocation; neither is counted as
validation evidence. The two focused unit tests above passed, but the complete
unit-test projects were not run. IDE rendering, live downstream behavior, CI,
and release-branch validation remain unverified.

Parallel-safety preflight found no shared-state conflict requiring a lock or
scheduling change. The MSTest acceptance assembly uses method-level parallelism
with CPU-count workers; each case generates its own test asset, and cleanup
fault injection is an environment override on the launched child process. The
adapter's internal `TestContainer` unit runner is serial, while the affected
MTP unit tests use per-test state. The two focused unit tests passed; the full
test assemblies were not run. This remains a bounded, static preflight rather
than a claim of exhaustive parallel-safety verification.

## Open questions

- Visual Studio and VS Code Test Explorer clients are external to this
  repository; whether/when they advertise hierarchy support and how they render
  the legacy flat action remain unverified.
- TRX's flat cleanup row and retry-attempt merge are covered by packed
  consumers. JUnit, Azure DevOps, GitHub Actions, and live IDE grouping still
  need consumer-specific validation before broader rollout.
- Setup failures and per-test hooks remain out of scope; their failure identity,
  test-outcome, and retry semantics need a separate decision if this experiment
  expands.
- No release branch or downstream rollout is selected. Choose the shipping
  branch and decide servicing/backports before enabling this in a release.

### 2026-11-25 — retry orchestration follow-up

Review identified that excluding fixture groups from retryable test counts also
prevents the retry controller from retaining their failure. A cleanup-only run
can relaunch with an empty filter, and a later successful retry can mask an
earlier cleanup failure.

The retry extension's parent and child are the same packaged executable and
communicate over a private named pipe. Report a separate fixture-failure flag
with the session counts; do not add fixture UIDs to the retry selection or
test totals. A fixture-only failure ends retrying without relaunching; a mixed
run retries actual failed tests while preserving the fixture failure in the
final exit and summary. Unexpected child exits retain their existing meaning.
The same signal must support launchers whose OS exit code is not authoritative.
This is a fix to the existing native MTP retry path, not a change to setup
hooks, IDE support, or retry selection semantics.

The two new orchestration tests failed before this change (expected exit 2,
actual 0) and passed afterward. The expanded focused unit selection executed
36 tests, all passing:

```powershell
.\.dotnet\dotnet.exe test test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj -c Release -f net11.0 --no-restore --filter 'FullyQualifiedName~OrchestrateTestHostExecutionAsync_|FullyQualifiedName~RetrySerializersTests|FullyQualifiedName~ConsumeAsync_FailedFixtureGroup|FullyQualifiedName~ConsumeAsync_GroupWithoutFailure|FullyQualifiedName~NonAuthoritativeHandle' '-bl:artifacts\log\Release\hook-retry-final-unit.binlog'
```

The Release pack succeeded with zero warnings and errors using
`.\build.cmd -pack -c Release -bl`. The packed acceptance selection
`CleanupFailure_WithRetry_PreservesFailureAndOnlyRetriesTests` discovered two
rows. On the source state above the cleanup-only row passed, while the mixed
row failed: its parent exited 2 and the markers showed one fixture execution
and two flaky-test executions, but the final top-level TRX contained only the
passing flaky result. The failed cleanup row and original passing test were
present in the first attempt's nested TRX, not the top-level result.

The same packed acceptance selection was rerun with:

```powershell
.\.dotnet\dotnet.exe test test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -f net12.0 --no-build --no-restore --filter 'FullyQualifiedName~CleanupFailure_WithRetry_PreservesFailureAndOnlyRetriesTests' -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\hook-retry-review-acceptance-confirmed '-bl:artifacts\log\Release\hook-retry-review-acceptance-confirmed.binlog'
```

Exit 2: `RetryCleanupOnly` passed; `RetryCleanupWithRecoveringTest` failed at
the final TRX identity assertion, for 1 passed / 1 failed / 0 skipped. The outer
test runner still wrote its default `TestResults` reporter files despite the
requested results-directory property; these files were not used as proof.

At that point, the existing artifact-postprocessing API had a `RetryAttempts`
mode used by HTML, JUnit, and CTRF, but TRX advertised only `TestModules` and
its merger unioned result rows and summed counters. Adding retry-aware TRX
processing was therefore a user-visible report behavior, not a
configuration-only fix.

The packed acceptance failure exposes why a generic retry merge is not a safe
small extension here. The TRX producer writes a fresh `executionId` per result,
uses `testId` for the test UID, and uses `testName` for its display name; it
does not write a stable row identity for distinct data rows with the same UID
and display name. More importantly, timeout results are written with
`outcome="Failed"`; only the aggregate `Counters.timeout` distinguishes them
from ordinary failures. Once superseded attempt rows are collapsed, that
aggregate cannot identify which retained row was a timeout, so truthful
per-attempt outcomes and counters cannot be reconstructed for all supported
cases. Copying both attempt reports instead duplicates the original suite and
leaves the superseded flaky failure visible. The mixed-run assertion was kept
strict and exposed this gap; the follow-up decision below resolves it with
attempt-aware merging rather than weakening the assertion or copying both
reports.

That earlier failed run used a generated consumer on `net11.0`, MSTest
`5.0.0-dev`, and MTP/Retry/TRX `3.0.0-dev`; seven runtime DLLs matched the
then-current shipping package payloads by SHA-256. Its outer acceptance host
was `net12.0` and exited 2. Logs and reports are under
`artifacts\log\Release\hook-retry-acceptance-3.log` and
`artifacts\TestResults\Release\hook-retry-review-acceptance-3`.

The retained output from that earlier consumer at
`artifacts\tmp\Release\testsuite\Ku1IT\RetryCleanupAsset\bin\Release\net11.0`
was rechecked against that run's Release Shipping `.nupkg` payloads. Each
selected runtime DLL hash matched its package entry: MSTest.TestAdapter
`MSTest.TestAdapter.dll` and `MSTestAdapter.PlatformServices.dll`;
MSTest.TestFramework `MSTest.TestFramework.dll`; Microsoft.Testing.Platform
`Microsoft.Testing.Platform.dll`; Microsoft.Testing.Extensions.Retry
`Microsoft.Testing.Extensions.Retry.dll`; and Microsoft.Testing.Extensions.TrxReport
plus its Abstractions package. Package archive SHA-256:

| Package | SHA-256 |
| --- | --- |
| `MSTest.TestAdapter.5.0.0-dev.nupkg` | `EA0AFE91A28C189B6AC114429F1B70C0E911560D5BC23E7CBF465ABF725E5A0A` |
| `MSTest.TestFramework.5.0.0-dev.nupkg` | `EB7AB16784105D053687B6BA0C9D0E726CC4F83D69B16DBC2B406A27C4AEE316` |
| `Microsoft.Testing.Platform.3.0.0-dev.nupkg` | `D9756BFA2743F4D165E5BE33E3E60C266DDF5E9CF768FA60D14B337D4C75AB2E` |
| `Microsoft.Testing.Extensions.Retry.3.0.0-dev.nupkg` | `50B9D81ABF6A26B086BCA145CA96D3559B6C484F5257839A8B9340E9BF83FB5C` |
| `Microsoft.Testing.Extensions.TrxReport.3.0.0-dev.nupkg` | `99FC7D695CEBCBE2972285F5DB83EC90B1F47F173496DDD1B759615020D4E048` |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions.3.0.0-dev.nupkg` | `B3CD54C13609946205EDA71ED1A7E75BE807D02D74C3E05302ECCE0FEFB5CED0` |

| Runtime DLL / package entry | SHA-256 |
| --- | --- |
| `MSTest.TestAdapter.dll` (`MSTest.TestAdapter`, `lib/net11.0`) | `FD1615FD91CDC04B579EA0260F0390D5C040F215D9BB4E15801637AB18E69BC5` |
| `MSTestAdapter.PlatformServices.dll` (`MSTest.TestAdapter`, `lib/net11.0`) | `76BBAD88FD82964AC51BCD57A228C83E795145DCA477D45F6AD0F02E1C9FCB9C` |
| `MSTest.TestFramework.dll` (`MSTest.TestFramework`, `lib/net11.0`) | `A7D5230235E96AAA2D8226041FA5014747EF289688D8C7F31BA765FE05CE5028` |
| `Microsoft.Testing.Platform.dll` (`Microsoft.Testing.Platform`, `lib/net11.0`) | `8EBCA2462556244F72AF5B1B697AA335C0DE5673288C8493F73A870AECA9883F` |
| `Microsoft.Testing.Extensions.Retry.dll` (`Microsoft.Testing.Extensions.Retry`, `lib/net11.0`) | `240637323D6ED0BC52716969D1FEF56AF229D41B852EF5B7560BB923021C6391` |
| `Microsoft.Testing.Extensions.TrxReport.dll` (`Microsoft.Testing.Extensions.TrxReport`, `lib/net11.0`) | `BE65DDFE7F1133039F2C87228E0003005DB88FE372FE338E5B1EB8CF88E02230` |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions.dll` (`Microsoft.Testing.Extensions.TrxReport.Abstractions`, `lib/net11.0`) | `29223EE349E913F0AD00147EC5E44618ABA580BA3E4BF957F1308AE8287D9F7F` |

### 2026-11-26 — retry-aware TRX decision

The previous entry correctly identified that concatenating attempt reports
duplicates tests and leaves superseded failures visible, but its conclusion
that TRX needs a new row-identity protocol was too broad. The retry consumer
selects tests by UID, and `TrxReportEngine` derives `TestDefinition.id` from
that same UID. Therefore the compatible aggregation unit is the complete
`testId` group: keep every row in the latest attempt for a UID, preserve rows
whose UID does not appear in later attempts (including fixture failures), and
never pair data rows by display name. This preserves duplicate rows sharing a
UID within an attempt without requiring an additional identity field.

Timeout rows continue to serialize as `outcome="Failed"` for compatibility;
the producer's mapping is unchanged. The TRX summary separately carries the
timeout count. During retry merging, the selected result rows rebuild the
total, passed, failed, and not-executed counts, while timeout counts are
reconciled from the final attempt's summary and any explicit timeout outcomes.
The regression covers a recovered timeout and a retained timeout. No custom XML
metadata or new public API is required. The final attempt's run-level
diagnostics remain authoritative; retained fixture failures remain represented
by their own result rows and details.

The contract is limited to complete TRX artifacts produced by MTP's TRX
extension for each retry attempt. `RetryArtifactProcessor` supplies all
attempts in numeric order and only replaces the final artifact when every
attempt contributed one report. The generated MSTest consumer uses the
shipping `MSTest.TestAdapter`, `MSTest.TestFramework`,
`Microsoft.Testing.Platform`, `Microsoft.Testing.Extensions.Retry`, and
`Microsoft.Testing.Extensions.TrxReport` packages at the versions recorded
above. Release/net11.0 is the inner consumer and Release/net12.0 is the outer
acceptance host. No release branch or downstream rollout is selected.

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Cleanup-only runs do not relaunch with an empty retry selection and keep the failure row | `CleanupFailure_WithRetry_PreservesFailureAndOnlyRetriesTests(RetryCleanupOnly)` checks process exit, attempt markers, row identity/outcome, and exact result counters. | TRX is a machine-readable report; its XML identities, outcomes, and counters are the affected surface. No separate visual layout changes. |
| A successful retry does not mask an earlier fixture failure or duplicate test rows | `CleanupFailure_WithRetry_PreservesFailureAndOnlyRetriesTests(RetryCleanupWithRecoveringTest)` checks exact row cardinality, each result identity/outcome, cleanup details, and aggregate counters. | Same XML-level surface; hosted report UI rendering is not exercised. |
| Retries replace whole UID groups, preserving duplicate data rows and timeout accounting | `MergeRetryAttempts_ReplacesCompleteTestIdGroupsAndRebuildsCounters` checks retained non-retried rows, cleanup identity, two same-UID rows, stale attempt removal, `TestEntry`/definition references, and a recovered versus retained timeout. | Internal TRX structure is checked; third-party hosted UI rendering remains unverified. |

The producer-consumer decision is therefore retry-specific post-processing, not
changing module merge behavior: keep the existing module union unchanged, add
attempt-aware collapsing, retain selected result/entry/definition associations,
rebuild counters for the selected rows, reconcile timeout counters without
changing the producer's outcome mapping, and carry run-level diagnostics from
the final attempt only. The earlier unsupported-identity concern is resolved by
the retry UID contract; a retry attempt with a different set of rows for the
same UID is authoritative for that whole group.

#### Final validation — 2026-11-26

The final source is the dirty worktree based on `b67602b1b4ce48fe73acd7c16d3b9185bc0e42d2`;
the changes are local and uncommitted. Validation used Windows x64, pinned SDK
`12.0.100-alpha.1.26570.125`, Release configuration, and the inner generated
consumer on `net11.0` with an outer acceptance host on `net12.0`.

| Requirement / risk | Executed evidence |
| --- | --- |
| Retry orchestration and TRX unit regressions | `Microsoft.Testing.Extensions.UnitTests` Release/net11.0: 3,935 passed, 26 skipped, 0 failed (3,961 total), exit 0. This includes retry serialization/orchestration and retry-aware TRX merge tests. |
| Final consumer source compiles | Acceptance project Release/net12.0 build, exit 0, 0 warnings/errors. |
| Cleanup-only and mixed successful-retry scenarios | Discovery selected exactly `RetryCleanupOnly` and `RetryCleanupWithRecoveringTest`; the outer Release/net12.0 TRX reports both passed, 2/2, 0 failed, 0 skipped, exit 0. Each case asserted child exit 2 and a failed final summary. |
| Child result identity and truthful final TRX | Cleanup-only output has exactly `FixtureTest:Passed` and `[CleanupTests ClassCleanup]:Failed` (total 2, passed 1, failed 1); mixed output adds exactly `FlakyTest:Passed` (total 3, passed 2, failed 1). The cleanup row retains its failure detail. Markers show fixture cleanup runs once, and the flaky test runs twice only in the mixed case. |
| Test sensitivity | The earlier packed run without retry-aware TRX merging executed the same two rows: cleanup-only passed and mixed failed at the final TRX identity assertion (outer exit 2). The final packed run passes both with the assertion unchanged. This is a local behavior-removal comparison; it is not a pristine clean-base checkout. |

Commands and retained evidence:

```powershell
.\build.cmd -pack -c Release -bl
.\.dotnet\dotnet.exe build test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -f net12.0 --no-restore -bl:artifacts\log\Release\hook-retry-review-acceptance-compile.binlog
.\.dotnet\dotnet.exe test --project test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj -c Release -f net11.0 --no-build --no-restore --results-directory artifacts\TestResults\Release\hook-retry-review-unit-confirmed -bl:artifacts\log\Release\hook-retry-review-unit-confirmed.binlog
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --list-tests text -p:UsingDotNetTest=true -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= -bl:artifacts\TestResults\Release\hook-retry-review-acceptance-final\discovery.binlog -- --filter FullyQualifiedName~CleanupFailure_WithRetry_PreservesFailureAndOnlyRetriesTests
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\hook-retry-review-acceptance-verified2 -bl:artifacts\TestResults\Release\hook-retry-review-acceptance-verified2\execution.binlog -- --filter FullyQualifiedName~CleanupFailure_WithRetry_PreservesFailureAndOnlyRetriesTests
```

The Release pack, acceptance-project build, complete unit-test run, filtered
discovery, and packed acceptance run each exited 0. Discovery found the two
identities above; its log is
`artifacts\TestResults\Release\hook-retry-review-acceptance-final\discovery-filter.log`.
For the retained-output acceptance run, the process environment set
`Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup=0` and removed it
when the command completed. The outer TRX passed
`verify_execution.py`: planned/selected/executed/passed `2/2/2/2`, failed and
skipped `0/0`, process exit 0, run start
`2026-11-26T11:09:02.456151+00:00`, finish
`2026-11-26T11:09:40.150187+00:00`.
The outer TRX and command log are under
`artifacts\TestResults\Release\hook-retry-review-acceptance-verified2`; the
earlier red run is under `artifacts\log\Release\hook-retry-acceptance-3.log`
and `artifacts\TestResults\Release\hook-retry-review-acceptance-3`.

The generated consumer's `project.assets.json` files under
`artifacts\tmp\Release\testsuite\XLWa1\RetryCleanupAsset` and
`artifacts\tmp\Release\testsuite\s3tV1\RetryCleanupAsset` resolve the aligned
packages at `net11.0`: MSTest `5.0.0-dev`, MTP and Retry/TRX `3.0.0-dev`.
Their restore cache is the run-specific
`artifacts\tmp\Release\testsuite\G6Nht\.packages`; its parent cache directory
is removed by the acceptance fixture's assembly cleanup, while the generated
asset, restore metadata, output, and child TRX are retained for inspection.
`NuGet.config` maps the local shipping feed to
`artifacts\packages\Release\Shipping`. Each of the seven relevant consumer
assemblies below matched the corresponding `lib/net11.0` package entry by
SHA-256:

| Package archive | Package SHA-256 |
| --- | --- |
| `MSTest.TestAdapter.5.0.0-dev.nupkg` | `120451B21A0D0EBC240EE2BCADF79CA70C6A85FBACDF8499A21E71B0A32E8404` |
| `MSTest.TestFramework.5.0.0-dev.nupkg` | `EB7AB16784105D053687B6BA0C9D0E726CC4F83D69B16DBC2B406A27C4AEE316` |
| `Microsoft.Testing.Platform.3.0.0-dev.nupkg` | `E1D267BE776DEB48870C810890EB28CE8B107CCB576B8365CAA39292546CF3CD` |
| `Microsoft.Testing.Extensions.Retry.3.0.0-dev.nupkg` | `30EDB467942F9A9BB7129951C5697F60E32EDF87335441E1F51794D271341426` |
| `Microsoft.Testing.Extensions.TrxReport.3.0.0-dev.nupkg` | `B5813C7319260B3F68600F1F3998BA1AA217AEA281A2E6CA45A25D84A59F129C` |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions.3.0.0-dev.nupkg` | `D0F8E66A17321E2B9C8C783FE00EF459F750D320BD60586E24D9B20E66D9417B` |

| Consumer assembly | Package payload SHA-256 |
| --- | --- |
| `MSTest.TestAdapter.dll` | `E63CAA0C70A6F2D44C8B120201FBE234648BA1D44F32123E2F2C49EC12962B10` |
| `MSTestAdapter.PlatformServices.dll` | `76BBAD88FD82964AC51BCD57A228C83E795145DCA477D45F6AD0F02E1C9FCB9C` |
| `MSTest.TestFramework.dll` | `A7D5230235E96AAA2D8226041FA5014747EF289688D8C7F31BA765FE05CE5028` |
| `Microsoft.Testing.Platform.dll` | `4D09CA8BEDDDDF55362D3B4D72E230FD7DB06BBD9920514527056AD80B710D42` |
| `Microsoft.Testing.Extensions.Retry.dll` | `C776B2BA74245E58185ED752DED4988305B9766D5AA84DD9FE45A2B712D5366B` |
| `Microsoft.Testing.Extensions.TrxReport.dll` | `9213C21E105C7F4ABF04C5650EEEFD3C46A1783AAFC5B2C5D4D294704CC88FCD` |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions.dll` | `F91D7C38C133C31B2C9EB49049F81B3C15B00FF9D3E1AB5697809B8A92810BB3` |

The PR Windows Release path uses the affected-test command against
`TestFx.slnx`; that solution includes
`MSTest.Acceptance.IntegrationTests` (line 97). The main-branch collection and
the full-suite fallback also use `TestFx.slnx`. CI itself was not run, so
selection of these new cases by the PR's affected-test map remains unverified.

No hosted TRX UI or external IDE rendering was exercised; the affected
contract is the final TRX XML, whose identities, outcomes, and counters are
asserted both by the packed consumer test and by inspection of its emitted
reports. The PR targets `main`; no servicing branch or downstream rollout was
selected in this local fix.
