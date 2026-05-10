# Perf Improver Memory - microsoft/testfx

## Build/Test Commands
- Build (Linux): `export PATH="$PWD/.dotnet:$PATH" && dotnet restore <project.csproj> && dotnet build <project.csproj> -f net8.0`
- Available runtimes: net8.0, net9.0, net10.0-preview (net8/9 targeting packs available after restore)
- Note: net8.0 and net9.0 targeting packs require `dotnet restore` before build
- Infrastructure failure: MSTest.TestAdapter build fails due to missing ApplicationInsights NuGet (pre-existing)
- Infrastructure failure: MSTestAdapter.PlatformServices.UnitTests build fails - AwesomeAssertions 9.3.0 only has netstandard2.1 lib, not net8.0 (pre-existing)
- Working build target: `MSTestAdapter.PlatformServices.csproj` builds cleanly after restore
- Performance runner: `test/Performance/MSTest.Performance.Runner/` (all pipelines currently Windows-only)

## Naming Conventions
- Private static fields: `_camelCase`
- Private static readonly fields: `PascalCase` (SA1311 enforced)
- Not `s_` prefix for static readonly fields
- SA1312: local variables (including deconstruction) must start with lower-case
- SA1316: Tuple element names should use correct casing - AVOID named tuples; use separate variables/dictionaries instead to prevent this error
- IDE0028: use collection expression syntax (`[]`) instead of `new()` for collections - enforced by -warnaserror

