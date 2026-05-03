# TestFX Test Improver Memory

## Last Updated
2026-05-03

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
  - Baseline (after 2026-05-01 additions): ~659 tests per TFM (~1318 total)
  - `PlatformResources.LoggerFactoryNotReady` NOT accessible in test project (only in IS_CORE_MTP mode)
  - For multi-interface mocks (e.g. ILoggerProvider + IExtension), define internal interface combining them and mock that
  - **IMPORTANT**: LoggerFactory wraps providers into a composite logger — don't use `Assert.AreSame` to verify a provider was included; instead use `mockProvider.Verify(p => p.CreateLogger(name), Times.Once)` after calling `factory.CreateLogger(name)` with provider's `CreateLogger` mocked to return something

## Testing Backlog (prioritized)

1. ✅ **DONE** `UseConditionBaseWithTestClassAnalyzer` (MSTEST0041) → PR #7809 merged
2. ✅ **DONE** `RetryAttribute` unit tests → PR #7838 merged
3. ✅ **DONE** `TimeSpanParser` unit tests → PR #7858 merged
4. ✅ **DONE** `PasteArguments` unit tests → PR #7888 merged
5. ✅ **DONE** `LoggerFactoryProxy` unit tests → PR #7916 merged
6. 🔄 **IN PROGRESS** `LoggingManager.BuildAsync` tests → 9 tests written and verified (4th attempt), persistent push failure. Latest patch in comment on #7986 (run 25265145801). Patch is correct and all 18 tests pass.
7. `ExtensionValidationHelper.ValidateUniqueExtension` — null guards + duplicate detection + error message formatting, no tests yet
8. Code fix test coverage for MSTEST0031 when `DoNotUseSystemDescriptionAttributeFixer` lands
9. `TestFramework.UnitTests` assertion edge cases

## Completed Work

### 2026-05-03
- **Task 3: Re-attempted LoggingManager.BuildAsync PR (4th attempt)**: 9 tests written (added `BuildAsync_NonExtensionInitializableProvider_CallsInitializeAsync` vs previous 8). All 18 tests pass. Push failed again via `safeoutputs-create_pull_request`.
- **Task 7: Updated Monthly Summary issue #7969** with new run entry and updated suggested actions

### 2026-05-02
- **Task 3: Re-attempted LoggingManager.BuildAsync PR**: Fixed bug from 2026-05-01 attempt (Assert.AreSame → Verify for provider inclusion tests). All 8 tests pass. Push failed again.
- **Task 7: Updated Monthly Summary issue #7969** with new run entry and suggested actions for stale issues

### 2026-05-01
- **Task 3: Attempted PR for LoggingManager.BuildAsync tests**: 8 new tests
  - AddProvider(null) → ArgumentNullException
  - BuildAsync with no providers → empty LoggerFactory
  - Factory delegate receives correct LogLevel and IServiceProvider
  - Non-IExtension provider → always included
  - IExtension enabled → included; disabled → excluded
  - IAsyncInitializableExtension → InitializeAsync called
  - Disabled IAsyncInitializableExtension → InitializeAsync NOT called
  - Push failed, patch in issue #7968
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
| Task 3: Implement tests | 2026-05-03 |
| Task 4: Maintain PRs | 2026-05-02 |
| Task 5: Comment on issues | 2026-04-29 |
| Task 6: Test infrastructure | 2026-04-29 |
| Task 7: Monthly summary | 2026-05-03 |

## Maintainer Priorities
- PRs merged quickly by Evangelink — receptive to focused test PRs for MTP and MSTest
- Issues #7790, #7942, #7968 are stale (see monthly summary for suggested actions)
- **PUSH FAILURE**: The `safeoutputs-create_pull_request` tool consistently creates a patch file but fails to push branches. This has happened 4 times for LoggingManager. Alternative: try moving to a different backlog item (ExtensionValidationHelper) and see if push works.

## Notes
- `PasteArguments` is `internal static partial class` — accessible via InternalsVisibleTo
- `AwesomeAssertions` is banned in MSTest.Analyzers.UnitTests and Microsoft.Testing.Platform.UnitTests (see BannedSymbols.txt)
- **IMPORTANT**: When adding tests to `TestFramework.UnitTests`, always validate with `TreatWarningsAsErrors=true`
  since Linux CI never builds this project (not in NonWindowsTests.slnf) but Windows CI does
- `TestFramework.UnitTests` requires `-p:UseSharedCompilation=false` to build outside the full Arcade SDK build
- `PlatformResources.cs` in test project compiles WITHOUT `IS_CORE_MTP` — only limited resource string properties are available
- For multi-interface test doubles in Microsoft.Testing.Platform.UnitTests, define an `internal interface ICombined : IA, IB;` and mock that interface — Moq handles this cleanly
- LoggingManager tests: IMonitor mock needs `_mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object)` for LoggerFactory to work
- LoggingManager BuildAsync: to verify provider is INCLUDED, call `factory.CreateLogger("name")` and use `Verify(p.CreateLogger("name"), Times.Once)` — NOT `Assert.AreSame` since factory wraps providers
