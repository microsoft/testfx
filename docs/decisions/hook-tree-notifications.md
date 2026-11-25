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
- TRX's flat, separate cleanup row is implemented and covered. JUnit, Azure
  DevOps, GitHub Actions, retry orchestration, and live IDE grouping still need
  consumer-specific validation before broader rollout.
- Setup failures and per-test hooks remain out of scope; their failure identity,
  test-outcome, and retry semantics need a separate decision if this experiment
  expands.
- No release branch or downstream rollout is selected. Choose the shipping
  branch and decide servicing/backports before enabling this in a release.
