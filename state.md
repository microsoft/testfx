# Perf Improver State

## Last Run
2026-06-01 15:37 UTC

## Commands Discovered
- Build: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- Build single project: `.dotnet/dotnet build <project.csproj> -c Debug -f net8.0`
- Run unit tests directly: `artifacts/bin/<Project>/Debug/net8.0/<Project>`
- Run platform unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`
- dotnet binary: `/usr/share/dotnet/dotnet` (system); build.sh installs pinned SDK into `.dotnet/`

## Tasks Last Run
- Task 1 (Discover Commands): 2026-05-29
- Task 2 (Identify Opportunities): 2026-06-01
- Task 3 (Implement Improvements): 2026-06-01
- Task 4 (Maintain PRs): 2026-05-31
- Task 7 (Monthly Summary): 2026-06-01

## Optimization Backlog
1. AnsiDetector regex → string ops (DONE - PR #8683 MERGED)
2. AsynchronousMessageBus drain dict → cached arrays (DONE - PR #8704 MERGED)
3. ServiceProvider.InternalOnlyExtensions array → HashSet (CLOSED without merge - PR #8717)
4. AnsiTerminal.SetColor string alloc + KnownFileExtensions array → HashSet (PR submitted 2026-06-01)
5. `GenerateLinesToRender` LINQ allocation per frame — low priority; small assemblies make this negligible
6. `AnsiTerminalTestProgressFrame` new object per 500ms render cycle — could use double-buffering; moderate complexity

## Completed Work
- PR #8683: perf: replace Regex array in AnsiDetector with direct string comparisons — MERGED ✅
- PR #8704: cache distinct processors in AsynchronousMessageBus — MERGED ✅
- PR #8717: convert InternalOnlyExtensions from array property to static readonly HashSet — CLOSED (not merged, reason unknown)
- PR (perf-assist/ansi-terminal-alloc-reduction): AnsiTerminal.SetColor cached strings + KnownFileExtensions HashSet

## Monthly Activity Issue
- Issue #8684 for 2026-05 (closed this run — new month)
- New June 2026 issue: (to be created this run)
