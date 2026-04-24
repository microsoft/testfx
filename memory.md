# TestFX Test Improver Memory

## Last Updated
2026-04-24

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
dotnet test test/UnitTests/TestFramework.UnitTests/TestFramework.UnitTests.csproj -c Debug
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
- **Microsoft.Testing.Platform.UnitTests**: Uses MSTest

## Testing Backlog (prioritized)

1. ✅ **DONE** `UseConditionBaseWithTestClassAnalyzer` (MSTEST0041) - was the only analyzer without tests → PR created
2. Investigate `Microsoft.Testing.Platform.UnitTests` for gaps in Configuration and ServerMode tests
3. Explore `TestFramework.UnitTests` assertion tests for edge cases
4. Check if MSTest integration test suite covers `RetryAttribute` and `CICondition/OSCondition` scenarios

## Completed Work

### 2026-04-24
- **PR created**: `[Test Improver] Add tests for UseConditionBaseWithTestClassAnalyzer (MSTEST0041)`
  - 9 new tests covering no-diagnostic and diagnostic paths
  - All tests pass (9/9)

## Round-Robin Task Status

| Task | Last Run |
|------|----------|
| Task 1: Discover commands | 2026-04-24 |
| Task 2: Identify opportunities | 2026-04-24 |
| Task 3: Implement tests | 2026-04-24 |
| Task 4: Maintain PRs | - |
| Task 5: Comment on issues | - |
| Task 6: Test infrastructure | - |
| Task 7: Monthly summary | 2026-04-24 |

## Maintainer Priorities
None noted yet.

## Notes
- Analyzer rule fields are `internal` unless multi-rule analyzers need test access
- `ConditionBaseAttribute` is in `Microsoft.VisualStudio.TestTools.UnitTesting` namespace
- OSConditionAttribute constructor: `OSConditionAttribute(OperatingSystems)` or `OSConditionAttribute(ConditionMode, OperatingSystems)`
- CIConditionAttribute constructor: `CIConditionAttribute(ConditionMode)`
