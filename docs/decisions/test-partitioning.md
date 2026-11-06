# Test partitioning: research and decisions

Status: design proposal, revision D1, 2026-11-06. D0 was rejected by both
independent reviewers; this revision changes the architecture. No product
implementation, shipping compatibility, or speedup has been demonstrated.

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

**Choose MSTest for v1.** Implement deterministic group selection in native
MSTest's normal MTP request path, shipped in `MSTest.TestAdapter`; no new NuGet
package or framework-wide public grouping/replay API is needed.
`--partition i/n` is an ordinary test run: no orchestrator, preliminary discovery
process, UID transport, or reconstruction through a fake framework.

Build local fan-out on that primitive with an internal MSTest implementation of
MTP's existing `ITestHostExecutionOrchestrator`. It launches normal
`--partition i/n` runs and owns bounded process scheduling and aggregation.
The single-partition CI feature can ship before local fan-out; local fan-out
is nevertheless fully specified below rather than mistaken for a free extra.

MSTest owns class/dependency topology, partition selection, and execution
receipts. MTP owns normal process verdicts and its existing lifecycle/protocol.
The small intentional-empty selection contract is initially internal, using the
adapter's existing friend relationship with MTP. Generalize only after a second
framework demonstrates the need; other frameworks must reject unknown options,
not fall back to running everything.

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
switch. Native .NET 10+ MTP forwarding is the first SDK surface. Legacy MSBuild
forwarding is a separate, explicitly validated surface, not implied support.
`--partition 1/1` selects the same work as an ordinary run.
`--list-tests --partition i/n` lists discovery-context selection, which can differ
from run-context provider selection; it is not execution evidence.
Reject `--list-tests --partition-count n`, conflicting partition options,
invalid numbers, and worker-only settings supplied without a coordinator.

Existing framework parallelization settings remain unchanged inside a worker.
The advertised process limit is per module, not solution-wide. With `m` modules
and `p` workers per module the outer SDK can schedule up to `m * p` workers.
Recommend `--max-parallel-test-modules 1` for the first local adoption; do not
pretend nested parallelism is a global CPU or memory budget.

### Safety boundary

The initial release keeps an entire concrete MSTest class and its data rows
together, coalescing classes connected by declared dependencies. It does not
split a class to manufacture balance.

**Explicit partitioning permits different processes to execute concurrently,
including classes marked `[DoNotParallelize]`.** In-process parallelization and
its deferred nonparallel tail remain unchanged inside each worker. This is not
permission to assume an external fixture is isolated.

`ResourceLock` is already documented as process-scoped
([implementation](../../src/Adapter/MSTestAdapter.PlatformServices/Execution/ResourceLockManager.cs)).
Do not silently turn process-global locks such as `"PATH"` into cross-process
serialization: that would eliminate the feature's benefit for its target users.
Keep existing lock behavior within each process and diagnose declared lock keys
whose users span partitions. Do not promise that diagnostic finds undeclared
resources or makes a file/database lock safe.

Assembly setup/cleanup repeats in each nonempty worker and may overlap.
Class setup/cleanup stays with its class. No global barrier or once-per-machine
assembly fixture is introduced. Users needing shared external resources must
isolate them, retain sequential execution, or supply their own coordination.
Set invocation-owned per-process `MSTEST_PARTITION_INDEX` and
`MSTEST_PARTITION_COUNT` environment values before user discovery/setup;
`TestContext.Properties["MSTest.PartitionIndex"]` and
`["MSTest.PartitionCount"]` expose the same 1-based numeric values during tests.
Resolve CLI values once and reject inconsistent inherited bootstrap values.
These are user-visible contracts to document, not existing variables.

A suite consisting of one class/dependency component may receive no speedup.
Report group count, nonempty buckets, and the largest group; do not silently
weaken the grouping contract to improve utilization.

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

### 2026-11-06: D0 review and design experiments

Claude Opus 5.5 (`claude-opus-5.5`) and Gemini 4 Argon (`gemini-4-argon`)
independently reviewed D0 at
`438bf1e5fbea39f746d58d7947be2a7bd2ebdaa5`, document SHA-256
`031A03A229AE7CCFF56A1151B12CCA459B12A1E8A3E7739EF02E6DDDE2C411D1`.
Both rejected discovery/replay as the first architecture: run-only providers,
execution-time data, UID-expression costs, dependency metadata, and the Retry
orchestrator conflict create avoidable correctness and adoption problems.
Their source analysis is review evidence, not executed product evidence.

