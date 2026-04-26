# Perf Improver Memory - microsoft/testfx

## Build/Test Commands
- Build (Linux): `export PATH="$PWD/.dotnet:$PATH" && dotnet restore <project.csproj> && dotnet build <project.csproj> -f net8.0`
- Available runtimes: net8.0, net9.0, net10.0-preview (net8/9 targeting packs available after restore)
- Note: net8.0 and net9.0 targeting packs require `dotnet restore` before build
- Infrastructure failure: MSTest.TestAdapter build fails due to missing ApplicationInsights NuGet (pre-existing)
- Working build target: `MSTestAdapter.PlatformServices.csproj` builds cleanly after restore

## Naming Conventions
- Private static fields: `_camelCase`
- Private static readonly fields: `PascalCase` (SA1311 enforced)
- Not `s_` prefix for static readonly fields

## Performance Notes
- ReflectHelper uses ConcurrentDictionary to cache attribute lookups - already well-optimized
- TypeCache uses GetOrAdd with static lambda on NETCOREAPP to avoid captures
- GetCustomAttributesCached() returns the cached Attribute[] directly - good for avoiding LINQ iterator allocations
- AssemblyEnumerator/TypeEnumerator is the hot path for test discovery
- PERF comments in code indicate previously addressed perf issues
- TypeValidator + TestMethodValidator are created once per type during discovery; lifting to per-assembly is constrained by virtual GetTypeEnumerator (overridden in tests) - not worth doing without signature change
- GetTestContextProperties / Properties: already a single Dictionary per TestContext instance (not per-access) - was a false backlog item
- Issue #2999: team has noted static check before attribute check in TypeCache.IsAssemblyOrClassInitializeMethod - assigned to nohwnd

## Optimization Backlog
1. **[SUBMITTED - #7815/#7834]** ValidSourceExtensions static cache + ReflectionTestMethodInfo deduplication
2. **[SUBMITTED - #7831/#7834]** Eliminate LINQ iterator allocations in TryUnfoldITestDataSources
3. Analyzer hot paths - investigate LINQ usage in RegisterSymbolStartAction handlers (only 3 usages found in Helpers/, low priority)
4. TestContextImplementation - SynchronizedStringBuilder uses [MethodImpl(Synchronized)]; consider lock-free if contention proven
5. TypeValidator/TestMethodValidator created per-type in GetTypeEnumerator - constrained by virtual method (testability), minor impact
6. DuplicateTestMethodAttribute check in TypeEnumerator - builds Dictionary<string,int> on duplicate detection path (already deferred, OK)

## Infrastructure Gaps
- Performance runner (test/Performance) Windows-only profiling pipelines
- PlainProcess + DotnetTrace steps could run cross-platform
- No BenchmarkDotNet micro-benchmarks for hot paths
- No CI-based perf regression detection

## Completed Work
- Branch: perf-assist/reduce-allocations-discovery-execution (from 2026-04-24)
  - ValidSourceExtensions cached as static readonly (TestSourceHandler.cs)
  - ReflectionTestMethodInfo dedup in ExecuteTestWithDataSourceAsync (TestMethodRunner.cs)
  - Issues: #7815, PR: #7834 (CI passing)
- Branch: perf-assist/avoid-linq-iterators-data-source-enumeration (from 2026-04-25)
  - Replace GetAttributes<Attribute>().OfType<ITestDataSource>() with GetCustomAttributesCached() direct iteration
  - Eliminates 2 iterator state machine allocations per data-driven test during discovery
  - Issues: #7831, PR: #7834 (CI passing)

## Last Run
- 2026-04-26: Tasks 4 (PR health check - both green), 5 (no perf issues to comment on), 7 (monthly summary)
- 2026-04-25: Tasks 3 (new optimization), 6 (infrastructure gap analysis), 7 (monthly summary)
- 2026-04-24: Tasks 1 (discovery), 2 (opportunities), 3 (implementation), 7 (monthly summary)

## Round Robin Status
- 2026-04-24: Tasks 1, 2, 3, 7 done
- 2026-04-25: Tasks 3, 6, 7 done
- 2026-04-26: Tasks 4, 5, 7 done
- Next run: should focus on Tasks 2 (new opportunities), 3 (new implementation), and 7
