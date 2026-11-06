# Test partitioning: research and decisions

Status: design proposal, revision D2, 2026-11-06. Two cross-model review rounds
have changed and tightened the design; final confirmation is pending. No
product implementation, shipping compatibility, or speedup is claimed.

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
The in-process intentional-empty token is internal, using the adapter's existing
friend relationship with MTP. Its meaning must also cross the SDK boundary via
the negotiated completion contract below; an internal token alone is not enough.
Generalize framework grouping only after a second framework demonstrates the
need. Other frameworks reject unknown options, not fall back to running everything.

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
An explicit `p` is an upper bound; active partition trees never exceed
`min(p, n)`, and no idle worker/process pool is eagerly allocated.
All numbers are positive invariant-culture Int32 values, and `1 <= i <= n`.
There is no arbitrary limit on partition count, and the planner must not
allocate an array of size `n` for an almost-empty suite.

These are test-application extension options, not a new SDK built-in `--shard`
switch. Native MTP mode (available from .NET 10, selected through `global.json`)
is the SDK surface, but partitioning requires an SDK with the new acknowledged
completion contract; today's generic MTP support is not sufficient. Direct
apphost/DLL execution can ship independently. Legacy MSBuild forwarding is
explicitly unsupported in v1, not implied support.
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

### 2026-11-06: D1 review and second counterexamples

