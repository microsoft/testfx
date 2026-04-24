# Perf Improver Memory - microsoft/testfx

## Build/Test Commands
- Build (Linux): `export PATH="$PWD/.dotnet:$PATH" && dotnet build <project.csproj> -f net8.0`
- Available runtimes: net8.0, net9.0, net10.0-preview (net8/9 targeting packs available)
- Note: net8.0 and net9.0 targeting packs ARE available; only net462 (NETFRAMEWORK) is missing
- Infrastructure failure: MSTest.TestAdapter build fails due to missing ApplicationInsights NuGet (pre-existing)
- Working build target: `MSTestAdapter.PlatformServices.csproj` builds cleanly

## Naming Conventions
- Private static fields: `_camelCase`
- Private static readonly fields: `PascalCase` (SA1311 enforced)
- Not `s_` prefix for static readonly fields

## Performance Notes
- ReflectHelper uses ConcurrentDictionary to cache attribute lookups - already well-optimized
- TypeCache uses GetOrAdd with static lambda on NETCOREAPP to avoid captures
- TestNodeProperties.ToString() uses StringBuilder - these are debug/diagnostics paths, not hot
- AssemblyEnumerator/TypeEnumerator is the hot path for test discovery
- PERF comments in code indicate previously addressed perf issues

## Optimization Backlog
1. **[DONE - PR created]** ValidSourceExtensions static cache + ReflectionTestMethodInfo deduplication
2. Analyzer hot paths - investigate LINQ usage in RegisterSymbolStartAction handlers
3. TestContextImplementation - SynchronizedStringBuilder uses lock-based synchronization; consider Channel or similar
4. GetTestContextProperties - creates new Dictionary per test, mostly unavoidable but maybe pool-able
5. DuplicateTestMethodAttribute check in TypeEnumerator - builds Dictionary<string,int> on duplicate detection path

## Completed Work
- PR: perf-assist/reduce-allocations-discovery-execution
  - ValidSourceExtensions cached as static readonly (TestSourceHandler.cs)
  - ReflectionTestMethodInfo dedup in ExecuteTestWithDataSourceAsync (TestMethodRunner.cs)

## Last Run
- 2026-04-24: Tasks 1 (discovery), 2 (opportunities), 3 (implementation), 7 (monthly summary)
