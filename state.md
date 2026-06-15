# Perf Improver State — microsoft/testfx

## Last Updated
2026-06-15 15:33 UTC — Run [27557150790](https://github.com/microsoft/testfx/actions/runs/27557150790)

## Validated Commands
- Build: `./build.sh -build -c Debug` (Linux)
- Full build all TFMs: `./build.sh`
- Unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`
- Profiling: `test/Performance/MSTest.Performance.Runner` (dotnet-trace, PerfView)
- NOTE: system dotnet is a stub; build.sh installs SDK into `.dotnet/`

## Round-Robin Task History (last run)
- 2026-06-15: Task 2 (Identify Opportunities), Task 3 (Implement), Task 7 (Monthly Summary)
- 2026-06-10: Task 3 (Implement), Task 7 (Monthly Summary)
- 2026-06-09: Task 3 (Implement), Task 7
- 2026-06-08: Task 3 (Implement), Task 4 (Maintain PRs), Task 6 (Infra), Task 7
- 2026-06-06: Task 3 (Implement), Task 7
- 2026-06-05: Task 3 (Implement), Task 6 (Infra), Task 7
- Next run: consider Task 4 (Maintain PRs), Task 5 (Comment on Issues)

## Monthly Activity Issue
- Issue #8933 closed by Evangelink 2026-06-14 (June issue)
- New June issue created this run — number pending

## Work In Progress
- **Branch**: `perf-assist/single-pass-propertybag-terminal`
- **PR**: submitted via safeoutputs (number pending)
- **Status**: committed; build 0 warnings/errors; 1160 tests passed
- **Description**: single-pass PropertyBag walk in TerminalOutputDevice (5→1 traversals + 1 LINQ alloc eliminated) and SimplifiedConsoleOutputDeviceBase (2→1 traversals)

## Completed Work (this month)
- PR #9108 (perf-assist/single-pass-propertybag-azdo): single-pass PropertyBag walk in AzureDevOps extension — **MERGED 2026-06-14 by Evangelink**
- PR #9084 (perf-assist/pool-rendered-progress-items): pool RenderedProgressItem instances — **MERGED 2026-06-14 by Evangelink**
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
1. `TestNodeResultsState.GetRunningTasks()` still allocs new `List<TestDetailState>` per assembly per tick; verify still optimal
2. `IConsole.Write(string)` forces `StringBuilder.ToString()` alloc in `StopUpdate`; needs interface change, low priority
3. `TerminalTestReporter` — look for remaining allocs in hot path after recent merges

## Performance Notes
- Render tick rate: ~2 fps (500ms `Thread.Sleep` in `TestProgressStateAwareTerminal.ThreadProc`)
- Double-buffer pattern: `AnsiTerminal._currentFrame` + `_spareFrame` swapped each tick
- `PropertyBag` uses singly-linked list; each `SingleOrDefault<T>()` / `OfType<T>()` is O(n) + possible LINQ heap alloc
- `GetStructEnumerator()` is a zero-allocation struct enumerator on `PropertyBag`; use for all multi-property reads
- `IComparer<T>` avoids closure alloc vs `Comparison<T>` lambda in `Array.Sort`
- Unit test baseline: 1160 passed, 3 skipped, 0 failed (net8.0)
- StyleCop: SA1214 = readonly fields before non-readonly; SA1401 = fields must be private
