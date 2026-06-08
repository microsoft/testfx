# Perf Improver State

## Last Run
2026-06-08 14:47 UTC

## Commands Discovered
- Build: `./build.sh -build -c Debug` (Linux) / `.\build.cmd -build -c Debug` (Windows)
- Full build (all TFMs): `./build.sh`
- Run platform unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`
- Profiling infrastructure: `test/Performance/MSTest.Performance.Runner` — uses dotnet-trace, PerfView, VSDiagnostics, ConcurrencyVisualizer
- Note: system dotnet is a stub; build.sh installs pinned SDK into `.dotnet/`

## Tasks Last Run
- Task 1 (Discover Commands): 2026-05-29
- Task 2 (Identify Opportunities): 2026-06-03
- Task 3 (Implement Improvements): 2026-06-08
- Task 4 (Maintain PRs): 2026-06-08
- Task 5 (Comment on Issues): 2026-06-05
- Task 6 (Measurement Infrastructure): 2026-06-08
- Task 7 (Monthly Summary): 2026-06-08

## Optimization Backlog
1. AnsiDetector regex → string ops (DONE - PR #8683 MERGED)
2. AsynchronousMessageBus drain dict → cached arrays (DONE - PR #8704 MERGED)
3. ServiceProvider.InternalOnlyExtensions array → HashSet (CLOSED without merge - PR #8717)
4. AnsiTerminal.SetColor string alloc + KnownFileExtensions array → HashSet (DONE - PR #8739 MERGED)
5. ANSI render hot path: CsiEraseInLine/CsiEraseInDisplay constants + MoveCursorUp StringBuilder (DONE - PR #8769 MERGED)
6. GetRunningTasks + GenerateLinesToRender LINQ allocations (DONE - PR #8799 MERGED)
7. VSTestBridge ObjectModelConverters double message iteration (DONE - PR #8823 MERGED)
8. HumanReadableDurationFormatter.Render fast path for sub-hour durations (DONE - PR #8861 MERGED 2026-06-07)
9. AnsiTerminalTestProgressFrame double-buffering (DONE - PR #8883 MERGED 2026-06-07)
10. SimpleTerminal.GetRunningTasks(1).FirstOrDefault() → GetLongestRunningTask() linear scan (SUBMITTED - branch perf-assist/longest-running-task-linear-scan)
11. GenerateLinesToRender per-frame array allocs — pool/reuse new TestProgressState[N], int[N], List<TestDetailState>[N] (high complexity, skip for now)
12. GenerateLinesToRender List<object> heterogeneous boxing — consider sealed base type (lower priority)

## Completed Work
- PR #8683: perf: replace Regex array in AnsiDetector with direct string comparisons — MERGED
- PR #8704: cache distinct processors in AsynchronousMessageBus — MERGED
- PR #8717: convert InternalOnlyExtensions from array to HashSet — CLOSED (not merged)
- PR #8739: perf: eliminate per-call string allocation in AnsiTerminal.SetColor and use HashSet for KnownFileExtensions — MERGED
- PR #8769: perf: eliminate string allocations in ANSI render hot path — MERGED
- PR #8799: perf: eliminate LINQ allocations in terminal progress render hot path — MERGED
- PR #8823: perf: single-pass message iteration in VSTestBridge ObjectModelConverters — MERGED 2026-06-05
- PR #8861: fast path in HumanReadableDurationFormatter.Render — MERGED 2026-06-07
- PR #8883: reuse AnsiTerminalTestProgressFrame via double-buffer swap — MERGED 2026-06-07
- PR perf-assist/longest-running-task-linear-scan: GetLongestRunningTask() O(n) scan — SUBMITTED 2026-06-08

## Monthly Activity Issue
- Issue #8740 for June 2026 was CLOSED by Evangelink on 2026-06-08
- New June 2026 issue created this run (number TBD)

## Notes
- All previously merged PRs were in the OutputDevice/Terminal path
- PR #8717 was closed without explanation — avoid similar HashSet-on-internal-collection patterns
- Full repo build has pre-existing XLF infrastructure failure (unrelated to our changes)
- net8.0 baseline after 2026-06-08 changes: 1103 passed, 0 failed, 3 skipped (platform unit tests; +17 vs previous)
- Measurement infrastructure exists: test/Performance/MSTest.Performance.Runner with dotnet-trace/PerfView; no BenchmarkDotNet microbenchmarks
