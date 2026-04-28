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
- IsAttributeDefined<T> / GetFirstAttributeOrDefault<T> / GetCustomAttributesCached() are allocation-free alternatives to GetAttributes<T>()
- GitHub Actions cannot create PRs in this repo; changes are pushed to branches and placeholder issues are created

## Optimization Backlog
1. **[In main]** ValidSourceExtensions static cache + ReflectionTestMethodInfo deduplication (branch applied to main by maintainer)
2. **[In main]** Eliminate LINQ iterator allocations in TryUnfoldITestDataSources (branch applied to main)
3. **[Issue #7868, branch pending]** GetTestCategories (6 iterators→0) + WorkItemAttribute double-pass + param string LINQ iterator - branch: perf-assist/reduce-linq-iterators-get-test-categories-d392d71fd502f8cc
4. **[Submitted 2026-04-28]** Avoid yield iterator in TryExecuteDataSourceBasedTestsAsync + GetRetryAttribute - branch: perf-assist/avoid-yield-iterator-in-test-execution-hot-path
5. TestContextImplementation - SynchronizedStringBuilder uses [MethodImpl(Synchronized)]; TestContext is per-test so uncontended, but monitor lock overhead exists
6. TypeValidator/TestMethodValidator created per-type in GetTypeEnumerator - constrained by virtual method (testability), minor impact
7. TreeNodeFilter MatchFilterPattern: string[start..end] substring allocated per path segment per node - could use span but lambda captures prevent it without larger refactor

## Completed Work
- Branch: perf-assist/reduce-allocations-discovery-execution (changes applied to main by maintainer, issue #7815 still open)
  - ValidSourceExtensions cached as static readonly (TestSourceHandler.cs) - CONFIRMED in main
  - ReflectionTestMethodInfo dedup in ExecuteTestWithDataSourceAsync (TestMethodRunner.cs) - CONFIRMED in main (PERF comment at line 324)
- Branch: perf-assist/avoid-linq-iterators-data-source-enumeration (changes applied to main, issue for #7831)
  - Replace GetAttributes<Attribute>().OfType<ITestDataSource>() with direct iteration
- Branch: perf-assist/reduce-linq-iterators-get-test-categories-d392d71fd502f8cc (issue #7868, pending)
  - GetTestCategories: 6 LINQ iterators → 0 (common case)
  - WorkItemAttribute: double-pass OfType/Any/Select → single-pass
  - Param string: LINQ Select iterator → short-circuit for 0-param case
- Branch: perf-assist/avoid-yield-iterator-in-test-execution-hot-path (submitted 2026-04-28)
  - TryExecuteDataSourceBasedTestsAsync: GetAttributes<DataSourceAttribute>() → IsAttributeDefined<>()
  - GetRetryAttribute: yield iterator + IEnumerator<T> → direct cached array iteration
  - Build: 0W/0E net8.0

## Last Run
- 2026-04-28: Tasks 3 (new optimization: avoid yield iterators), 7 (monthly summary)
- 2026-04-27: Tasks 2 (verify merged PRs), 3 (new optimization), 7 (monthly summary)
- 2026-04-26: Tasks 4 (PR health check - both green), 5 (no perf issues), 7 (monthly summary)
- 2026-04-25: Tasks 3 (new optimization), 6 (infrastructure gap analysis), 7 (monthly summary)
- 2026-04-24: Tasks 1 (discovery), 2 (opportunities), 3 (implementation), 7 (monthly summary)

## Round Robin Status
- 2026-04-24: Tasks 1, 2, 3, 7 done
- 2026-04-25: Tasks 3, 6, 7 done
- 2026-04-26: Tasks 4, 5, 7 done
- 2026-04-27: Tasks 2, 3, 7 done
- 2026-04-28: Tasks 3, 7 done
- Next run: should focus on Tasks 4 (PR health), 5 (perf issues), 6 (infra), 7
