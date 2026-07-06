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
```

## Work In Progress

None — last PR submitted on current run.

## Completed Work

| Date | Change | Status |
|---|---|---|
| 2026-06-29 | Cache ParameterInfo[] in TestMethodInfo.ParameterTypes (efficiency-improver PR #9514) | Merged 2026-06-30 |
| 2026-07-04 | Issue #9602: skip intermediate Dict alloc in CloneForDataDrivenIteration | Open (branch available) |
| 2026-07-04 | Issue #9603: skip TCS bridge in ExecuteTestAsync when capturedContext==null | Merged via PR #9636 (2026-07-06) |
| 2026-07-05 | PR #9617 by Evangelink: all 4 optimizations grouped — dirty (needs rebase after #9636) | Open |
| 2026-07-06 | PR submitted on branch perf-assist/cache-invoke-params: pass cached ParameterTypes to GetInvokeResultAsync | Pending review |

## Optimization Backlog (prioritized)

1. **[PR open, needs rebase]** PR #9617: skip Dict alloc in CloneForDataDrivenIteration + 3 others
2. **[PR submitted]** Pass cached ParameterTypes to GetInvokeResultAsync — eliminates GetParameters() alloc per test invocation (perf-assist/cache-invoke-params)
3. AntiTerminal.StopUpdate(): StringBuilder.ToString() on every flush — blocked on IConsole abstraction
4. SilenceDrivenHeartbeatRenderer: allocs only on rare heartbeat paths — low priority
5. ClassifyOutcome: Array.IndexOf fallback — very low priority

## Perf Notes

- MethodInfo.GetParameters() always allocates a fresh ParameterInfo[] (CLR safety). Cache wherever used in loops or hot paths.
- TestMethodInfo.ParameterTypes uses `field ??=` (C# 14) lazy init — valid because MethodInfo is get-only.
- ITestDataSource test execution: hot path is TestMethodRunner → ExecuteTestAsync → ExecuteInternalAsync → GetInvokeResultAsync. Each method called N times (N = data rows).
- Lifecycle methods (AssemblyInit/Cleanup, ClassInit/Cleanup, TestInit/Cleanup): called 1-2 times per class/assembly. Not worth micro-optimizing.

## Monthly Activity Issue

Issue #9604: [perf-improver] Monthly Activity 2026-07 (OPEN)

## Task Schedule (last run dates)

| Task | Last Run |
|---|---|
| Task 1: Discover commands | 2026-06-28 |
| Task 2: Identify opportunities | 2026-07-06 |
| Task 3: Implement improvements | 2026-07-06 |
| Task 4: Maintain PRs | 2026-07-06 |
| Task 5: Comment on issues | 2026-06-28 |
| Task 6: Perf infra | 2026-06-28 |
| Task 7: Monthly summary | 2026-07-06 |

## Next Run Priority

Tasks 5 and 6 (oldest) — comment on perf issues, check perf measurement infrastructure.
