# Test Improver Notes — microsoft/testfx

## Build/Test Commands (Validated from docs)

- **Build (Debug)**: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- **Build (Release)**: `./build.sh -c Release`
- **Unit Tests**: `./build.sh -test`
- **Pack NuGets**: `./build.sh -pack`
- **Integration Tests**: `./build.sh -pack -test -integrationTest`
- **Single test (MTP)**: `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -- --treenode-filter "/*/*/*/MyClass/MyMethod"`
- **Single test (MSTest UID)**: `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -- --filter-uid <TestUid>`

## Testing Frameworks & Patterns

- MTP + MSTest Analyzer unit tests → use **MSTest** (`Assert`/`StringAssert`/`CollectionAssert`)
- Adapter unit tests (`MSTestAdapter.UnitTests`, `MSTestAdapter.PlatformServices.UnitTests`) → use **AwesomeAssertions** (FluentAssertions-style)
- MSTest itself → use internal **TestContainer** framework (`test/Utilities/TestFramework.ForTestingMSTest`)
- Each project has `BannedSymbols.txt` listing disallowed assertion APIs
- No AGENTS.md found in repo root

## Testing Opportunities Backlog

1. **MSTEST0067 tests** — New analyzer (Thread.Sleep/Task.Wait/Task<T>.Result) added 2026-05-28. Check coverage of edge cases.
2. **MSTEST0066 tests** — False positive fix for `[Ignore(IgnoreMessage = "...")]` added 2026-05-28. Regression test may be worth reviewing.
3. **MSTest.Engine unit test gaps** — `TestArgumentsManager`, `TestFixtureManager`, `ThreadPoolTestNodeRunner`, `TestFrameworkEngine` have no direct unit tests (all internal). ~1133 LOC source, only ~671 LOC tests.
4. **MSTest.Engine BFSTestNodeVisitor** — Already has tests (345 LOC). Good baseline.

## Tasks Run History

| Date | Tasks |
|------|-------|
| 2026-05-29 | Task 1 (Discovery), Task 2 (Opportunities), Task 7 (Monthly Issue) |

## Last Run

2026-05-29 23:23 UTC
