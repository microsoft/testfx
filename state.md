# Perf Improver State — microsoft/testfx

## Last Updated
2026-06-10 14:52 UTC — Run [27283591745](https://github.com/microsoft/testfx/actions/runs/27283591745)

## Validated Commands
- Build: `./build.sh -build -c Debug` (Linux)
- Full build all TFMs: `./build.sh`
- Unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`
- Profiling: `test/Performance/MSTest.Performance.Runner` (dotnet-trace, PerfView)
- NOTE: system dotnet is a stub; build.sh installs SDK into `.dotnet/`

## Round-Robin Task History (last run)
- 2026-06-10: Task 3 (Implement), Task 7 (Monthly Summary)
- 2026-06-09: Task 3 (Implement), Task 7
- 2026-06-08: Task 3 (Implement), Task 4 (Maintain PRs), Task 6 (Infra), Task 7
- 2026-06-06: Task 3 (Implement), Task 7
- 2026-06-05: Task 3 (Implement), Task 6 (Infra), Task 7
- Next run: consider Task 2 (Identify Opportunities) or Task 5 (Comment on Issues)

## Monthly Activity Issue
- Issue #8933 — "[perf-improver] Monthly Activity 2026-06" (open)

## Work In Progress
- **Branch**: `perf-assist/cache-generate-lines-buffers`
- **PR**: created draft (number pending); eliminates 5 allocs/tick in `GenerateLinesToRender`
- **Status**: committed, PR submitted via safeoutputs, tests passing

## Completed Work (this month)
- PR #8970 (perf-assist/cache-ansi-cursor-sequences): cache ANSI cursor-positioning strings — **MERGED 2026-06-10 by Evangelink**
- PR #8932: `GetLongestRunningTask()` O(n) scan — **MERGED 2026-06-09**
- PR #8883: double-buffer frame reuse — **MERGED 2026-06-07**
- PR #8861: `HumanReadableDurationFormatter` fast path — **MERGED 2026-06-07**
- PR #8823: VSTestBridge single-pass message iteration — **MERGED 2026-06-05**
- PR #8799: LINQ allocations in terminal hot path — **MERGED**
- PR #8769: string allocations in ANSI render hot path — **MERGED**
- PR #8739: `AnsiTerminal.SetColor` + `KnownFileExtensions` HashSet — **MERGED**
- PR #8704: `AsynchronousMessageBus` distinct processors cache — **MERGED**
- PR #8683: `AnsiDetector` Regex → direct string comparison — **MERGED**

## Optimization Backlog
1. `GenerateLinesToRender` `List<object>` heterogeneous boxing → sealed base type; low priority
2. `TestNodeResultsState.GetRunningTasks()` still allocs new `List<TestDetailState>` per assembly per tick; cacheable similarly to current PR
3. `IConsole.Write(string)` forces `StringBuilder.ToString()` alloc in `StopUpdate`; needs interface change, low priority

## Performance Notes
- Render tick rate: ~2 fps (500ms `Thread.Sleep` in `TestProgressStateAwareTerminal.ThreadProc`)
- Double-buffer pattern: `AnsiTerminal._currentFrame` + `_spareFrame` swapped each tick
- `IComparer<T>` avoids closure alloc vs `Comparison<T>` lambda in `Array.Sort`
- `Array.Sort(array, 0, count, comparer)` sorts only the valid slice (avoids sorting uninitialized buffer tail)
- `Comparer<int>.Create(lambda)` creates new wrapper per call — use custom class instead
- Unit test baseline: 1107 passed, 3 skipped, 0 failed (net8.0)
- StyleCop: SA1214 = readonly fields before non-readonly; SA1401 = fields must be private
- Auto-properties preferred over manual backing fields (IDE0032)
