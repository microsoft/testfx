# Perf Improver Memory - microsoft/testfx

## Build/Test Commands
- Build (Linux): `export PATH="$PWD/.dotnet:$PATH" && dotnet restore <project.csproj> && dotnet build <project.csproj> -f net8.0`
- Available runtimes: net8.0, net9.0, net10.0-preview (net8/9 targeting packs available after restore)
- Note: net8.0 and net9.0 targeting packs require `dotnet restore` before build
- Infrastructure failure: MSTest.TestAdapter build fails due to missing ApplicationInsights NuGet (pre-existing)
- Infrastructure failure: MSTestAdapter.PlatformServices.UnitTests build fails - AwesomeAssertions 9.3.0 only has netstandard2.1 lib, not net8.0 (pre-existing)
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
- GetTestPropertiesAsTraits: already uses allocation-free pattern (no LINQ iterators) - good reference
- GetTestCategories: optimized in 2026-04-27 run to use same pattern as GetTestPropertiesAsTraits

## Optimization Backlog
1. **[MERGED - #7834]** ValidSourceExtensions static cache + ReflectionTestMethodInfo deduplication
2. **[MERGED - #7834]** Eliminate LINQ iterator allocations in TryUnfoldITestDataSources
3. **[SUBMITTED]** GetTestCategories + WorkItemAttribute double-pass + param string LINQ iterator (perf-assist/reduce-linq-iterators-get-test-categories branch)
4. TestContextImplementation - SynchronizedStringBuilder uses [MethodImpl(Synchronized)]; consider lock-free if contention proven
5. TypeValidator/TestMethodValidator created per-type in GetTypeEnumerator - constrained by virtual method (testability), minor impact
6. Analyzer hot paths - LINQ usage in RegisterSymbolStartAction handlers (only 3 usages found in Helpers/, low priority)

## Infrastructure Gaps
- Performance runner (test/Performance) Windows-only profiling pipelines
- PlainProcess + DotnetTrace steps could run cross-platform
- No BenchmarkDotNet micro-benchmarks for hot paths
- No CI-based perf regression detection

## Completed Work
- Branch: perf-assist/reduce-allocations-discovery-execution (merged as #7834 on 2026-04-27)
  - ValidSourceExtensions cached as static readonly (TestSourceHandler.cs)
  - ReflectionTestMethodInfo dedup in ExecuteTestWithDataSourceAsync (TestMethodRunner.cs)
- Branch: perf-assist/avoid-linq-iterators-data-source-enumeration (merged as #7834 on 2026-04-27)
  - Replace GetAttributes<Attribute>().OfType<ITestDataSource>() with GetCustomAttributesCached() direct iteration
  - Eliminates 2 iterator state machine allocations per data-driven test during discovery
- Branch: perf-assist/reduce-linq-iterators-get-test-categories (submitted 2026-04-27)
  - GetTestCategories: 6 LINQ iterators → 0 (common case)
  - WorkItemAttribute: double-pass OfType/Any/Select → single-pass
  - Param string: LINQ Select iterator → short-circuit for 0-param case
  - Build: 0W/0E net8.0

## Last Run
- 2026-04-27: Tasks 2 (verify merged PRs), 3 (new optimization), 7 (monthly summary)
- 2026-04-26: Tasks 4 (PR health check - both green), 5 (no perf issues), 7 (monthly summary)
- 2026-04-25: Tasks 3 (new optimization), 6 (infrastructure gap analysis), 7 (monthly summary)
- 2026-04-24: Tasks 1 (discovery), 2 (opportunities), 3 (implementation), 7 (monthly summary)

## Round Robin Status
- 2026-04-24: Tasks 1, 2, 3, 7 done
- 2026-04-25: Tasks 3, 6, 7 done
- 2026-04-26: Tasks 4, 5, 7 done
- 2026-04-27: Tasks 2, 3, 7 done
- Next run: should focus on Tasks 4 (PR health check), 5 (perf issues), 6 (infra), and 7