Both reviewers accepted the native-selection direction but required targeted
corrections. The coordinator independently traced the SDK's
[whole-command policy](https://github.com/dotnet/sdk/blob/5245becf38b656f2a2e2bd49d99024ca03f7cf51/src/Cli/dotnet/Commands/Test/MTP/MicrosoftTestingPlatformTestCommand.cs):
even a normal TestHost exiting `0` is insufficient when
`ShouldFailForNoExecutedTests` sees zero invocation results. D1's internal token
therefore did not establish native SDK empty-partition success. D2 introduces
an explicit versioned completion message and acknowledged support, with no
claim that existing SDKs already implement it.

The design experiment also demonstrated a count-only coverage hole: universe
`{A,B,C,D}`, worker 1 executing `{A,C}`, and worker 2 executing `{A,C}` have
matching universe/count claims and sum to four executions, but miss `B,D`.
D2 requires independently recomputed bucket ownership and actual settled-set
verification, not only summed counts.

Source verification corrected two review details: the inherited ignore variable
is `TESTINGPLATFORM_EXITCODE_IGNORE`, not the review's guessed spelling; and
the current narrowed discovery cache is JSON-RPC-only, already outside v1.
The design still explicitly guards against any narrowed cache supplying
placement topology. Neither correction changes non-partition execution.

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
| Require every bucket to be nonempty, or ignore exit `8` | Avoids an SDK feature in a few simple suites. | Rejected: hashing can leave empty buckets even with many groups, filters/modules differ, and ignoring `8` can hide genuinely empty selection. Use direct execution until the SDK contract ships, not a success-shaped workaround. |
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
class/dependency topology. Collect the catalog once during the same metadata
walk; do not rediscover the assembly or repeat discovery-time factories or
warnings to recover filtered-away metadata. Validate configuration declarations
against the pre-partition catalog, so excluding a valid dependent by partition
does not manufacture an unmatched-declaration warning.

Never derive topology from a UID-narrowed discovery cache. The current native
cache is enabled only for JSON-RPC, which v1 rejects; keep an explicit
partition-mode guard rather than rely on that incidental limitation. A future
cache must supply the complete topology or be bypassed for partitioned runs.
The option-absent discovery/cache path must not change.

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

Each partitioned request produces a versioned MSTest-owned **catalog plus
receipt**, used for verification only, never for UID replay. Catalog schema v1
contains module/binary and selection-configuration fingerprints, all class keys,
resolved dependency edges and their normalized configuration inputs, and the
eligible class/method anchors under the normal host filter. Hash canonical
ordered records with the same unambiguous length framing as above.

Placement/coverage anchors are concrete class plus managed method signature,
deduplicated across materialized data rows. All eligible rows of that method
are assigned together. Do not compare unstable row-index UIDs or runtime-only
row counts across processes. Within a worker retain its exact selected concrete
elements and runtime enumeration lifecycle to prove their ordinary completion.
The exported contract proves method-placement coverage, not deterministic
runtime data or the correctness of a custom test-method implementation.

Receipt schema v1 contains: schema/algorithm versions; catalog content hash;
module, configuration, topology and eligible-anchor digests; logical run ID;
request/attempt identity and Initial/RetrySubset role; `i`, `n`; eligible and
assigned group/method counts; planned bucket-set digest; independently observed
settled bucket-set digest; result counts; raw verdict; and complete/partial state.
The role is validated against the Retry parent, not inferred only from an
inherited attempt-number environment variable.

Write the catalog/plan atomically before selected work. Seal a receipt only
after a normal engine return, all scheduled element decisions and required
class/assembly cleanup have finished, no cancellation/graceful/deadline/maximum
stop is active, and framework/session messages have drained. A `finally` call
to `context.Complete()` is never sufficient. Cleanup failure results must be
included in result counts/raw verdict; a complete failed run is not successful.
An escaped fault, unhandled background/session failure, crash, or unsettled
element produces partial evidence, not an intentional-empty token.

An element settles by normal completion, skip, or explicit programmatic drop.
The executor separately checks all assigned concrete elements and that any
runtime data enumeration finished normally before settling their method anchor.
Superseded in-process retry results are not final outcomes. Multiple results
for one UID are not treated as either duplication or proof of full completion.

The local/CI verifier reads one common catalog, validates every receipt's
catalog/fingerprint equality, independently rebuilds components and recomputes
the specified SHA-256 assignment, and obtains the exact expected anchor set
for each index. Require the planned **and independently settled** set digests
and counts to equal that expected bucket, every index `1..n` exactly once,
disjoint ownership, and exact union coverage. Do not accept a job's own claimed
bucket hash as the reference. Store only nonempty bucket entries, not an
Int32-sized array; empty buckets have the canonical empty-set digest.
Catalog and verification cost is proportional to classes/methods, not `n`
times an eager array or a cross-process per-row UID list.

Selection/catalog drift is a non-success partial-run diagnostic. Other workers
may already have produced side effects; no distributed transaction is promised.

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
`E` is measured after all ordinary host filters, including UID constraints,
and before partition selection. RetrySubset requests must never issue an empty
token: zero matched/assigned work in a retry is a missing-retry failure.

In single-partition mode a test-application minimum/timeout/maximum-failure
option retains its ordinary per-job meaning: its minimum applies to that job's
actual result count, not the whole eligible universe `E`. SDK options before `--` retain
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
must treat receipt validation as **mandatory control-plane work**, not a
best-effort report post-processor whose failure only warns.

Extend Retry's existing private parent/child channel with a versioned partition
receipt capability and acknowledgement. A partition-aware child requires that
acknowledgement before tests; an older Retry peer is rejected with upgrade
guidance. Once acknowledged, missing/partial completion metadata from any
scheduled attempt fails the Retry parent regardless of an OS exit of `0`.
No new public grouping API or reverse Retry-to-MSTest package dependency is
required; use the shared first-party wire contract.

The reducer takes catalog, topology, effective initial selection, and eligible/
assigned sets **only from the Initial attempt 1 receipt**. That receipt must be
sealed and cover its complete assigned set even when tests failed. Later
RetrySubset attempts must be sealed, share the same topology/partition, and
settle only requested failing identities from that initial partition. They can
update the latest outcome, not replace the original coverage universe. Reject
an unrelated subset, zero-work retry, missing initial receipt, partial initial
coverage, or an attempt-number-only artifact presented as a root receipt.
The reduced receipt preserves raw failures/truncation independently of final
exit-code ignores. The final Retry/job exit is authoritative for the result;
CI verification also requires the valid reduced coverage evidence.

Reject **local fan-out plus process-level Retry** before launching anything
until explicit nested-orchestrator semantics are designed. In-process MSTest
retry is ordinary framework behavior in both modes.

### SDK and host modes

Single-partition mode is a normal TestHost in direct execution and native
`dotnet test`, even for intentional empty selection. No orchestrator
no-handshake exception is used, but the SDK's own zero-test policy needs the
following **new, additive wire contract**:

1. The host offers partition-completion contract version `1` in its normal
   handshake; the SDK acknowledges supported version `1`. Absent, explicit
   false, unknown version, and acknowledged support are distinct states.
   Partition execution under an unaware/incompatible SDK fails before tests
   with upgrade/direct-executable guidance; never silently run unpartitioned
   or inject `--ignore-exit-code 8`. Direct execution needs no SDK negotiation.
2. Allocate a new optional `TestPartitionCompletionMessage` in the shared binary
   protocol, sent only after acknowledgement and receipt sealing. Include
   version, Execution/Instance/Session identity, attempt/Initial-or-RetrySubset
   role, `i/n`, topology/selection digests, eligible/assigned method-anchor counts,
   completion/selection reason, and raw verdict. Receive/acknowledge it before
   process teardown. Missing, duplicate, inconsistent, truncated, or foreign
   summaries cannot prove intentional emptiness. No fake test or normal test
   result is added.
3. SDK aggregation suppresses **only** implicit/default or strict zero-test
   failure, and only if every targeted module completed successfully with a
   valid Initial intentional-empty summary (`E>0`, `A_i=0`). A module with
   truly empty selection, ordinary zero results/drop, partial/unknown metadata,
   failed exit, or strict all-skipped work invalidates this exception.
   An explicit global minimum is evaluated first and still fails with `9`.
   Normal nonempty runs retain existing result/count policy; this exception
   does not reinterpret skipped results as an empty bucket.
4. SDK rendering must show deliberate partition selection, for example
   `Partition 2/4: 0 methods assigned from 12 eligible methods`, without a red
   failure, fabricated passing test, or misleading claim that 12 tests ran.
   Update native MTP rendering independently. Keep result counts in their
   existing units and planned method-anchor counts explicitly labeled.

Implement both ends together: MTP models/serializers/field IDs and
`DotnetTestProtocolContract.props`, the SDK's vendored-file inventory, handshake
and message handling, `MicrosoftTestingPlatformTestCommand` zero-policy logic,
terminal state, and protocol/real-product tests. Reserve identifiers against the
live wire catalog during implementation; this proposal does not invent an
already assigned message ID. Older peers never receive an unacknowledged
message. General protocol compatibility is not evidence of this capability.

Legacy MSBuild uses a different, positional `RunSummaryInfoRequest` channel and
its own zero-result summary logic. V1 rejects partition options on that path
before user execution. Later support requires its own negotiated summary
extension and packed MSBuild proof; do not append positional fields and assume
compatibility. The serializer is source-linked into both
`Microsoft.Testing.Extensions.MSBuild` and `Microsoft.Testing.Platform.MSBuild`.
Both assemblies/consumers would have to move together.

Local mode handshakes once as an orchestrator; all workers use the existing SDK
channel as TestHosts sharing the module's Execution ID, unique Instance IDs,
and attempt `1`. Partition index is not attempt number. Workers publish their
original complete framework/report streams; the parent does not reconstruct
TestNodes or register a fake framework. Multiple modules/TFMs remain distinct.
Standalone execution needs no SDK handshake but still requires child
acknowledgement and completion receipts.

Validate single-partition completion and concurrent local instances with real
shipping SDKs. Static source support is not compatibility evidence. Local
fan-out requires both partition-completion version `1` and a separate
SDK acknowledgement of local-partition-orchestration version `1` in the
orchestrator handshake before any child starts. That handshake also declares
the validated partition count `n`, allowing the SDK to require exactly the
expected Initial indices and reject inconsistent child counts. The SDK tracks all expected
instances and one final module verdict; local workers are not extra modules.
Verify multiple-instance summaries and this final verdict, rather than assuming
ordinary-host acknowledgement proves orchestration. Reject absent, explicit
false or incompatible local support before work.
Do not silently enable a path just because a generic orchestrator handshake
was accepted. A local-mode preview may support direct executables first; the
single-partition SDK surface waits only for its smaller completion-contract
gate, not for local fan-out.

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
Remove `--ignore-exit-code` from every CLI/response/config source and clear the
actual inherited variable `TESTINGPLATFORM_EXITCODE_IGNORE` in local children.
Validate the finalized environment after providers; if a locked provider would
reintroduce a child-local ignore override, reject the combination rather than
break the lock. Keep the requested policy only at the coordinator.

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
   catalog/receipts, expose partition context, and wire the internal intentional-empty
   token into MTP result/summary handling. Update internal API baselines and
   resource accessors/catalogs through their generators; no new public grouping
   API. Add versioned private Retry receipt acknowledgement and mandatory parent
   validation before advertising that combination.
3. **Direct-executable shipping slice.** Pack `MSTest.TestAdapter`,
   `MSTest.TestFramework`, and the aligned `Microsoft.Testing.Platform`;
   include `Microsoft.Testing.Extensions.Retry` in its composition cases.
   Prove direct apphost/DLL `i/n`, existing reporters, help/info, and a CI sample
   invoking built executables and independently verifying catalog/receipts.
   Document per-job count policy, repeated fixtures, resource scope, and limits.
4. **Coordinated native SDK slice.** Implement the additive completion protocol
   in testfx and `dotnet/sdk/main`, vendor the exact shared sources, add the
   acknowledged capability, SDK whole-run zero-policy correction and rendered
   selection state. Prove real native `dotnet test` for a nonempty leg,
   intentional empty leg, truly empty selection, explicit minimum, strict
   skipped/drop cases, old/unaware SDK rejection and mixed modules/TFMs.
   Advertise no SDK version until the actual released/pinned build passes.
   Legacy MSBuild remains rejected until a separately scoped implementation.
5. **Local orchestration.** Add `--partition-count` and
   `--max-parallel-partitions`, streaming scheduler, private receipts/progress,
   owned cancellation/teardown, module policy aggregation and output subtrees.
   Reuse MTP lifecycle helpers; reject unsupported launchers and simultaneous
   process Retry. Prove actual concurrent execution with isolated statics.
6. **Local consuming surfaces.** Add and track the experimental
   `TestPartitions` post-processing mode and supported TRX/HTML processors.
   Complete single-owner CI publication, or reject unsupported reporters.
   Implement SDK local-orchestration acknowledgement and validate concurrent
   instances/module aggregation on the exact minimum supported versions.
   Direct local fan-out can ship before its SDK surface.
   No fake server or ordinary unit test substitutes for this gate.
7. **Preview and rollout.** A named delivery owner carries all dependency flow,
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
Proposed initial branch decision: testfx mainline MSTest 5/MTP 3 and SDK
`main`; no stable product or SDK backport is assumed. A servicing request for
an older SDK is an explicit additional release decision and validation matrix,
not an implied benefit of an additive wire format.
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
| Receipts and drift | Duplicate `{A,C}` buckets summing to universe `{A,B,C,D}`; recomputed expected bucket vs planned/settled digests; changed dependency config; harmless row reordering; malformed receipt; stale/missing/duplicate job; retry subset masquerading as initial coverage. | Explicit failure/partial status, independent ownership verification and outer job exits. |
| No false green | Throw, cancel, graceful/deadline/maximum stop, early zero exit, missing handshake, crash after one folded row, class/assembly cleanup failure and missing row completion; `context.Complete` in a fault path is insufficient. Test legitimate drops separately. | Required cleanup/element closure, unignored raw verdict, partial seal and parent exits, not one result per UID. |
| Empty and aggregate policies | One group across many buckets; true empty filter; all skipped/drop; strict zero policy; explicit per-job minimum; local aggregate minimum; SDK global minimum; maximum failures and timeout. | Intentional-empty success only with proof; actual empty/default `8`, minimum `9`, strict all-skipped `8`. |
| Process composition | Apphost, DLL host, response files, spaced paths, environment/config providers, launcher rejection, cancellation and nested module limits. | No orphan task-owned PIDs, shared-output collisions, or leaked temporary files. |
| Native SDK identity | Acknowledged versioned completion; ordinary empty host that would previously trigger global `8`; minimum `9`; normal empty/strict skipped/drop not excused; foreign/duplicate/missing summary; local capability; mixed modules/TFMs and old SDK rejection. | Real SDK whole-command exit and deliberate-selection terminal state, not only fake protocol fixtures. |
| Reports and publishing | Distinguishable pass/fail/skip rows with attachments; local TRX/HTML union and module count 1; explicit filename collisions; partial merge; CI identity/count association and unsupported combinations. | Open actual HTML and terminal; hosted CI UI remains a gap unless available and authorized. |
| Feature interactions | Retry private acknowledgement, complete failed Initial plus passing subset, partial Initial plus passing subset remains failed, zero-work subset never empty-success; ignore policy via CLI/config/actual environment variable; JSON-RPC/cache/legacy/device exclusions. | Exact errors/help/info, raw and filtered exits, and unchanged option-absent behavior. |

### Proposed executable case locations

These are planned new files/case names, not claims that tests already exist.
Keep adapter unit tests on the internal TestContainer framework and its allowed
assertions; MTP/acceptance tests use MSTest and their project-specific assertion
rules. Reuse existing help/info tests and package-consuming fixtures.

| Planned location | Concrete cases and decisive assertions |
| --- | --- |
| `test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/Execution/PartitionPlannerTests.cs` | `CanonicalHashMatchesVectors`, `DependencyTopologySurvivesUidNarrowing`, `ConfiguredDependencyCollectedBeforeHostFilter`, `UnrelatedComponentDoesNotMoveAssignment`: compare exact component membership/buckets, not only counts. |
| `test/IntegrationTests/MSTest.Acceptance.IntegrationTests/PartitionSelectionTests.cs` | `FilteredPartitionsAreDisjointAndCoverBaseline`, `RuntimeRowsRemainWithTheirClass`, `RunOnlyProviderAndProgrammaticDropRetainSemantics`, `KnownEmptyDoesNotExcuseEmptyFilterOrExplicitMinimum`, `RetrySubsetCannotReplaceInitialCoverage`: inspect concrete result identities/outcomes, sealed method coverage and exact child exits. |
| `test/IntegrationTests/MSTest.Acceptance.IntegrationTests/PartitionNativeDotnetTestTests.cs` | `NativeSdkKnownEmptySucceedsWithoutFakeResults`, `NativeSdkExplicitMinimumStillFails`, `NativeSdkMixedEmptyAndOrdinaryZeroIsNotExcused`, `NativeSdkOldCapabilityFailsBeforeUserCode`: real SDK command exit, normal handshake/session, result counts, and selection text. |
| `test/IntegrationTests/MSTest.Acceptance.IntegrationTests/PartitionFanOutTests.cs` | `IsolatedStaticsOverlapWithBoundedWorkers`, `ZeroExitWithoutCompletionFails`, `CleanupFaultOrGracefulStopCannotSealSuccess`, `CancellationStopsOnlyOwnedTrees`, `InheritedIgnoreDoesNotHideWorkerFailure`: rendezvous/PID markers, raw receipts, process closure, and final coordinator/SDK exits. |
| `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/DotnetTestPipe/PartitionCompletionTests.cs` | `SummaryRequiresAcknowledgement`, `ForeignDuplicateOrUnsealedSummaryIsRejected`, `LocalModeRequiresSeparateCapabilityAndAllIndices`: wire-contract tests supplement, not replace, native SDK tests. |
| Existing report acceptance tests plus `PartitionFanOutTests` | `PartitionReportsUnionResultsAndRelocateAttachments`, `UnsupportedPublisherOrAbsoluteOutputFailsBeforeLaunch`: parse per-result associations and inspect the actual rendered report/terminal. |
| Planned CI sample verifier tests | `OverlappingBucketsWithCorrectTotalAreRejected`, `RetrySubsetOrStaleRunCannotMasqueradeAsRoot`, `MissingIndexAndChangedDependencyCatalogAreRejected`: use independent catalog recomputation, correct exits and mutation-sensitive negative inputs. |

Implementation commands must use the pinned toolchain and pack after every
producer change, for example `.\build.cmd -pack -c Release -bl`, followed by
focused acceptance runs using the existing fixture and isolated consumer cache.
Record exact SDK/package versions and hashes, loaded binaries, source revisions,
commands, expected and executed identities/counts, artifacts, and parent/child
exits. Real `dotnet test` SDK versions/branches need an explicit matrix rather
than assuming the repository SDK is the shipping consumer.

Focused implementation entry points, **not executed in this design session**:

```powershell
.\build.cmd -pack -c Release -bl
.\.dotnet\dotnet.exe test --project .\test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Partition" -p:UsingDotNetTest=true
.\.dotnet\dotnet.exe test --project .\test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~PartitionCompletion" -p:UsingDotNetTest=true
```

Use the SDK installation selected by the repository build scripts if it reuses
a matching `DOTNET_INSTALL_DIR` instead of `.dotnet`. Each native-SDK acceptance
case must isolate its child SDK with the existing infrastructure helper and
record the actual consumer version. Add unique binlog/result paths and verify
selected/executed case identities when running these commands; no selection
currently exists merely because a future command is documented.

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

D1 was submitted to the same two independent models for a second,
counterexample-driven review. Coordinator synthesis is GPT-6.1 Sol
(`gpt-6.1-sol`). Review acceptance does not satisfy package/SDK release gates.

### Round 2: D1 direction accepted, six contracts tightened

The same models reviewed
`81d20b699fe92221416353c2ff2dd15fc37b4271`, Git blob
`d6c484bbc6b7ba6b54f5e16c9758bf1da3613f67` (raw LF blob SHA-256
`d18903ae3a0b1c274af73c56757de4db1a11da095ee9dfc4cdad2f363406668c`;
the earlier `352B...` review identifier hashes the CRLF checkout).
Claude conditionally accepted the direct-selector design and rejected its
unproven native-SDK empty claim; Gemini conditionally accepted pending D2.

| Remaining finding | D2 correction |
| --- | --- |
| SDK whole-command zero policy overrides host `0`. | Acknowledged completion metadata, exact all-module exception, explicit minimum precedence, deliberate-selection rendering, and separate direct/SDK ship gates. |
| Counts and shared digests permit duplicate/missing work. | Independent recomputation from the canonical catalog and comparison of planned and observed settled bucket-set digests; no count-only proof. |
| Context completion is not execution/cleanup success. | Normative normal-return, stop-state, element/enumeration closure, cleanup and bus-drain seal; failed cleanup retained in raw results. |
| Narrowed cache / duplicate discovery / false declaration warnings. | One metadata walk, pre-partition declaration validation, complete-topology-only cache guard; note current JSON-RPC-only path is excluded. |
| A passing Retry subset can replace partial initial coverage. | Mandatory negotiated control receipt verification, immutable Initial coverage and restricted RetrySubset updates; no warning-only report reducer. |
| Inherited ignore policy can conceal a worker failure. | Strip every child option source and the verified `TESTINGPLATFORM_EXITCODE_IGNORE`; validate provider-finalized environment and preserve raw verdict. |

D2 is submitted for a final targeted confirmation of these contracts. No
implementation validation or claimed performance result is added by agreement.

## Implementation gates

1. Implementation must demonstrate the neutral metadata seam is available on
   every supported native discovery path, including any source-generated path,
   without executing runtime data to infer class topology. Unsupported variants
   must reject the option until they satisfy the same contract.
2. The exact minimum shipping SDK versions for negotiated intentional-empty
   completion and local-worker support need coordinated source flow and real
   product runs. The SDK change is required, not an optional assumption.
3. TRX/HTML local merge and each CI reporter require separate consumer evidence;
   unsupported local combinations must remain disabled/rejected.
4. Assign the delivery owner, package versions, preview branch and explicit
   backport decision before implementation rollout. No global distributed
   scheduling, cross-process locks, duration history or automatic retries of
   partly completed partitions are included.
