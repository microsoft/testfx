# MSTest partition selection and MTP distribution: research and decisions

Status: selector and MTP-owned local fan-out implemented and locally validated
(2026-11-24). The follow-up below records the current distribution contract.
The original selector history later in this document predates fan-out and is
not evidence for it. Its initial scope was **only `--partition` at MSTest level**;
the renewed request extends that scope without replacing the selector.
Local implementation and validation do not imply a published release.

## Follow-up: MTP-owned local fan-out

### Shard reporting review (2026-11-25)

The Azure DevOps live publisher's result-id handoff was designed for sequential
Retry attempts. Shards instead run concurrent, independent Retry sequences.
Each publisher currently opens a private snapshot and replaces the same map;
an interleaving can discard another shard's result IDs and make its retry
publish duplicates. The run ID must remain shared, but each partition needs a
separate result map whose path stays stable across its attempts and is removed
when the owning orchestration finishes. The chosen approach derives a bounded
filename suffix from the forwarded numeric `--partition` selector in the
publisher and cleans only maps belonging to the owner's unique handoff path.
This keeps the coordination protocol inside the Azure DevOps extension rather
than teaching the platform about a particular reporter. Unpartitioned and
unrecognized selectors retain the existing map path. Before this change, the
new `PartitionedAttempts_InterleavedShardsUpdateTheirOwnPublishedResults`
unit test executed and failed: a retry created result 103 instead of updating
result 101 (one test executed, exit 2). With the change, the Azure publisher
and orchestrator behavior selection passed 49 tests, including the same
interleaving and owned-file cleanup cases:

```powershell
.\.dotnet\dotnet.exe artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\net10.0\Microsoft.Testing.Extensions.UnitTests.dll --filter 'FullyQualifiedName~AzureDevOpsOrchestratorBehaviorTests|FullyQualifiedName~AzureDevOpsPublisherBehaviorTests' --ansi off --progress off
```

In the dirty source state based on `b6881fecf1074638c26dfa8b5d524ac30ab430c0`,
`.\build.cmd -pack -c Release -bl` exited 0. The packed consumer ran on
Windows x64, net10.0, using Microsoft.Testing.Platform, AzureDevOpsReport, and
Retry `3.0.0-dev`, plus MSTest.TestAdapter and MSTest.TestFramework `5.0.0-dev`.
The generated host binaries matched the SHA-256 of their respective net10.0
shipping-package payloads. The parent invocation returned success; two
different shards published `FirstTests.Flaky` (failed then passed) and
`ThirdTests.Passing` (passed). The loopback service observed one completed
run, two original result IDs, and a retry updating the original flaky result
with both attempt outcomes. Both initial maps were saved before the retry and
the owner's coordination files were gone at completion.

The outer net12.0 acceptance host executed the following selection: three
tests passed, including the shard/Retry case and the existing reflection and
source-generation DataRow cases:

```powershell
.\.dotnet\dotnet.exe test --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release --no-build --filter 'FullyQualifiedName~AzureDevOpsLivePublishingTests.ShardsWithRetry|FullyQualifiedName~AzureDevOpsLivePublishingTests.DataDrivenRows' -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=<results-directory> --show-test-results all
```

The packaged acceptance case has not been run against the pre-fix product;
its regression sensitivity is therefore not established independently of the
unit red/green result. The Azure DevOps Tests tab, Unix, servicing branches,
and externally hosted CI remain unverified. A separate observed issue remains
open: the GitHub Actions reporter can mark empty shards as failed while the
aggregate invocation succeeds. Suppressing every zero-test exit would also
hide all-empty and minimum-test-policy failures.

### Naming decision: framework partitions, platform shards (2026-11-25)

The distribution option was renamed from `--partition-count` to `--shard-count`
before release. Terminology now separates ownership: a test framework
*partitions* tests (`--partition index/count`, `ITestPartitioningCapability`),
while the platform *shards* one invocation into child processes, each running
one partition. Alternatives considered included `--partitions`,
`--parallel-partitions`, `--fan-out`, `--process-count` and `--shard-count`;
the user chose `--shard-count` because it names the platform's role and follows
the existing MTP `--<noun>-<qualifier>` option style (`--maximum-failed-tests`,
`--minimum-expected-tests`, `--results-directory`). Platform-owned names follow
the rename: `ShardingOrchestrator`, `ShardingExtension` (Uid `TestSharding`),
`ShardingCommandLineOptionsProvider`, the `Shards/<run-id>/<index>` results
directory and the `[shard i/n]` console prefix. The framework-owned selector,
capability and their wire contract (`--partition i/n`) are unchanged.

Evidence sections dated before this decision ran under the previous names
(`--partition-count`, `PartitionCount_*` tests, `PartitioningOrchestrator*`,
`Partitions/` directory). Their commands and names below were mechanically
updated to the current names; their recorded outcomes were not re-executed
unless a later section says so.

### Contract and decision (2026-11-24)

