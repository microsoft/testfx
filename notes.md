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
- **TimeoutAttribute is sealed** — cannot derive from it in test scenarios (confirmed CS0509).
- **`--treenode-filter` format**: Does NOT work for class-level filtering in MSTest.Analyzers.UnitTests; use `--filter "ClassName~MyClass"` or `--filter-uid "Namespace.ClassName.MethodName"` instead.
- **DoNotUseShadowingAnalyzer**: `GetBaseMembers` walks the full inheritance chain via while-loop on `BaseType`; `IsMemberShadowing` handles only `IMethodSymbol` and `IPropertySymbol` — fields fall through to `return false`. Property type must match via `SymbolEqualityComparer` for shadowing detection.
- **`[TestClass]` on structs**: CS0592 — `[TestClass]` is only valid on class declarations. For tests involving struct containing types, omit `[TestClass]` from the struct.
- **GitHub issue/list APIs**: Failing with enterprise fine-grained token restriction (token lifetime >8 days). PR searches still work. Issue creation/commenting via safeoutputs works. Cannot list or search issues via MCP tools.
- **`--no-build` on stale DLL**: After editing tests, always rebuild (`dotnet build`) before using `--no-build`; stale binary gives wrong test results.
- **Generic class in FixtureUtils**: `ContainingType.IsGenericType && !allowGenericType` fires for GlobalTestFixtureShouldBeValid because `allowGenericType: false`. A `[TestClass] public class MyTestClass<T>` containing `[GlobalTestInitialize]` produces a diagnostic.
- **DuplicateTestMethodAttributeAnalyzer has NO TestClass guard**: fires on duplicate TestMethod-derived attrs on any method, regardless of [TestClass].
- **DuplicateTestMethodAttributeFixer first-wins**: keeps the first TestMethod-derived attribute encountered in attribute list order; subsequent ones are removed.
- **PreferDisposeOverTestCleanupAnalyzer has NO TestClass guard**: fires on any method with [TestCleanup] regardless of whether the class has [TestClass]. ImplementsIDisposable fixer method checks semantic BaseList only (not full inheritance hierarchy).
- **PreferConstructorOverTestInitializeAnalyzer has NO TestClass guard**: fires on any method with [TestInitialize] that returns void. Fixer merges into the FIRST non-static constructor found, even parameterized ctors.
- **Unused using after code fix**: When a code fix removes the only usage of a namespace, the `using` becomes unused but remains in fixed code (fixers don't remove usings). Tests pass because unused usings are warnings not errors.

## Testing Opportunities Backlog

1. **MSTest.Engine internal class coverage** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner` are internal (~135+ LOC each). Would need `InternalsVisibleTo` or integration tests.
2. **More Assert method coverage** — Any remaining gaps in newer Assert overloads.
3. **Analyzer edge cases (ongoing)** — Continue systematic coverage of untested paths in MSTest.Analyzers. After exhaustive coverage of most analyzers, look for remaining untested paths.

## Tasks Run History

| Date | Tasks |
|------|-------|
| 2026-07-10 | Task 3 (MSTEST0063 fix: exact-match→Inherits for TestClassAttribute guard; 4 tests for STATestClass/custom derived attrs), Task 7 |
| 2026-07-09 (2nd run) | Task 3 (MSTEST0061 UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer: OSPlatform.Create known+unknown, IsIOS, IsAndroid), Task 7 |
| 2026-07-09 | Task 3 (MSTEST0029 PublicMethodShouldBeTestMethod: virtual/override NoDiagnostic, make-private fixer, fix misleading [TestInitialize]→[TestCleanup] in existing test), Task 7 |
| 2026-07-07 | Task 3 (MSTEST0062 AvoidOutRefTestMethodParameters edge cases), Task 4, Task 7 |
| 2026-07-06 | Task 3 (MSTEST0020/0021 edge cases — redo after prior PR intent did not materialize), Task 7 |
| 2026-07-05 | Task 3 (PreferConstructorOverTestInitialize MSTEST0020 + PreferDisposeOverTestCleanup MSTEST0021 edge cases), Task 7 |
| 2026-07-04 | Task 3 (UseCooperativeCancellationForTimeout MSTEST0045: async method, non-TestClass, named arg), Task 7 |
| 2026-07-03 | Task 3 (GlobalTestFixtureShouldBeValid MSTEST0050 generic+derivedAttr, DuplicateTestMethodAttribute MSTEST0060 no-TestClass-guard+first-wins-fixer), Task 7 |
| 2026-07-02 | Task 3 (GlobalTestFixtureShouldBeValidAnalyzer MSTEST0050 generic+struct+derivedAttr, DuplicateTestMethodAttributeAnalyzer MSTEST0060 outside-TestClass+inline-mixed+first-wins), Task 7 |
| 2026-07-01 | Task 3 (DuplicateTestMethodAttributeAnalyzer MSTEST0060: method outside TestClass, mixed inline list, first-wins fixer), Task 7 |
| 2026-06-30 | Task 3 (GlobalTestFixtureShouldBeValidAnalyzer MSTEST0050 edge cases: generic class, struct, derived TestClass attribute), Task 7 |
| 2026-06-29 | Task 3 (UseAttributeOnTestMethodAnalyzer MSTEST0007: DataTestMethod early-return, OSCondition ConditionBase subclass), Task 4, Task 7 |
| 2026-06-28 | Task 3 (DoNotUseShadowingAnalyzer MSTEST0036: multi-level inheritance, property type mismatch, field shadowing), Task 7 |
| 2026-06-27 | Task 3 (TestContextPropertyUsageAnalyzer MSTEST0048: non-TestContext type guard, lambda ContainingSymbol), Task 7 |
| 2026-06-26 | Task 4 (verified PRs #9438, #9410 merged), Task 3 (IgnoreStringMethodReturnValue edge cases), Task 7 |
| (pre-06-26) | Task 3 for analyzers: many MSTEST00xx, Assert methods, suppressors, fixers |

## Last Run

2026-07-10 23:16 UTC

## Completed Work (recent)

- PR for MSTEST0063 fix (created 2026-07-10) — Fixed exact-match→IsTestClass() guard so [STATestClass] and derived attrs trigger the constructor validity diagnostic. 4 new tests.
- PR for MSTEST0061 (created 2026-07-09) — MERGED: UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer: 4 edge cases
- PR for MSTEST0029 (created 2026-07-09) — PublicMethodShouldBeTestMethod edge cases: virtual/override NoDiagnostic paths (IsVirtual/IsOverride analyzer guard), make-private code fix (CodeActionIndex=1), fix misleading [TestInitialize]→[TestCleanup] in existing test (3 new tests + 1 fix)
- PR #9731 MERGED (2026-07-08 by Evangelink) — MSTEST0062 edge cases (derived attr, no-TestClass guard, `in` parameter)
- PR for MSTEST0062 (created 2026-07-07) — AvoidOutRefTestMethodParameters edge cases: derived attr, no-TestClass guard, `in` parameter (3 new tests)
- PR #9669 MERGED (2026-07-07 by Evangelink) — MSTEST0020/0021 edge cases (non-TestClass diagnostic + parameterized-ctor fixer merge)
- PR #9615 MERGED — MSTEST0045/0050/0060 edge cases (merged 2026-07-05 by Evangelink)
- PR #9516 merged — UseAttributeOnTestMethodAnalyzer (MSTEST0007) edge cases (merged 2026-06-30)
- PR #9489 merged — DoNotUseShadowingAnalyzer MSTEST0036
- PR #9481 merged — TestContextPropertyUsageAnalyzer MSTEST0048
- PR #9468 merged — IgnoreStringMethodReturnValueAnalyzer
- PR #9438 merged — UseExecuteAsyncOverrideFixer
- PR #9410 merged — RemoveClassCleanupBehaviorArgumentFixer
- PR #9382 merged — PreferTestCleanupOverDispose + PreferTestInitializeOverConstructor edge cases
- PR #9355 merged — UseCancellationTokenPropertyAnalyzer MSTEST0054 edge cases
- PR #9314 merged — async-suffix suppressors + redundant display name edge cases
- PR #9301 merged — UnusedParameterSuppressor MSTEST0047 edge cases
- PRs #9223, #9199, #9164, #9103, #9092, #9061, #9020, #8977, #8941, #8909, #8885, #8869, #8837, #8809, #8781, #8721, #8706 — all merged
