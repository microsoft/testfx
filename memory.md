# TestFX Test Improver Memory

## Last Updated
2026-04-27

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
  - Diagnostic without args: `public class [|MyClass|]`
  - `EmptyCodeFixProvider` when no code fix exists
  - Rule fields are `internal` on analyzers; tests use `VerifyCS.Diagnostic()` (single-rule analyzers)
- **TestFramework.UnitTests**: Uses internal test framework from `TestFramework.ForTestingMSTest`
  - Inherits `TestContainer`, public void/async methods as tests (no [TestMethod])
  - Uses `AwesomeAssertions` for assertions
  - Namespace: `UnitTestFramework.Tests` or `Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes`
  - Requires `-p:UseSharedCompilation=false` due to sandbox restrictions on shared compiler server
  - **NOT included in `NonWindowsTests.slnf`** — Linux/macOS CI never builds or tests this project
  - Windows CI builds it with `-p:TreatWarningsAsErrors=true` — all IDE style warnings become errors
  - Always validate new tests with: `dotnet build ... -p:TreatWarningsAsErrors=true` before pushing
- **Microsoft.Testing.Platform.UnitTests**: Uses MSTest
  - Uses `Assert.ThrowsExactly<T>()` for exception assertions (NOT `ThrowsException`)
  - `AwesomeAssertions` is BANNED — use MSTest `Assert.*` methods
  - Has `InternalsVisibleTo` access to `Microsoft.Testing.Platform` internals
  - Tests run on both net8.0 and net9.0 — total count doubled
  - Baseline: 1150 tests (575 per TFM) before 2026-04-27 additions

## Testing Backlog (prioritized)

1. ✅ **DONE** `UseConditionBaseWithTestClassAnalyzer` (MSTEST0041) → PR #7809 merged
2. ✅ **DONE** `RetryAttribute` unit tests → PR #7838 open, CI green
3. ✅ **DONE** `TimeSpanParser` unit tests → PR created 2026-04-27
4. Investigate `Microsoft.Testing.Platform.UnitTests` Logging and Telemetry test gaps
5. `TestFramework.UnitTests` assertion edge cases
6. Check if MSTest integration test suite covers `CICondition/OSCondition` scenarios
7. `TypeContainingTestMethodShouldBeATestClassAnalyzer.cs` in test folder has wrong filename (should be `...AnalyzerTests.cs`) — minor infrastructure issue

## Completed Work

### 2026-04-27
- **Task 3: Created PR for TimeSpanParser tests**: 116 new tests in `Microsoft.Testing.Platform.UnitTests`
  - Covers TryParse + Parse for all suffix formats (ms, mil, s, m, h, d), decimals, null/empty, invalid inputs
  - 1150 → 1266 tests (net8.0 + net9.0 combined)
  - Key discovery: s/m/h/d suffixes use `StringComparison.Ordinal` (case-sensitive), ms/mil use `OrdinalIgnoreCase`

### 2026-04-28
- **Fixed PR #7838 CI failures**: IDE0008/IDE0017 code style violations
  - `TestFramework.UnitTests` not in `NonWindowsTests.slnf` → Linux CI passes, Windows CI fails
  - Windows uses `TreatWarningsAsErrors=true` → IDE0008/IDE0017 become errors
  - Fix: object initializers (IDE0017), explicit types for non-apparent var (IDE0008)

### 2026-04-25
- **PR #7838 created**: `[Test Improver] Add unit tests for RetryAttribute`
  - 13 new tests covering constructor validation, BackoffType, and ExecuteAsync retry logic
  - All 809 tests pass (796 baseline + 13 new)

### 2026-04-24
- **PR #7809 merged**: `[Test Improver] Add tests for UseConditionBaseWithTestClassAnalyzer (MSTEST0041)`
  - 9 new tests - merged same day by Evangelink

## Round-Robin Task Status

| Task | Last Run |
|------|----------|
| Task 1: Discover commands | 2026-04-24 |
| Task 2: Identify opportunities | 2026-04-24 |
| Task 3: Implement tests | 2026-04-27 |
| Task 4: Maintain PRs | 2026-04-28 |
| Task 5: Comment on issues | - |
| Task 6: Test infrastructure | - |
| Task 7: Monthly summary | 2026-04-27 |

## Maintainer Priorities
- PR #7809 (MSTEST0041 tests) was merged within hours - maintainer is receptive to test PRs

## Notes
- Analyzer rule fields are `internal` unless multi-rule analyzers need test access
- `ConditionBaseAttribute` is in `Microsoft.VisualStudio.TestTools.UnitTesting` namespace
- `RetryContext` constructor is `internal`, so TestFramework.UnitTests (via InternalsVisibleTo) can create it
- `ExecuteAsync` is `protected internal` and `[Experimental("MSTESTEXP")]` - suppress with `#pragma warning disable MSTESTEXP`
- `TestFramework.UnitTests` requires `-p:UseSharedCompilation=false` to build outside the full Arcade SDK build
  - The shared Roslyn compiler server is sandboxed and cannot access NuGet package DLLs in ~/.nuget
  - The full ./build.sh does NOT have this problem
- `AwesomeAssertions` is banned in MSTest.Analyzers.UnitTests and Microsoft.Testing.Platform.UnitTests (see BannedSymbols.txt)
- **IMPORTANT**: When adding tests to `TestFramework.UnitTests`, always validate with `TreatWarningsAsErrors=true`
  since Linux CI never builds this project (not in NonWindowsTests.slnf) but Windows CI does and treats
  all warnings as errors. Use: `dotnet build ... -f net8.0 -p:UseSharedCompilation=false -p:TreatWarningsAsErrors=true`
- `TypeContainingTestMethodShouldBeATestClassAnalyzer.cs` in test folder is named without "Tests" suffix (inconsistent but tests run fine)
