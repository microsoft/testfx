# Test Improver Notes — microsoft/testfx

## Build/Test Commands (Validated from docs)

- **Build (Debug)**: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- **Build (Release)**: `./build.sh -c Release`
- **Unit Tests**: `./build.sh -test`
- **Pack NuGets**: `./build.sh -pack`
- **Integration Tests**: `./build.sh -pack -test -integrationTest`
- **Single test (MTP)**: `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -- --treenode-filter "/*/*/*/MyClass/MyMethod"`
- **Single test via dotnet test**: `dotnet test test/UnitTests/<Project>/<Project>.csproj -f net8.0 --no-build -c Debug --filter "FullyQualifiedName~ClassName"`
- **Single project test**: `./build.sh --test --projects "$(pwd)/test/UnitTests/<Project>/<Project>.csproj"`
- **Install SDK first**: `./build.sh --restore` (installs .dotnet/ SDK + runtimes, then can use `.dotnet/dotnet`)

## Testing Frameworks & Patterns

- MTP + MSTest Analyzer unit tests → use **MSTest** (`Assert`/`StringAssert`/`CollectionAssert`)
- Adapter unit tests (`MSTestAdapter.UnitTests`, `MSTestAdapter.PlatformServices.UnitTests`) → use **AwesomeAssertions** (FluentAssertions-style)
- MSTest itself (`TestFramework.UnitTests`) → use **AwesomeAssertions** in partial class `AssertTests : TestContainer` (TestContainer framework)
- Each project has `BannedSymbols.txt` listing disallowed assertion APIs
- **No VB.NET tests** for analyzers — repo constraint, maintainers not interested
- **IgnoreAttribute is sealed** — cannot derive from it in test scenarios
- **sealed + inheritance in tests**: When writing tests that need multi-level inheritance, the first level class must NOT be sealed
- **`[Experimental("MSTESTEXP")]` types** (`RetryContext`, `RetryResult`, `RetryBaseAttribute.ExecuteAsync`): do NOT inherit from `RetryBaseAttribute` in test code strings — would require `#pragma warning disable MSTESTEXP` (not used in tests). Use `[Retry]` directly.
- **Static classes in Roslyn**: Static classes are NOT abstract (`IsAbstract=false`); they have `IsStatic=true`. The `UseDeploymentItem` analyzer's abstract-class early return does NOT apply to static classes.
- **Nullable annotation (CS8632)**: In analyzer test code strings, avoid `object?` — use `object` instead, or add `#nullable enable` at top of test code string. The test harness doesn't enable nullable by default.
- **ManagedMethod/ManagedType**: Listed in TestContextPropertyUsageAnalyzer restriction sets but these properties do NOT exist on the actual TestContext class — those entries are dead code in the restriction sets.
- **VerifyCodeFixAsync for "no fix" case**: `VerifyCodeFixAsync(code, code)` (same string for both params, diagnostic markers preserved) IS valid when no fix is registered — framework compares actual output (unchanged) to expected fixedCode (same as original), and the kept diagnostic markers in fixedCode correctly express that the diagnostic remains. Verified working in `RemoveClassCleanupBehaviorArgumentFixerTests.WhenClassCleanupBehaviorReferencedOutsideAttribute_NoFix`.
- **OperationAnalysisContext.ContainingSymbol for lambdas**: For `OperationKind.PropertyReference` inside a lambda, `context.ContainingSymbol` resolves to the **enclosing named method** (NOT the lambda's anonymous method). This means [AssemblyInitialize] attribute IS visible even inside a lambda — the TestContextPropertyUsageAnalyzer correctly fires for accesses inside lambdas.
- **Discard variable name clash**: Do NOT use `_` as a parameter name if the test code also uses `_ = expr` discard assignments — the compiler binds `_` to the parameter (CS0029 if types differ).
- **DoNotUseShadowingAnalyzer**: `GetBaseMembers` walks the full inheritance chain via while-loop on `BaseType`; `IsMemberShadowing` handles only `IMethodSymbol` and `IPropertySymbol` — fields fall through to `return false`. Property type must match via `SymbolEqualityComparer` for shadowing detection.

## Testing Opportunities Backlog

1. **MSTest.Engine internal class coverage** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner` are internal (~135+ LOC each). Would need `InternalsVisibleTo` or integration tests.
2. **More Assert method coverage** — Any remaining gaps in newer Assert overloads.
3. **Analyzer edge cases (ongoing)** — Continue systematic coverage of untested paths in MSTest.Analyzers. Next candidates:
   - `UseCooperativeCancellationForTimeoutAnalyzerTests` (14 tests) — covered relative to complexity
   - `PreferDisposeOverTestCleanupAnalyzerTests` (11 tests) — covered relative to complexity
   - `PreferConstructorOverTestInitializeAnalyzerTests` (15 tests) — covered relative to complexity
   - `DuplicateTestMethodAttributeAnalyzerTests` (23 tests) — possible additional scenarios
   - `GlobalTestFixtureShouldBeValidAnalyzerTests` — done 2026-06-30 (generic class, struct, derived TestClass attribute)

## Tasks Run History

| Date | Tasks |
|------|-------|
| 2026-06-30 | Task 3 (GlobalTestFixtureShouldBeValidAnalyzer MSTEST0050 edge cases: generic class, struct, derived TestClass attribute), Task 7 |
| 2026-06-29 | Task 3 (UseAttributeOnTestMethodAnalyzer MSTEST0007: DataTestMethod early-return, OSCondition ConditionBase subclass), Task 4, Task 7 |
| 2026-06-28 | Task 3 (DoNotUseShadowingAnalyzer MSTEST0036: multi-level inheritance, property type mismatch, field shadowing), Task 7 |
| 2026-06-27 | Task 3 (TestContextPropertyUsageAnalyzer MSTEST0048: non-TestContext type guard, lambda ContainingSymbol), Task 7 |
| 2026-06-26 | Task 4 (verified PRs #9438, #9410 merged), Task 3 (IgnoreStringMethodReturnValue edge cases), Task 7 |
| 2026-06-25 | Task 3 (UseExecuteAsyncOverrideFixer edge cases), Task 7 |
| 2026-06-24 | Task 3 (RemoveClassCleanupBehaviorArgumentFixer edge cases), Task 7 |
| 2026-06-23 | Task 3 (PreferTestCleanupOverDispose + PreferTestInitializeOverConstructor), Task 7 |
| 2026-06-22 | Task 3 (UseCancellationTokenPropertyAnalyzer MSTEST0054 edge cases), Task 7 |
| 2026-06-21 | Task 3 (RedundantTestMethodDisplayName + UseAsyncSuffix suppressors), Task 7 |
| 2026-06-20 | Task 3 (UnusedParameterSuppressor MSTEST0047 edge cases), Task 7 |
| (pre-06-20) | Task 3 for analyzers: MSTEST0038, MSTEST0005, MSTEST0042, MSTEST0070, MSTEST0041, MSTEST0030, MSTEST0016, MSTEST0031, MSTEST0035, MSTEST0004, MSTEST0035, PreferTestMethod, IgnoreNotIgnored, NonNullableSuppressor, DoNotStoreStaticCtx, Assert.StartsWith/EndsWith |

## Last Run

2026-06-30 23:21 UTC

## Completed Work (recent)

- PR (pending) for GlobalTestFixtureShouldBeValidAnalyzer MSTEST0050 edge cases (2026-06-30) — generic class, struct, derived TestClass attribute; 18/18 pass
- PR (pending) for UseAttributeOnTestMethodAnalyzer MSTEST0007 edge cases (2026-06-29) — DataTestMethod early-return, OSCondition ConditionBase; 39/39 pass
- PR #9489 merged — DoNotUseShadowingAnalyzer MSTEST0036 (multi-level inheritance, property type mismatch, field fallthrough)
- PR #9481 merged — TestContextPropertyUsageAnalyzer MSTEST0048 (non-TestContext guard, lambda ContainingSymbol)
- PR #9468 merged — IgnoreStringMethodReturnValueAnalyzer (discard, lambda block body, chained receiver)
- PR #9438 merged — UseExecuteAsyncOverrideFixer (no public modifier, zero params, wrong param type)
- PR #9410 merged — RemoveClassCleanupBehaviorArgumentFixer (first-arg ordering, non-attribute context guard)
- PR #9382 merged — PreferTestCleanupOverDispose + PreferTestInitializeOverConstructor edge cases
- PR #9355 merged — UseCancellationTokenPropertyAnalyzer MSTEST0054 edge cases
- PR #9314 merged — async-suffix suppressors + redundant display name edge cases
- PR #9301 merged — UnusedParameterSuppressor MSTEST0047 edge cases
- PRs #9223, #9199, #9164, #9103, #9092, #9061, #9020, #8977, #8941, #8909, #8885, #8869, #8837, #8809, #8781, #8721, #8706 — all merged
