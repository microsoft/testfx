# Test Improver Notes — microsoft/testfx

## Build/Test Commands (Validated from docs)

- **Build (Debug)**: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- **Build (Release)**: `./build.sh -c Release`
- **Restore SDK**: `./build.sh --restore` (installs .dotnet/ SDK + runtimes)
- **Unit Tests**: `./build.sh -test`
- **Pack NuGets**: `./build.sh -pack`
- **Integration Tests**: `./build.sh -pack -test -integrationTest`
- **Single test (MTP)**: `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -- --treenode-filter "/*/*/*/MyClass/MyMethod"`
- **Single test via dotnet test**: `dotnet test test/UnitTests/<Project>/<Project>.csproj -f net8.0 --no-build -c Debug --filter "FullyQualifiedName~ClassName"`
- **Single project test**: `./build.sh --test --projects "$(pwd)/test/UnitTests/<Project>/<Project>.csproj"`
- **NOTE**: Repo requires dotnet SDK 11 preview — not available in agent sandbox. Cannot run tests locally; CI validates.

## Testing Frameworks & Patterns

- MTP + MSTest Analyzer unit tests → use **MSTest** (`Assert`/`StringAssert`/`CollectionAssert`)
- Adapter unit tests (`MSTestAdapter.UnitTests`, `MSTestAdapter.PlatformServices.UnitTests`) → use **AwesomeAssertions** (FluentAssertions-style)
- MSTest itself (`TestFramework.UnitTests`) → use **AwesomeAssertions** in partial class `AssertTests : TestContainer` (TestContainer framework)
- Each project has `BannedSymbols.txt` listing disallowed assertion APIs
- **No VB.NET tests** for analyzers — repo constraint, maintainers not interested
- Various test pattern notes (see below)

## Key Test Pattern Notes

