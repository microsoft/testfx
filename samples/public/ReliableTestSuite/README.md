# Reliable test suites at scale with MSTest

A single coherent sample that shows how to make a **parallel** MSTest suite reliable by
*engineering determinism* instead of *re-rolling the dice with retries*.

The project opts into method-level parallelization (`MSTestParallelizeScope=MethodLevel` in the
`.csproj`). MSTest does not parallelize by default; turning it on is what surfaces latent
shared-state bugs. Each file then demonstrates one rung of the reliability ladder:

| File | Technique | Why it matters |
| --- | --- | --- |
| `OrderExportTests.cs` | **Eliminate** — per-test unique temp directory | No shared resource ⇒ no lock needed ⇒ full parallelism. The cheapest concurrency bug is the one that cannot exist. (After the framework package pin moves to MSTest 4.4, `TestContext.TestTempDirectory` can manage this for you.) |
| `EnvironmentPricingTests.cs` | **Coordinate** — `[DoNotParallelize]` with the pinned MSTest 4.3 packages, `[ResourceLock(WellKnownResources.EnvironmentVariables)]` after upgrading them to 4.4 | Genuinely process-global state must be serialized. `[ResourceLock]` serializes *only* the tests that share the named resource, not the whole suite. |
| `PlatformSpecificTests.cs` | **Gate** — `[OSCondition]`, `[CICondition]`, `[ExecutableCondition]` | A test that silently early-returns on the wrong OS reports a false pass. Conditions report *not run* for the right reason. |
| `CancellableWorkTests.cs` | **Bound time** — `[Timeout(CooperativeCancellation = true)]` + `TestContext.CancellationToken` | A test that can hang has no place in a reliable suite; cooperative cancellation stops the work cleanly. No `Thread.Sleep`. |
| `FlakyDependencyTests.cs` | **Contain** — `[Retry]` | Retry makes a nondeterministic test pass more often; it does **not** make it deterministic. Last resort for residual, external flakiness only. |
| `testconfig.json` | **Expose** — `randomizeTestOrder` + fixed seed | Random order surfaces hidden inter-test ordering dependencies; the reported seed makes any failure reproducible. |
| `GlobalFixtures.cs` | **Bootstrap** — `[AssemblyInitialize]`/`[AssemblyCleanup]` (once per run) vs `[GlobalTestInitialize]`/`[GlobalTestCleanup]` (before/after **every** test) | Suite-wide setup has two cadences and mixing them up is a classic scaling bug: once-only work (start a server, seed a DB) goes in `[AssemblyInitialize]`; per-test ambient reset goes in the global test hooks. |

## The thesis

**Determinism is a property you engineer, not a dice roll you re-roll.** Work down the ladder in
order: eliminate sharing, isolate what is left, coordinate the truly-global remainder with named
resource locks, and only then contain whatever residual nondeterminism you could not remove.
`[Retry]` sits at the bottom on purpose.

## `[ResourceLock]` note

`[ResourceLock]` and `WellKnownResources` ship in the **MSTest 4.4 framework packages**. This sample
uses `MSTest.Sdk` **4.4.0** from `../global.json`, but `../Directory.Build.props` independently pins
the MSTest framework and adapter packages to **4.3.2** through `MSTestVersion`. The available APIs
therefore remain those from MSTest 4.3. The sample demonstrates the coordination step with
`[DoNotParallelize]` and shows the exact `[ResourceLock]` upgrade in the comments of
`EnvironmentPricingTests.cs`. Once `MSTestVersion` moves to 4.4 or later, the migration is a
one-for-one swap: **remove** `[DoNotParallelize]` and **add** `[ResourceLock(...)]` (keeping both
would just re-serialize the class).

`[ResourceLock]` limitations worth stating up front:

- It is **cooperative** — it only coordinates tests that opt in with the **same** key. A test that
  touches the resource without declaring the lock is not held back.
- Its scope is a single **test source (assembly)**. The adapter creates a separate lock manager per
  source, so matching keys serialize only the parallel tests *within one assembly's* run — they do
  **not** coordinate tests in a *different* assembly, even when both run in the same test-host
  process. It is not a cross-assembly, cross-process, or distributed/cross-agent mutex, so don't rely
  on it for global state shared across assemblies.

## What to expect when you run it

- **11 tests pass** locally on Windows. Some condition-gated tests report **not run** (rather than a
  hollow pass) depending on your environment — e.g. `UsesWindowsPathSemantics` runs only on Windows,
  and `InteractiveOnlyCheck_NotOnHeadlessCI` is excluded on CI.
- The banner prints the worker count, parallel scope, and the **random-order seed**. The seed
  reproduces the shuffled *queue order*, not the exact concurrent interleaving: with multiple workers
  dequeuing and running tests in parallel, timing-dependent races can still vary run-to-run under the
  same seed. To reproduce a race deterministically, reduce parallelism (or serialize the suspects)
  first, then use the seed to pin the order. In CI you should **rotate the seed** (or leave it unset)
  so runs keep exploring new orderings instead of freezing on one.
- With the framework packages pinned to **MSTest 4.3.x**, a retried-then-passed test reports as an
  ordinary pass. After `MSTestVersion` moves to **4.4+**, it surfaces as *flaky* instead.

## Run it

```console
dotnet run
```
