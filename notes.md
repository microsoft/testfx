# Test Improver Notes — microsoft/testfx

## Build/Test Commands (Validated from docs)

- **Build (Debug)**: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- **Build (Release)**: `./build.sh -c Release`
- **Unit Tests**: `./build.sh -test`
- **Pack NuGets**: `./build.sh -pack`
- **Integration Tests**: `./build.sh -pack -test -integrationTest`
- **Single test (MTP)**: `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -- --treenode-filter "/*/*/*/MyClass/MyMethod"`
- **Single test (MSTest UID)**: `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -- --filter-uid <TestUid>`
- **Single project test**: `./build.sh -test --projects "$(pwd)/test/UnitTests/<Project>/<Project>.csproj"`

## Testing Frameworks & Patterns

- MTP + MSTest Analyzer unit tests → use **MSTest** (`Assert`/`StringAssert`/`CollectionAssert`)
- Adapter unit tests (`MSTestAdapter.UnitTests`, `MSTestAdapter.PlatformServices.UnitTests`) → use **AwesomeAssertions** (FluentAssertions-style)
- MSTest itself → use internal **TestContainer** framework (`test/Utilities/TestFramework.ForTestingMSTest`)
- Each project has `BannedSymbols.txt` listing disallowed assertion APIs
- VB.NET test support: use `VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<...>` alias
- No AGENTS.md found in repo root

## Testing Opportunities Backlog

1. ~~**MSTEST0067 tests**~~ — PR #8706 merged 2026-05-31: 8 new edge case tests (lambda/local function + VB.NET)
2. ~~**MSTEST0041 VB.NET + abstract method**~~ — PR submitted 2026-05-31 (test-assist/deployment-item-edge-cases)
3. **MSTEST0066 tests** — Tests appear comprehensive already; de-prioritized
4. **MSTest.Engine internal class coverage** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner` are internal (~135+ LOC each). Would need `InternalsVisibleTo`.
5. **Remaining VB.NET coverage gaps** — Many analyzers have no VB tests: `AssertionArgsShouldBePassedInCorrectOrder`, `AvoidUsingAssertsInAsyncVoidContext`, `ClassCleanupShouldBeValid`, `DynamicDataShouldBeValid`, etc.

## Tasks Run History

| Date | Tasks |
|------|-------|
| 2026-05-31 | Task 3 (MSTEST0041 edge cases + VB), Task 7 (Monthly Issue) |
| 2026-05-30 | Task 3 (Implement MSTEST0067 edge cases), Task 7 (Monthly Issue) |
| 2026-05-29 | Task 1 (Discovery), Task 2 (Opportunities), Task 7 (Monthly Issue) |

## Last Run

2026-05-31 23:17 UTC
