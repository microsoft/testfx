# Perf Improver State — microsoft/testfx

## Build/Test Commands

```sh
./build.sh              # restore + build (Debug)
./build.sh -c Release   # release build
./build.sh -test        # unit tests (net8.0 + net9.0)
./build.sh -pack        # produce NuGet packages
./build.sh -pack -test -integrationTest  # full acceptance suite
# Run single test project (after build):
.dotnet/dotnet run --project <proj> -f net9.0 --no-build -- --treenode-filter "*/*/Class/Method"
# Run perf timing (after -pack):
.dotnet/dotnet run --project test/Performance/MSTest.Performance.Runner -c Release -- execute --pipelineNameFilter "*PlainProcess*"
```

## Work In Progress

None.

## Completed Work

| Date | Change | Status |
|---|---|---|
| 2026-06-29 | Cache ParameterInfo[] in TestMethodInfo.ParameterTypes (PR #9514) | Merged 2026-06-30 |
| 2026-07-05 | PR #9617: skip Dict alloc + TCS bridge + cache ReflectionTestMethodInfo + ParameterTypes — all 4 data-driven hot-path optimizations | Merged 2026-07-07 |
| 2026-07-07 | PR perf-assist/scenario2-data-driven: add Scenario2 (data-driven) to perf runner + cache JsonSerializerOptions in PlainProcess | Draft PR submitted |

## Optimization Backlog (prioritized)

1. **[Draft PR]** Add Scenario2 data-driven perf scenario (branch `perf-assist/scenario2-data-driven`) — measurement infrastructure for the hot path in #9617
2. `AntiTerminal.StopUpdate()` — `_stringBuilder.ToString()` on every flush; blocked on `IConsole`/netstandard2.0. Low priority.
3. `SilenceDrivenHeartbeatRenderer` — allocs only on rare heartbeat paths. Low priority.
4. `ClassifyOutcome` in `TestResultCaptureHelper.cs` — `Array.IndexOf` fallback. Very low priority.

## Perf Notes

- MethodInfo.GetParameters() always allocates a fresh ParameterInfo[] (CLR safety). Cache wherever used in loops or hot paths.
- TestMethodInfo.ParameterTypes uses `field ??=` (C# 14) lazy init — valid because MethodInfo is get-only.
- ITestDataSource test execution hot path: TestMethodRunner → ExecuteTestAsync → ExecuteInternalAsync → GetInvokeResultAsync. Each called N times (N = data rows).
- Perf runner Scenario1: 100 classes × 100 methods × 1 row = 10,000 plain test executions.
- Perf runner Scenario2 (new): 100 classes × 10 methods × 10 DataRow rows = 10,000 data-driven executions.
- nightly perf CI: `perf-timing-nightly.yml` runs `*PlainProcess*` filter automatically.

## Monthly Activity Issue

Issue #9604: [perf-improver] Monthly Activity 2026-07 (OPEN)

## Task Schedule (last run dates)

| Task | Last Run |
|---|---|
| Task 1: Discover commands | 2026-06-28 |
| Task 2: Identify opportunities | 2026-07-06 |
| Task 3: Implement improvements | 2026-07-06 |
| Task 4: Maintain PRs | 2026-07-07 |
| Task 5: Comment on issues | 2026-06-28 |
| Task 6: Perf infra | 2026-07-07 |
| Task 7: Monthly summary | 2026-07-07 |

## Next Run Priority

Tasks 2 and 5 (oldest) — identify new opportunities, comment on perf issues.
