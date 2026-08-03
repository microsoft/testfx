# Reliable test suites at scale with MSTest

A single coherent sample that shows how to make a **parallel** MSTest suite reliable by
*engineering determinism* instead of *re-rolling the dice with retries*.

The project opts into method-level parallelization (`MSTestParallelizeScope=MethodLevel` in the
`.csproj`). MSTest does not parallelize by default; turning it on is what surfaces latent
shared-state bugs. Each file then demonstrates one rung of the reliability ladder:

| File | Technique | Why it matters |
| --- | --- | --- |
| `OrderExportTests.cs` | **Eliminate** — per-test unique temp directory | No shared resource ⇒ no lock needed ⇒ full parallelism. The cheapest concurrency bug is the one that cannot exist. (MSTest 4.4 adds `TestContext.TestTempDirectory` so the platform manages this for you.) |
| `EnvironmentPricingTests.cs` | **Coordinate** — `[DoNotParallelize]` today, `[ResourceLock(WellKnownResources.EnvironmentVariables)]` in MSTest 4.4 | Genuinely process-global state must be serialized. `[ResourceLock]` serializes *only* the tests that share the named resource, not the whole suite. |
| `PlatformSpecificTests.cs` | **Gate** — `[OSCondition]`, `[CICondition]`, `[ExecutableCondition]` | A test that silently early-returns on the wrong OS reports a false pass. Conditions report *not run* for the right reason. |
| `CancellableWorkTests.cs` | **Bound time** — `[Timeout(CooperativeCancellation = true)]` + `TestContext.CancellationToken` | A test that can hang has no place in a reliable suite; cooperative cancellation stops the work cleanly. No `Thread.Sleep`. |
| `FlakyDependencyTests.cs` | **Contain** — `[Retry]` | Retry makes a nondeterministic test pass more often; it does **not** make it deterministic. Last resort for residual, external flakiness only. |
| `testconfig.json` | **Expose** — `randomizeTestOrder` + fixed seed | Random order surfaces hidden inter-test ordering dependencies; the reported seed makes any failure reproducible. |
| `GlobalFixtures.cs` | **Bootstrap** — `[GlobalTestInitialize]` / `[GlobalTestCleanup]` | One obvious place for suite-wide setup/teardown, independent of any single class. |

## The thesis

**Determinism is a property you engineer, not a dice roll you re-roll.** Work down the ladder in
order: eliminate sharing, isolate what is left, coordinate the truly-global remainder with named
resource locks, and only then contain whatever residual nondeterminism you could not remove.
`[Retry]` sits at the bottom on purpose.

## `[ResourceLock]` note

`[ResourceLock]` and `WellKnownResources` ship in **MSTest 4.4**. This sample pins the shipped
`MSTest.Sdk` (see `../global.json`), so it demonstrates the coordination step with the
shipped-today `[DoNotParallelize]` and shows the exact `[ResourceLock]` upgrade in the comments of
`EnvironmentPricingTests.cs`.

## Run it

```console
dotnet run
```
