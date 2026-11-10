# MTP 3 legacy extension contracts

## Problem and contract

[Tracking issue #12314](https://github.com/microsoft/testfx/issues/12314) authorizes
retiring four specific binary-compatibility contracts in MTP 3, not all MTP 2.x
extensions:

| Retired contract | Migration and failure boundary |
| --- | --- |
| `Extensions.TestHostOrchestrator.ITestHostOrchestrator`, the old manager interface/concrete facade, and the concrete `TestApplicationBuilder.TestHostOrchestrator` getter | Register `ITestHostExecutionOrchestrator` through `ITestApplicationBuilder.TestHostOrchestrator`. Already compiled callers requesting the old identities can fail with type/member resolution errors; rebuild those extensions. The replacement manager, lifetime callbacks, and execution middleware remain. |
| `IProcess.WaitForExitAsync()` and its two implementations | Use `WaitForExitAsync(CancellationToken.None)` for an unbounded wait, or pass the operation token. Already compiled calls to the zero-argument member can fail with `MissingMethodException`. Cancellation and observation of the original exit task remain unchanged. |
| Old platform `Polyfills.Polyfill` exports from `PolyfillBackwardCompatibility.cs` | Extensions must embed their own legacy-target helpers or use the applicable BCL API. Old member references to the platform can fail at runtime. The shared source remains available to extensions: current TRX/JUnit reporters still need `SaveAsync` on legacy targets. Other polyfills and supported .NET Framework/netstandard targets remain. |
| Friend access for `Microsoft.Testing.Framework` and `MSTest.Engine` | Retire the deprecated engine packages; use current `MSTest.SourceGeneration` and the adapter-backed execution path. Old engines calling platform internals can fail with `MethodAccessException`. This is not retirement of current MSTest source generation or `Microsoft.Testing.Internal.Framework`. |

No protocol versions, Jsonite, serialization constructors, server/client wire
states, `HasExitedGracefully`, application-model defaults, or package boundaries
are changed here. The other major-version removals tracked by
[#12312](https://github.com/microsoft/testfx/issues/12312) are separate changes.

## Research and experiments

### 2026-11-10: establish package-specific boundaries before removal

Starting source is `c46cfa7394e7be10c4057d3ba8d1d3b698fe242e`, a descendant of
the reviewed `471a807cf8182e0bcd0d9967c8dbf002c04945d2`. Version prefixes are
MSTest 5 / MTP 3 in `eng/Versions.props`.

The original platform unit-test project built successfully with the pinned SDK
using:

```powershell
.\build.cmd -c Release -projects test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -bl
```

The focused orchestrator/process baseline executed 68 tests on each of `net10.0`,
`net11.0`, and `net462`: 204 passed, none failed or skipped.

Package DLLs were inspected without loading extension code, using
`System.Reflection.PortableExecutable.PEReader`, `MetadataReader.TypeReferences`,
and `MetadataReader.MemberReferences`, including each reference's assembly scope
and method signature. Missing packages were downloaded from NuGet's official
flat-container feed into task-owned storage; shared package caches were not
modified. The important stable-release boundaries are:

| Package / assets | Concrete observation | Floor for the contracts in this change |
| --- | --- | --- |
| Retry 2.1.0, all three assets | References the old concrete builder getter, old orchestrator identities, and zero-argument `IProcess.WaitForExitAsync`. | Migrate the old orchestrator contracts to Retry 2.2.1 or later; the wait removal imposes the higher floor below. |
| Retry 2.2.1, 2.2.2, and 2.2.3, all three assets | Use the replacement execution orchestrator, but still reference the zero-argument wait. | Do not survive the wait removal. |
| Retry 2.3.0, all three assets | Uses the replacement execution orchestrator and one-argument wait. | 2.3.0 for these removals. |
| Telemetry 2.0.0, 2.0.1, and 2.0.2, `netstandard2.0` | Reference `[Microsoft.Testing.Platform]Polyfills.Polyfill.Deconstruct`; enabling telemetry exercises the background ingest loop. | 2.1.0 on .NET Framework/netstandard. The modern assets do not have this dependency. |
| Telemetry 2.1.0, all three assets | No platform polyfill references. | Keep an enabled-telemetry .NET Framework canary at 2.1.0. |
| TRX report / abstractions 2.0.0, 2.0.1, and 2.0.2, legacy assets | Reference platform polyfills, including `CopyToAsync`, `SaveAsync`, and `AppendJoin`. | 2.1.0 for the removed platform exports; TRX report / abstractions 2.1.0 have no such references. |
| VSTestBridge 2.0.0, 2.0.1, and 2.0.2, `netstandard2.0` | Reference the two string `Contains` polyfills. | 2.1.0 has no platform polyfill references. Unrelated guard/helper compatibility is outside this removal. |

These are contract-specific floors, not blanket compatibility promises for every
later package. In particular Retry 2.3.0 and Retry/TRX 2.5.2 reference
`CancelledTestNodeStateProperty`, whereas Retry/TRX 2.2.1 do not. Combining this
change with that type's removal in #12312 therefore requires a Retry build that
also drops that separate reference (the aligned MTP 3 build), not a blind upgrade
of every extension to the same old version.

The official package indexes were checked for intervening stable patch releases;
the 2.0.1/2.0.2 and Retry 2.2.2/2.2.3 assets were also inspected before fixing
these floors. A version bump alone was not used as dependency evidence.

Current code also uses dictionary deconstruction in `ActiveTestTracker` and
`TestExecutionActivityBroker`; these legacy-target consumers must use the
key/value members directly before the platform stops exporting `Deconstruct`.
Character containment checks use `IndexOf(char)` directly rather than retaining
the old helper or silently falling back to LINQ enumeration on netstandard.
These changes preserve locking, ordering, disposal, and result associations.

The first implementation pack attempt stopped at CA1852 after removing the
compatibility subclass. The remaining internal manager implementation is sealed,
as required by the repository analyzer once it has no in-assembly subtypes; the
public manager interface remains the supported extension point.

The next full build compiled the affected shipping projects but stopped at
IDE0004 in the migrated builder unit test: the now-sealed concrete manager no
longer needs a cast to its middleware capability interface. The redundant cast
was removed. An earlier test-only build also rejected two obsolete
`CollectionAssert.DoesNotContain` calls (MSTEST0068); those use
`Assert.DoesNotContain` now. None of these build/setup failures counts as
behavioral red-phase evidence.

## Alternatives and decision

Keeping all shims would preserve old binaries but contradict the explicit MTP 3
retirement. Deleting the shared polyfill source would break supported legacy
reporters and is rejected. Dropping every MTP 2.x extension or replacing the
entire forward-compatibility fixture with current packages would conceal
unrelated compatibility regressions and is also rejected.

Remove the named platform identities, compile the shared legacy-target helpers
only into non-core consumers, and migrate only package pins whose actual
references require it. Preserve shipped API history with exact `*REMOVED*`
entries in unshipped baselines. No replacement facade, extra public API, or
silent runtime fallback is introduced.

## Validation and demonstration

| Requirement / risk | Automated evidence | Consuming surface |
| --- | --- | --- |
| Removed binary identities really disappear | Focused runtime metadata assertions for retired orchestrator types, no-token waits, polyfill exports, and friend grants, with the same tests against preserved baseline platform binaries. | Nonvisual assembly contract; exact failing assertions are more useful than screenshots. |
| Execution registration, middleware, lifetime and process cancellation still work | Existing `TestHostOrchestratorManagerTests`, `TestHostHandleToProcessAdapterTests`, and the two process-wrapper cases, across all test TFMs. | Real owned child processes plus interface registration; no external service. |
| Required dictionary behavior survives | Existing `ActiveTestTrackerTests` and `TestExecutionActivityBrokerTests`. | Diagnostic/result identity and activity finalization, not a layout change. |
| Supported old extensions remain usable | Packed `ForwardCompatibilityTests` with the narrowly migrated Retry pin and enabled-telemetry `TelemetryForwardCompatibilityTests` on .NET Framework. | Actual generated consumers of local shipping packages. |
| Current source generation and legacy reporters survive | Focused existing source-generation and TRX/JUnit packaged consumers. | Current source-generated adapter and final report artifacts. |

Implementation, exact commands, revision-bound paired results, package
provenance, and any unavailable evidence will be recorded here as they are run.
External deprecated-engine applications are intentionally retired, not claimed
as validated consumers. No user dashboard or external service is accessed.

## Open questions

The owner of the coordinated #12314 delivery must reconcile the separate
cancelled-state package-floor change before merging the independent PRs.
CLI/filename and duplicate OpenTelemetry removals are separate PR scopes;
neither is claimed as implemented by this record.
