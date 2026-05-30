# Perf Improver State

## Last Run
2026-05-30 14:08 UTC

## Commands Discovered
- Build: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- Build single project: `.dotnet/dotnet build <project.csproj> -c Debug -f net8.0`
- Run unit tests directly: `artifacts/bin/<Project>/Debug/net8.0/<Project>`
- Run platform unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`

## Tasks Last Run
- Task 1 (Discover Commands): 2026-05-29
- Task 2 (Identify Opportunities): 2026-05-29
- Task 3 (Implement Improvements): 2026-05-30
- Task 4 (Maintain PRs): 2026-05-30
- Task 7 (Monthly Summary): 2026-05-30

## Optimization Backlog
1. AnsiDetector regex → string ops (DONE - PR #8683 open, CI green)
2. AsynchronousMessageBus drain dict → cached arrays (DONE - PR submitted this run, branch: perf-assist/messagebus-drain-alloc-reduction)
3. `PublishAsync` Array.IndexOf is O(n) per publish — could cache HashSet<Type> per producer (low priority; typical arrays are small)
4. `CommandLineOptionsValidator`: several `new HashSet<string>()` allocations at startup with no capacity hints

## Completed Work
- PR #8683: perf: replace Regex array in AnsiDetector with direct string comparisons
  - Eliminates 17 Regex allocations on process start
  - All 992 unit tests pass, CI all green
- PR (perf-assist/messagebus-drain-alloc-reduction): cache distinct processors in AsynchronousMessageBus
  - Eliminates Dictionary allocation per DrainDataAsync call
  - Fixes duplicate processor visits in DisableAsync/Dispose
  - 992 unit tests pass

## Monthly Activity Issue
- Issue #8684 for 2026-05 (open)
