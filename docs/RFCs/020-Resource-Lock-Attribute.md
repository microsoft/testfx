# RFC 020 - Resource Lock Attribute

- [ ] Approved in principle
- [x] Under discussion
- [x] Implementation
- [ ] Shipped

## Summary

Add a `[ResourceLock]` attribute to MSTest that lets tests declare the **named shared
resources** they contend on, so the in-assembly parallel scheduler can serialize only the
*conflicting* tests instead of forcing users to disable parallelization wholesale with
`[DoNotParallelize]`. A `Read`/`ReadWrite` access mode lets read-only tests keep running
concurrently with each other and block only against a writer.

This extends the parallelization model defined in
[RFC 004 - In-assembly Parallel Execution](004-In-Assembly-Parallel-Execution.md). The prior
art is JUnit 5's [`@ResourceLock`](https://junit.org/junit5/docs/current/user-guide/#writing-tests-parallel-execution-synchronization),
introduced alongside parallel execution in 5.3 and refined in 5.12.

## Motivation

Today MSTest's only tool for tests that contend on shared process-global state — files,
environment variables, the current working directory, the console — is `[DoNotParallelize]`.
It is all-or-nothing: a single contended variable forces a whole class or assembly to run
serially, *and* those tests are deferred to run after the parallel set drains (see the
`nonParallelizableTestSet` handling in `TestExecutionManager.Parallelization.cs`). Repos with
integration / acceptance / E2E suites that touch the same paths end up serializing large
swaths of their tests, making runs slow.