The coordinator independently read
[SDK TestApplicationHandler](https://github.com/dotnet/sdk/blob/5245becf38b656f2a2e2bd49d99024ca03f7cf51/src/Cli/dotnet/Commands/Test/MTP/TestApplicationHandler.cs).
Unknown orchestrator UIDs are accepted at handshake, but an orchestrator-only
successful exit without a TestHost is not generally accepted as no-work:
affected-test/mapping features have specific exceptions. D1 avoids relying on
such an exception. This observation is about that source revision, not proof
that every released SDK behaves identically.

A standalone PowerShell design-algebra experiment, **not an implementation of
MSTest/MTP**, exercised 10,000 synthetic group keys across seven partitions:
complete membership, range, enumeration-order independence, unrelated-group
stability, large counts, UTF-8 length framing, and JSON string round trips.
Observed bucket counts were `1394, 1473, 1417, 1444, 1480, 1449, 1343`; these
demonstrate neither real-suite balance nor speedup.

It also falsified the architecture reviewer's proposed post-filter component
key: with fields `Acme.Tests, Acme.A, Acme.B` and `n=2`, the component hashes to
partition 1; narrowing the key to `Acme.Tests, Acme.A` hashes to partition 2.
A failed `A` retried in partition 1 could disappear. Therefore partition
topology must be independent of host filter narrowing, even though execution
continues to honor the existing filtered-prerequisite semantics.
Reproduction encoding and vectors are specified below; the scratch experiment
is not a product test or a substitute for the planned package tests.

### Alternatives and decision

| Alternative | Benefit | Cost / disposition |
| --- | --- | --- |
| Keep projects/categories and hand-written scripts | No new product surface. | Does not solve the stated project proliferation and discovery-output problem; retain as workaround. |
| MSTest-native hash selection plus thin optional fan-out | CI mode is an ordinary run; selection uses native metadata and normal run filters. | Chosen for v1. Other frameworks do not gain support automatically; local fan-out still needs lifecycle/report integration. |
| MTP filter provider performs discovery and launches workers | Looks reusable. | Conflates request constraints with planning, creates recursion/lifecycle problems, and does not make adapters support a new filter. Reject. |
| Generic UID-level round-robin | Simple and roughly balances discovered case counts. | Depends on discovery ordering and splits fixtures/dependencies/data. Reject as the default. |
| Fake framework wrapping every result | Prototype proves the broad concept. | Reconstructs framework/report semantics and can lose metadata, attachments, or outcomes. Reject. |
| MTP orchestrator plus generic grouping/replay capability | Potential shared planning across frameworks. | D0 direction, rejected in round 1. Discovery/run drift, replay semantics, and a speculative public API are unnecessary for the first supported framework. |
| Sorted ordinal rank modulo `n` | Exactly balances group counts when group sizes are ignored. | Not chosen: adding/filtering another group can move existing assignments, particularly across retry narrowing. Hashing provides a stable predicate; neither method balances duration. |
| Coalesce `ResourceLock` / all `[DoNotParallelize]` users | Appears more conservative. | Rejected as the default: strengthens process-scoped contracts and serializes the process-global-state suites this feature targets. Explicit cross-process locks are future work. |
| SDK-only scheduler with historical timings | Potential global balancing. | Cross-repository coordination and a larger shipping/deployment contract. Defer; do not require it for the first useful release. |

## Selection contract

### Topology first, selection second

Introduce an internal native-MSTest planning seam over neutral test metadata,
**before discovery's current host-filter rejection**. Merely moving code inside
`ExecuteTestsInSourceAsync` is insufficient: `UnitTestDiscoverer` can already
have removed tests by then.

The seam reads all supported concrete classes/method declarations in a source,
attribute dependencies, and resolved `testconfig.json` dependency declarations.
Use a non-mutating edge collector sharing the existing declaration resolver;
do not apply declarations twice to the elements later executed. Do not execute
assembly/class setup, test methods, or runtime data factories to build this
class/dependency topology.

Make each concrete class a vertex and each resolvable declared dependency an
undirected edge for placement only. Its connected component is an indivisible
placement group. Preserve directed dependencies for the existing executor.
No class instance, row, namespace parsed from a UID, or display name is the key.
Inherited tests use their concrete execution class. Support only generic class
shapes already executable by MSTest; this feature does not add generic tests.

Compute this topology independently of the host filter. Then evaluate the
normal request's composed MTP/CLI/runsettings selection `F` and retain:

```text
selected(i, n, F) = { t in ordinaryEligible(F) : partition(group(t), n) == i }
```

In particular, for a narrowing constraint `Q`,
`selected(F AND Q) == selected(F) intersect ordinaryEligible(Q)`.
Retry UID narrowing cannot move a test to another partition.
This intentionally retains filtered-out topology bridges for **placement only**;
it never reintroduces those tests into execution. Missing filtered prerequisites,
cycle diagnostics, and skip propagation retain existing executor semantics.
Unresolvable declarations retain the existing warning/error behavior.

Run-only MTP providers are evaluated once in the normal Run request. There is
no Discovery-to-Run snapshot or UID replay. MSTest's later programmatic
`TestFilterProvider` still makes its ordinary run/drop/skip decision. A
legitimate drop is not a missing UID error. Dropping every selected test still
follows ordinary zero-test policy; it is not an intentionally empty bucket.

Rows remain inside their class, including folded and execution-time data.
Normal discovery may still invoke discovery-time user data factories in each
process; do not promise side-effect-free discovery or one global enumeration.
All jobs must use the same test bits, topology, selection inputs, and compatible
environment. Runtime data nondeterminism inside an assigned class is the
framework/user's normal behavior, not a row replay contract.

### Canonical algorithm

For each placement component, encode fields in this order:
`"mtp.partition.v1"`, the assembly's metadata simple name, and its distinct
concrete class full names sorted with `StringComparer.Ordinal`.
For each field write its UTF-8 byte length as UInt32 big-endian followed by
its UTF-8 bytes, without a BOM or terminator. Do not normalize Unicode,
case, paths, or assembly versions. Assembly simple name is metadata, not
the deployment filename; paths, row/display labels, and randomized
`GetHashCode` never participate.

```text
partition(component, n) = 1 + UInt64BE(SHA256(encodedFields)[0..8]) mod n
```

Examples from the design experiment (the domain field is implicit here):

| Remaining fields | SHA-256 | `n=4` | `n=7` |
| --- | --- | --- | --- |
| `Acme.Tests`, `Acme.CheckoutTests` | `69a7b6b77714706c93cabfc95fe8e9a16f80ea87ef48ce219f5fe4c08e342e45` | 1 | 5 |
| `Acme.Tests`, `Acme.CartTests` | `134e357316c9c3bd8d18a561192be5c6ccdec18302c5e919405e169dd9780e94` | 2 | 7 |
| `Acme.Tests`, `Acme.Nested+Fixture` | `0c175d40a80712742b08686ef72c281ba461868a84a823d5c6ec68d24586795e` | 1 | 2 |

Byte framing removes concatenation ambiguity. Full component records remain
distinct; a bucket/hash collision only affects balance. Adding an unrelated
component does not change an existing assignment; changing a dependency
component, class name, assembly simple name, or `n` may do so.
TFMs/architectures are separate test modules, not different buckets of one
module, even when their class assignments happen to coincide.

### Execution receipts, not replay manifests

Each ordinary partitioned request produces a versioned MSTest-owned receipt.
Write the plan record before executing selected anchors and atomically seal
completion only after normal framework completion and message-bus drain.
`context.Complete()` in a `finally` block is not proof of successful execution.

Schema v1 contains: algorithm/schema version; module binary fingerprint and
TFM/architecture; selection-configuration fingerprint; logical run ID when
supplied; request/attempt identity;
`i`, `n`; topology digest; effective-selection digest; eligible and assigned
group/anchor counts; settled anchor dispositions; final result counts; raw
verdict; and completion/truncation status. Hash ordered, length-framed records,
not ambiguous joined strings. Effective-selection digest includes stable
method/known-row anchors, not group count alone. Runtime-only rows use their
method anchor; this does not assert a known row count before enumeration.

An anchor settles by ordinary completion, skip, or an explicit legitimate
programmatic drop. Superseded in-process retry outcomes are not final outcomes.
Only the native executor can seal the receipt; missing receipt, early exit,
unsettled scheduling anchors, or framework failure cannot become a complete
successful receipt. This proves framework-owned scheduling completion, not
that an arbitrary custom test method implementation produced every theoretical
data row.

Local coordination verifies one complete receipt for each index `1..n`, matching
module/topology/effective-selection fingerprints and counts, and that assigned
anchor/group counts sum to the common eligible counts. Do not compare only
observed result count: folds, retries, drops, and dynamic rows use other units.
Selection drift is a non-success partial-run diagnostic. Other workers may
already have produced side effects; no distributed transaction is promised.

Independent CI jobs have no shared in-product coordinator. Ship a receipt
verifier in the CI sample: require every index exactly once, equal `n`, common
bits/configuration/topology/selection and a common caller-supplied logical run
ID (`TESTINGPLATFORM_LOGICAL_RUN_ID`), complete coverage, and successful
**outer job exits**. Download artifacts
only from that CI run/attempt; reject stale, missing, duplicate, or incompatible
receipts. Receipts prove selection coverage, not an outer orchestrator's exit.
No local job can certify that its siblings were launched.

### Empty selection and count-policy ownership

Let `E` be eligible scheduling anchors before partition selection and `A_i`
the assigned anchors. These are not test-result counts.

| State | Required behavior |
| --- | --- |
| `E=0` | Preserve ordinary zero-test policy (`8` by default), or an explicit minimum's failure (`9`). No new success exception. |
| `E>0`, `A_i=0`, normal framework completion | Intentional empty partition: suppress only the default/strict zero-test check. No fixture execution; normal TestHost handshake/session still occurs. |
| `A_i>0`, all results skipped | Existing `allow-skipped`/`strict` behavior; strict remains `8`. |
| `A_i>0`, programmatic drop removes everything | Existing zero-test behavior; not a known-empty partition. |
| Positive assignment but no normal completion/receipt | Failure regardless of an OS exit of `0`. |

Implement a request-owned internal intentional-empty selection token consumed
by `TestApplicationResult` and normal summary formatting. Only native partition
selection can issue it, and only completed execution can validate it. Do not
inject `--ignore-exit-code 8`, report a fabricated passing test, or classify a
zero-result worker by exit alone. An explicit minimum is **not** suppressed.

In single-partition mode a test-application minimum/timeout/maximum-failure
option retains its ordinary per-job meaning. SDK options before `--` retain
their documented whole-invocation meaning. In local fan-out, the coordinator
owns module-wide minimum/zero/maximum/deadline policy: do not copy it to every
worker. Its private worker context defers count policy to the coordinator;
failures, aborts, and incomplete execution remain visible. Folded result counts
follow the platform's existing counting units, not the number of groups.

## Local fan-out, composition, and consumers

### Process lifecycle and Retry

Local mode registers exactly one internal `MSTestPartitionOrchestrator`; it
does not use middleware to repeat `next`. Launch `n` ordinary runs, each with
`--partition i/n`, through a streaming queue bounded by `p`. There is no
discovery child and no UID list in the launch command. Launch empty buckets too:
their normal handshakes/receipts distinguish no-work from broken workers.
No count-sized preallocation is needed.

Preserve working directory, expanded user arguments, response-file semantics,
and inherited settings using the existing launch/argument helpers. Response
files are not banned; no new UID quoting problem is introduced. Strip local
fan-out options and coordinator-owned policies before relaunch, preventing
recursion. Finalize per-worker environment/results paths without bypassing
environment-provider locks or controller authorization.

The coordinator owns handles, private receipt/progress transport, deadlines,
and cleanup. Stop scheduling on cancellation or infrastructure failure; request
cooperative stop, then terminate only owned remaining process trees after a
bounded grace period. Continue independent partitions after ordinary test
failure. Drain available diagnostics and produce a partial ledger. Do not
automatically rerun partially completed partitions.

`--partition i/n` does not occupy an orchestrator slot and must compose with
process-level Retry: original partition arguments survive relaunch and stable
pre-filter topology keeps failed tests in their partition. Retry integration
must preserve the first full-selection coverage receipt and distinguish later
UID-narrowed attempt receipts. The existing artifact post-processing hook can
host a MSTest-owned retry receipt reducer; the final Retry/job exit remains
authoritative for outcomes. Missing/partial initial coverage is not repaired
by selecting a later attempt with fewer tests.

Reject **local fan-out plus process-level Retry** before launching anything
until explicit nested-orchestrator semantics are designed. In-process MSTest
retry is ordinary framework behavior in both modes.

### SDK and host modes

Single-partition mode is a normal TestHost in direct execution and native
`dotnet test`, even for intentional empty selection. It adds no orchestrator
wire behavior or SDK no-handshake exception.

Local mode handshakes once as an orchestrator; all workers use the existing SDK
channel as TestHosts sharing the module's Execution ID, unique Instance IDs,
and attempt `1`. Partition index is not attempt number. Workers publish their
original complete framework/report streams; the parent does not reconstruct
TestNodes or register a fake framework. Multiple modules/TFMs remain distinct.
Standalone execution needs no SDK handshake but still requires child
acknowledgement and completion receipts.

Validate concurrent instances and the final module verdict with real shipping
SDKs. Static source support is not compatibility evidence. If local fan-out
requires an SDK addition, use presence-aware capability negotiation and reject
undeclared/false/incompatible support before work, or defer that SDK surface.
Do not silently enable a path just because a generic orchestrator handshake
was accepted. A local-mode preview may support direct executables first; the
single-partition SDK surface must not wait for this gate.

V1 targets native MSTest/MTP desktop processes on Windows, Linux, and macOS.
Reject JSON-RPC/IDE server requests and hot reload for partition options;
reject device/browser/WASI/packaged-app/custom launcher execution in local
fan-out until lifecycle support is explicit. Cover apphost, `dotnet Tests.dll`,
and supported Native AOT separately. Existing no-handshake help/informational
paths remain informational, not failed execution workers.

### Verdict ledger

Keep raw child exits, structured count/result causes, complete/partial receipts,
and scheduling state. Never aggregate using maximum/last child exit.
Precedence for the local coordinator is: invalid protocol/launch/incomplete
work (`1`, or configuration/protocol-specific existing code); explicit stop
policy/cancellation (`13` maximum failures, `3` abort/timeout); test failure
(`2`); module count policy (`9` minimum before `8` zero); success (`0`).
Persist every contributing cause even when one has precedence.
Cleanup errors are diagnosed and cannot turn an already unsuccessful run green.

Default behavior must never ignore missing work. A user-specified global
exit-code-ignore remains an explicit existing policy, applied once at the outer
owner after raw verdict calculation; the receipt and ledger stay unsuccessful
or partial and the CI coverage verifier rejects incomplete work. Do not forward
ignore settings to workers and then infer success from their filtered exits.

### Files, reports, and presentation

Single-partition CI jobs produce ordinary existing reports and CI publishing
for their selected work, plus a receipt. Do not make cross-job merging or a
coordinated hosted CI run a prerequisite for this useful first feature.

Local results live below
`<results>\partitioning\<run-id>\<module-key>\partition-<i>-of-<n>\`.
Make report, receipt, diagnostic, dump, and relative attachment paths resolve
inside that partition root. Preserve user-selected leaf filenames there.
Reject a configured shared absolute output destination that cannot be safely
isolated; do not silently overwrite it or assume every extension honors the
results directory. Undeclared test-authored external files remain user-owned.

For local report merging add an explicit experimental
`ArtifactPostProcessingMode.TestPartitions`; it does not exist today.
Enable only processors declaring this disjoint-union mode. TRX and HTML
are the first complete-report targets; keep other artifacts as separate indexed
outputs unless their processor is enabled. Preserve originals and relocate
relative attachments. A failed merge is a reported gap, not a successful
complete report, and retains the underlying test verdict under existing
artifact-failure policy. A partition is not an additional logical test module.

Local CI summary/publication extensions need explicit defer/merge support with
one coordinator owner. Reject unsupported combinations before launching rather
than allowing competing final summaries/uploads. Collectors with whole-run
coverage thresholds likewise need a real aggregate or a clear rejection;
passing each partial coverage threshold is not equivalent.
The module coordinator combines partitions once; the outer SDK can then
combine modules once. Never publish both partial inputs and the same merged
results as independent copies.

For standalone fan-out, the parent renders bounded partition progress and an
aggregate verdict; capture full per-worker stdout/stderr logs and serialize
completed failure blocks so child terminal streams cannot interleave. For
native SDK execution, use its existing result renderer. Inspect both rendered
surfaces, including narrow widths, ANSI disabled, long names and failures.

## Implementation plan and release gates

Each phase is an independently reviewable change, not authorization to implement
it in this design-only session:

1. **Native planning seam and algebra.** In
   `MSTestAdapter.PlatformServices/Discovery` and `Execution`, collect unfiltered
   class/dependency metadata without changing normal discovery publication.
   Share declaration parsing with `TestDependencyDeclaration`, compute immutable
   components, and implement hash vectors and filter-narrowing invariants.
   Keep the option-absent path unchanged. Prove configuration dependencies are
   collected before host filters, not only inside `SourceExecution`.
2. **Single-partition product.** Register `--partition` in native
   `MSTest.TestAdapter/TestingPlatformAdapter`, add request selection and
   receipts, expose partition context, and wire the internal intentional-empty
   token into MTP result/summary handling. Update internal API baselines and
   resource accessors/catalogs through their generators; no new public grouping
   API. Add the Retry receipt reducer before advertising Retry composition.
3. **Ship the first useful slice.** Pack `MSTest.TestAdapter`,
   `MSTest.TestFramework`, and the aligned `Microsoft.Testing.Platform`;
   include `Microsoft.Testing.Extensions.Retry` in its composition cases.
   Prove direct apphost/DLL and real native SDK `i/n` behavior, existing
   reporters, help/info, and a local/CI receipt-verification sample.
   Document per-job count policy, repeated fixtures, resource scope, and limits.
4. **Local orchestration.** Add `--partition-count` and
   `--max-parallel-partitions`, streaming scheduler, private receipts/progress,
   owned cancellation/teardown, module policy aggregation and output subtrees.
   Reuse MTP lifecycle helpers; reject unsupported launchers and simultaneous
   process Retry. Prove actual concurrent execution with isolated statics.
5. **Local consuming surfaces.** Add and track the experimental
   `TestPartitions` post-processing mode and supported TRX/HTML processors.
   Complete single-owner CI publication, or reject unsupported reporters.
   Validate real SDK concurrent instances/module aggregation on the exact
   minimum supported versions; defer only that surface if not yet supported.
   No fake server or ordinary unit test substitutes for this gate.
6. **Preview and rollout.** A named delivery owner carries all dependency flow,
   release-branch proof/backport decisions and downstream enablement. Do not
   enable by default in `MSTest.Sdk`. Measure discovery/setup cost, memory,
   actual overlap, imbalance and wall time on real sequential suites. Reject
   linear-speedup claims and advise smaller `n` when startup or fixtures dominate.

Target the next compatible MSTest 5/MTP 3 mainline preview, based on the observed
source train, not a fictional published version number. No stable backport is
assumed. The internal MTP/adapter contract requires aligned package minimums;
do not mix a new adapter with an older MTP and silently lose empty-selection
behavior. The owner must record exact package IDs/versions, SDK versions,
shipping branches and servicing decisions before enabling downstream use.
Assignment of that named owner is an implementation handoff gate, not a design
claim that a particular person has accepted the work.

## Validation and demonstration

All cases below are **planned**, not executed product evidence.

| Requirement / risk | Proposed automated evidence | Consuming surface / rendered evidence |
| --- | --- | --- |
| Disabled means unchanged | Compare old/new aligned packages with partitioning absent: exact identities, lifecycle counts, outcomes and exits. | Existing terminal/report behavior. |
| Correct partition algebra | Known vectors; shuffled metadata, Unicode, duplicate labels, large `n`; union equals baseline, intersections empty, and filter/Retry narrowing preserves assignment. | Diagnostics show algorithm, group count, nonempty buckets, largest group and effective budget. |
| Real isolation benefit | Classes sharing a static can each run sequentially inside different PIDs; use rendezvous markers to prove overlap without wall-clock assertions. | Direct terminal and native SDK at normal/constrained widths. |
| Fixtures, constraints, data | Classes/rows stay together; attribute/configuration dependencies co-locate, including filtered bridges; no filtered prerequisites are added; runtime/folded data complete. Test resource diagnostics, not invented cross-process locks. | Exact per-result identity/outcome associations, lifecycle markers and isolated resource names. |
| Original filters preserved | Category/runsettings/UID/provider intersections; run-only providers; programmatic drop/skip; empty selection; Retry with only one failed member of a component. | Same work as ordinary filtering intersected with partition assignment, without UID replay. |
| Receipts and drift | Same-count/different-anchor universes; changed topology/configuration; partial discovery; malformed/incompatible receipt; stale/duplicate/missing CI job; Retry narrowed receipt mistaken for full coverage. | Explicit failure/partial status and outer job exits; no full-suite fallback. |
| No false green | Child exits zero before a completion seal, fails before handshake, or crashes after one folded row; `context.Complete` in a fault path is insufficient. Test legitimate drops separately. | Check scheduling-anchor dispositions and child/coordinator/SDK exits, not one result per UID. |
| Empty and aggregate policies | One group across many buckets; true empty filter; all skipped/drop; strict zero policy; explicit per-job minimum; local aggregate minimum; SDK global minimum; maximum failures and timeout. | Intentional-empty success only with proof; actual empty/default `8`, minimum `9`, strict all-skipped `8`. |
| Process composition | Apphost, DLL host, response files, spaced paths, environment/config providers, launcher rejection, cancellation and nested module limits. | No orphan task-owned PIDs, shared-output collisions, or leaked temporary files. |
| Native SDK identity | Ordinary/empty single-partition host; concurrent local Instance IDs in attempt 1; mixed-success modules/TFMs; supported/old SDKs; no-worker/incompatible/absent handshake and capability cases. | Real shipping SDK terminal, not only fake protocol fixtures. |
| Reports and publishing | Distinguishable pass/fail/skip rows with attachments; local TRX/HTML union and module count 1; explicit filename collisions; partial merge; CI identity/count association and unsupported combinations. | Open actual HTML and terminal; hosted CI UI remains a gap unless available and authorized. |
| Feature interactions | Single-partition Retry preserves first coverage and narrowed attempts; local process Retry rejected; in-process retries, server/hot-reload/device exclusions and helper responses. | Exact errors and help/info availability; no change to default behavior. |

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
No product tests or demonstrations have been run for this documentation-only
proposal. Standalone algorithm experiments establish only the stated algebra.

## Review history

### Round 1: D0 rejected, architecture replaced

| Finding / disagreement | Resolution in D1 |
| --- | --- |
| Both models: generic discovery/replay fails run-only selection and dynamic/folded identities; Retry loses its orchestrator slot. | Choose native MSTest selection; defer the generic capability. Separate ordinary CI runs from local orchestration. |
| Both models: missing configuration dependencies, partial work, empty buckets and report ownership need mechanisms, not "test later." | Define an unfiltered metadata seam, sealed receipts, internal intentional-empty token, policy ownership and staged local consumers. |
| Claude: coalesce dependencies after filtering; Gemini: before filtering. | Coordinator's concrete retry-narrowing counterexample rejects post-filter placement keys. Use pre-filter topology but preserve existing filtered execution semantics. |
| Gemini: coalesce resource locks and all nonparallel classes across processes. | Rejected as a stronger, unrequested guarantee contradicting process-scoped locks and the motivating process-global-state use case. State opt-in concurrency and external-resource limits explicitly; expose partition context and diagnostics. |
| Both models: SDK empty orchestrator-only success is unsafe. | No orchestrator in CI mode; local mode launches normal hosts even for empty buckets. Real SDK verification remains required. |
| Gemini: optimize the existing UID expression path. | Not needed by the selected architecture; defer this unrelated existing optimization rather than expand the feature. |

D1 is being submitted to the same two independent models for a second,
counterexample-driven review. Coordinator synthesis is GPT-6.1 Sol
(`gpt-6.1-sol`). Review acceptance does not satisfy package/SDK release gates.

## Open questions

1. Implementation must demonstrate the neutral metadata seam is available on
   every supported native discovery path, including any source-generated path,
   without executing runtime data to infer class topology. Unsupported variants
   must reject the option until they satisfy the same contract.
2. The exact minimum shipping SDK versions for intentional-empty ordinary hosts
   and concurrent local workers need real product runs. A required local-mode
   SDK change is a separately owned dependency, not an assumption.
3. TRX/HTML local merge and each CI reporter require separate consumer evidence;
   unsupported local combinations must remain disabled/rejected.
4. Assign the delivery owner, package versions, preview branch and explicit
   backport decision before implementation rollout. No global distributed
   scheduling, cross-process locks, duration history or automatic retries of
   partly completed partitions are included.
