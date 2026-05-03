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
- IMPORTANT: Maintainers (Youssef1313, Evangelink) noted that hot path detection is not great - many things flagged that don't show in actual profiler traces. Require stronger evidence before submitting new allocations issues.
- SynchronizedStringBuilder in TestContextImplementation uses [MethodImpl(Synchronized)]; safe to skip - TestContext may receive output from background threads spawned by tests
- TypeValidator/TestMethodValidator created per-type in GetTypeEnumerator - minor impact (2 small objects per type), low priority
- TreeNodeFilter covered by Efficiency Improver (#7947, #7974) - do not duplicate work
- IsIgnored() in AttributeHelpers.cs is called 2x per test execution; optimization: use GetCustomAttributesCached() directly to eliminate GetAttributes<T> iterator + GroupBy LINQ operator

## Optimization Backlog
1. **[In main]** ValidSourceExtensions static cache + ReflectionTestMethodInfo deduplication
2. **[In main]** Eliminate LINQ iterator allocations in TryUnfoldITestDataSources
3. **[Merged PR #7927 - 2026-04-30]** GetTestCategories (6 iterators→0) + WorkItemAttribute double-pass + param string LINQ iterator - fixes issue #7868
4. **[Deprioritized - no profiler evidence]** Avoid yield iterator in TryExecuteDataSourceBasedTestsAsync + GetRetryAttribute (issue #7904 - branch perf-assist/avoid-yield-iterator-in-test-execution-hot-path can be discarded)
5. **[Patch ready 🔧]** IsIgnored() LINQ allocation elimination - patch in run 25280157015 artifact. Closes #7992, #7993
6. BenchmarkDotNet micro-benchmark project for discovery/execution hot paths - proposed infrastructure, no active issue
7. TreeNodeFilter MatchFilterPattern: LINQ closure allocations - covered by Efficiency Improver (#7947, #7974)
8. SynchronizedStringBuilder lock overhead - LOW PRIORITY, requires profiler evidence, may be intentionally thread-safe

## Completed Work
- Branch: perf-assist/reduce-allocations-discovery-execution (changes applied to main by maintainer, issue #7815 still open - suggest closing)
- Branch: perf-assist/avoid-linq-iterators-data-source-enumeration (changes applied to main, issue for #7831)
- Branch: perf-assist/reduce-linq-iterators-get-test-categories-d392d71fd502f8cc → PR #7927 MERGED 2026-04-30 by Evangelink
- Branch: perf-assist/avoid-yield-iterator-in-test-execution-hot-path (issue #7904 - DEPRIORITIZED)
- IsIgnored patch: CREATED 2026-05-03 in run 25280157015 artifact (previous branch deleted from remote)

## Monthly Activity
- April 2026 issue #7816: CLOSED 2026-05-01
- May 2026 issue #7981: OPEN

## Last Run
- 2026-05-03: Tasks 3/4 (re-implemented IsIgnored opt, previous remote branch was deleted; patch in run 25280157015 artifact), 7 (monthly summary updated)
- 2026-05-02: Tasks 3 (IsIgnored optimization, placeholder issues #7992, #7993), 7 (monthly summary updated)
- 2026-05-01: Tasks 4 (PR #7927 merged, no open PRs), 2 (explored - SynchronizedStringBuilder skipped, TreeNodeFilter covered by EI), 7 (closed April issue, created May issue)
- 2026-04-30: Tasks 2 (explored new opportunities), 6 (BenchmarkDotNet infra proposal), 7 (monthly summary)
- 2026-04-29: Tasks 4 (PR #7927 health), 5 (commented on #7904), 7 (monthly summary)

## Round Robin Status
- 2026-04-29: Tasks 4, 5, 7 done
- 2026-04-30: Tasks 2, 6, 7 done
- 2026-05-01: Tasks 4, 2, 7 done
- 2026-05-02: Tasks 3, 7 done
- 2026-05-03: Tasks 3/4, 7 done
- Next run: should focus on Tasks 2 (new opportunities), 5 (perf issues), 6 (infra)