## Performance Notes
- ReflectHelper uses ConcurrentDictionary to cache attribute lookups - already well-optimized
- TypeCache uses GetOrAdd with static lambda on NETCOREAPP to avoid captures
- GetCustomAttributesCached() returns the cached Attribute[] directly - good for avoiding LINQ iterator allocations
- AssemblyEnumerator/TypeEnumerator is the hot path for test discovery
- PERF comments in code indicate previously addressed perf issues
- GetTestPropertiesAsTraits: already uses allocation-free pattern (no LINQ iterators) - good reference
- IsAttributeDefined<T> / GetFirstAttributeOrDefault<T> / GetCustomAttributesCached() are allocation-free alternatives to GetAttributes<T>()
- GitHub Actions cannot push branches directly; create_pull_request tool creates a patch artifact but may fail
- IMPORTANT: Maintainers (Youssef1313, Evangelink) noted that hot path detection is not great - many things flagged that don't show in actual profiler traces. Require stronger evidence before submitting new allocations issues.
- SynchronizedStringBuilder in TestContextImplementation uses [MethodImpl(Synchronized)]; safe to skip - TestContext may receive output from background threads spawned by tests
- TypeValidator/TestMethodValidator created per-type in GetTypeEnumerator - minor impact (2 small objects per type), low priority
- TreeNodeFilter covered by Efficiency Improver (#7947, #7974, #8035) - do not duplicate work
- TestMethodInfo.GetAttributes<T>() wraps cached array with yield iterator + [..] spread - returns a filtered copy, allocation by design (prevents cache mutation), low priority
- GetRetryAttribute() in TestMethodInfo uses GetAttributes<RetryBaseAttribute> - called once per test method construction (not per execution), low priority
- BenchmarkDotNet external benchmark repo: https://github.com/Youssef1313/MSTestBench - shows impressive results (3.10→3.11: 90% reduction in time/alloc for SingleClass10KTests)
- IsIgnored fix approach (VERIFIED 0 warnings/errors): use two separate Dictionary<string,string?> and HashSet<string> instead of named tuple to avoid SA1316
- BDN infrastructure proposal (#7959): CLOSED as not_planned by Evangelink on 2026-04-30 - do not re-propose
- DotnetTrace step in perf runner is cross-platform (handles Linux exe name), but all pipelines restricted to Windows in Program.cs
- TypeEnumerator.GetTests() already fast-paths common case (no duplicate test methods) - GroupBy/OrderBy only runs for inherited test classes
- TestExecutionManager.cs GroupBy/Where - called once per source/assembly, not per-test - low priority

## Optimization Backlog
1. **[In main]** ValidSourceExtensions static cache + ReflectionTestMethodInfo deduplication
2. **[In main]** Eliminate LINQ iterator allocations in TryUnfoldITestDataSources
3. **[Merged PR #7927 - 2026-04-30]** GetTestCategories (6 iterators→0) + WorkItemAttribute double-pass + param string LINQ iterator - fixes issue #7868
4. **[Deprioritized - no profiler evidence]** Avoid yield iterator in TryExecuteDataSourceBasedTestsAsync + GetRetryAttribute (issue #7904 - branch perf-assist/avoid-yield-iterator-in-test-execution-hot-path can be discarded)
5. **[Patch in #7992 comment and #8075 - 2026-05-09]** IsIgnored() LINQ allocation elimination. Patch in #8075 (fallback issue) and #7992 latest comment. SA1316 fixed by using two dicts (unsatisfiedGroups+satisfiedGroups) instead of named tuple. Closes #7992, #7993, #8000, #8016, #8028, #8044, #8055, #8067, #8075.
6. TreeNodeFilter MatchFilterPattern: LINQ closure allocations - covered by Efficiency Improver (#7947, #7974, #8035)
7. SynchronizedStringBuilder lock overhead - LOW PRIORITY, requires profiler evidence, may be intentionally thread-safe
8. Scanned Execution/, Discovery/, Helpers/ on 2026-05-06 - no new high-confidence targets beyond backlog items

## Completed Work
- Branch: perf-assist/reduce-allocations-discovery-execution (changes applied to main by maintainer, issue #7815 still open - suggest closing)
- Branch: perf-assist/avoid-linq-iterators-data-source-enumeration (changes applied to main, issue for #7831)
- Branch: perf-assist/reduce-linq-iterators-get-test-categories-d392d71fd502f8cc → PR #7927 MERGED 2026-04-30 by Evangelink
- Branch: perf-assist/avoid-yield-iterator-in-test-execution-hot-path (issue #7904 - DEPRIORITIZED)
- IsIgnored patches: 9 attempts. Most recent: #7992 comment 2026-05-09, SA1316/SA1312 both fixed. Fallback issue: #8075.
  - Duplicate issues to close: #7993, #8000, #8016, #8028, #8044, #8055, #8067, #8075 (apply patch from any of these)
- Commented on #6326 (Track perf over time) - suggested allocation scenarios + BDN thresholds

## Monthly Activity
- April 2026 issue #7816: CLOSED 2026-05-01
- May 2026 issue #7981: OPEN

## Last Run
- 2026-05-10: Tasks 4 (no open PRs), 6 (assessed perf runner - all Windows-only; DotnetTrace step cross-platform-ready), 7 (monthly summary updated). NO new issues, no new PRs.
- 2026-05-09: Tasks 3 (IsIgnored 9th attempt, SA1316 fixed, patch in #7992 comment), 5 (posted patch to #7992), 7 (monthly summary updated)
- 2026-05-08: Tasks 3 (IsIgnored 8th attempt, SA1312 fixed, patch in run 25557658324), 7 (monthly summary updated)
- 2026-05-07: Tasks 3 (IsIgnored 7th attempt, SA1316 fixed, patch in run 25498381330), 7 (monthly summary updated)
- 2026-05-06: Tasks 3 (IsIgnored 6th attempt, patch in run 25437832278, NO new issue created), 2 (scanned - no new targets), 7 (monthly summary updated)
- 2026-05-05: Tasks 3 (IsIgnored re-impl, 5th attempt, issue #8028), 5 (commented #6326), 7 (monthly summary updated)
- 2026-05-04: Tasks 3 (IsIgnored re-impl again, patch in run 25321208683), 2 (no new high-confidence targets), 7 (monthly summary updated)

## Round Robin Status
- 2026-05-04: Tasks 3, 2, 7 done
- 2026-05-05: Tasks 3, 5, 7 done
- 2026-05-06: Tasks 3, 2, 7 done
- 2026-05-07: Tasks 3, 7 done
- 2026-05-08: Tasks 3, 7 done
- 2026-05-09: Tasks 3, 5, 7 done
- 2026-05-10: Tasks 4, 6, 7 done
- Next run: should focus on Tasks 1, 2, 3 (new optimization targets - stop repeating IsIgnored until maintainers apply patch)

## IMPORTANT NOTES FOR FUTURE RUNS
- **DO NOT create more IsIgnored issues/PRs** - 9 attempts already. Patch is in #8075 and #7992 latest comment. Wait for maintainers to apply it.
- **DO NOT propose BDN infrastructure** - rejected by Evangelink in #7959 (closed as not_planned)
- If stuck, look at NEW opportunities in test execution/discovery code using profiler evidence
- The maintainers want profiler evidence before accepting allocation-optimization issues