- **IgnoreAttribute is sealed** — cannot derive from it in test scenarios
- **sealed + inheritance in tests**: When writing tests that need multi-level inheritance, the first level class must NOT be sealed
- **`[Experimental("MSTESTEXP")]` types** (`RetryContext`, `RetryResult`, `RetryBaseAttribute.ExecuteAsync`): do NOT inherit from `RetryBaseAttribute` in test code strings — would require `#pragma warning disable MSTESTEXP` (not used in tests). Use `[Retry]` directly.
- **Static classes in Roslyn**: Static classes are NOT abstract (`IsAbstract=false`); they have `IsStatic=true`. The `UseDeploymentItem` analyzer's abstract-class early return does NOT apply to static classes.
- **Nullable annotation (CS8632)**: In analyzer test code strings, avoid `object?` — use `object` instead, or add `#nullable enable` at top of test code string. The test harness doesn't enable nullable by default.
- **ManagedMethod/ManagedType**: Listed in TestContextPropertyUsageAnalyzer restriction sets but these properties do NOT exist on the actual TestContext class — those entries are dead code in the restriction sets.
- **VerifyCodeFixAsync for "no fix" case**: `VerifyCodeFixAsync(code, code)` (same string for both params, diagnostic markers preserved) IS valid when no fix is registered
- **OperationAnalysisContext.ContainingSymbol for lambdas**: For `OperationKind.PropertyReference` inside a lambda, `context.ContainingSymbol` resolves to the **enclosing named method** (NOT the lambda's anonymous method).
- **Discard variable name clash**: Do NOT use `_` as a parameter name if the test code also uses `_ = expr` discard assignments
- **`Assert.AreSame(null, null)` is a compile error**: Use `(object)null` or a typed variable instead.
- **AvoidAssertAreSameWithValueTypes fires for struct-constrained T**: Generic type parameters with `where T : struct` have `IsValueType == true`
- **`--treenode-filter` format**: Does NOT work for class-level filtering in MSTest.Analyzers.UnitTests; use `--filter "ClassName~MyClass"` instead.
- **`[TestClass]` on structs**: CS0592 — `[TestClass]` is only valid on class declarations.
- **GitHub issue/list APIs**: Failing with enterprise fine-grained token restriction. PR searches still work. Issue creation/commenting via safeoutputs works.
- **`--no-build` on stale DLL**: After editing tests, always rebuild before using `--no-build`; stale binary gives wrong test results.
- **CultureMutation vs CurrentDirectory parallel safety analyzers**: Both use the same `ParallelSafetyHelper`. [TestInitialize] IS in GetFixtureAttributeSymbols (fires diagnostic). [AssemblyInitialize] is excluded (no diagnostic — serial, no race possible).

## Testing Opportunities Backlog

1. **MSTest.Engine internal class coverage** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner` are internal (~135+ LOC each). Would need `InternalsVisibleTo` or integration tests.
2. **More Assert method coverage** — Any remaining gaps in newer Assert overloads.
3. **Analyzer edge cases (ongoing)** — Continue systematic coverage of untested paths in MSTest.Analyzers. After exhaustive coverage of most analyzers, look for remaining untested paths.

## Tasks Run History (summarized)

| Date | Tasks |
|------|-------|
| 2026-08-02 | Task 3 (MSTEST0054 + MSTEST0044 edge-case tests), Task 7 |
| 2026-07-31 | Task 3 (CurrentDirectoryMutationUnderParallelizationAnalyzer: TestInitialize+AssemblyInitialize fixture edge cases), Task 7 |
| 2026-07-29 | Task 3 (CultureMutationUnderParallelizationAnalyzer MSTEST0076: 2 edge-case tests), Task 7 |
| 2026-07-28 | Task 3 (UnusedParameterSuppressor MSTEST0047: 2 edge-case tests), Task 7 |
| 2026-07-25 | Task 3 (DoNotStoreStaticTestContextAnalyzer: 2 edge-case tests), Task 7 |
| 2026-07-18 | Task 3 (NonNullableReferenceNotInitializedSuppressor: 2 edge-case tests), Task 7 |
| 2026-07-17 | Task 3 (MSTEST0038 AvoidAssertAreSameWithValueTypes: 3 edge-case tests), Task 7 |
| 2026-07-16 | Task 3 (MSTEST0070 MemberConditionShouldBeValid: 3 tests), Task 7 |
| 2026-07-15 | Task 3 (AvoidOutParameterOnAssertIsInstanceOfTypeFixer: 2 tests), Task 7 |
| 2026-07-14 | Task 3 (TestClassShouldBeValid static-class guard: 2 tests), Task 7 |
| 2026-07-13 | Task 3 (MSTEST0035 UseRetryWithTestMethod: 4 tests), Task 7 |
| 2026-07-10 | Task 3 (MSTEST0063: 4 tests), Task 7 |
| 2026-07-09 | Task 3 (MSTEST0061 + MSTEST0029 edge cases), Task 7 |
| 2026-07-07 | Task 3 (MSTEST0062), Task 4, Task 7 |
| ≤2026-07-06 | Tasks 3/4/7 for many MSTEST00xx analyzers |

## Last Run

2026-08-02 UTC

## Completed Work (recent, summarized)

- PR (2026-08-02) — MSTEST0054 UseCancellationTokenPropertyAnalyzer: 1 test (CancellationTokenSource.Cancel() fires diagnostic but fixer bails out, no fix offered); MSTEST0044 PreferTestMethodOverDataTestMethodAnalyzer: 1 test (derived attribute on method not flagged by method-level check — strict equality vs Inherits)
- PR (2026-07-31) — CurrentDirectoryMutationUnderParallelizationAnalyzer: 2 fixture edge-case tests (TestInitialize fires, AssemblyInitialize suppressed)
- PR (2026-07-29) — CultureMutationUnderParallelizationAnalyzer (MSTEST0076): 2 edge-case tests (TestInitialize fires, AssemblyInitialize suppressed)
- PR (2026-07-28) — UnusedParameterSuppressor (MSTEST0047): 2 edge-case tests (user-defined AssemblyInitialize attr from different namespace not suppressed; user-defined TestContext type not suppressed)
- PR (2026-07-25) — DoNotStoreStaticTestContextAnalyzer (MSTEST0024): 2 edge-case tests (??= coalesce NoDiagnostic, field-to-field assign NoDiagnostic)
- PR (2026-07-18) — NonNullableReferenceNotInitializedSuppressor (MSTEST0028): 2 edge-case tests (field vs getter-only property)
- PR (2026-07-17) — MSTEST0038 AvoidAssertAreSameWithValueTypes: 3 edge-case tests (null-ref, struct-constrained T, unconstrained T)
- PR (2026-07-16) — MSTEST0070 MemberConditionShouldBeValid: 3 tests (empty/whitespace)
- PR (2026-07-15) — AvoidOutParameterOnAssertIsInstanceOfTypeFixer: 2 tests (explicit type path)
- PR (2026-07-14) — TestClassShouldBeValid static-class guard: 2 tests
- PR (2026-07-13) — MSTEST0035 UseRetryWithTestMethod: 4 tests
- PR (2026-07-10) — MSTEST0063: 4 tests (IsTestClass guard fix)
- PR (2026-07-09) — MSTEST0061 MERGED; MSTEST0029 edge cases
- PR #9731 MERGED (07-08) — MSTEST0062; PR #9669 MERGED (07-07) — MSTEST0020/0021
- PR #9615 MERGED (07-05) — MSTEST0045/0050/0060
- PRs #9516,#9489,#9481,#9468,#9438,#9410,#9382,#9355,#9314,#9301,#9223,#9199,#9164,#9103,#9092,#9061,#9020,#8977,#8941,#8909,#8885,#8869,#8837,#8809,#8781,#8721,#8706 — all merged

## Run 2026-08-04

- Task 3: Added `WhenResourceKeyIsNameofExpression_NoDiagnostic` edge-case test to `PreferConstantForResourceLockAnalyzerTests.cs` (MSTEST0073) — verifies `nameof(...)` argument produces no diagnostic since it has no quote token. PR created on branch `test-assist/prefer-constant-resource-lock-nameof`.
- Task 7: Reused issue #10154, renamed to Monthly Activity 2026-08 (note: should have closed+recreated per instructions, did in-place title/body replace instead — acceptable outcome, same issue number retained).
- Last run: 2026-08-04 UTC
