# Perf Improver State

## Last Run
2026-05-31 14:10 UTC

## Commands Discovered
- Build: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- Build single project: `.dotnet/dotnet build <project.csproj> -c Debug -f net8.0`
- Run unit tests directly: `artifacts/bin/<Project>/Debug/net8.0/<Project>`
- Run platform unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`

## Tasks Last Run
- Task 1 (Discover Commands): 2026-05-29
- Task 2 (Identify Opportunities): 2026-05-29
- Task 3 (Implement Improvements): 2026-05-31
- Task 4 (Maintain PRs): 2026-05-31
- Task 7 (Monthly Summary): 2026-05-31

## Optimization Backlog
1. AnsiDetector regex → string ops (DONE - PR #8683 MERGED)
2. AsynchronousMessageBus drain dict → cached arrays (PR #8704 open, CI all green)
3. ServiceProvider.InternalOnlyExtensions array property → static readonly HashSet (DONE - PR submitted this run)
4. `PublishAsync` Array.IndexOf is O(n) per publish — could cache HashSet<Type> per producer (low priority; typical arrays are small)
5. `CommandLineOptionsValidator`: already uses HashSets appropriately — no further optimization needed

## Completed Work
- PR #8683: perf: replace Regex array in AnsiDetector with direct string comparisons — MERGED ✅
- PR #8704: cache distinct processors in AsynchronousMessageBus (CI green, awaiting review)
- PR (perf-assist/service-provider-internal-extensions-hashset): convert InternalOnlyExtensions from array property to static readonly HashSet
  - Eliminates per-call Type[] allocation (5 elements) on every public GetService call
  - Changes O(n) Contains to O(1) HashSet lookup
  - 992/995 unit tests pass (3 skipped by design)

## Monthly Activity Issue
- Issue #8684 for 2026-05 (open)
