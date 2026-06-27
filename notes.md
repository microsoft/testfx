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

## Testing Opportunities Backlog

1. **MSTest.Engine internal class coverage** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner` are internal (~135+ LOC each). Would need `InternalsVisibleTo` or integration tests.
2. **More Assert method coverage** — Any remaining gaps in newer Assert overloads.
3. **Analyzer edge cases (ongoing)** — Continue with analyzers with few tests. Next candidates:
   - `UseCooperativeCancellationForTimeoutAnalyzerTests` (33 tests) — possible additional fixture method scenarios
   - `UseParallelizeAttributeAnalyzerTests` (7 tests) — well covered relative to complexity
   - `PreferDisposeOverTestCleanupAnalyzerTests` (11 tests) — similar to PreferTestCleanupOverDispose but opposite direction; abstract class / non-TestClass scenarios
   - `DoNotUseShadowingAnalyzerTests` (18 tests) — look at multi-level inheritance chain edge cases

## Tasks Run History

| Date | Tasks |
|------|-------|
| 2026-06-27 | Task 3 (TestContextPropertyUsageAnalyzer MSTEST0048 edge cases: non-TestContext type guard, lambda ContainingSymbol behavior), Task 7 (Monthly Issue Jun) |
| 2026-06-26 | Task 4 (verified PRs #9438 and #9410 merged), Task 3 (IgnoreStringMethodReturnValueAnalyzer edge cases: discard assignment, lambda block body, chained receiver), Task 7 (Monthly Issue Jun) |
| 2026-06-25 | Task 3 (UseExecuteAsyncOverrideFixer edge cases: no public modifier, zero params, wrong param type), Task 7 (Monthly Issue Jun) |
| 2026-06-24 | Task 3 (RemoveClassCleanupBehaviorArgumentFixer edge cases: first-arg ordering, non-attribute context guard), Task 7 (Monthly Issue Jun) |
| 2026-06-23 | Task 3 (PreferTestCleanupOverDispose + PreferTestInitializeOverConstructor edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-22 | Task 3 (UseCancellationTokenPropertyAnalyzer MSTEST0054 edge cases: TestInitialize method, non-TestContext symbol, parameter receiver), Task 7 (Monthly Issue Jun) |
| 2026-06-21 | Task 3 (RedundantTestMethodDisplayNameAnalyzer custom-derived attribute + UseAsyncSuffix suppressors negative boundary cases), Task 7 (Monthly Issue Jun) |
| 2026-06-20 | Task 3 (UnusedParameterSuppressor MSTEST0047 edge cases: TestMethod+TestContext not suppressed, AssemblyInitialize+non-TestContext not suppressed), Task 7 (Monthly Issue Jun) |
| 2026-06-19 | Task 3 (AvoidAssertAreSameWithValueTypesAnalyzer MSTEST0038 edge cases: enum/struct/nullable), Task 4 (verify open PRs), Task 7 (Monthly Issue Jun) |
| 2026-06-17 | Task 3 (TestContextShouldBeValidAnalyzer MSTEST0005 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-16 | Task 3 (DuplicateDataRowAnalyzer MSTEST0042 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-15 | Task 3 (MemberConditionShouldBeValidAnalyzer MSTEST0070 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-13 | Task 3 (UseConditionBaseWithTestClassAnalyzer MSTEST0041 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-12 | Task 3 (TypeContainingTestMethodShouldBeATestClassAnalyzer MSTEST0030 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-11 | Task 3 (TestClassShouldHaveTestMethodAnalyzer MSTEST0016 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-10 | Task 3 (DoNotUseSystemDescriptionAttributeAnalyzer MSTEST0031 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-09 | Task 3 (UseDeploymentItemWithTestMethodOrTestClass MSTEST0035 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-08 | Task 3 (PublicTypeShouldBeTestClassAnalyzer MSTEST0004 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-07 | Task 3 (UseRetryWithTestMethodAnalyzer MSTEST0035 edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-06 | Task 3 (PreferTestMethodOverDataTestMethodAnalyzer edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-05 | Task 3 (TestMethodShouldNotBeIgnoredAnalyzer edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-04 | Task 3 (NonNullableReferenceNotInitializedSuppressor edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-03 | Task 3 (DoNotStoreStaticTestContextAnalyzer edge cases), Task 7 (Monthly Issue Jun) |
| 2026-06-02 | Task 2 (opportunities), Task 3 (Assert.StartsWith/EndsWith tests), Task 7 (Monthly Issue Jun) |
| 2026-06-01 | Task 3 (VB tests — constraint violation, removed), Task 7 (Monthly Issue Jun) |
| 2026-05-31 | Task 3 (MSTEST0041 edge cases), Task 7 (Monthly Issue) |
| 2026-05-30 | Task 3 (MSTEST0067 edge cases), Task 7 (Monthly Issue) |
| 2026-05-29 | Task 1 (Discovery), Task 2 (Opportunities), Task 7 (Monthly Issue) |

## Last Run

2026-06-27 23:16 UTC

## Completed Work

- PR (pending) for TestContextPropertyUsageAnalyzer MSTEST0048 edge cases (2026-06-27) — non-TestContext type guard, lambda ContainingSymbol behavior; 10/10 pass
- PR #9468 (pending) for IgnoreStringMethodReturnValueAnalyzer edge cases (2026-06-26) — discard assignment (no diagnostic), lambda block body (diagnostic), chained receiver (no diagnostic); 8/8 pass
- PR #9438 merged (2026-06-26 by Evangelink) — UseExecuteAsyncOverrideFixer edge cases: no public modifier, zero params, wrong param type
- PR #9410 merged (2026-06-25 by Evangelink) — RemoveClassCleanupBehaviorArgumentFixer edge cases: first-arg ordering, non-attribute context guard
- PR #9382 merged (2026-06-24 by Evangelink) — PreferTestCleanupOverDispose + PreferTestInitializeOverConstructor edge cases: full dispose pattern, Dispose+TestCleanup coexistence, static constructor
- PR #9355 merged (UseCancellationTokenPropertyAnalyzer MSTEST0054 edge cases) — merged 2026-06-23 by Evangelink
- PR #9314 merged (async-suffix suppressors + redundant display name edge cases) — merged 2026-06-22 by Evangelink
- PR #9301 merged (UnusedParameterSuppressor MSTEST0047 edge cases) — merged 2026-06-21 by Evangelink
- PR (merged) for AvoidAssertAreSameWithValueTypesAnalyzer MSTEST0038 edge cases — enum/struct/nullable types; in main branch
- PR #9223 merged (TestContextShouldBeValidAnalyzer MSTEST0005 edge cases) — merged 2026-06-18
- PR #9199 merged (DuplicateDataRowAnalyzer MSTEST0042 edge cases) — merged 2026-06-17
- PR #9164 merged (MemberConditionShouldBeValidAnalyzer MSTEST0070 edge cases) — merged 2026-06-16
- PR #9103 merged (UseConditionBaseWithTestClassAnalyzer MSTEST0041 edge cases) — merged 2026-06-14
- PR #9092 merged (TypeContainingTestMethodShouldBeATestClassAnalyzer MSTEST0030 edge cases) — merged 2026-06-13
- PR #9061 merged (TestClassShouldHaveTestMethodAnalyzer MSTEST0016 edge cases) — merged 2026-06-12
- PR #9020 merged (DoNotUseSystemDescriptionAttributeAnalyzer MSTEST0031 edge cases) — merged 2026-06-11
- PR #8977 merged (UseDeploymentItemWithTestMethodOrTestClass MSTEST0035 edge cases) — merged 2026-06-10
- PR #8941 merged (PublicTypeShouldBeTestClassAnalyzer MSTEST0004 edge cases) — merged 2026-06-09
- PR #8909 merged (UseRetryWithTestMethodAnalyzer MSTEST0035 edge cases) — merged 2026-06-08
- PR #8885 merged (PreferTestMethodOverDataTestMethodAnalyzer edge cases) — merged 2026-06-07
- PR #8869 merged (TestMethodShouldNotBeIgnoredAnalyzer edge cases) — merged 2026-06-07
- PR #8837 merged (NonNullableReferenceNotInitializedSuppressor edge cases)
- PR #8809 merged (DoNotStoreStaticTestContextAnalyzer edge cases)
- PR #8781 merged (Assert.StartsWith/EndsWith StringComparison overloads and null handling)
- PR #8721 merged (MSTEST0041 abstract method edge case)
- PR #8706 merged (MSTEST0067 AvoidThreadSleepAndTaskWaitInTests edge cases)
