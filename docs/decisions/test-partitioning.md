# Test partitioning: research and decisions

Status: design proposal, revision D0, 2026-11-06. No product implementation or
performance claim is implied. Independent multi-model reviews are pending.

## Problem and contract

Teams with process-global test state often disable in-process parallelization.
They should be able to divide a large test assembly across isolated processes,
locally or in CI, without reorganizing it into many projects or parsing
human-readable discovery output.

The request is [#4068](https://github.com/microsoft/testfx/issues/4068).
This is **partitioning**: a predetermined, disjoint assignment of execution
groups. It is not a distributed scheduler, work stealing, infrastructure retry,
or a promise of duration-balanced work.

### Confirmed requirements and assumptions

- Support CI jobs selecting one numbered partition, as requested in the issue.
- Address the motivating sequential-suite problem with bounded local
  multi-process execution of the same assembly.
- Keep existing runs unchanged unless the feature is explicitly enabled.
- Preserve filtering, test identities, outcomes, diagnostics, and artifacts.
- Do not silently run the entire suite when partitioning is unsupported.
- Assume users can make assembly initialization repeatable and can isolate
  external resources. Separate processes isolate statics, not a database,
  fixed TCP port, absolute file path, environment outside the child, or account.
- Treat explicit opt-in as permission to repeat assembly setup/cleanup in each
  nonempty worker, not permission to discard declared fixture constraints.
- Design only in this change. Product code, benchmarks, package tests, and
  downstream SDK changes belong to subsequent implementation.

### Proposed ownership

Use an opt-in **MTP extension**, tentatively
`Microsoft.Testing.Extensions.Partitioning`, implementing one
`ITestHostExecutionOrchestrator`. MTP owns process lifecycle, planning,
concurrency, result reconciliation, and artifact coordination. Frameworks own
the meaning of an indivisible execution group and replayability of their UIDs.

Ship native MSTest support first. This does not claim that installing the
extension makes every MTP framework partitionable. A new experimental framework
capability explicitly advertises compatible discovery, atomic group metadata,
and exact UID replay. Missing capability means unsupported, distinct from an
explicitly supported capability whose current plan contains no tests.

Do not implement this as a second test framework, as a custom per-test predicate,
or by calling an orchestrator middleware's `next` multiple times.

### Proposed CLI

Names are proposals, not existing options:

```powershell
# One partition per CI job; repeat for indices 1 through 4.
dotnet test --project .\Tests\Tests.csproj --partition 2/4

# All four partitions, with at most two workers for this module.
dotnet test --project .\Tests\Tests.csproj --partition-count 4 --max-parallel-partitions 2

# A built executable uses the same extension options.
.\Tests.exe --partition-count 4 --max-parallel-partitions 2
```

`--partition i/n` selects exactly one partition. `--partition-count n` schedules
all partitions. They are mutually exclusive. `--max-parallel-partitions p` is
valid only with local scheduling and defaults to `min(n, processor count)`.
All numbers are positive invariant-culture Int32 values, and `1 <= i <= n`.
There is no arbitrary limit on partition count, and the planner must not
allocate an array of size `n` for an almost-empty suite.

These are test-application extension options, not a new SDK built-in `--shard`
switch. Documentation must distinguish native .NET 10+ MTP `dotnet test`
forwarding from legacy MSBuild forwarding; the latter's exact syntax and
support remain an implementation validation gate.

Existing framework parallelization settings remain unchanged inside a worker.
The advertised process limit is per module, not solution-wide. With `m` modules
and `p` workers per module the outer SDK can schedule up to `m * p` workers.
Recommend `--max-parallel-test-modules 1` for the first local adoption; do not
pretend nested parallelism is a global CPU or memory budget.

### Safety boundary

The initial release preserves an entire MSTest class, all its data rows, and
connected classes with declared dependencies or conflicting resource locks as
an atomic group. It does not split a class to manufacture better balance.
`[DoNotParallelize]` retains its existing process-local meaning; explicit
partitioning can run different groups in different processes. Dependencies and
resource declarations must not silently lose their meaning when a process
boundary is introduced.

Unknown external resource relationships cannot be inferred. The documentation
must say that assembly-wide static sharing may be isolated, but cross-process
or cross-machine resources require user isolation. A suite with a single
indivisible group may receive no speedup; report this clearly.

## Research and experiments

### 2026-11-06: observed source and precedents

Repository baseline:
`a8a62a02627b9e9d9c33949d06a1540f84b78bc4`. It declares MSTest `5.0.0` and
MTP `3.0.0` version prefixes in
[`eng/Versions.props`](../../eng/Versions.props). These are source-train
observations, not an assignment of the feature to an already published version.

| Source | Observation and design consequence |
| --- | --- |
| [Issue 4068 and discussion](https://github.com/microsoft/testfx/issues/4068) | The original ask is `i/n` across CI runners; discussion also asks for multiple copies of a large assembly. Support both use cases without equating them. |
| [Youssef's design](https://github.com/Youssef1313/MTPSharding/blob/398ba09ebbdf4da4136230cb595644034d3b62f0/docs/Design.md) and [implementation](https://github.com/Youssef1313/MTPSharding/blob/398ba09ebbdf4da4136230cb595644034d3b62f0/src/YTest.MTP.Sharding/FakeShardingFramework/ShardingTestFramework.cs) | Demonstrates discovery, UID assignment, child processes, and result forwarding. Its fake framework replaces the real framework; the design explicitly acknowledges missing extension/report behavior. Do not copy that workaround as the product architecture. |
| [Prototype runner](https://github.com/Youssef1313/MTPSharding/blob/398ba09ebbdf4da4136230cb595644034d3b62f0/src/YTest.MTP.PipeProtocol/MTPPipeRunner.cs) and [package versions](https://github.com/Youssef1313/MTPSharding/blob/398ba09ebbdf4da4136230cb595644034d3b62f0/Directory.Packages.props) | Built around MTP 1.8.2/MSTest 3.10.2. UID shell escaping, long argument handling, and reporting cannot be assumed production-ready or current. |
| [Public orchestrator](../../src/Platform/Microsoft.Testing.Platform/TestHostOrchestrator/ITestHostExecutionOrchestrator.cs), [host](../../src/Platform/Microsoft.Testing.Platform/Hosts/TestHostOchestratorHost.cs) | The orchestrator is now public experimental and has cancellation. The host allows one active orchestrator, not an arbitrary stack. |
| [Middleware](../../src/Platform/Microsoft.Testing.Platform/TestHostOrchestrator/ITestHostExecutionOrchestratorMiddleware.cs) and [official extension docs](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-architecture-extensions) | Middleware wraps one invocation; it is not a multi-run engine and cannot rewrite the downstream verdict. |
| [RFC 020](../RFCs/020-Test-Execution-Filter-Providers.md) and [composer](../../src/Platform/Microsoft.Testing.Platform/Requests/TestExecutionFilterComposer.cs) | Filter providers contribute AND constraints to a request. UID intersections and match-none already exist; orchestration remains separate. Providers can distinguish discovery from run. |
| [Native MSTest filters](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestFilterContext.cs) and [converter](../../src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestNodeConverter.cs) | Native selection can replay UIDs, but method identity metadata is not an authoritative fixture grouping contract; some assembly fields are empty. Do not parse display names or infer fixtures from opaque UIDs. |
| [Dependencies RFC](../RFCs/022-Test-Dependencies.md) and [parallel scheduler](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.ParallelExecution.cs) | Class chunks, dependency edges, and resource locks affect scheduling. A UID-only round-robin can break that behavior. |
| [Binary protocol](../mstest-runner-protocol/004-protocol-dotnet-test-pipe.md) | Execution ID identifies a module's process tree; Instance ID identifies a process; concurrent partition workers are not retry attempts. Protocol source is vendored into the SDK, but transport implementations differ. |
| [Artifact post-processing](../../src/Platform/Microsoft.Testing.Platform/Extensions/ArtifactPostProcessing/IArtifactPostProcessor.cs) | Existing processors can combine supported artifact kinds; retry and disjoint partitions need different merge semantics. Preserve originals on merge failure. |
| [Native SDK CLI](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp) | Module concurrency already exists. Extension options require registration in each application. Real SDK validation, not a fake pipe server alone, is required. |
| [NUnit partitioning](https://docs.nunit.org/articles/nunit/running-tests/Partitioning-Tests.html) | Hash assignment supports independent `i/n` jobs, but does not balance duration. Fixture grouping is a meaningful separate contract. |
| [Playwright sharding](https://playwright.dev/docs/test-sharding) | Default file grouping and explicit fully-parallel opt-in distinguish safety from balance; merging reports is part of the end-user story. |

Executed research: read-only GitHub/API/documentation retrieval and local source
inspection. The prototype was **not run**. No partitioning product, benchmark,
or packaged SDK experiment has been executed. C# LSP initialization was
unavailable; source inspection used file reads/search instead.

### Alternatives and decision

| Alternative | Benefit | Cost / disposition |
| --- | --- | --- |
| Keep projects/categories and hand-written scripts | No new product surface. | Does not solve the stated project proliferation and discovery-output problem; retain as workaround. |
| MSTest-only hash selection during native discovery/run | Smallest useful distributed selector; no coordinator or UID round-trip. | Duplicates framework-specific planning if generalized and does not by itself provide local fan-out. Keep as the strongest simplifying alternative for review. |
| MTP filter provider performs discovery and launches workers | Looks reusable. | Conflates request constraints with planning, creates recursion/lifecycle problems, and does not make adapters support a new filter. Reject. |
| Generic UID-level round-robin | Simple and roughly balances discovered case counts. | Depends on discovery ordering and splits fixtures/dependencies/data. Reject as the default. |
| Fake framework wrapping every result | Prototype proves the broad concept. | Reconstructs framework/report semantics and can lose metadata, attachments, or outcomes. Reject. |
| MTP orchestrator plus explicit framework grouping/replay capability | Shared process/result lifecycle with framework-owned safety semantics. | More initial contract work and a separate discovery phase. Preferred D0 direction, subject to adversarial review. |
| SDK-only scheduler with historical timings | Potential global balancing. | Cross-repository coordination and a larger shipping/deployment contract. Defer; do not require it for the first useful release. |

## Proposed architecture

### Execution groups and deterministic assignment

Framework discovery must return replayable atomic units, an opaque stable
group key for each unit, and a capability contract version. MSTest constructs
groups from concrete classes, including inherited/generic instances, then
coalesces the undirected connected components of declared dependency and
conflicting-resource relationships. Splitting a method's data rows is not v1.
Assembly identity namespaces group keys; absolute paths, discovery order,
display labels, randomized `GetHashCode`, and localized strings do not.

For partition count `n`, assign each group using versioned SHA-256 over a
length-prefixed UTF-8 canonical group key. Interpret the first eight digest
bytes as an unsigned big-endian integer:

```text
partition(group, n) = 1 + UInt64BE(SHA256(key)[0..8]) mod n
```

Fix the canonicalization and known vectors before publishing the feature.
Sort component member keys ordinally before encoding a coalesced group.
Assignment is independent of enumeration order, path, culture, OS, and other
unrelated groups. Renaming a group or changing `n` can move it. Hash collisions
affect balance only; selection always retains distinct group/UID records.

Apply the effective user selection before execution; never broaden a category,
runsettings, UID, or provider constraint. Grouping may co-locate selected
members but must not auto-include filtered-out prerequisites. Compute safety
relationships conservatively before filtering away members that bridge groups.

### Lifecycle

1. Validate options, supported host mode, extension combination, executable
   relaunchability, and framework capability before scheduling work.
2. Launch one discovery-only child using the same shipping executable,
   configuration, working directory, environment, and user selection. Suppress
   worker recursion, external reporting, and the outer SDK results channel.
3. Collect a versioned private discovery manifest, not terminal output. Check
   successful discovery, completion, duplicate/conflicting identities, grouping
   metadata, and effective-selection provenance. Seal the plan atomically.
4. Compute assignments; skip empty buckets without launching a worker. For
   external mode, launch only the requested bucket. Local mode uses a bounded
   queue and launches at most `p` workers.
5. Launch each worker in a fresh process with a versioned private assignment
   file/channel and the original selection. A worker-side filter provider adds
   its UID set using existing AND composition; never replace the user filter.
   Do not inject a long string of shell-quoted UIDs or use lossy response files.
6. Require worker capability/plan acknowledgement before accepting results.
   Track assigned, observed, completed, and unexpected identities separately.
   Folded data can produce multiple results for one replay UID; raw result
   count is not assignment coverage.
7. Drain messages, wait for child exit, verify completion, reconcile results,
   and finalize artifacts. A successful OS exit alone cannot establish that
   the assigned work ran.
8. On cancellation stop scheduling, cancel active workers, allow bounded
   graceful teardown, terminate only owned remaining process trees, drain what
   is available, mark partial results, and dispose once.

MSTest assembly initialization/cleanup repeats per nonempty worker; class
initialization/cleanup stays with its group. The discovery process may execute
user data-source code; documentation must not promise side-effect-free discovery.
Execution-time-only data remains atomic at its discovered parent anchor.
Unstable or non-replayable discovery is unsupported rather than silently
discarding or duplicating tests.

Providers can intentionally differ between discovery and run. D0 does not yet
prove that a discovery manifest represents every effective run-only selection.
This is a release-blocking contract question, not permission to treat missing
results as passed.

### Process, protocol, and identity contract

The coordinator owns discovery/worker processes, private IPC, assignment files,
deadlines, cleanup, and final module verdict. Reuse platform launch/controller
helpers and lifecycle/security rules where applicable; do not bypass registered
environment providers, custom launchers, or authorization.

In native `dotnet test`, the coordinator handshakes as an orchestrator. Execution
workers use the existing SDK data channel and preserve the original framework's
complete result stream rather than replaying it through a fake framework.
The module process tree shares Execution ID. Each worker has a unique Instance
ID and attempt number `1`; partition number is not an attempt number.
Multiple modules/TFMs retain separate module identities and verdicts.

Private discovery and genuinely empty partitions intentionally do not produce
an execution-worker handshake. A started execution worker missing its required
handshake/completion is an error. For standalone execution, no SDK handshake is
required, but private worker acknowledgement/completion remains required.
Missing or incompatible private protocol versions fail explicitly; there is
no fallback to an unpartitioned run.

Do not assume the SDK already understands an arbitrary orchestrator UID, child
count, no-work exit, concurrent instances, or global count policy. Before
advertising SDK support, run the real selected shipping SDK and decide whether
additive capabilities or SDK changes are needed. Old/undeclared capability and
explicit false must be distinguished. If unsupported, fail before execution
with upgrade guidance rather than returning misleading success.

### Verdicts and partial work

The coordinator needs a structured ledger, not `max(childExitCodes)` or "last
child wins." Record every partition's lifecycle and original exit.

- Any protocol, launch, discovery, incomplete-work, or cleanup failure produces
  a non-success infrastructure/configuration verdict and retains diagnostics.
- Cancellation/deadline produces the existing aborted-session semantics.
- Completed tests failing retain the existing test-failure semantics.
- Success requires every scheduled nonempty partition to complete and the
  observed identities/outcomes to satisfy the plan.
- A known-empty selected bucket is successful partition work and is reported
  as such. An empty complete user selection retains existing zero-test/minimum
  policies; it must not be hidden by treating all emptiness alike.
- Whole-run minimum/maximum/timeout and per-module policies must be applied at
  their actual owner. Never copy a whole-module minimum into every worker or
  allow exit-code-ignore options to make missing assigned work successful.

Continue independent partitions after an ordinary test failure. Stop scheduling
on cancellation or a coordinator/protocol failure. Preserve completed results;
do not rerun partly completed partitions automatically because side effects
may already have occurred.

### Artifacts and consumers

Give each invocation/module/partition a collision-free result subtree, including
explicit filenames and relative test attachments. Preserve normal test UIDs,
display names, outcomes, timing, stdout/stderr, exceptions, and attachments.
Partition metadata annotates execution provenance, not test identity.

Merge supported reports in **disjoint-union**, not retry-collapse, mode.
Preserve original reports and relative attachments; an unsupported report kind
remains a per-partition artifact with an index. A post-processing failure warns,
retains originals, and follows the existing artifact policy; it must not erase
the underlying test verdict or advertise a partial merge as complete.

Choose exactly one merge/publication owner for each boundary: module coordinator
for direct execution, outer SDK for invocation-wide combination where supported.
CI reporters need one logical module run rather than one competing "final"
summary/upload from each worker. This is a required integration, not something
proved by a correct TRX file. Reporters lacking coordination support must be
explicitly rejected or documented as per-partition outputs before enabling them.

### Compatibility and exclusions

V1 targets desktop local processes on Windows, Linux, and macOS with native
MSTest on MTP. Direct apphost and `dotnet Tests.dll` must be covered; never assume
`Environment.ProcessPath` is the test executable. Native AOT is a separate
launch/replay test, not implied by apphost support.

Reject JSON-RPC/IDE server mode, hot reload, device/browser/WASI/packaged-app
launchers, and simultaneous process-level Retry in v1. The existing middleware
does not make two orchestrators composable. In-process MSTest retry remains
framework behavior; its multiple attempts must not be mistaken for duplicates.
Do not change the default meaning of `[DoNotParallelize]` when partitioning is
absent. No global distributed resource locks, scheduling service, historical
timing database, automatic optimal partition count, or cross-job report upload.

## Implementation plan and release gates

Each phase is an independently reviewable change, not authorization to implement
it in this design-only session:

1. **Contract feasibility.** Prove the smallest supported MSTest fixture/
   dependency/resource grouping and stable UID replay using existing discovery,
   including folded/unfolded and runtime data. Resolve run-only providers and
   native SDK concurrent-instance/no-work behavior. Reconsider MSTest-native
   selection if generic replay requires a broad new planning API.
2. **Planning and external partition selection.** Add experimental capability/
   group metadata, canonical hash vectors, private manifest validation, option
   parsing, one-partition execution, and disabled/unsupported diagnostics.
   Track public/internal API baselines, XML experimental remarks, and resources.
3. **Bounded local fan-out.** Add worker scheduling, controller/launcher-aware
   lifecycle, cancellation, owned cleanup, per-module policy aggregation, and
   identity reconciliation. Keep process-level Retry rejection explicit.
4. **Shipping consumers.** Complete native SDK support, report merging and CI
   publishing coordination, packed-package end-to-end coverage, help/info
   expectations, docs, and a runnable local/CI sample.
5. **Preview and rollout.** One delivery owner carries MTP, MSTest, and any SDK
   dependency flow through exact shipping branches and versions. Do not enable
   by default in `MSTest.Sdk`. Collect discovery/setup cost, peak memory, actual
   overlap, imbalance, and total wall time on real sequential suites. Publish
   the limits; do not promise linear speedup.

Target the next compatible MTP/MSTest mainline preview; no backport to a stable
branch is assumed. The implementation owner must explicitly decide servicing
and the minimum real SDK/package version before downstream enablement. Assign
a named owner during implementation; this proposal does not invent one.

## Validation and demonstration

All cases below are **planned**, not executed product evidence.

| Requirement / risk | Proposed automated evidence | Consuming surface / rendered evidence |
| --- | --- | --- |
| Disabled means unchanged | Compare ordinary runs with feature package absent/present but disabled: exact identities, lifecycle counts, outcomes, exits. | Existing terminal/report behavior. |
| Correct partition algebra | Independent known hash vectors; shuffled discovery, Unicode, duplicate display names, large `n`; union equals baseline and intersections are empty. | Plan diagnostics show algorithm, group count, nonempty buckets, and effective process budget. |
| Real isolation benefit | Classes sharing a static can each run sequentially inside different PIDs; use rendezvous markers to prove overlap without wall-clock assertions. | Direct terminal and native SDK at normal/constrained widths. |
| Fixtures, constraints, data | One class/rows in one partition; transitive dependencies and conflicting locks co-located; filtered prerequisites not added; runtime/folded data complete. | Parse per-result identities and lifecycle marker files, not counts alone. |
| Original filters preserved | Category/runsettings/UID/provider intersections, empty intersection, run-only providers, and a worker receiving no eligible tests. | Exact selected/executed sets; distinguish expected empty from missing work. |
| Private transport and drift | Quotes, newlines, `@` and long UIDs; duplicate/conflicting records; incomplete discovery; mismatched versions; changed data between plan/run; missing acknowledgement/completion. | Non-success diagnostic, no full-suite fallback, retained partial ledger. |
| No false green | Worker exits zero without results, fails before handshake, crashes after one result, or reports an unexpected UID; exit ignores do not suppress plan failure. | Assert coordinator and outer SDK exits independently. |
| Empty and aggregate policies | More partitions than groups; truly empty suite; all skipped; per-module minimum; global SDK minimum; maximum failures; timeout. | Whole-command summary and exact child/coordinator/SDK exits. |
| Process composition | Apphost, DLL host, response files, spaced paths, environment/config providers, launcher rejection, cancellation and nested module limits. | No orphan task-owned PIDs, shared-output collisions, or leaked temporary files. |
| Native SDK identity | Multiple concurrent Instance IDs in attempt 1; two modules/TFMs including one failure; supported, absent, false, old/incompatible capabilities and intentionally absent discovery handshake. | Real shipping SDK terminal, not only fake protocol fixtures. |
| Reports and publishing | Multiple distinguishable data rows with pass/fail/skip and attachments; TRX/HTML disjoint merge; filename collisions; partial report; CI run/count association; unsupported-kind preservation. | Open actual merged HTML, inspect terminal, and record hosted CI UI as unverified unless available and authorized. |
| Feature interactions | Explicit process-Retry/server/hot-reload/device rejection; in-process retries retain result identity and attempt meaning. | Error text and help/info extension availability. |

Implementation commands must use the pinned toolchain and pack after every
producer change, for example `.\build.cmd -pack -c Release -bl`, followed by
focused acceptance runs using the existing fixture and isolated consumer cache.
Record exact SDK/package versions and hashes, loaded binaries, source revisions,
commands, expected and executed identities/counts, artifacts, and parent/child
exits. Real `dotnet test` SDK versions/branches need an explicit matrix rather
than assuming the repository SDK is the shipping consumer.

For new behavioral cases, retain tests while removing the relevant behavior in
an isolated compiling comparison: grouping/selection, concurrency, missing-work
detection, and result merge must fail at the intended assertions, then pass with
the feature. Build/restore failures, zero tests, and skips are not red evidence.
No tests or product demonstrations are needed for this documentation-only
proposal, and none are claimed.

## Review history

### D0: initial synthesis

Independent multi-model architecture and adversarial reviews are pending.
Record each round's model, reviewed revision/digest, concrete counterexample,
resolution or rejection rationale, and remaining blocking concerns here.
Agreement is not a substitute for the shipping validation gates above.

## Open questions

1. Can the generic plan safely snapshot run-only provider selection without
   adding a second execution-planning framework? MSTest-native hash selection is
   the preferred simplification if not.
2. Can stable UID anchors represent all supported runtime/folded data while
   reconciling missing work without mistaking multiple results for duplication?
3. What additive SDK negotiation is necessary for concurrent workers, truly
   empty partitions, original exit policies, and report/publication ownership?
4. Should declared resource/dependency components be co-located automatically,
   or should v1 reject their presence rather than imply cross-process safety?
5. Which report/CI integrations must be supported for the initial useful preview,
   and what exact package/SDK versions and delivery owner will ship it?
