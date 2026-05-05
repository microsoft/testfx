# TestFX Test Improver Memory

## Last Updated
2026-05-05

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
  - Baseline (after 2026-05-04 additions): ~666 tests per TFM (~1332 total)
  - `PlatformResources.LoggerFactoryNotReady` NOT accessible in test project (only in IS_CORE_MTP mode)
  - For multi-interface mocks (e.g. ILoggerProvider + IExtension), define internal interface combining them and mock that
  - **IMPORTANT**: LoggerFactory wraps providers into a composite logger — don't use `Assert.AreSame` to verify a provider was included; instead use `mockProvider.Verify(p => p.CreateLogger(name), Times.Once)` after calling `factory.CreateLogger(name)` with provider's `CreateLogger` mocked to return something
  - Use `Assert.Contains(string, string)` NOT `StringAssert.Contains` (MSTEST0046 analyzer rule)
  - SA1512: No blank line after single-line comments in test files
  - `PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage` is NOT available in test project — use `Assert.Contains(uid, ex.Message)` instead
  - `ExtensionValidationHelper` in `Microsoft.Testing.Platform.Helpers` namespace — accessible via InternalsVisibleTo in test project

## Testing Backlog (prioritized)

1. ✅ **DONE** `UseConditionBaseWithTestClassAnalyzer` (MSTEST0041) → PR #7809 merged
2. ✅ **DONE** `RetryAttribute` unit tests → PR #7838 merged
3. ✅ **DONE** `TimeSpanParser` unit tests → PR #7858 merged
4. ✅ **DONE** `PasteArguments` unit tests → PR #7888 merged
5. ✅ **DONE** `LoggerFactoryProxy` unit tests → PR #7916 merged
6. 🔄 **PATCH READY** `LoggingManager.BuildAsync` tests → 9 tests written and verified (5th attempt). Latest patch in run 25350394357 artifacts.
7. 🔄 **PATCH READY** `ExtensionValidationHelper.ValidateUniqueExtension` → 15 tests written and verified (2026-05-05, 2nd attempt). Patch in run 25350394357 artifacts.
8. Code fix test coverage for MSTEST0031 when `DoNotUseSystemDescriptionAttributeFixer` lands
9. `TestFramework.UnitTests` assertion edge cases

## Completed Work

### 2026-05-05
- **Task 3: Re-implemented LoggingManager.BuildAsync tests**: 9 tests (all 18 pass net8.0+net9.0). Patch in run 25350394357 artifacts.
- **Task 3: Re-implemented ExtensionValidationHelper.ValidateUniqueExtension tests**: 15 tests (all 30 pass net8.0+net9.0). Fixed SA1512 and MSTEST0037 errors vs previous attempt. Patch in run 25350394357 artifacts.
- **Task 7: Updated Monthly Summary issue #7969** with new run entry and updated patch references.

### 2026-05-04
- **Task 3: Implemented ExtensionValidationHelper.ValidateUniqueExtension tests**: 15 tests
  - 3 null guard tests (existingExtensions, newExtension, extensionSelector)
  - 2 no-throw tests (empty collection, no duplicate UID)
  - 1 throws-InvalidOperationException test
  - 2 error message content tests (UID + type name)
  - 1 multiple-duplicates test (all 3 types in message)
  - 2 wrapper-type selector tests (via Func<T, IExtension>)
  - 4 simple-overload tests
  - All 1332 tests pass (net8.0+net9.0). Push failed again.
- **Task 7: Updated Monthly Summary issue #7969**

### 2026-05-03
- **Task 3: Re-attempted LoggingManager.BuildAsync PR (4th attempt)**: 9 tests written (added `BuildAsync_NonExtensionInitializableProvider_CallsInitializeAsync` vs previous 8). All 18 tests pass. Push failed again via `safeoutputs-create_pull_request`.
- **Task 7: Updated Monthly Summary issue #7969** with new run entry and updated suggested actions

### 2026-05-02
- **Task 3: Re-attempted LoggingManager.BuildAsync PR**: Fixed bug from 2026-05-01 attempt (Assert.AreSame → Verify for provider inclusion tests). All 8 tests pass. Push failed again.
- **Task 7: Updated Monthly Summary issue #7969** with new run entry and suggested actions for stale issues

### 2026-05-01
- **Task 3: Attempted PR for LoggingManager.BuildAsync tests**: 8 new tests. Push failed, patch in issue #7968
- **Task 7: Monthly summary**: Created May 2026 issue #7969

### 2026-04-29
- **PR #7916 (LoggerFactoryProxy) merged** same day by Evangelink

### 2026-04-28
- Created PR #7888 for PasteArguments tests (17 tests)

### 2026-04-27
- PR #7858 for TimeSpanParser tests: 116 new tests, merged same day

### 2026-04-25
- PR #7838: `RetryAttribute` tests → merged 2026-04-27

### 2026-04-24
- PR #7809 merged: `UseConditionBaseWithTestClass` tests

## Round-Robin Task Status

| Task | Last Run |
|------|----------|
| Task 1: Discover commands | 2026-04-24 |
| Task 2: Identify opportunities | 2026-05-01 |
| Task 3: Implement tests | 2026-05-05 |
| Task 4: Maintain PRs | 2026-05-02 |
| Task 5: Comment on issues | 2026-04-29 |
| Task 6: Test infrastructure | 2026-04-29 |
| Task 7: Monthly summary | 2026-05-05 |

## Maintainer Priorities
- PRs merged quickly by Evangelink — receptive to focused test PRs for MTP and MSTest
- Issues #7790, #7942, #7968, #7995 are stale (see monthly summary for suggested actions)
- **PERSISTENT PUSH FAILURE**: `safeoutputs-create_pull_request` consistently returns `{"result":"success","patch":{...}}` but does NOT push branches to GitHub. Has happened for every test PR attempt (5+ runs). The tool creates a `.patch` file in `/tmp/gh-aw/` which gets included in the workflow run artifacts. Patches are referenced in monthly summary #7969.

## Notes
- `PasteArguments` is `internal static partial class` — accessible via InternalsVisibleTo
- `AwesomeAssertions` is banned in MSTest.Analyzers.UnitTests and Microsoft.Testing.Platform.UnitTests (see BannedSymbols.txt)
- **IMPORTANT**: When adding tests to `TestFramework.UnitTests`, always validate with `TreatWarningsAsErrors=true`
  since Linux CI never builds this project (not in NonWindowsTests.slnf) but Windows CI does
- `TestFramework.UnitTests` requires `-p:UseSharedCompilation=false` to build outside the full Arcade SDK build
- `PlatformResources.cs` in test project compiles WITHOUT `IS_CORE_MTP` — only limited resource string properties are available
- For multi-interface test doubles in Microsoft.Testing.Platform.UnitTests, define an `internal interface ICombined : IA, IB;` and mock that interface — Moq handles this cleanly
  - `Assert.IsGreaterThan(lowerBound, value)` asserts `value > lowerBound` — NOT the other way around. So to check `x > 0`, write `Assert.IsGreaterThan(0, x)`.
