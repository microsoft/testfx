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
- **`Assert.AreSame(null, null)` is a compile error**: Calling `Assert.AreSame` with untyped null literals causes CS0411 (type inference failure). Use `(object)null` or a typed variable instead.
- **AvoidAssertAreSameWithValueTypes fires for struct-constrained T**: Generic type parameters with `where T : struct` have `IsValueType == true`, so the analyzer correctly fires.
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

## Tasks Run History (summarized)

| Date | Tasks |
|------|-------|
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

2026-07-17 UTC

## Completed Work (recent, summarized)

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
