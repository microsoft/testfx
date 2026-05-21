# Perf Improver Memory - microsoft/testfx

## Build/Test Commands
- Build (Linux): `export PATH="$PWD/.dotnet:$PATH" && dotnet restore <project.csproj> && dotnet build <project.csproj> -f net8.0`
- Available runtimes: net8.0, net9.0, net10.0-preview (net8/9 targeting packs available after restore)
- Note: net8.0 and net9.0 targeting packs require `dotnet restore` before build
- Infrastructure failure: MSTest.TestAdapter build fails due to missing ApplicationInsights NuGet (pre-existing)
- Infrastructure failure: MSTestAdapter.PlatformServices.UnitTests build fails - AwesomeAssertions 9.3.0 only has netstandard2.1 lib, not net8.0 (pre-existing; fixed in 9.4.0 but not in local env)
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
- DotnetTrace step in perf runner is cross-platform (handles Linux exe name), but all pipelines restricted to Windows in Program.cs
- TypeEnumerator.GetTests() already fast-paths common case (no duplicate test methods) - GroupBy/OrderBy only runs for inherited test classes
- TestExecutionManager.cs GroupBy/Where - called once per source/assembly, not per-test - low priority
- IMPORTANT: TestMethodInfo.Execution.cs has ConfigureAwait(true) on RunTestInitializeMethodAsync and invokeResult - INTENTIONAL for WinUI synchronization context (UI thread requirement). DO NOT change to false.
- MSTest.Engine BFSTestNodeVisitor: PropertyBag.Any<TestNodeStateProperty>() already optimized (fast path, no allocation). BFS uses immutable string paths (already optimized per code comments).
- MSTest.Engine TestFixtureManager.RegisterFixtureUsage: uses LINQ Select to create FixtureId[], but this is one-time registration (not per-execution), low priority.
- MSTest.Engine: PropertyBag.Any<T>() has fast-path for TestNodeStateProperty, no LINQ, already well-optimized.
- MSTest.Engine, Platform/: scanned on 2026-05-15 - no new high-confidence hot-path targets found beyond backlog items.
- MSTest.SourceGeneration: scanned 2026-05-16 - ~20 files, mostly uses EquatableArray<T> and incremental generators (already well-designed for incremental compilation perf), no obvious hot-path allocation issues.

## Optimization Backlog
1. **[In main]** ValidSourceExtensions static cache + ReflectionTestMethodInfo deduplication
2. **[In main]** Eliminate LINQ iterator allocations in TryUnfoldITestDataSources
3. **[Merged PR #7927 - 2026-04-30]** GetTestCategories (6 iterators→0) + WorkItemAttribute double-pass + param string LINQ iterator - fixes issue #7868
4. **[Deprioritized - no profiler evidence]** Avoid yield iterator in TryExecuteDataSourceBasedTestsAsync + GetRetryAttribute (issue #7904 - branch perf-assist/avoid-yield-iterator-in-test-execution-hot-path can be discarded)
5. **[MERGED PR #8095 - 2026-05-13]** IsIgnored() LINQ allocation elimination. Issue #7992 still open (pending close by maintainer).
6. TreeNodeFilter MatchFilterPattern: LINQ closure allocations - covered by Efficiency Improver (#7947, #7974, #8035)
7. SynchronizedStringBuilder lock overhead - LOW PRIORITY, requires profiler evidence, may be intentionally thread-safe
8. Scanned Execution/, Discovery/, Helpers/ on 2026-05-06 - no new high-confidence targets beyond backlog items
9. Scanned Platform/ briefly on 2026-05-14 - LINQ usage found but in non-hot-path areas (CommandLine, ServerMode)
10. Scanned MSTest.Engine/ and Platform/ deeper on 2026-05-15 - no new high-confidence targets
11. Scanned MSTest.SourceGeneration on 2026-05-16 - no new high-confidence targets (incremental generator design already perf-conscious)

## Completed Work
- Branch: perf-assist/reduce-allocations-discovery-execution (changes applied to main by maintainer, issue #7815 still open - suggest closing)
- Branch: perf-assist/avoid-linq-iterators-data-source-enumeration (changes applied to main, issue for #7831)
- Branch: perf-assist/reduce-linq-iterators-get-test-categories-d392d71fd502f8cc → PR #7927 MERGED 2026-04-30 by Evangelink
- Branch: perf-assist/avoid-yield-iterator-in-test-execution-hot-path (issue #7904 - DEPRIORITIZED)
- IsIgnored patches: PR #8095 MERGED 2026-05-13 by Evangelink
  - Duplicate issue still open: #7992 (pending close by maintainer)

## Monthly Activity
- April 2026 issue #7816: CLOSED 2026-05-01
- May 2026 issue #7981: CLOSED 2026-05-13 by Evangelink as "not_planned"
- IMPORTANT: Do NOT create a new monthly activity issue - maintainer closed #7981 as not_planned on 2026-05-13

## Last Run
- 2026-05-21: Checked open issues/PRs - no new activity since 2026-05-20. Issue #7992 still pending maintainer close (PR #8095 merged). Issue #7904 deprioritized. Backlog exhausted. Noop.
- 2026-05-20: Checked open issues/PRs - no new activity since 2026-05-19. Backlog exhausted. Noop.
- 2026-05-19: Checked open issues/PRs - no new activity, backlog exhausted, noop.
- 2026-05-17: Checked open issues/PRs - nothing new. Backlog exhausted, waiting for maintainer feedback. Noop.
- 2026-05-16: Scanned MSTest.SourceGeneration - already well-designed for incremental compilation perf, no new targets. All backlog items exhausted. Noop.
- 2026-05-15: Task 2/6 (deeper scan MSTest.Engine + Platform - no new high-confidence targets); noop
- 2026-05-14: Task 2/5 (scanned Platform/ briefly - no new high-confidence targets), noop (monthly issue closed by maintainer)
- 2026-05-13: Task 4/2 (PR #8095 merged!), Task 7 (monthly summary updated - but issue subsequently closed by Evangelink)

## Round Robin Status
- 2026-05-13: Tasks 4, 2, 7 done
- 2026-05-14: Tasks 2, 5 attempted; noop
- 2026-05-15: Tasks 2, 6 attempted; noop (no new high-confidence targets)
- 2026-05-16: Task 2 (SourceGeneration scan); noop (backlog exhausted, waiting for profiler evidence from maintainers)
- 2026-05-17: Noop (nothing new, backlog exhausted, waiting for maintainer feedback or new issues)
- 2026-05-19: Noop (nothing new since last check)
- 2026-05-20: Noop (nothing new since 2026-05-19)
- 2026-05-21: Noop (nothing new since 2026-05-20)
- Next run: Continue waiting for profiler evidence or new performance issues. Consider scanning test/ directory for infrastructure gaps.

## IMPORTANT NOTES FOR FUTURE RUNS
- **DO NOT create more IsIgnored issues/PRs** - PR #8095 is MERGED.
- **DO NOT propose BDN infrastructure** - rejected by Evangelink in #7959 (closed as not_planned)
- **DO NOT create Monthly Activity tracking issues** - maintainer closed #7981 as not_planned 2026-05-13
- **DO NOT change ConfigureAwait(true) in TestMethodInfo.Execution.cs** - intentional for WinUI UI thread requirement
- If stuck, look at NEW opportunities in test execution/discovery code using profiler evidence
- The maintainers want profiler evidence before accepting allocation-optimization issues
- Consider looking at test/ directory perf test infrastructure next, or wait for new issues
