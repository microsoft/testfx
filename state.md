# Perf Improver State

## Last Run
2026-05-29 14:29 UTC

## Commands Discovered
- Build: `./build.sh` (Linux) / `.\build.cmd` (Windows)
- Build single project: `.dotnet/dotnet build <project.csproj> -c Debug -f net8.0`
- Run unit tests directly: `artifacts/bin/<Project>/Debug/net8.0/<Project>`
- Run platform unit tests: `artifacts/bin/Microsoft.Testing.Platform.UnitTests/Debug/net8.0/Microsoft.Testing.Platform.UnitTests`

## Tasks Last Run
- Task 1 (Discover Commands): 2026-05-29
- Task 2 (Identify Opportunities): 2026-05-29
- Task 3 (Implement Improvements): 2026-05-29
- Task 7 (Monthly Summary): 2026-05-29

## Optimization Backlog
1. AnsiDetector regex → string ops (DONE - PR pending)
2. `DrainDataAsync` allocs `Dictionary<IAsyncConsumerDataProcessor, long>` each call — can be cached as a field initialized in `InitAsync`
3. `AsynchronousMessageBus.PublishAsync`: `Array.IndexOf` is O(n) per publish — could use a HashSet for O(1) lookup
4. `CommandLineOptionsValidator`: several `new HashSet<string>()` allocations at startup
5. `TerminalTestReporter.Formatting.ControlCharacters.cs`: `new List<char>()` could benefit from estimated capacity

## Completed Work
- PR: perf: replace Regex array in AnsiDetector with direct string comparisons (branch: perf-assist/ansi-detector-no-regex)
  - Eliminates 17 Regex allocations on process start
  - All 992 unit tests pass

## Monthly Activity Issue
- Created 2026-05-29 for 2026-05
