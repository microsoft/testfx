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

## Testing Opportunities Backlog

1. **MSTest.Engine internal class coverage** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner` are internal (~135+ LOC each). Would need `InternalsVisibleTo` or integration tests.
2. **More Assert method coverage** — Any remaining gaps in newer Assert overloads.

## Tasks Run History

| Date | Tasks |
|------|-------|
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

2026-06-15 23:30 UTC

## Completed Work

- PR (pending) for MemberConditionShouldBeValidAnalyzer MSTEST0070 edge cases (2026-06-15) — instance method→NotStatic, internal method→NotPublic, private getter→NotReadable, params+ConditionMode
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
