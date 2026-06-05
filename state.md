# Perf Improver State

## Last Run
2026-06-05 14:22 UTC

## Commands Discovered
- Build: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- Build single project: `.dotnet/dotnet build <project.csproj> -c Debug -f net8.0`
- Run unit tests directly: `artifacts/bin/<Project>/Debug/net8.0/<Project>`
- Run platform unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`
- dotnet binary: system dotnet is a stub; build.sh installs pinned SDK into `.dotnet/`

## Tasks Last Run
- Task 1 (Discover Commands): 2026-05-29
- Task 2 (Identify Opportunities): 2026-06-03
- Task 3 (Implement Improvements): 2026-06-05
- Task 4 (Maintain PRs): 2026-06-04
- Task 5 (Comment on Issues): 2026-06-05
- Task 7 (Monthly Summary): 2026-06-05

## Optimization Backlog
1. AnsiDetector regex → string ops (DONE - PR #8683 MERGED)
2. AsynchronousMessageBus drain dict → cached arrays (DONE - PR #8704 MERGED)
3. ServiceProvider.InternalOnlyExtensions array → HashSet (CLOSED without merge - PR #8717)
4. AnsiTerminal.SetColor string alloc + KnownFileExtensions array → HashSet (DONE - PR #8739 MERGED)
5. ANSI render hot path: CsiEraseInLine/CsiEraseInDisplay constants + MoveCursorUp StringBuilder (DONE - PR #8769 MERGED)
6. GetRunningTasks + GenerateLinesToRender LINQ allocations (DONE - PR #8799 MERGED)
7. VSTestBridge ObjectModelConverters double message iteration (DONE - PR #8823 MERGED ✅)
8. HumanReadableDurationFormatter.Render fast path for sub-hour durations (SUBMITTED - perf-assist/duration-formatter-fast-path)
9. AnsiTerminalTestProgressFrame double-buffering — moderate complexity
10. GenerateLinesToRender List<object> boxing — higher complexity

## Completed Work
- PR #8683: perf: replace Regex array in AnsiDetector with direct string comparisons — MERGED ✅
- PR #8704: cache distinct processors in AsynchronousMessageBus — MERGED ✅
- PR #8717: convert InternalOnlyExtensions from array property to static readonly HashSet — CLOSED (not merged)
- PR #8739: perf: eliminate per-call string allocation in AnsiTerminal.SetColor and use HashSet for KnownFileExtensions — MERGED ✅
- PR #8769: perf: eliminate string allocations in ANSI render hot path — MERGED ✅
- PR #8799: perf: eliminate LINQ allocations in terminal progress render hot path — MERGED ✅
- PR #8823: perf: single-pass message iteration in VSTestBridge ObjectModelConverters — MERGED ✅ 2026-06-05
- PR (perf-assist/duration-formatter-fast-path): fast path in HumanReadableDurationFormatter.Render; string.Create+stackalloc for sub-hour durations (3–4 allocs → 1)

## Monthly Activity Issue
- Issue #8740 for June 2026 (open)

## Notes
- All previously merged PRs were in the OutputDevice/Terminal path
- PR #8717 was closed without explanation — avoid similar HashSet-on-internal-collection patterns
- Full repo build has pre-existing XLF infrastructure failure (unrelated to our changes)
- net8.0 baseline: 1086 passed, 0 failed, 3 skipped (platform unit tests after this run's changes)