The renewed requirement is to split one invocation into child invocations now
that MTP supports composable orchestrators. Preserve `--partition i/n` as the
manual CI selector and add opt-in `--shard-count N` for local distribution.
MTP owns argument forwarding, child scheduling, cancellation, results-directory
isolation and exit aggregation; MSTest still owns class/dependency assignment.
Initially only native MSTest registered the MTP distribution implementation;
this was later replaced by a public opt-in capability (see "Framework capability
gate" below). No separately shipped package is necessary.
Other frameworks must not silently accept an unsupported selector.

Assumptions, not additional confirmed requirements: one module is partitioned
independently; concurrency is bounded by the lesser of N and the logical CPU
count; reports remain separate rather than being merged across partitions.
Distribution runs outside Retry, so each partition independently retries its
own failed tests. Existing filters and policies apply in every child. Explicit
minimum-test limits are per child, not an aggregate minimum.

`--shard-count` and `--partition` are mutually exclusive when both are
explicitly supplied on the command line. If `--shard-count` is inherited
from configuration, an explicit `--partition` selector takes precedence; this
also prevents fan-out children from recursively distributing. Discovery does not
fan out and lists the ordinary unpartitioned selection. Distribution requires
the .NET 10+ platform implementation: its process wrapper can terminate child
trees, whereas the netstandard implementation only terminates direct children.
JSON-RPC/IDE server mode, WebAssembly, iOS/tvOS, hot reload and custom test-host
launchers are unsupported. A distribution child is always the same local
executable, with forwarded arguments, inherited environment and inner
orchestrator options. Preserve response-file backing where arguments can
round-trip through the existing parser; keep literal quotes and line breaks inline.
Segments preserve argument order even when other arguments exceed OS limits. Remove
the fan-out option to prevent recursion; replace results-directory and terminal
progress options. Each invocation gets a unique `Shards/<run-id>/<index>`
directory. Explicit absolute report filenames/external output paths are not
rewritten; use relative report names to obtain isolation. Child console lines
are serialized, prefixed with shard identity,
and use non-ANSI, noninteractive output. The orchestrator owns and joins all
children and output readers before returning. Output draining honors application
cancellation and fails after 30 seconds if a descendant retains an inherited pipe.

Normal native `dotnet test` handshakes remain per process. Direct invocation
requires no SDK handshake. A new orchestrator Uid must be exercised against the
shipping SDK before claiming CLI support. Cancellation stops scheduling and
terminates active child process trees; it returns the platform's abort verdict.
Any nonzero child other than ZeroTests fails the invocation (including unknown
native crash exits). A mixture of success and empty partitions succeeds; all
empty partitions return ZeroTests. Never inject `--ignore-exit-code 8`, because
that would conceal an entirely empty invocation.

Alternatives considered: external scripts (no product-owned lifecycle or
aggregation); moving discovery and UID replay into MTP (unnecessary discovery
process, and risks splitting data/fixture affinity); a new extension package
(extra package/registration contract without a second framework consumer).
The initial implementation kept the existing selector and put only distribution
in MTP. The user later requested a framework capability before another framework
adopts the contract; the decision below supersedes the initial decision against
adding that capability, while retaining the existing framework-owned selector.

Producer/consumer train: mainline MTP 3 / MSTest 5, aligned repository-built
Microsoft.Testing.Platform, MSTest.TestAdapter/PlatformServices, TestFramework,
Retry and TRX packages. No stable backport or downstream rollout is enabled.
The implementation owner retains integration/release-branch follow-through;
servicing requires a separate maintainer decision.

### Coverage plan recorded before implementation

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| One call executes disjoint partitions exactly once | Extend `PartitionTests` using the existing packed independent-class asset; assert union, UIDs, outcomes and separate reports. | Direct terminal transcript, normal and constrained width. |
| Defaults, filters, empty and failure policies | Existing selector tests plus fan-out filtered/all-empty and nonzero-child cases. | Empty/error diagnostics must remain attributable to their partition. |
| Retry stays inside distribution | Packed dependency asset with its existing deliberate first-attempt failure; inspect per-partition retry artifacts and identities. | Real SDK must tolerate per-partition attempt counters. |
| CLI/protocol compatibility | Real native `dotnet test`, discovery and help/info expectations. | SDK-specific rendering and legacy compatibility remain unverified until executed. |
| Cancellation and isolation | Cancel active children, join them, and verify no child work continues; independent results roots for concurrent modules. | No hosted UI change intended; reports are intentionally not merged. |

The planned rows above are not validation claims. The following records the
executed results and remaining gaps.

### Experiments and corrections (2026-11-24)

- Follow-up investigation (2026-11-25): an explicit manual selector must
  override a configured count before distribution-only runtime and server-mode
  restrictions are evaluated. SDK display forwarding also needs bounded
  cancellation when a peer reads a diagnostic but never acknowledges it. The
  data connection is request/reply serialized, so cancelling an in-flight pipe
  request must not let a later request consume its stale acknowledgement.
- Review follow-up (2026-11-25): preserve an existing consumed orchestrator
  chain and explicitly pass the distribution UID to standalone children. The
  argument filter now follows the parser's treatment of three or more leading
  hyphens as values. Child SDK diagnostics are forwarded via display messages
  when supported, with console fallback for older protocol versions. Segmented
  response files use private temporary files and are removed after the child,
  including cancellation and failures. The acceptance class remains nonparallel
  and uses per-test assets and per-run result paths.
- Review follow-up: the launcher must be built and checked before local
  distribution, and owned option names must be removed case-insensitively.
  Separate response-file segments preserve large arguments alongside literal
  newlines or quotes; keeping all arguments inline would reintroduce OS limits.
  The partition capability documents the existing one-based selector contract.
- A pack with `SkipUapTargetFramework=true` failed the Windows package-shape
  guard. Repacking without that suppression passed the complete shipping-layout
  and metadata checks; the earlier selector's partial-package limitation does
  not describe this follow-up's final packages.
- Current CLI validation rejects the older `--no-ansi` spelling. Distribution
  and the directly affected selector acceptance call now use `--ansi off`.
- Expanding a large response file into an inline Windows child command can
  exceed the OS limit. Rebuilding a child response file preserves a tested
  35,000-character filter. Literal quotes remain inline because the existing
  response-file parser strips them; a general parser redesign was not attempted.
- Two same-named `Acme.Tests.csproj` projects were rejected in one root solution
  folder. Separate solution folders enable the real two-module SDK experiment.
- Throwing for hot reload initially produced an unhandled CLR exit. The
  distributor now emits the normal error output and returns invalid-CLI exit
  `5`; both hot-reload activation variables execute this rejection.
- `--exit-on-process-exit` deliberately invokes noncooperative `Environment.Exit`
  with exit `11`; it is not an application-cancellation trigger. The cancellation
  fixture instead requests the real application token through test-only
  middleware, avoiding console signals to unrelated processes. Active child
  tests and their grandchildren deliberately ignore cancellation; the root
  returns abort exit `3` and the test joins every tracked descendant.
- Counterfactual setup first lacked repository metadata and a NonShipping
  dependency. Those pack/restore failures are not behavioral red evidence.
  Correcting the isolated setup permitted the unchanged exact-once test to
  execute and fail at its intended report-count assertion.

### Review-fix validation (2026-11-25)

Further review fixes were tested on `4b9930a2a1b4f9387740eb63baab5c9c54abddfa`
with production/test patch SHA-256
`E2A6C4BA56006B54909788045198C6662D16E90C3B06FF614EC356E7C484A026`.
The explicit selector now bypasses restrictions that apply only to distribution.
SDK display sends link the reader and application cancellation tokens. A
cancelled in-flight request makes its pipe unusable, because a partial request
or a late response cannot safely be associated with another request; cancelling
a request while it waits for the pipe lock does not poison the connection.
Client writes no longer synchronously wait for a Windows pipe drain: their
reply already establishes that the peer consumed the request. Server writes
retain their prior drain behavior.

- Before the fixes, `ConfiguredShardCount_ExplicitSelectorBypassesDistributionOnlyRestrictions`
  executed and failed its validity assertion (1/1 failed); the caller-cancellation
  row of `DisplayAsync_StalledDisplayAcknowledgementHonorsCancellation` failed
  its stop assertion (1 failed, 1 passed). The earlier pipe-reuse test failed
  because the second request timed out instead of being rejected. Its final
  shape additionally attempts a late reply; paired sensitivity for that final
  shape and for the undrained-write case remains unverified.
- `.\.dotnet\dotnet.exe build test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -f <tfm> -c Debug -v:q`
  followed by `.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -f <tfm> -c Debug --no-build -- --filter 'FullyQualifiedName~ShardingOrchestratorTests|FullyQualifiedName~DotnetTestPassthroughOutputDeviceTests|FullyQualifiedName~DotnetTestConnectionTests|FullyQualifiedName~IPCTests' --ansi off --progress off`
  exited 0 on net10.0 (146 passed) and net462 (142 passed), with no failures,
  skips or build warnings. After adding the late-reply attempt, the three pipe
  cancellation tests were rebuilt and rerun on net10.0: 3 passed.
- `.\build.cmd -pack -c Release -bl` exited 0 with no warnings. The
  `ShardCount_` packed acceptance selection, run using the net12.0 Release
  host and `--report-trx`, exited 0: 34 executed and passed, none skipped.
  It includes native SDK happy paths, two modules, configured count,
  cancellation and an inherited-pipe timeout. The native run used the repository
  SDK `12.0.100-alpha.1.26570.125`, not a newly validated released SDK.
  A generated net11.0 consumer's isolated cache contained the current MTP
  `3.0.0-dev`, Retry/TRX `3.0.0-dev`, and adapter/framework `5.0.0-dev` packages;
  their SHA-256 hashes matched the Release shipping packages. Its output MTP
  DLL and the cached net11.0 DLL both had SHA-256
  `C3C1E4FD915B02496508FADED8EB625C3C996FA428A16379A67A5FD114BA282E`.
  The MTP package SHA-256 was
  `5F383ACA8DB03AC681B1A1611FF401365916F83A0B36D6DBC3332D9E754ED2E8`.

These are local results, not CI results. Real SDK control-channel cancellation,
a real stalled SDK acknowledgement, Unix behavior and the remaining compatibility,
absolute-output-path and servicing gaps below remain unverified.

Further follow-up was run on Windows x64 with local changes based on
`cd40018e0174ed7fd3e3c27ef31de435991d42c9`, repository SDK
`12.0.100-alpha.1.26570.125`, net12.0 acceptance host and net11.0 generated
consumers. `.\build.cmd -pack -c Debug -bl` exited 0; the shipping
`Microsoft.Testing.Platform.3.0.0-dev.nupkg` SHA-256 was
`203271BF24BE3DCEA3ECED1A43CC0206713C49409120A50A5591C5747566E584`.

- `.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests -f net10.0 -c Debug --no-build -- --filter 'FullyQualifiedName~ShardingOrchestratorTests' --ansi off --progress off`:
  18 passed.
- `.\.dotnet\dotnet.exe run --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests -f net12.0 -c Debug --no-build -- --filter 'FullyQualifiedName~ShardCount_' --ansi off --progress off`:
  34 passed, including `ShardCount_LeadingHyphensInParameter_AreForwarded`,
  both `ShardCount_UnconditionalMiddleware_WrapsOnlyTheOuterProcess` rows,
  `ShardCount_NativeDotnetTest_PreservesChildStartupDiagnostic`, and both
  `ShardCount_ResponseFiles_ArePrivateAndRemoved` rows.
- Before the production fixes, these new argument, standalone middleware, SDK
  diagnostics and response-file location checks failed at their behavioral
  assertions. The response-file fixture was corrected after an initial run
  failed on duplicate terminal options, so that setup failure is not counted as
  regression evidence. Exact paired consumer package provenance is unavailable.
- Hosted CI exposed two assertions assuming a fixed set of built-in providers
  and diagnostic fixtures returning a null capability collection. After
  correcting those tests, focused net10.0 runs of
  `TestApplicationDiagnosticOptionsTests`, `RetryExtensionsTests`, and
  `BuilderServiceRegistrationTests` exited 0 with 13, 3, and 2 passes
  respectively, using `--filter 'FullyQualifiedName~<class>' --ansi off
  --progress off` with `dotnet run --no-build`.

Windows response-file permissions and cleanup were exercised; Unix permissions
and a real older SDK's console fallback were not. SDK control-channel
cancellation, shared explicit absolute output paths, and downstream
release-branch validation and servicing remain open. Local results are not hosted
CI results.

The review fixes were tested on base
`1349e60b01f97020ef732ebcfa3b7649b96d1395` with production/test patch SHA-256
`1241B4189A27C094B15E1B290C1B585C25534F50B0B5FA2DA9538535D703EB45`.
Windows x64 used repository SDK `12.0.100-alpha.1.26570.125`, the net12.0
acceptance host, and net11.0 generated consumers. `.\build.cmd -pack -c Debug
-bl` exited 0; `Microsoft.Testing.Platform.3.0.0-dev.nupkg` SHA-256 was
`588197A244066A281CC38E49AEE6B139B4E206FDC606DE9AE25551EB12D67AA1`.

The packed pre-fix run selected four cases: mixed-case option forwarding,
multiline response-file forwarding with a 35,000-character filter, and both
launcher states. Three failed at the intended assertions and the disabled
launcher passed. An earlier cancellation test also failed its bounded-exit
assertion with a descendant holding the pipe. That cancellation fixture was
subsequently extended to cover the uncancelled timeout, so exact paired evidence
for its final test shape is unavailable; the old package's resolved consumer
hash was not captured either.

After packing the fixes, the focused commands below exited 0:

- `.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests -f net10.0 -c Debug --no-build -- --filter 'FullyQualifiedName~ShardingOrchestratorTests' --ansi off --progress off`:
  17 passed, including mixed-case removal (the single-case pre-fix unit run
  failed with nine arguments instead of three).
- `.\.dotnet\dotnet.exe run --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests -f net12.0 -c Debug --no-build -- --filter 'FullyQualifiedName~ShardCount_' --ansi off --progress off`:
  27 passed, including enabled/disabled launchers, mixed-case and long/multiline
  arguments, cancellation and uncancelled output timeout, native SDK and
  multi-module cases. An earlier run required manually stopping its owned
  sentinel because the new test awaited output before cleaning it up; that
  run is not used as green evidence. The final run required no intervention.

The SDK control-channel cancellation, downstream rollout and release-branch
servicing gaps recorded below remain open. No hosted CI result is implied.

### Executed validation

Source state: local, uncommitted changes on base
`09c901458d491e05a8a2c9adc89ee6ddfea834cc`. The reproducible production/test/resource
patch, including all three untracked C# files, is
`artifacts/TestResults/Release/partition-green-final/tested-source.patch`,
SHA-256 `B606CDEED0FA93AE5AF576FC6D526387039B5F9B09B6FE45306CB9DD1895DB3E`.
Reverse-apply checking verifies that it matches the worktree; no source was
reverted. Documentation changes are deliberately outside this tested-source hash.
No commit, push, PR publication or CI result is claimed.

The pinned SDK was `12.0.100-alpha.1.26570.125`; outer acceptance targets
net12.0, generated consumers net11.0. Packages are aligned MSTest `5.0.0-dev`
and MTP/Retry/TRX `3.0.0-dev`. Executed pack commands:

```powershell
.\build.cmd -c Release -pack -bl
.\build.cmd -c Release -pack -projects src\MTP\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -bl
```

Both exited `0`. The first established the complete Windows shipping layout;
the second repacked the final core error-handling change against that aligned
set. Logs: `artifacts/log/partition-pack-verified.log` and
`artifacts/log/partition-final-core-pack.log`.

The executed acceptance commands used this process-local setup:

```powershell
$env:DOTNET_ROOT = Join-Path (Get-Location) '.dotnet'
$env:DOTNET_CLI_CONTEXT_VERBOSE = '1'
$env:Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup = '0'
$results = Join-Path (Get-Location) 'artifacts\TestResults\Release\partition-green-final'
& .\.dotnet\dotnet.exe artifacts\bin\MSTest.Acceptance.IntegrationTests\Release\net12.0\MSTest.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~ShardCount' --report-trx --report-trx-filename acceptance.trx --results-directory $results --ansi off --progress off > "$results\execution.log" 2>&1

$results = Join-Path (Get-Location) 'artifacts\TestResults\Release\partition-baseline-help'
$filter = '(FullyQualifiedName~PartitionTests&FullyQualifiedName!~ShardCount)|FullyQualifiedName~HelpInfoTests'
& .\.dotnet\dotnet.exe artifacts\bin\MSTest.Acceptance.IntegrationTests\Release\net12.0\MSTest.Acceptance.IntegrationTests.dll --filter $filter --report-trx --report-trx-filename acceptance.trx --results-directory $results --ansi off --progress off > "$results\execution.log" 2>&1
```

Results: first exit `0`, planned/selected/executed/passed **20**, failed/skipped
**0**, independently reconciled by the execution gate; second exit `0`,
**34 passed**, failed/skipped **0**, comprising 28 existing selector cases and
six help/info cases on net11.0, net10.0 and net462.

| Requirement | Executed named evidence and distinguishing outcome |
| --- | --- |
| One invocation, exact disjoint identities and isolated final outputs | `ShardCount_ExecutesEveryIdentityOnceInIsolatedReports`: four reports, exact five-test union, golden partition placement, matching discovery UIDs, no shared root report. |
| Filter and zero-result policy | `ShardCount_FilterAndEmptySelections_PreserveAggregatePolicy`: one selected result succeeds despite empty children; all-empty returns `8`. `ShardCount_Discovery_DoesNotFanOut` retains ordinary discovery. |
| Dependency affinity and Retry composition | `ShardCount_FailureAndRetry_StayInsideDependencyPartition`: exact four initial outcomes, failing `Buy` remains partition 4, final retry narrows to the same UID. `ShardCount_NativeDotnetTest_ExecutesAllPartitions` covers normal and ordered retries in two nonempty partitions. |
| CLI, unsupported modes and long arguments | Eight `ShardCount_InvalidOptions_FailBeforeExecution` rows, two `ShardCount_HotReload_FailsBeforeExecution` rows, and `ShardCount_LongResponseFile_PreservesSelection`; exact expected exits and selected results, no body execution for rejected modes. |
| Cancellation and cleanup | `ShardCount_Cancellation_JoinsUncooperativeChildren`: readiness markers precede cancellation; original process handles confirm root exit `3` and termination of every active child/grandchild. |
| Multiple SDK modules | Both `ShardCount_NativeDotnetTest_MultipleModulesPreserveResults` rows: two independent result roots, four reports per module, exact nine name/outcome associations, parent `0` or `2` according to the one deliberately failing module. |
| Defaults and existing manual selection | The 28 unchanged selector cases include `Partition_AbsentAndOneOfOne_PreserveExactBaselineIdentitiesAndOutcomes`; help/info expectations include the new registered option. |

Pure helper tests passed **12 on each of net10.0, net11.0 and net462**:

```powershell
$env:DOTNET_ROOT = Join-Path (Get-Location) '.dotnet'
foreach ($tfm in @('net10.0', 'net11.0', 'net462')) {
    $arguments = @('--filter', 'FullyQualifiedName~ShardingOrchestratorTests', '--report-trx', '--report-trx-filename', 'unit.trx', '--results-directory', "artifacts\TestResults\Release\partition-unit-final-$tfm", '--ansi', 'off', '--progress', 'off')
    if ($tfm -eq 'net462') {
        & "artifacts\bin\Microsoft.Testing.Platform.UnitTests\Release\$tfm\Microsoft.Testing.Platform.UnitTests.exe" @arguments
    } else {
        & .\.dotnet\dotnet.exe "artifacts\bin\Microsoft.Testing.Platform.UnitTests\Release\$tfm\Microsoft.Testing.Platform.UnitTests.dll" @arguments
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

Each exited `0`, with no skips/failures: **90 automated passing executions**
across the feature, baseline/help and helper gates. All changed/new C# files
retain UTF-8 BOM; `git diff --check` is clean. Owning-project `UpdateXlf`
regenerated all 13 catalogs. `python .github\scripts\test_check_localization.py`
passed 41 self-tests; `python .github\scripts\check_localization.py` validated
all 364 catalogs.

Assembly scheduling was not weakened: acceptance retains method parallelization
with 32 workers and the existing class-level `[DoNotParallelize]` protecting its
shared mutable generated assets. Fan-out disables the selector fixture's shared
append-only body log and uses isolated TRX identities instead. Cancellation and
retry rendezvous paths are run-owned; environment changes are child-process
overrides, and cleanup joins retained process/task handles with an independent
deadline. Pure helper tests introduce no shared resources.

### Test-value evidence

The isolated comparison uses the same base and retained source/test snapshot,
changing only `nextPartition == count` to
`nextPartition == Math.Min(count, 1)`. This is a compiling behavior-removal
mutation, not a pristine-baseline execution.

| Contract / case | Without distribution behavior | Correct implementation | Evidence / gap |
| --- | --- | --- | --- |
| Exact-once fan-out / `ShardCount_ExecutesEveryIdentityOnceInIsolatedReports` | Executed: detected. Selected/executed 1, failed 1, skipped 0, outer exit `2`; expected four reports, actual one. | Same case passed in the 20-case green gate and again after the red run: selected/executed/passed 1, failed/skipped 0, exit `0`. | Repacked variants, separate effective caches, matching loaded binaries; unchanged assertions and identical outer harness. |
| Forwarding, exit policy, Retry, cancellation, SDK aggregation | No individual compiling mutation executed for each contract. | Named cases above execute and pass. | Green consumer evidence; paired sensitivity for these additional contracts remains unverified. |

The red command, run from the isolated comparison after repacking its core, was:

```powershell
$results = Join-Path (Get-Location) 'artifacts\TestResults\Release\partition-red-final'
& .\.dotnet\dotnet.exe artifacts\bin\MSTest.Acceptance.IntegrationTests\Release\net12.0\MSTest.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~ShardCount_ExecutesEveryIdentityOnceInIsolatedReports' --report-trx --report-trx-filename acceptance.trx --results-directory $results --ansi off --progress off > "$results\execution.log" 2>&1
```

The post-red green command used that same selector and command from the primary
worktree with `$results` set to
`artifacts/TestResults/Release/partition-paired-green`. Its TRX and
`resolved-provenance.json` confirm the single named case and final loaded
platform DLL. This additional passing execution is separate from the 90-case
matrix above.

Green package SHA-256:
`614BC09B450A58CA1BBE20DC1142196BD33BEDBF47A534064E46C2FCAA7F0B09`;
mutated package SHA-256:
`B6981E382CDD3263D2DA86C41865A761BD45BBA2473445CF311A20FAC4863599`.
Each consumer's cached archive and loaded platform DLL matched its producer.
Green net11 platform DLL SHA-256:
`C27909631AFC74D57F8B2F6E51F259F6575E02F2E8C39693154C5373A30445C2`.
Both outer acceptance assemblies have SHA-256
`60978570051CBA0C436161B50CC7B9365878405BD77E578724767B4F24F41DDF`.

Green TRX/log/provenance files are under
`artifacts/TestResults/Release/partition-green-final`; red equivalents are
under the session-owned `files/partition-comparison/artifacts/TestResults/Release/partition-red-final`.
These are retained local evidence, not uploaded reviewer-accessible artifacts.

### Released SDK and rendered demonstration

The released SDK **10.0.404**, selected by an isolated `global.json`, executed
the final packed net11 application: ordinary fan-out and ordered two-partition
Retry both returned `0` with five final passing logical results. The latter
reported five results plus two retries and produced all three rendezvous markers.
For built modules use a rooted relative glob; an absolute `--test-modules`
pattern was not accepted:

```powershell
# In the isolated directory selecting SDK 10.0.404:
& $env:DOTNET_HOST_PATH test --root-directory $bin --test-modules Acme.Tests.exe --shard-count 4 --report-trx --report-trx-filename run.trx --results-directory $results --ansi off --progress off
& $env:DOTNET_HOST_PATH test --root-directory $modules --test-modules '**/Acme.Tests.exe' --max-parallel-test-modules 2 --shard-count 4 --report-trx --report-trx-filename run.trx --results-directory $results --ansi off --progress off
```

The second command also executed both two-module states: parent `0` with nine
passed results, and parent `2` with eight passed/one failed. Each had eight
reports, two distinct module roots and exact per-module outcome associations.
Logs/reports remain in the session-owned `files/partition-sdk10` directory.
An isolated net10 consumer built against the same final package also returned
`0`, four reports and five passing identities. With
`DOTNET_ROLL_FORWARD=LatestPatch`, host tracing confirms root and all four
children resolve **.NET 10.0.15**, rather than silently using the installed
net11 runtime.

The terminal canvas rendered the final packed application at **80 columns by
24 rows**, with outer `--ansi on --progress on --show-test-results all`.
Child output remained static/non-ANSI and correctly partition-prefixed. Inspected
states included passed tests, empty partitions, the failed `Buy` result, its
associated assertion/stack trace, long paths and parent exits `0`/`2`.
The frontend did not resize when the backend window width was changed to 60;
an actual narrower viewport remains unverified, and the backend was restored.
No hosted report/IDE UI mapping is changed; TRX remains partition-separated.

### Framework capability gate (2026-11-24)

Requirement (user-directed): the platform must know whether the active framework
supports partitioning and must not show `--shard-count` for xUnit or other
frameworks until they explicitly add support. MSTest opts in.

Decision: add experimental public `ITestPartitioningCapability : ITestFrameworkCapability`
with `bool IsSupported`. Absent or `false` means unsupported, so frameworks that
predate the capability keep their current option set. MTP now registers the
`--shard-count` provider (extension Uid `TestSharding`) and the
`ShardingOrchestrator` itself; MSTest no longer calls an internal
registration hook and only declares the capability. After framework
capabilities are created, and before options are validated or rendered in
`--help`/`--info`/the SDK handshake, MTP removes the provider when the capability
is not supported. An unsupported framework therefore rejects the option as
unknown (exit code 5). The orchestrator also checks the capability in
`IsEnabledAsync` as defense in depth.

The child selector `--partition <index>/<count>` stays framework-owned: only the
framework knows its affinity units (MSTest classes and dependencies). The
capability documents this contract. Alternatives rejected: keep the internal
MSTest-only registration (third parties cannot opt in); have MTP own `--partition`
and select by discovered UIDs (extra discovery process and loses framework
affinity); a separate extension package (extra registration step without
avoiding the framework contract). A boolean property, rather than marker-type
presence alone, follows existing capability shape and lets a framework decide
at run time.

Evidence on this working tree (base `09c901458d491e05a8a2c9adc89ee6ddfea834cc`,
uncommitted): `ShardingOrchestratorTests` covers absent/false/true capability
evaluation, provider removal, and a real `TestApplicationBuilder` run passing
`--shard-count 2` with no capability and with `IsSupported = false`.
Behavior-removal comparison: with the filtering condition replaced by `false &&`,
the same 16 selected tests ran; the two end-to-end cases failed at
`Assert.AreEqual(5, RunAsync())` with actual `8` (the option was accepted and an
empty ordinary run executed). With filtering restored, 16/16 passed. The MTP
`HelpInfoTests` now explicitly assert that `--shard-count` is absent from
both no-extension Help and Info output. The MTP
`HelpInfoTests|HelpInfoAllExtensionsTests` selection passed 31/31 after those
assertions were added. The MSTest `--info` expectation asserts a separate
`TestSharding` extension block.

Packed composed-product run on the same tree: `.\build.cmd -pack -c Debug`
(exit 0, 0 warnings), then
`dotnet run --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests -f net12.0 --no-build -- --filter "FullyQualifiedName~MSTest.Acceptance.IntegrationTests.HelpInfoTests|FullyQualifiedName~MSTest.Acceptance.IntegrationTests.PartitionTests"`
passed 54/54 (exit 0), and the MTP acceptance `HelpInfoTests|HelpInfoAllExtensionsTests`
selection passed 31/31 (exit 0). After the config-inheritance fix, the renamed
`ShardCount_ConfiguredOptionDoesNotRecurseIntoChildren` regression ran
inside `PartitionTests`; the full partition acceptance selection passed 49/49.
It first reproduced the failure with inherited `shard-count` in the
children, then passed with the fix. The config regression run confirms the
configured count does not re-trigger fan-out when children receive
`--partition <index>/<count>`.

Remaining limits: no cross-machine scheduler or report merge;
no netstandard/.NET Framework distribution;
explicit absolute outputs can still be shared. Custom-launcher rejection is
a static guard, not a separately executed case. Genuine application-token
cleanup is executed, but the SDK cancellation control-channel itself has not
received a new E2E case. The cancellation test joins captured descendants after
observing the root exit; it does not assert strict descendant-versus-root exit
timestamp ordering. No release backport, downstream enablement or live CI
validation is claimed.

## Original selector record (historical)

### Problem and contract

Teams that cannot enable in-process parallelization should be able to run
independent selections of the same assembly in separate CI jobs without
creating extra projects or parsing human-readable discovery output.
The originating request is [#4068](https://github.com/microsoft/testfx/issues/4068).

```powershell
# Native MSTest/MTP applications; run every index with the same count and bits.
dotnet test --project .\Tests\Tests.csproj --partition 1/4
dotnet test --project .\Tests\Tests.csproj --partition 2/4
dotnet test --project .\Tests\Tests.csproj --partition 3/4
dotnet test --project .\Tests\Tests.csproj --partition 4/4

# The same option is available when invoking the built application directly.
.\Tests.exe --partition 2/4
```

Each invocation runs only its numbered selection. **The option does not start
additional processes.** CI or an external launcher controls concurrency.
There is no `--shard-count`, worker limit, new orchestrator, SDK protocol,
public grouping API, receipt/catalog format, or cross-job report aggregator.

### Input and compatibility

- Syntax is `index/count`, with positive invariant decimal Int32 values and
  `1 <= index <= count`; indexing is 1-based. Reject malformed, signed,
  embedded-whitespace, zero, out-of-range and overflowing values through normal
  CLI validation. Only ASCII digits are accepted; leading zeros are allowed.
  MTP's ordinary outer-whitespace argument normalization still applies before
  provider validation.
- `1/1` selects the same work as an ordinary run. Absence of the option keeps
  the existing discovery, execution and discovery-cache paths.
- This is native MSTest's MTP integration, not a new VSTest command-line option.
  JSON-RPC/IDE server mode and WebAssembly are explicitly unsupported in this
  first slice. Native `dotnet test` uses the ordinary existing test-host
  handshake; direct execution requires no SDK handshake.
- Existing user, runsettings, UID and MTP filter-provider constraints still
  narrow the selection. Partitioning must never broaden them. Discovery uses
  its normal discovery-context filters; execution uses Run-context filters.
  `--list-tests` is not proof that tests executed.
- **Keep normal zero-test/count policies.** An empty partition is an empty
  filtered run: exit `8` by default, or `9` when an explicit minimum is not
  met. All-skipped and programmatic drop follow existing policy. Existing
  `--ignore-exit-code` behavior remains available but is not injected by this
  feature. No intentional-empty success exception or fake passing test is added.

The user was unavailable to answer the empty-policy clarification. Retaining
existing policy is the conservative selector-only choice; transparent
intentional-empty success under every SDK is separate work.

### Selection and lifecycle

A concrete test class and all its data rows stay together, including folded,
discovery-time and execution-time data. Classes connected by resolvable declared
dependencies are co-located. Include attribute and `testconfig.json`
declarations in placement without mutating the elements later executed.
Resource locks and `[DoNotParallelize]` keep their existing process-local
meaning; they do not become distributed locks.

Compute placement from the **complete source discovery before host filtering**.
Only then intersect the assigned class set with ordinary filters. This keeps
placement stable when Retry narrows a request to failed UIDs. It does not
auto-include filtered-out prerequisites, alter directed dependency execution,
or create unmatched-declaration warnings merely because a different partition
contains a valid configured dependent.

Use the existing metadata/data discovery once; do not run a preliminary test
host or repeat discovery factories. Keep normal data expansion and result
identity in the framework rather than replaying discovery rows across processes.
Partition state is per request, not static mutable state. In-process scheduling,
fixture cleanup, cancellation, outcome counting and reporter behavior remain
ordinary MSTest behavior.

Separate invocations repeat their applicable assembly/class initialization.
Process isolation does not isolate a shared database, file, fixed port or
machine-wide resource. Users must make those fixtures independent.

### Deterministic assignment

For each dependency-connected class component, encode fields in this order:
`"mtp.partition.v1"`, the source module's filename without extension, and the
distinct concrete class full names sorted ordinally. Prefix each field with
its UTF-8 byte length as UInt32 big-endian and append its UTF-8 bytes, without
a BOM, terminator, case folding or Unicode normalization.

```text
partition(component, n) = 1 + (UInt64BE(SHA256(encodedFields)[0..8]) mod n)
```

The filename key follows the adapter's source-oriented identity and avoids
absolute checkout/deployment paths or loading assembly metadata from native
AOT executables. It is not the assembly version or a localized display name.
Placement is independent of test arrival order, row labels and unrelated
classes/components. Changing a class/component, module filename or count can
change its assignment. A hash collision affects balance, not identity.
No exact count/duration balance or linear speedup is promised.

Independent vectors for `Acme.Tests.dll`:

| Class | SHA-256 | `n=4` | `n=7` |
| --- | --- | --- | --- |
| `Acme.CheckoutTests` | `69a7b6b77714706c93cabfc95fe8e9a16f80ea87ef48ce219f5fe4c08e342e45` | 1 | 5 |
| `Acme.CartTests` | `134e357316c9c3bd8d18a561192be5c6ccdec18302c5e919405e169dd9780e94` | 2 | 7 |
| `Acme.Nested+Fixture` | `0c175d40a80712742b08686ef72c281ba461868a84a823d5c6ec68d24586795e` | 1 | 2 |

More partitions than components can leave empty selections. The implementation
must not allocate an array proportional to the user-supplied partition count.

### Retry composition

`--partition i/n --retry-failed-tests k` keeps Retry as the sole process
orchestrator. Retry retains the partition option when relaunching; subsequent
attempts use their failed-UID constraint and the same full-source placement.
Never rebuild component keys from only the failed subset.

Retry percentage/count thresholds apply to that invocation's selected work,
just as they do with an ordinary filter, not all tests physically present in
the assembly or other CI jobs. MSTest's in-process `[Retry]` is unchanged.
No automatic local partition fan-out or nested-orchestrator composition is
included in this PR.

## Research and alternatives

- [Issue discussion](https://github.com/microsoft/testfx/issues/4068) asks for
  numbered CI selections and also discusses process-level local scheduling.
  They are distinct features; only the former is in this implementation.
- [Youssef's prototype](https://github.com/Youssef1313/MTPSharding/blob/398ba09ebbdf4da4136230cb595644034d3b62f0/docs/Design.md)
  demonstrates the discover/partition/run orchestration approach. It remains a
  valid MTP-level candidate, not something ruled out by choosing this first slice.
- Native MSTest has neutral elements before host filtering in
  [UnitTestDiscoverer](../../src/MSTest/MSTestAdapter.PlatformServices/Discovery/UnitTestDiscoverer.cs).
  It can form stable class/dependency selections without a discovery child,
  UID replay or a cross-framework API.
- [Filter providers](../RFCs/020-Test-Execution-Filter-Providers.md) can distinguish
  discovery and run. Use the normal request path rather than inventing a
  discovery snapshot of all future execution decisions.
- [Dependencies](../RFCs/022-Test-Dependencies.md) require placement before filter
  narrowing while preserving the executor's existing selected-test semantics.
- [NUnit](https://docs.nunit.org/articles/nunit/running-tests/Partitioning-Tests.html)
  and [Playwright](https://playwright.dev/docs/test-sharding) demonstrate the
  distinction between deterministic assignment, fixture granularity and duration
  balance.

Rejected for this slice: a discover/replay orchestrator (more process/lifecycle
scope), method/row splitting (fixture/data affinity), post-filter component
hashing (Retry can move a failed test to another bucket), and independent local
partition/concurrency knobs without a demonstrated workload need.

The earlier broader design was reviewed in three rounds by Claude Opus 5.5 and
Gemini 4 Argon, with GPT-6.1 Sol synthesis. Its accepted technical snapshot was
`5a0dd5c32c6c48976611e519ae59c9ddf69ded18`; the full review record remains in
the version history at `42f2e6666a91a8c9fbc6aa5eebd2cc721c5d2f49`.
Those reviews do not constitute a review of this implementation.

Important later clarifications: MTP ownership is not intrinsically problematic;
basic discovery/replay can work for selectable stable discovery units. Automatic
multi-process fan-out needs orchestration wherever selection is implemented.
The user then explicitly chose only MSTest `--partition` for the first PR.

## Implementation and validation

Producer: `MSTest.TestAdapter` and its bundled
`MSTestAdapter.PlatformServices`, aligned with the repository's existing
`MSTest.TestFramework`/MTP packages. Consumers are direct native test
applications and native MTP `dotnet test`. No SDK, transport, reporter or package
dependency change is required. Target the current mainline MSTest 5/MTP 3
source train; no stable backport or automatic downstream enablement is implied.
The PR author owns this slice through review; release/backport decisions follow
normal maintainer approval.

| Requirement | Planned decisive evidence |
| --- | --- |
| Parsing and deterministic placement | Exact hash vectors, invalid/boundary inputs, `1/1`, shuffled input and relocated paths. |
| Full coverage without overlap | Union all partitions and compare exact class/test identities to the ordinary run; pairwise intersections are empty. |
| Data and dependency affinity | Multiple distinct rows, runtime/folded data, attribute/config dependency components, filtered bridges and UID-narrowed Retry. |
| Filter and normal-policy preservation | Category/runsettings/UID/provider intersections, legitimate drop/skip, default run, empty `8`, explicit minimum `9`, and option-absent behavior. |
| Real shipping consumer | Freshly packed local NuGets, isolated fixture restore, direct and native SDK runs, exact loaded/resolved package provenance, TRX identities/outcomes and help/info output. |
| Regression sensitivity | Same tests retained in an isolated compiling comparison with the selector changed to select all; intended assertions fail, then pass with the feature. |

Use the pinned SDK through the repository scripts. Pack before package-consuming
acceptance runs and repack after producer changes. Record exact commands,
SDK/packages/source states, binlogs, report paths, selected/executed/pass/fail/skip
counts, inner/outer exits and the intended counterfactual assertion failures.
Do not treat build failures, skipped/zero tests or green CI alone as regression
sensitivity.

### Implementation log

- Implemented the parser/planner, pre-filter discovery selection, native option
  registration, ordinary filter/Retry composition, and unchanged count policies.
  The original constructor and static discovery-filter entry point remain
  available to existing internal callers.
- Source `net10.0` compilation succeeded with the pinned SDK. The owning
  project's `UpdateXlf` generated all 13 catalogs; the localization regression
  suite (41 tests) and the 364-catalog guard passed.
- Independent code-review round 1 inspected
  `4a42a14dc6fa12d0b47633cd03db9466d13e9ec9`: Claude Opus 5.5 and Gemini
  4 Argon found no blocking correctness defects. Follow-up fixes clear stale
  diagnostic state on manager reuse and guard deliberate cache/factory bypass.
- Both reviewers noted unnecessary row-level cloning for configured placement.
  The implementation now resolves class edges through the existing private
  dependency-reference parser and a class/method index, without cloning
  elements or expanding configuration arrays per data row. Attribute edges
  are not discarded merely because methods have the same name: overloads can
  declare different prerequisites.
- Multi-target compilation covered `net462`, `net10.0`, `net11.0`, modern UWP
  and WinUI. It caught an older-TFM style diagnostic in the SHA-256 fallback,
  which was corrected. Classic UWP requires full Visual Studio MSBuild, absent
  on this host; local packing uses the repository's supported
  `SkipUapTargetFramework=true` build setting and must not be presented as a
  complete classic-UWP shipping-package validation.
  The full Windows file-count guard consequently reports omitted classic-UWP
  payloads (`MSTest` 9/10, adapter 64/76, framework 88/105). This is an explicit
  local validation gap, not a passed full-Windows package check; these partial
  local test packages are not for publication.
- This host cannot resolve bare `powershell` from the repository's cmd/Exec
  wrappers, despite the full executable being present. SDK/runtimes were
  initialized through the same repository scripts using native PowerShell.
  Post-pack validation is invoked explicitly through its native entry point;
  no repository build script or validation rule is weakened.

The focused Release/net10 unit gate passed **34/34**: 21 parser/planner cases,
12 native-option/discovery/factory cases and one manager-reuse regression.
TRX, JUnit XML and CTRF JSON exports were parsed for all three scopes with no
missing, duplicate, skipped or failed cases. The overload regression preserves
both same-named methods' different dependency edges, rather than incorrectly
deduplicating by method name.

Code-review round 2 inspected
`3be09024cb5759473ecf2bb6cb8efb19a84ccab6` against the original production
baseline `a8a62a02627b9e9d9c33949d06a1540f84b78bc4`. Claude Opus 5.5 and
Gemini 4 Argon both approved with no remaining code/test findings. Those were
read-only static reviews, not executed acceptance or mutation evidence.

### Executed unit sensitivity

The isolated comparison retains the tests from
`3be09024cb5759473ecf2bb6cb8efb19a84ccab6`. These are compiling targeted
counterfactuals, not tests run on the pristine production baseline.

| Contract | Counterfactual | Without behavior | Restored implementation |
| --- | --- | --- | --- |
| `TestPartitionTests` selection contracts | Change the valid-input fast path from `Count == 1` to `Count > 0`, selecting all tests. Patch SHA-256 `419C6FB7065F97DC6E406718C7DF96FE64590D810C209208C84AAA61EA29A863`. | Executed: 21, passed 5, failed 16, skipped 0; exit 2. Golden-vector, affinity and disjointness assertions detect the missing selection. | Same 21 source-declared identities: passed 21, failed/skipped 0; exit 0. |
| `TestsBasedRunAfterPartitionedSourceRun_UsesCurrentDependencyDiagnosticSelection` | Remove only the explicit-element entry point's `_unpartitionedTests = null` reset. Patch SHA-256 `70DD4A5358B3ED5E8AA9F1011FA02E7C24A5B24D6BE63F66520FA9FE955356A4`. | Executed: 1, failed 1, skipped 0; exit 2. The assertion expects one genuine missing-prerequisite warning but observes two, including a false unmatched-dependent warning from the previous request. | Same test: passed 1, failed/skipped 0; exit 0. |

Both comparisons used Release/net10.0, the pinned
`12.0.100-alpha.1.26509.101` SDK, fresh test-project builds, unique binlogs and
separate TRX/JUnit/CTRF result directories. The selectors were respectively
`--treenode-filter '/*/*/TestPartitionTests/*'` and
`--treenode-filter '/*/*/ExecutionManagerContinuationTests/TestsBasedRunAfterPartitionedSourceRun_UsesCurrentDependencyDiagnosticSelection'`.
TRX identities were reconciled with source declarations; there were no skips
or duplicate execution IDs. Test assertions were not changed between states.
The correct source was restored after each experiment.

### Packaged-consumer execution gates

The source-only producer pack passed with warnings treated as errors. The final
bounded local gate passed **35 distinct unit tests and 34 packed acceptance
cases**, with no failures or skips. Acceptance discovery selected exactly the
same 34 source-planned identities: 28 partition cases and six existing help/info
cases. The outer project used its declared net12.0; generated partition
consumers used net11.0, with help/info additionally exercising net10.0 and
net462. The real pinned SDK was `12.0.100-alpha.1.26509.101`.

The producer remained
`3be09024cb5759473ecf2bb6cb8efb19a84ccab6`. The validated test/support delta
has SHA-256
`50BF541DEA4243C0183C00A4EB8D2D7FE60F752DA06D86EAFA432735812A3FA7`;
it changes only the two partition test files. The final selector container has
22 passing methods, including independent 255/256/257-byte framing vectors
and an unresolved-dependent-method case. These two additions received bounded
static gap/assertion review and green execution, not new executed mutation
proof. The 21-test paired selector evidence above remains bound to its
original unchanged tests.

| Requirement | Executed acceptance evidence |
| --- | --- |
| Complete, disjoint class selection and matching discovery/run identities | `Partition_DiscoveryAndRun_SelectSameDisjointClasses` and `Partition_AbsentAndOneOfOne_PreserveExactBaselineIdentitiesAndOutcomes`. |
| Native SDK and reflection/generated metadata | `Partition_NativeDotnetTest_MatchesConsoleSelection` and `Partition_MetadataModes_PreserveClassAndDependencyPlacement`. |
| Dependency affinity and UID-narrowed Retry | `Partition_AttributeAndConfiguredDependencies_KeepFullComponentTogether` and `Partition_RetryUidNarrowing_DoesNotMoveDependencyComponent`. |
| Data rows and programmatic Run/Skip/Drop | `Partition_DataRows_RemainWithTheirConcreteClass` and `Partition_ProgrammaticDropAndSkip_AreNotBroadened`. |
| Existing empty/minimum/override policy | `Partition_EmptyBucket_PreservesExitEight`, `Partition_MinimumExpectedTests_UsesSelectedCountAndExitNine` and `Partition_IgnoreExitCode_PreservesExistingPolicy`. |
| CLI input and help/info | `Partition_CommandLineWhitespaceNormalization_PreservesExistingPolicy`, eleven invalid-option cases and the existing registered-extension help/info tests. |

The common MTP parser trims outer argument whitespace before provider
validation; the option preserves that policy. The direct partition parser
still rejects raw whitespace, and embedded CLI whitespace remains invalid.
The programmatic filter acts at invocation, so discovery can include a test
that execution later drops. Retry's top-level TRX describes the final attempt;
the acceptance test separately asserts both attempt maps, stable UIDs and all
final logical outcomes. These are existing consumer contracts, not feature
behavior changes made to obtain green tests.

Fresh consumer resolution was checked against current package SHA-512 values
in ten `project.assets.json` files. SHA-256 comparison matched 1,428 physical
runtime-output DLL files to resolved package payloads. The 73 recorded
partition child invocations returned `0` (48), `5` (11), `8` (11) and `9` (3);
the negative child exits were asserted by passing outer tests. Output-bit
agreement is not presented as an independent loaded-module inspection.
TRX, JUnit and CTRF identities/outcomes were reconciled, and the repository's
execution verifier confirmed planned/selected/executed/passed = 34, with
unique execution IDs and outer exit 0.

The decisive acceptance command was:

```powershell
# $results and $binlog name unique, retained artifact paths.
& .\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -p:SkipUapTargetFramework=true -p:UsingDotNetTest=true -p:EnableCodeCoverage=false -p:TreatWarningsAsErrors=true -p:MSBuildTreatWarningsAsErrors=true "-p:ArtifactsTestResultsDir=$results\" "-bl:$binlog" --filter 'FullyQualifiedName~PartitionTests|FullyQualifiedName~HelpInfoTests' --no-ansi --no-progress --show-test-results all
```

Result: exit 0, 34 passed, zero failed/skipped. The matching independent
discovery used `--list-tests text` with the same filter. Runtime package
families were MSTest `5.0.0-dev` and MTP `3.0.0-dev`; eager fixture dependencies
were packed from unchanged source rather than bypassing package lookups.

Initial failed attempts exposed outer-TFM, eager-package, comparison-local
toolchain-path and generated filter-type qualification problems. Those setup
and compilation failures are not counted as behavioral red evidence. The
corrections do not change production code, repository build policy, shared
caches or assertions about the feature.

**Remaining limits:** packed-consumer counterfactual red/green, independent
loaded-module inspection and an interactive rendered-terminal demonstration
have not been completed. Classic-UWP/full-Windows package validation remains
the host/CI gap described above. The earlier standalone PowerShell/Node
experiments confirmed hash vectors and ordering only, not product execution.

### Review correction: acceptance source-generation opt-out

Review identified that the new fixture and its fixed metadata data rows ignored
the existing `MSTEST_ACCEPTANCE_SKIP_SOURCEGEN=1` acceptance-harness policy.
Both now reuse `MetadataModesToRun`: fixture builds consume that list, and
`[CombinatorialData]` with `[MetadataModeValues]` selects matching test cases.
This changes acceptance configuration only, not the product's partition UX.

At `90c9d074cc49192c963a3661d51d2b7c46494a0d`, discovery with the opt-out set
still selected all three modes. With the correction, the same discovery selects
only Reflection. This is executed discovery/configuration evidence, not an
old-revision failed-execution or mutation claim.

The focused acceptance method
`Partition_MetadataModes_PreserveClassAndDependencyPlacement` then passed
**1/1 with the opt-out** and **3/3 with it unset**, with no failures or skips.
Both runs used the pinned SDK, Release/net12.0 outer host and freshly generated
net11.0 consumers. Each run's canonical execution gate reconciled exact
source-planned mode names, unique execution IDs and exit 0.
Retained build manifests confirm that both independent and dependency assets
built only Reflection under the opt-out, with no `bin/SourceGen` or
`bin/AotSourceGen` directory; the default run built and executed all three
modes. Resolved adapter/framework/platform package hashes matched the current
unchanged producer packages.

The correction's test-file patch against that revision has SHA-256
`D9C1D48C995E315D8CE2C60AF323AC64A97361790A3DFA433E51A120C21FFF98`.
Both execution commands used the acceptance command above with
`--filter 'FullyQualifiedName~Partition_MetadataModes_PreserveClassAndDependencyPlacement'`
and separate fresh results/binlogs. The environment value was set or removed
only in the owning validation process. No assembly scheduling, assertions,
production files or shared/global environment were changed.

### CI compilation corrections

[Azure build 1668030](https://dev.azure.com/dnceng-public/cbb18261-c48f-4abb-8651-8cdcb5474649/_build/results?buildId=1668030)
tested PR head `ef14255e79617000327243bd2421f320d1c9013b` through merge
commit `e3add7f9ccaec3fcaf3410b2ff5719d971b5ee07`. All four failing build
legs were traced to the new test files, not a transient service failure:

- The Windows legs also compile net462/net48 tests. Three array
  `Reverse().ToArray()` expressions bound to a void-returning extension on
  those targets, producing CS0023/CS0030. Explicit
  `Enumerable.Reverse(tests).ToArray()` preserves the intended non-mutating
  reversed sequence on every target. A direct local net48 build reproduced
  the failure; the previously green net10/net11 runs did not cover it.
- Two multiline collection expressions in the discovery/TRX readers failed
  SA1500/SA1413. Their opening/closing delimiters and trailing commas now follow
  the repository format. The same six analyzer errors also caused the
  [shipping-attachments workflow failure](https://github.com/microsoft/testfx/actions/runs/42261351459/job/128041798600).

The earlier Azure build 1668021 was canceled, not a passing baseline; its merge
commit was `4f898f42afcbe668aa811b95ee661adad3ec07c3`. The failed build above
is the completed failure used for this correction.

Validation used the pinned SDK and unchanged assertions:

- A forced analyzer-enabled Rebuild of the owning acceptance project passed
  with zero warnings/errors. Existing, already-built references were reused
  with `BuildProjectReferences=false`; analyzers were not disabled.
- `TestPartitionTests` passed **22/22 on each of net462, net48, net10.0 and
  net11.0**. This includes
  `SelectTests_ReorderedDiscoveryAndCheckoutPaths_PreserveClassPlacement`,
  `SelectTests_TransitiveAndCyclicDependencies_UseOneSortedComponent` and
  `SelectTests_SameNamedOverloadsWithDifferentDependencies_CoLocateEveryTargetClass`.
- Packed `Partition_DiscoveryAndRun_SelectSameDisjointClasses` passed **1/1**
  through the formatted discovery/TRX helpers, with the existing source-gen
  opt-out set only for this process. Exact result identities, disjointness and
  associated outcomes remained asserted.

Each test invocation exited 0; TRX/JUnit/CTRF exports and exact source-planned
counts were verified, with no failures or skips. The two-test-file correction
against that PR head has SHA-256
`2036D8E158D4AD16F0E7679C387C54F446266E1B339D3E81A472C5E19FF8EC72`.
These are compilation/formatting repairs, not new product behavior or a
behavioral red/green claim. CI must still rerun on the pushed correction.

### Base merge and validation blocker

Merged main `0801b9574b165c714801ce56256f774c9619f9d7` in
`9ade8703ec4a7a8f5614306664a89fc553329df5`. The two text conflicts were
additive: retain both partition and expected-duration changelog entries, and
both sets of internal API declarations. The automatic framework merge retains
partition-aware manager creation/cache bypass and main's durable-result-file
lifecycle changes.

The API-owning PlatformServices project built successfully with warnings as
errors on net462, net10.0, net11.0, modern UWP and WinUI, with classic UWP
excluded by the existing local host limitation. Restoring the merged package
versions succeeded, but the configuration-matched producer repack failed on
SA1500/SA1413/SA1137 in
`RetryOrchestrator.OrchestrationSupport.cs:213-228`. That file's blob is
`59bf30fe55bae42a20871b115d3c83e03b9c5c0f` in both target main and the
merged branch; this PR does not modify it.

The unrelated upstream Retry formatting was not changed or suppressed.
Post-merge package-consuming validation is blocked, and the partial pack
outputs must not be used as if they were a complete aligned package set.
Earlier acceptance results remain evidence for their recorded revisions,
not executed proof of this merged revision.

### Retry artifact-layout follow-up

The external branch update
`038bca74da5220ebd958efdf8c20176bab80b9b3` corrected the upstream Retry
formatting. Both aligned source-package sets then packed successfully with
warnings as errors; the earlier pack blocker no longer applies.

[Azure build 1668369](https://dev.azure.com/dnceng-public/cbb18261-c48f-4abb-8651-8cdcb5474649/_build/results?buildId=1668369)
exposed a stale expectation in
`Partition_RetryUidNarrowing_DoesNotMoveDependencyComponent` on Windows and
Linux. Main's duplicate-TRX fix intentionally moves the final raw attempt
report to the results root. Earlier attempts remain under `Retries`, so the
test must assert one first-attempt report there and one final report at the
root, not expect a duplicate final report inside `Retries`.

The same focused packed test reproduced the failure locally: one executed
case, expected two nested reports but observed one, exit 2. After correcting
the locations, the same case passed 1/1 with exit 0 and no skips. It still
checks the four first-attempt identities/outcomes, the final failed-UID
selection, stable UIDs, all final logical outcomes, and the exact body-probe
multiplicity. The final-result map is compared against the first attempt's
failed identities with their expected Passed outcome, not against another
parse of the same final report.

The analyzer-enabled owning-project build passed with zero warnings/errors.
TRX/JUnit/CTRF exports and resolved current adapter/platform/Retry package
hashes were verified; physical reports were exactly `run.trx` and
`Retries/<run>/1/run.trx`. Validation used Release/net12.0, fresh net11.0
consumers and the existing process-local source-gen opt-out. The test-file
patch against the external update has SHA-256
`371760D9E3A6F39314D5E6C3BDC0245603CA878B40B5D0691902367801C6A454`.
This is executed evidence for repairing the test's artifact contract, not a
claim that product behavior was changed or the partition selector was mutated.

### CI eligibility

Static inspection finds the acceptance project in both `TestFx.slnx` and
`NonWindowsTests.slnf`. Windows Release uses
`eng/pipelines/steps/test-windows-configuration-tests.yml`; Linux/macOS Release
use `eng/pipelines/steps/test-non-windows.yml`. Their normal full-test commands
select the solution without excluding this class, and the calling jobs honor
`SkipTests`. The shared environment pins `.dotnet`, the matching `.packages`
cache and `DOTNET_CLI_CONTEXT_VERBOSE=1`. This establishes eligibility, not an
executed CI result; no CI-verified claim is made before publication.