`[ResourceLock]` replaces "this test can't run in parallel with *anything*" with "this test
can't run in parallel with *other tests that touch resource X*". Tests that touch other resources,
or none, are not excluded by it — subject to worker availability, see
[Scheduling and throughput](#scheduling-and-throughput).

### Where the value actually comes from

The feature's benefit depends on `ExecutionScope`, and it is worth being precise because the naive
framing ("replace `[DoNotParallelize]` with `[ResourceLock]` and the test gets faster") is not
generally true:

- **`ClassLevel` (the default):** selective coordination *between classes*. Methods of one class are
  already sequential with respect to each other, so a lock adds nothing within a class; it earns its
  keep when a *different* class declares the same key.
- **`MethodLevel`:** selective coordination *between methods and test cases*, including within a
  single class.
- **Parallelization disabled:** no effect at all — everything is already sequential.

### Specimens in this repository

Three existing tests motivate the design and drive the acceptance tests:

1. **`test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureFoundryChatClientProviderTests.cs`**
   — the clearest illustration of the *cost* of `[DoNotParallelize]`. It is `[DoNotParallelize]`; its
   own comment says the tests "must not run concurrently **with each other**" because the provider
   reads exactly three process-wide variables: `AZURE_OPENAI_ENDPOINT`,
   `AZURE_OPENAI_DEPLOYMENT_NAME`, `AZURE_OPENAI_API_KEY`. The blast radius is three known variables,
   yet the class is serialized against the *entire* suite and deferred to the end of the run.

   Note precisely what `[ResourceLock]` would and would not buy here, since it is easy to overstate:
   at the default `ClassLevel` scope this class's methods are *already* sequential with respect to
   each other, so simply **removing** `[DoNotParallelize]` would preserve that mutual exclusion while
   also un-deferring the class from the tail and letting it run alongside unrelated classes. The lock
   adds value on top of that only if another class declares the same key, or under `MethodLevel`.
   What the specimen really demonstrates is that `[DoNotParallelize]` is a blunt instrument for a
   three-variable blast radius — which is the motivation for having a keyed alternative at all.
   Migrating it in-repo is deferred to follow-up work (see
   [Migration and adoption](#migration-and-adoption)): this project consumes the *shipped*
   `MSTest.TestFramework` NuGet package rather than the in-source framework, so the migration lands
   once the attribute ships. The end-to-end acceptance test below exercises the feature against the
   locally packed package instead.

2. **`test/UnitTests/TestFramework.UnitTests/Attributes/ExecutableConditionAttributeTests.cs`**
   — mutates `PATH`. Because child processes inherit `PATH`, the blast radius is effectively
   unbounded, so the correct key is *coarse* (the well-known environment-variables key), not a
   narrow per-variable key. Illustrates blast-radius-driven granularity.

3. **`test/IntegrationTests/MSTest.Acceptance.IntegrationTests/TelemetryTests.cs`** —
   `[DoNotParallelize]` because parallel `dotnet test` invocations race on shared MSBuild
   `bin`/`obj` output. This one is **better fixed by eliminating the sharing** (per-TFM output
   paths) than by locking. It illustrates the "eliminate before you lock" guidance below: a
   resource each test can *own* needs no lock at all.

## Design

### API shape

Namespace `Microsoft.VisualStudio.TestTools.UnitTesting`:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ResourceLockAttribute : Attribute
{
    public ResourceLockAttribute(string resource);
    public string Resource { get; }
    public ResourceAccessMode Mode { get; set; } = ResourceAccessMode.ReadWrite;
}

public enum ResourceAccessMode
{
    ReadWrite = 0,  // default: unspecified values fail closed (exclusive)
    Read = 1,
}

public static class WellKnownResources
{
    public const string CurrentDirectory = "System.Environment.CurrentDirectory";
    public const string EnvironmentVariables = "System.Environment.Variables";
    public const string Console = "System.Console";
}
```

`ReadWrite` is deliberately the **zero value** so that `default(ResourceAccessMode)` is the exclusive,
safe mode. Under-locking fails flaky; over-locking merely runs slower, so any unspecified or
default-constructed value must fail closed. (The attribute always sets `Mode` explicitly, so this
matters only for future paths that default-construct — deserialization, a provider hook, interop.)

The `WellKnownResources` values are **permanent public API** — once shipped they can never change
without silently breaking every user key that happens to match them. They are namespaced,
collision-resistant constants in the spirit of JUnit's `"java.lang.System.properties"`. They are
purely conventional: the engine gives them **no** special treatment and they use the exact same
equality-based conflict mechanism as any user-invented key. They exist only so that tests contending
on the same ambient state agree on a spelling. Each is rooted at the BCL type that owns the state.

The set is deliberately limited to these **three** keys in v1, all of which are unambiguously
process-global. Culture and time zone are **not** included, despite JUnit having `LOCALE` and
`TIME_ZONE`, because in .NET the distinction cannot be named precisely with a single key:
`CultureInfo.CurrentCulture` is thread-scoped (`AsyncLocal`-backed) while
`CultureInfo.DefaultThreadCurrentCulture` is process-wide, so a key called "culture" would be
ambiguous about which of the two it protects and would mislead. Additive later, once the distinction
can be expressed.

There is deliberately **no `Global` key in v1.** A key named `Global` would imply it conflicts with
every other lock, but conflict detection is pure string equality, so it would in fact conflict only
with other tests that also spelled `Global` — a footgun that fails open. Making it genuinely
privileged requires the mechanism JUnit uses (see [Future work](#future-work)), which is out of scope
here; `[DoNotParallelize]` already covers "serialize against everything".

Usage:

```csharp
[TestClass]
public sealed class AzureFoundryChatClientProviderTests
{
    private const string AzureOpenAIEnvironment = "AZURE_OPENAI_*";

    [TestMethod]
    [ResourceLock(AzureOpenAIEnvironment)]
    public void IsAvailable_...() { }
}

[TestMethod]
[ResourceLock(WellKnownResources.EnvironmentVariables, Mode = ResourceAccessMode.ReadWrite)]
public void MutatesPath() { }

private const string DatabaseFixture = "db-fixture";

[TestMethod]
[ResourceLock(DatabaseFixture, Mode = ResourceAccessMode.Read)]  // readers run concurrently
public void ReadsSharedAsset() { }
```

### Design decisions

1. **The key is a free-form string.** Conflict detection is equality-based: same string means
   same resource. This is deliberately **not** an enum, matching JUnit, whose `Resources` class
   is just `String` constants (e.g. `"java.lang.System.properties"`). Well-known names and
   user-invented names use the identical mechanism, so nothing is privileged and users can
   invent keys freely. `WellKnownResources` is a small set of `const string` values (not an
   enum) so it composes with user keys and can grow without an API break.

2. **Comparison is ordinal and case-sensitive.** Keys are *opaque identifiers*, not paths. Two
   keys are the same resource iff they are the same string, with **no hierarchical semantics**:
   `C:\out` and `C:\out\sub` are unrelated keys. A test that wants to key on a filesystem path
   writes a normalized path as a `const` string itself. A `ForPath`-style normalization *helper*
   was considered and rejected for v1: because attribute arguments must be compile-time constants,
   `[ResourceLock(ResourceLock.ForPath(@"C:\out"))]` fails to compile (CS0182, "an attribute
   argument must be a constant expression"), so the helper would be unusable in the exact position
   it is needed. Path normalization is inherently a *runtime* operation and therefore belongs with
   the dynamic resource-lock provider in [Future work](#future-work), not with the v1 attribute.
   (MSTEST0073 flags bare string-literal keys generally; extending it to specifically detect
   path-shaped keys that are non-rooted or non-normalized — the ones that silently fail to match —
   is a natural refinement.)

3. **`Read` / `ReadWrite` modes.** This is where the throughput wins are: `Read` locks run
   concurrently with each other and block only against a `ReadWrite` holder. Most acceptance
   tests in this repo only *read* a shared built asset, so they should never block one another.
   `ReadWrite` (the default) is exclusive.

4. **Scope is the test host process only, in v1.** No cross-process or machine-wide locking. The
   XML docs state this explicitly so nobody assumes otherwise. Rationale (all verified):
   - A named `Mutex` has **thread affinity** — it "can be released only by the thread that owns
     it" — which is fatal for an `async`/`Task`-based scheduler, since a continuation after
     `await` can resume on a different pool thread and throw `ApplicationException` on release.
   - On Unix a named mutex's name "after excluding the namespace must be a valid file name", so
     arbitrary keys would have to be *hashed*, not truncated.
   - Abandoned mutexes throw `AbandonedMutexException` in the next acquirer with no good recovery.
   - **.NET has no cross-process reader-writer lock**, so `Mode = Read` would silently degrade to
     exclusive at machine scope — a bad API smell.
   - JUnit's `@ResourceLock` still has *no* cross-JVM scope after 7+ years.

   Adding an optional `Scope` property later is source- and binary-compatible, so nothing is
   foreclosed. If it is ever added, the values should name the physical boundary
   (`TestHost` / `Machine`), because the *process* is the real boundary, not the assembly.

5. **No `ResourceLockTarget.SELF` / `CHILDREN` in v1.** JUnit took ~7 years (5.3 → 5.12) to need
   it. MSTest's scheduler is *flat*, not hierarchical, so the concept only has meaning under
   `ExecutionScope.ClassLevel`: under `MethodLevel` each chunk is already a single test, so
   `CHILDREN` semantics are the default by construction and `SELF` has nothing to attach to.
   Class-level locks default to `SELF` semantics — held across the whole class chunk — which is
   the conservative choice and is required when `[ClassInitialize]` / `[ClassCleanup]` own the
   resource. Note that there is **no v1 workaround** that recovers `CHILDREN` semantics under
   `ExecutionScope.ClassLevel`: annotating individual methods instead of the class does *not* help,
   because the scheduler groups the entire class into one chunk, unions every method's keys, and
   upgrades each to its strongest declared mode for the whole chunk. So one locked method locks the
   whole class, unrelated keys declared on different methods are all held for the class's full
   duration, and a single `ReadWrite` method promotes every `Read` use of that key class-wide.
   Method-level placement is only granular under `ExecutionScope.MethodLevel`.

6. **Locks span lifecycle methods.** A method-level lock is acquired *before* `[TestInitialize]`
   and released *after* `[TestCleanup]`; a class-level lock spans `[ClassInitialize]` /
   `[ClassCleanup]`. JUnit does exactly this, and it is a correctness requirement — otherwise
   setup that touches the resource races outside the lock.

7. **Deadlock-free by construction.** When a test declares multiple locks, the keys are sorted
   ordinally and always acquired in that order.

8. **`[DoNotParallelize]` is not reimplemented on top of this.** They look equivalent but are
   not: non-parallelizable tests today run sequentially *at the end*, after the parallel set
   drains. A global exclusive lock would instead *interleave* them with the parallel set.
   `[DoNotParallelize]` is kept exactly as-is and `[ResourceLock]` is added alongside. Changing
   `[DoNotParallelize]` would be a silent behavioral break. **Precedence when both are applied:
   `[DoNotParallelize]` wins and the resource locks are ignored.** Such a test is routed to the
   sequential tail and never passes through the resource-lock scheduler at all, so its declared
   locks have no effect. This is safe — the sequential tail runs nothing else concurrently — but it
   also means the lock buys nothing there, so combining them is pointless rather than additive.

9. **`Mode` uses `{ get; set; }`, never `init`** — per the repository rule that new public API
   must not use `init` accessors.

### Access-mode conflict matrix

| Held \ Requested | `Read`      | `ReadWrite` |
| ---------------- | ----------- | ----------- |
| `Read`           | concurrent  | blocks      |
| `ReadWrite`      | blocks      | blocks      |

### Scheduling and throughput

Two consequences of the flat, chunk-based scheduler are easy to get wrong, so they are stated
plainly rather than left implied.

**A blocked chunk occupies a worker.** Workers dequeue a chunk and then wait for its locks, so a
worker waiting on a contended key is not available for unrelated work sitting in the queue behind
it. With `Workers = 4`, four dequeued chunks contending on one exclusive key leave three workers
parked and the rest of the queue stalled — even though those queued chunks declare no locks and
could otherwise run. So `[ResourceLock]` guarantees *correct mutual exclusion*, not that unrelated
tests always keep running; heavy contention still reduces effective parallelism. Keep locked tests a
small fraction of the suite (see the speedup math under [Granularity guidance](#guidance-granularity)).
Lock-aware dispatch — skipping over a chunk whose locks are unavailable and taking the next one — is
[Future work](#future-work); it is deliberately not attempted in v1 because a naive version can
livelock without a bounded retry policy.

**Lock granularity follows the scheduling chunk, and the chunk depends on `ExecutionScope`.** A lock
is acquired before a chunk starts and released after it finishes, so the *chunk* — not the test
method — is the unit of lock lifetime:

| `ExecutionScope`         | Chunk           | Effect on a class-level `[ResourceLock]`                                                     |
| ------------------------ | --------------- | ------------------------------------------------------------------------------------------- |
| `ClassLevel` *(default)* | the whole class | Held once across every method, spanning `[ClassInitialize]` / `[ClassCleanup]`                |
| `MethodLevel`            | a single test   | Copied to each test and acquired/released per method; other classes may interleave between them |

Under `ClassLevel` the chunk's locks are the **union** of the class's and every method's keys, each
upgraded to the strongest mode declared anywhere in the class. That is why method-level annotation
does not buy granularity under `ClassLevel` (see decision 5).

Data-driven tests follow the same rule: when rows are unfolded into separate test cases they are
separate chunks under `MethodLevel`, so the lock is released between rows and other tests may
interleave; when rows are not unfolded, all rows share one acquisition.

## Implementation

The scheduler lives in
`src/Adapter/MSTestAdapter.PlatformServices/Execution/TestExecutionManager.Parallelization.cs`.
It is a `ConcurrentQueue<IEnumerable<UnitTestElement>>` drained by *N* worker `Task`s, chunked
per-method or per-class depending on `ExecutionScope`. Before dispatching a chunk, the worker
acquires the union of that chunk's declared locks (in sorted key order) from a per-run keyed
registry of async reader-writer locks, and releases them after the chunk finishes.

The declared locks are surfaced on `UnitTestElement` and plumbed through discovery exactly like
`DoNotParallelize`:

- `TypeEnumerator` reads class-level and method-level `[ResourceLock]` attributes via
  `ReflectHelper` (class locks apply to every method in the class).
- `UnitTestElement` carries the resolved lock list.
- The list round-trips through a hidden `TestProperty`
  (`AdapterTestProperties` / `UnitTestElementExtensions` / `TestCaseExtensions`) so it survives
  the discovery → execution and AppDomain boundaries the parent scheduler runs across.

There is no async reader-writer lock in the BCL, so a small internal one is added. It holds a FIFO
queue of waiters in a `LinkedList` guarded by a monitor, and hands the lock off from that queue: a
writer at the head blocks everyone behind it, and a leading run of readers is granted together. FIFO
order is what prevents starvation in both directions — a queued writer is not overtaken by later
readers, and a queued reader is not starved by a stream of writers. It is correct under cancellation,
since the scheduler honors `_testRunCancellationToken`; a cancelled waiter is removed from the queue,
and granted waiters are completed only after the monitor is released, because completing disposes the
cancellation registration and that disposal blocks until any in-flight cancel callback finishes — a
callback which itself needs the monitor.

When parallelization is disabled (`Workers == 0`, `DisableParallelization`, or an assembly that
cannot parallelize), tests already run serially and no locks are taken.

## Analyzer

Free-form string keys **fail open**: a typo silently produces a race rather than a compile error.
A new analyzer (**MSTEST0073**) flags bare string literals passed to `[ResourceLock]`, steering
users toward `const` fields and `WellKnownResources` so that a mistyped key becomes a symbol
error instead of a silent race. It follows `docs/AddingAnalyzerCodeFix.md`.

## Guidance (granularity)

The part users get wrong is *how coarse* a key should be:

- **Eliminate before you lock.** A resource each test can *own* (a unique temp file/dir) needs no
  lock at all. Only genuinely process-global state — environment variables, current directory,
  console — requires one. (This is why `TelemetryTests` is better fixed with per-TFM output.)
- **Granularity follows blast radius.** Use a narrow key when you can enumerate the readers (the
  three `AZURE_OPENAI_*` variables). Use a coarse key for ambient state whose reach you cannot
  bound (`PATH`, inherited by child processes).
- **Key what the code under test *reads*, not just what your test *writes*.** If the code reads a
  wider slice of state than your test mutates, the key must cover the read.
- **Speedup math.** With *N* workers, a lock covering fraction *s* of suite time caps speedup at
  `min(N, 1/s)`. Splitting a key only pays when `s > 1/N`.
- **Over-locking fails slow (safe, diagnosable); under-locking fails flaky (expensive).** Start
  coarse and refine only on measurement.

## Migration and adoption

Adopting `[ResourceLock]` in place of existing `[DoNotParallelize]` usages is **out of scope for the
feature PR** — each conversion is a behavior change to an existing test class and is best reviewed on
its own. Once the attribute ships, the repo's `[DoNotParallelize]` sites should be audited one by one
against the "eliminate before you lock" guidance above. Only sites that serialize because of genuine
*same-process* shared state are candidates; sites that need end-of-run sequencing, deterministic
output ordering, or that drive out-of-process `dotnet test` invocations should stay as they are.

The current inventory of `[DoNotParallelize]` test classes:

| Test class | Why serialized today | Recommended action |
| --- | --- | --- |
| `AzureFoundryChatClientProviderTests` | Reads 3 process-wide `AZURE_OPENAI_*` env vars | **Migrate** — narrow key (first follow-up candidate) |
| `ExecutableConditionAttributeTests` | Mutates `PATH` (inherited by children) | **Migrate** — coarse `WellKnownResources.EnvironmentVariables` key |
| `TelemetryTests` | Parallel `dotnet test` races on shared MSBuild `bin`/`obj` | **Eliminate** — per-TFM output paths, no lock needed |
| `ParallelExecutionTests`, `TestCaseFilteringTests`, `JUnitReportTests`, `LegacyLifecycleObjectModelTests`, `DiscoveryIdentityTests`, `ReflectionOperationsTests`, `TestExecutionManagerTests`, `InvokeTestingPlatformTaskTests`, `AsynchronousMessageBusTests`, `TerminalOutputDeviceTests` | Need sequential/end-of-run execution, output determinism, or drive subprocesses — not same-process shared-state contention | **Keep as-is** |

`AzureFoundryChatClientProviderTests` is the strongest first follow-up: its blast radius is exactly
three named variables, so it is the clearest win from "serialized against the whole suite and deferred
to the end of the run" to "runs concurrently with everything that does not touch those variables". It
is deferred here only because the project consumes the *shipped* `MSTest.TestFramework` NuGet package,
so the attribute is not visible until it ships.

## Future work

- **A resource-locks provider / dynamic hook** (JUnit added `ResourceLocksProvider` in 5.12).
  Attributes take only compile-time constants, so `[DynamicData]`-parameterized tests cannot
  express "lock the asset for *this* TFM". **Explicitly deferred out of v1** (proposed; see
  [Unresolved questions](#unresolved-questions)). The concrete v1 consequence is that a
  parameterized test uses a single constant key across all of its variants, so those variants
  serialize against each other unnecessarily — **correct, just coarser**, which is the failure
  direction this RFC argues for throughout ("over-locking fails slow and diagnosable; under-locking
  fails flaky"). Deferring is also this RFC's own guidance applied to itself: start coarse, refine on
  measurement, and there is as yet no measurement showing TFM-variant serialization is a real
  bottleneck. Precedent: JUnit shipped `@ResourceLock` in 5.3 (2018) and `ResourceLocksProvider` only
  in 5.12 (2025) — seven years of production use before the dynamic hook was needed. The hook is
  purely additive (source- and binary-compatible) and its shape is designed for here, so nothing is
  foreclosed; meanwhile the v1 public surface stays minimal, which matters disproportionately because
  every symbol is permanent. **Runtime path normalization** (the rejected `ForPath` helper from
  decision 2) belongs here too: a provider runs at execution time, where `Path.GetFullPath` and
  platform-appropriate casing can be applied, unlike a compile-time attribute argument.
- **`ResourceLockTarget.SELF` / `CHILDREN`** for class-level locks, if a concrete need emerges
  (see decision 5).
- **An optional `Scope` property** (`TestHost` / `Machine`) if cross-process locking is ever
  required (see decision 4). Purely additive.
- **A privileged `WellKnownResources.Global` key.** Dropped from v1 because pure equality-based
  conflict detection cannot make it global (it would conflict only with tests that also spell
  `Global`). JUnit's `GLOBAL` *is* genuinely privileged, but via a mechanism not ported here: every
  direct child of the root engine descriptor **implicitly acquires the global key in `READ` mode**,
  so anything taking it `READ_WRITE` blocks everything. The privilege is emergent from the implicit
  read lock, not from special-casing in the lock manager. Porting it correctly means having **every
  parallel chunk — including chunks that declare no explicit locks — implicitly take the global key
  in `Read` mode**; restricting the implicit read to chunks that already declare a lock would not
  work, because an explicit global *writer* would then still fail to exclude unlocked tests, which is
  precisely the property that makes it global. The existing strongest-mode-wins merge then handles an
  explicit `ReadWrite` on the key correctly, and `ResourceLockManager` needs no change. If added, it
  must be documented as serializing against *every* parallel test, not merely the locked set. The
  real gap this leaves in v1: `[DoNotParallelize]` provides exclusivity but forces execution into the
  sequential tail, so v1 cannot express *interleaved* global exclusivity.
- **Lock-aware dispatch.** Today a worker that dequeues a chunk then blocks on its locks holds that
  worker until the locks are available (see [Scheduling and throughput](#scheduling-and-throughput)).
  A scheduler that attempts a non-blocking acquire and, on failure, re-enqueues the chunk and takes
  the next one would keep unrelated work flowing. This needs a **bounded** number of skip attempts
  before falling back to a blocking wait, otherwise a chunk whose key is permanently busy can be
  passed over indefinitely (livelock). Deferred from v1 because the bookkeeping is easy to get subtly
  wrong and the simple design is correct, merely slower under contention.
- **Instrumentation-derived locks.** `[ResourceLock]` is not only a scheduling hint; it is *the
  contract a future collector can enforce*. This reframes the free-form key's fail-open weakness
  (a typo silently races) into a fail-closed property. The design space, with prior art:
  - *Prior art.* **ElectricTest** (Bell, Kaiser, Melski, Dattatreya, ESEC/FSE 2015, "Efficient
    Dependency Detection for Safe Java Test Acceleration") detects inter-test data dependencies to
    parallelize safely; **PolDet** (ISSTA 2015) detects filesystem pollution between tests via file
    hashing and modification times; **PRADET** (ICST 2018) detects manifest test-order
    dependencies. All infer, from observation, the very conflicts `[ResourceLock]` lets a test
    *declare*.
  - *Why coverage callbacks are insufficient.* Coverage records *which code ran*, not *which
    resource* — the same call site touches different paths on different runs. Worse, managed
    in-process instrumentation is blind to **child processes**, which is exactly where E2E
    contention lives (`TelemetryTests` races on MSBuild's writes to `obj/`). Seeing paths and
    children requires OS-level tracing — ETW kernel file-I/O on Windows, eBPF/`fanotify` on Linux —
    at the cost of elevation and noise. A further constraint pushes the same way: **only one CLR
    profiler can attach to a process at a time** ("Only one profiler can profile a process at one
    time in a given environment"), so a resource-access collector cannot run as a *sibling* to the
    code-coverage profiler — it would have to live inside it or use a non-profiler mechanism. .NET 8
    notification profilers do not rescue this, since they cannot rewrite IL. Together with the
    child-process blindness above, this argues for OS-level tracing over profiler-based collection.
  - *Attribution needs a sequential run.* You cannot attribute an I/O to a specific test while tests
    run concurrently, so collection is a separate profiling mode, not always-on.
  - *Soundness.* Observation is a *sample*; declaration is a *specification*. A learned profile
    under-approximates via data-dependent paths, unexercised branches, the unprofiled first run,
    staleness after edits, and child processes. Inference is therefore inherently unsound as a
    source of truth.
  - *Enforcement, not inference, is the sound direction.* A **verify** mode fails a test that
    touches a resource it did not declare — turning fail-open into fail-closed (precedent: Bazel's
    sandbox fails actions on undeclared inputs; Google's test-size enforcement uses a
    `SecurityManager` that fails "small" tests attempting disk access). **Suggest** (generate
    attributes) and **prune** (flag declared-but-never-touched locks) remain advisory.
  - *Cheap high-value increment.* Short of any scheduling change, enriching failure messages with
    detected races — "failed while racing test *Y* on `<path>`" — makes the worst class of flaky
    failure self-diagnosing.

## Unresolved questions

Both questions below now have a **proposed** answer with rationale. They are recorded as proposals
rather than settled decisions because each is cheap to reverse before merge and impossible to reverse
after ship, so both await ratification on review.

- **Exact `WellKnownResources` values — proposed, pending sign-off.**
  `CurrentDirectory = "System.Environment.CurrentDirectory"`,
  `EnvironmentVariables = "System.Environment.Variables"`, `Console = "System.Console"`, pinned in
  `PublicAPI.Unshipped.txt`. `CurrentDirectory` deliberately names `System.Environment.CurrentDirectory`
  rather than `System.IO.Directory.CurrentDirectory`, because the former is a real BCL property while
  the latter names no actual member (`Directory` exposes `GetCurrentDirectory()`/`SetCurrentDirectory()`,
  not a property). This also makes the set internally consistent: every key is rooted at the BCL type
  that owns the state, and it pairs naturally with `System.Environment.Variables`. The set is frozen at
  these three keys for v1; culture and time zone are excluded for the reason given in the API section.
- **Whether the dynamic resource-locks provider lands in v1 — proposed: no, follow-up.** The attribute
  API ships in v1; the provider hook (and the runtime path normalization that depends on it) follows.
  Full rationale in [Future work](#future-work): the v1 consequence is coarser-but-correct locking for
  parameterized tests, it applies this RFC's own start-coarse guidance, JUnit needed seven years before
  adding its equivalent, and the hook is purely additive so nothing is foreclosed.
