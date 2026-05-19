# TestFX Test Improver Memory

## Last Updated
2026-05-19

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
  - `PlatformResources.LoggerFactoryNotReady` NOT accessible in test project (only in IS_CORE_MTP mode)
  - For multi-interface mocks (e.g. ILoggerProvider + IExtension), define internal interface combining them and mock that
  - **IMPORTANT**: LoggerFactory wraps providers into a composite Logger — Logger.IsEnabled uses `logLevel >= _logLevel` directly, does NOT delegate to mock logger. Test IsEnabled on the factory-created logger directly.
  - Use `Assert.Contains(string, string)` NOT `StringAssert.Contains` (MSTEST0046 analyzer rule)
  - SA1512: No blank line after single-line comments in test files
  - `PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage` is NOT available in test project — use `Assert.Contains(uid, ex.Message)` instead
  - `ExtensionValidationHelper` in `Microsoft.Testing.Platform.Helpers` namespace — accessible via InternalsVisibleTo in test project
  - `TestExtension` in `Helpers/TestExtension.cs` accepts optional `uid` constructor parameter (default "Uid") for configurable UIDs in tests
  - SA1516: Internal interface declarations at file scope need blank lines between them
  - MSTEST0037: Use `Assert.IsEmpty`/`Assert.HasCount` instead of `Assert.AreEqual(0/N, ...)` for collections
  - MSTEST0017: `Assert.AreEqual/AreNotEqual` — expected comes FIRST, actual second

## Testing Backlog (prioritized)

1. ✅ **DONE** `UseConditionBaseWithTestClassAnalyzer` (MSTEST0041) → PR #7809 merged
2. ✅ **DONE** `RetryAttribute` unit tests → PR #7838 merged
3. ✅ **DONE** `TimeSpanParser` unit tests → PR #7858 merged
4. ✅ **DONE** `PasteArguments` unit tests → PR #7888 merged
5. ✅ **DONE** `LoggerFactoryProxy` unit tests → PR #7916 merged
6. ✅ **DONE** `LoggingManager.BuildAsync` tests → merged via #8124 and #8130
7. ✅ **DONE** `ExtensionValidationHelper.ValidateUniqueExtension` → merged via #8128
8. ✅ **DONE** MSTEST0031 code fix tests → confirmed merged via #7898 (2026-05-12)
9. ✅ **DONE** Quality improvements to `LoggingManagerTests.cs` → merged via #8129 (2026-05-14)
10. ✅ **DONE** `StopPoliciesService` unit tests → patch in 2026-05-14 run
11. ✅ **DONE** `CommandLineParseResult` unit tests → PR #8249 merged 2026-05-15
12. ✅ **DONE** `ResponseFileHelper.SplitCommandLine` — tests already in repo
13. ✅ **DONE** `CommandLineOption` validation & equality — tests already in repo
14. 🔄 `ResponseFileHelper.TryReadResponseFile` — patch created 2026-05-19 (6 new tests, bundle: aw-test-assist-response-file-helper-tests)
15. Code fix test coverage for MSTEST0040 when `AvoidUsingAssertsInAsyncVoidContextFixer` lands (#7891)
16. `TestFramework.UnitTests` assertion edge cases

## Completed Work

### 2026-05-19
- **Task 3**: Added 6 unit tests for `ResponseFileHelper.TryReadResponseFile` (FileNotFound, valid file, comments, blank lines, quoted args, empty file). 44 total ResponseFileHelper tests pass. PR push fell back to patch artifact (bundle: aw-test-assist-response-file-helper-tests).
- **Task 7**: Created new Monthly Summary issue for May 2026.

### 2026-05-18
- **Task 3**: Implemented 26 unit tests for `CommandLineOption`. PR push fell back to patch artifact.
- **Task 7**: Updated Monthly Summary issue #8301.

### 2026-05-17
- **Task 3**: Implemented 14 unit tests for `ResponseFileHelper.SplitCommandLine`. PR push fell back to patch artifact.
- **Task 7**: Created new Monthly Summary issue for May 2026.

### 2026-05-15
- **Task 3**: Created 20 unit tests for `CommandLineParseResult` → PR #8249 merged same day.
- **Task 7**: Created new Monthly Summary issue for May 2026.

### 2026-05-14
- **Task 3**: Implemented 10 unit tests for `StopPoliciesService`. PR push fell back to patch artifact.
- **Task 7**: Created new Monthly Summary issue #8206 (closed by Evangelink as "completed").

### 2026-05-13
- **Task 3**: Created quality improvements PR for `LoggingManagerTests.cs`. Closes #8140.
- **Task 7**: Updated Monthly Summary issue #7969.

### 2026-05-12
- **Task 4**: PRs #8124, #8128, #8130 merged. Commented on duplicate PRs.
- **Task 7**: Updated Monthly Summary issue #7969.

## Round-Robin Task Status

| Task | Last Run |
|------|----------|
| Task 1: Discover commands | 2026-04-24 |
| Task 2: Identify opportunities | 2026-05-15 |
| Task 3: Implement tests | 2026-05-19 |
| Task 4: Maintain PRs | 2026-05-12 |
| Task 5: Comment on issues | 2026-05-08 |
| Task 6: Test infrastructure | 2026-04-29 |
| Task 7: Monthly summary | 2026-05-19 |

## Maintainer Priorities
- PRs merged quickly by Evangelink — receptive to focused test PRs for MTP and MSTest
- Evangelink closed monthly issues #7969, #8206, #8301 — will keep creating new ones each run
- **PR CREATION**: `safeoutputs-create_pull_request` consistently falls back to patch artifact. Patch files are at `/tmp/gh-aw/aw-test-assist-*.patch`.

## Notes
- `PasteArguments` is `internal static partial class` — accessible via InternalsVisibleTo
- `AwesomeAssertions` is banned in MSTest.Analyzers.UnitTests and Microsoft.Testing.Platform.UnitTests (see BannedSymbols.txt)
- **IMPORTANT**: When adding tests to `TestFramework.UnitTests`, always validate with `TreatWarningsAsErrors=true`
  since Linux CI never builds this project (not in NonWindowsTests.slnf) but Windows CI does
- `TestFramework.UnitTests` requires `-p:UseSharedCompilation=false` to build outside the full Arcade SDK build
- `PlatformResources.cs` in test project compiles WITHOUT `IS_CORE_MTP` — only limited resource string properties are available
- For multi-interface test doubles in Microsoft.Testing.Platform.UnitTests, define an `internal interface ICombined : IA, IB;` and mock that interface — Moq handles this cleanly
- `Assert.IsGreaterThan(lowerBound, value)` asserts `value > lowerBound` — NOT the other way around. So to check `x > 0`, write `Assert.IsGreaterThan(0, x)`.
- SA1512: No blank line after single-line comments — section dividers like `// ---- Section ----` must immediately be followed by `[TestMethod]` without a blank line
- SA1516: Internal interface declarations at file scope need blank lines between them
- MSTEST0037: Use `Assert.IsEmpty`/`Assert.HasCount` for collection length checks; `Assert.IsNotNull` for null-not-equal checks
- MSTEST0017: In `Assert.AreEqual(expected, actual)` — expected is FIRST parameter
- `CommandLineParseResult.IsOptionSet` is case-insensitive and strips leading dashes from the option name before comparing
- `CommandLineOption` constructor throws `ArgumentNullException` for null name/description (via `Ensure.NotNullOrWhiteSpace`), `ArgumentException` for empty/whitespace or invalid chars
