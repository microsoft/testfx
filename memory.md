# TestFX Test Improver Memory

## Last Updated
2026-04-29

## Build/Test Commands

### Build
```bash
export PATH="$PATH:.dotnet"
./build.sh               # full Arcade SDK build (restore+build)
dotnet build <project>   # direct project build (after restore)
```

### Test (unit tests)
```bash
dotnet test test/UnitTests/MSTest.Analyzers.UnitTests/MSTest.Analyzers.UnitTests.csproj -c Debug [--filter "TestName"]
dotnet test test/UnitTests/TestFramework.UnitTests/TestFramework.UnitTests.csproj -c Debug -f net8.0 -p:UseSharedCompilation=false
dotnet test test/UnitTests/Microsoft.Testing.Platform.UnitTests/Microsoft.Testing.Platform.UnitTests.csproj -c Debug
```

### Acceptance/Integration tests
```bash
./build.sh -pack    # must run first
./test.sh           # runs integration tests
```

### Coverage
- Codecov via CI (see codecov.yml)
- Coverage report in artifacts: artifacts/bin/<project>/Debug/<tfm>/TestResults/*.coverage

## Test Frameworks & Patterns

- **MSTest.Analyzers.UnitTests**: Uses MSTest + Roslyn `CSharpCodeFixVerifier<TAnalyzer, TCodeFix>` pattern
  - No diagnostic: `await VerifyCS.VerifyAnalyzerAsync(code)`
  - Diagnostic with location markup: `public class {|#0:MyClass|}` + `VerifyCS.Diagnostic().WithLocation(0).WithArguments("arg")`
  - `EmptyCodeFixProvider` when no code fix exists; update to real fixer when one is added
- **TestFramework.UnitTests**: Uses internal test framework from `TestFramework.ForTestingMSTest`
  - Inherits `TestContainer`, public void/async methods as tests (no [TestMethod])
  - Uses `AwesomeAssertions` for assertions
  - Requires `-p:UseSharedCompilation=false` due to sandbox restrictions on shared compiler server
  - **NOT included in `NonWindowsTests.slnf`** — Linux/macOS CI never builds or tests this project
  - Windows CI builds it with `-p:TreatWarningsAsErrors=true` — all IDE style warnings become errors
  - Always validate new tests with: `dotnet build ... -p:TreatWarningsAsErrors=true` before pushing
- **Microsoft.Testing.Platform.UnitTests**: Uses MSTest
  - Uses `Assert.ThrowsExactly<T>()` for exception assertions (NOT `ThrowsException`)
  - `AwesomeAssertions` is BANNED — use MSTest `Assert.*` methods
  - Has `InternalsVisibleTo` access to `Microsoft.Testing.Platform` internals
  - Tests run on both net8.0 and net9.0 — total count doubled
  - Baseline (after 2026-04-29 additions): 653 tests (net9.0)
  - `PlatformResources.LoggerFactoryNotReady` NOT accessible in test project (only in IS_CORE_MTP mode)

## Testing Backlog (prioritized)

1. ✅ **DONE** `UseConditionBaseWithTestClassAnalyzer` (MSTEST0041) → PR #7809 merged
2. ✅ **DONE** `RetryAttribute` unit tests → PR #7838 merged
3. ✅ **DONE** `TimeSpanParser` unit tests → PR #7858 merged
4. ✅ **DONE** `PasteArguments` unit tests → PR #7888 open (CI green, awaiting review)
5. ✅ **DONE** `LoggerFactoryProxy` unit tests → PR created 2026-04-29
6. `LoggingManager.BuildAsync` coverage — filters disabled extensions, skips FileLoggerProvider
7. `TestFramework.UnitTests` assertion edge cases
8. Code fix test coverage for MSTEST0031 when `DoNotUseSystemDescriptionAttributeFixer` lands

## Completed Work

### 2026-04-29 (run 1)
- **Task 3: Created PR for LoggerFactoryProxy tests**: 3 new tests
  - CreateLogger before init → InvalidOperationException
  - SetLoggerFactory(null) → ArgumentNullException
  - CreateLogger after init → delegates to inner factory
  - 650 → 653 tests (net9.0)
- **Task 5: Commented on #7897**: Outlined code fix test strategy for MSTEST0031
- **Task 6 assessment**: LoggerFactoryProxy zero coverage identified and addressed; LoggingManager.BuildAsync still untested

### 2026-04-28 (run 2)
- **Task 3: Created PR for PasteArguments tests**: 17 new tests in `Microsoft.Testing.Platform.UnitTests`
  - 633 → 650 tests (net8.0)

### 2026-04-27
- **Task 3: PR #7858 for TimeSpanParser tests**: 116 new tests, merged same day
  - 1150 → 1266 tests (net8.0 + net9.0 combined)

### 2026-04-28 (run 1)
- **Fixed PR #7838 CI failures**: IDE0008/IDE0017 code style violations
  - `TestFramework.UnitTests` not in `NonWindowsTests.slnf` → Linux CI passes, Windows CI fails
  - Windows uses `TreatWarningsAsErrors=true` → IDE0008/IDE0017 become errors

### 2026-04-25
- **PR #7838**: `[Test Improver] Add unit tests for RetryAttribute` → merged 2026-04-27

### 2026-04-24
- **PR #7809 merged**: `[Test Improver] Add tests for UseConditionBaseWithTestClassAnalyzer (MSTEST0041)`

## Round-Robin Task Status

| Task | Last Run |
|------|----------|
| Task 1: Discover commands | 2026-04-24 |
| Task 2: Identify opportunities | 2026-04-24 |
| Task 3: Implement tests | 2026-04-29 |
| Task 4: Maintain PRs | 2026-04-28 |
| Task 5: Comment on issues | 2026-04-29 |
| Task 6: Test infrastructure | 2026-04-29 |
| Task 7: Monthly summary | 2026-04-29 |

## Maintainer Priorities
- PRs #7809, #7838, #7858 all merged quickly — Evangelink is receptive to focused test PRs
- Issue #7790 is stale (created as workaround for PR permissions, underlying work is done)

## Notes
- `PasteArguments` is `internal static partial class` — accessible via InternalsVisibleTo
- `PasteArguments.AppendArgument(sb, arg)`: if no whitespace/quotes → simple append; otherwise wraps in quotes with escaping
  - Backslash at end → doubled; backslash before quote → 2N+1 rule; backslash elsewhere → unchanged
- `AwesomeAssertions` is banned in MSTest.Analyzers.UnitTests and Microsoft.Testing.Platform.UnitTests (see BannedSymbols.txt)
- **IMPORTANT**: When adding tests to `TestFramework.UnitTests`, always validate with `TreatWarningsAsErrors=true`
  since Linux CI never builds this project (not in NonWindowsTests.slnf) but Windows CI does
- `TestFramework.UnitTests` requires `-p:UseSharedCompilation=false` to build outside the full Arcade SDK build
- `PlatformResources.cs` in test project compiles WITHOUT `IS_CORE_MTP` — only limited resource string properties are available; most internal resource strings (e.g., `LoggerFactoryNotReady`) are NOT accessible in tests via this link
- Copilot SWE agent is also active in this repo — PRs #7894 and #7896 add code fixes for MSTEST0060 and MSTEST0042 with tests already included
