# Efficiency Improver — Persistent Memory for microsoft/testfx

## Last Updated
2026-08-04 UTC

## Round-Robin Schedule

Tasks run this session: **1 (validate), 2 (scan), 5 (issue check), 7 (monthly summary)**
Last run before this: Tasks 2/7 (2026-08-03, run 30856871010) and 2/7 (2026-08-02, run 30769014001)
Next run should prioritise: Task 3 (implementation — backlog is empty, need fresh scan first), Task 6 (infra), 7 (always)

## Known Process Issue (IMPORTANT)

- **Duplicate monthly issues**: On 2026-08-01 through 2026-08-03, THREE separate `[efficiency-improver] Monthly Activity 2026-08` issues were created by different runs on the same days instead of updating the existing one: #10377 (closed as duplicate by maintainer), #10382 (the canonical one — kept updated), #10419 (duplicate, created 2026-08-03 22:05, needs manual closure by maintainer — flagged in Suggested Actions).
- **ALWAYS search for `is:issue is:open in:title "Monthly Activity"` with label `area/performance` BEFORE creating a new one.** Only create when none exists for the current month, or when the existing one is for a previous month (then close old, open new).
- **Malformed labels**: maintainer found 13 junk labels applied as literal bracket/string artifacts (e.g. `[efficiency]`, `[[efficiency]]`, `[area/performance`, `type/automation]`) from a labels *string* being passed instead of a list somewhere in tooling. Maintainer cleaned the 4 tracking issues; label definitions still need deletion (maintainer's call, not ours). If using labels via CLI/API in future, pass as a proper array, not a bracketed string.

## Build / Test / Benchmark Commands

| Command | Purpose | Validated |
|---------|---------|-----------|
| `./build.sh` | Full restore + build (installs SDK to `.dotnet/` first) | ✅ |
| `./build.sh -test` | Run unit tests | ✅ |
| `./build.sh -pack` | Build + produce NuGet packages | ✅ |
| `./build.sh -pack -test -integrationTest` | Full pipeline incl. acceptance tests | ✅ |

Notes:
- Repo-local SDK at `.dotnet/dotnet` (Arcade-provisioned). Must run `./build.sh` first to install.
- Required SDK version: `11.0.100-preview.7.26359.110` (not available in agent env)
- Performance runner: `test/Performance/MSTest.Performance.Runner/`

## Efficiency Notes

- **Hot paths already optimised**: `TestMethodRunner`, `TestMethodInfo`, `ReflectionTestMethodInfo` — data-driven allocation paths covered by #9514 + #9617
- **MSTest.Performance.Runner**: Has Scenario1 (plain methods) and Scenario2 (data-driven, added by #9728). JsonSerializerOptions now cached as static readonly.
- **TypeCache**: `ConcurrentDictionary.GetOrAdd` caches TestClassInfo per type name. Already well-optimized.
- **TelemetryCollector**: `Lazy<bool>` for opt-out check, `ConcurrentDictionary` for counts, `AggressiveInlining`. Already optimal.
- **Assert.That**: Compiles expression trees on every call (by design). Not cacheable without significant complexity.
- **Report generators well-optimized**: CtrfReport uses custom `Utf8JsonWriter`-based streaming serialiser; HtmlReport is single-pass. No significant opportunities found.
- **OpenTelemetry `Properties.OfType()`** in yield iterator — LOW priority, not worth changing without profiling evidence.
- **MSBuildCompatibilityHelper**: Already caches MSBuild version and feature-check results with `??=` pattern.
- **TrxReport**: Well-optimized — binary format for streaming store, XElement DOM only at report-generation time (not hot path).
- **bool.Parse in InvokeTestingPlatformTask**: Already cached as fields in RFC 018 commit (c66515a). No pending PR needed.
- **StackTraceHelper.TryFindLocationFromStackFrame (MSBuild)**: Already fixed in main — uses string.Split + for loop (no Regex.Split or LINQ).
- **Server mode TestNode serializer**: Uses LINQ Select().ToList() per test update — minor, dominated by network I/O, not worth changing.
- **TestCaseExtensions.GetTestName / GetClassNameWhenFullyQualifiedNameStartsWith**: Was allocating `$"{testClassName}."` on every call per test case. Now in main.
- **Maintainer commit #10141**: "Pool IPC serializer string buffers" — maintainers independently pooling buffers in IPC serializer (2026-07-22).
- **TreeNodeFilter**: Already well-optimized — struct enumerator for PropertyBag, compiled Regex per ValueExpression, no allocations in hot matching path.
- **AzureDevOpsReport (new, #10331)**: All new files reviewed — ConcurrentQueue for pending results, appropriate Dictionary/StringComparer patterns, `BuildMarkdown` uses `OrderByDescending` only at report-generation time (not per-test). No hot-path inefficiencies.
- **Assert.ContainsAll / AreAllDistinct**: ReadOnlySpan overloads call `.ToArray()` then pass to generic impl — unavoidable; no span-based impl for dictionary counting. Not a hot path.
- **TestMethodFilter.cs (new)**: No LINQ or Regex allocations. Already efficient.
- **OpenTelemetryResultHandler** (new #10358): Uses struct enumerator for single-pass property bag walk in `SetResultDetails`. `GetSuiteName` calls `SingleOrDefault<T>` separately (minor dup) — but this is OTel opt-in path, not worth optimizing.
- **MSTestTestNodeConverter** (new #10366): Uses `ConditionalWeakTable` to cache `ParsedManagedName` parsing per TestMethod — excellent. Maintainer independently implemented this caching.
- **TestResult.cs** (new #10353): `FindAssertionTexts` uses bounded recursive walk (MaxDepth=10), only called on failure path. Well-optimized.
- **AssertionFailureProperty.ToString()**: Uses StringBuilder for simple string — only called for debugging. No action needed.

## Open PRs / Issues Created by Efficiency Improver

- No open PRs from Efficiency Improver at this time.
- Previous work:
  - #9713 (Scenario2 proposal) — closed as completed by Evangelink, resolved by #9728
  - #9714 (JsonSerializerOptions caching) — closed as completed by Evangelink

## Monthly Summary Issue

- Issue #9594 — `[efficiency-improver] Monthly Activity 2026-07` — closed (month ended)
- New August issue: TBD (to be created this run)

## Issue Comments (Task 5)

- **#8824** — commented 2026-06-24 (energy analysis of LLM output proposals). No new human comments since.
- **#9712** — commented 2026-07-08 (energy impact of Azure.Identity dependency, energy-proportionality recommendation).

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| LOW | Code-Level | OpenTelemetry: `Properties.OfType()` in `yield` — needs non-iterator helper | Not worth changing without profiling |
| LOW | Code-Level | `TerminalTestReporter.TotalTests`: `_assemblies.Values.Sum()` on every access | Negligible — called only for display |
| LOW | Code-Level | `DynamicDataShouldBeValidAnalyzer`: `candidateMethods.Where().ToImmutableArray()` per `[DynamicData]` attribute | Marginal — triggered once per attribute per compilation |
| LOW | Code-Level | `TestExecutionManager` MethodLevel parallel: `Select(t => new[] { t })` — 1 array per test in setup path | One-time setup cost, ~80KB for 10K tests |
| LOW | Code-Level | `TestContextImplementation.SanitizeName`: `Array.IndexOf` over invalid chars per character | Only called when TestTempDirectory is first accessed |
| LOW | Infrastructure | CI output-byte-count health metric | Needs maintainer discussion |

## Completed Work

| Date | PR/Issue | Summary |
|------|----------|---------|
| 2026-08-01 | scan only | Scanned new OTel (#10358), MSTestTestNodeConverter caching (#10366), TestResult assertion texts (#10353) — all well-optimized by maintainers; no new HIGH/MEDIUM opportunities |
| 2026-07-31 | scan only | Scanned new OpenTelemetry spans (#10358), CtrfReportMerger partials (#10354), RetryOrchestrator — all well-optimized |
| 2026-07-29 | scan only | Scanned TreeNodeFilter, new AzureDevOps extension (#10331), TestMethodFilter, Assert.ContainsAll — all already well-optimized |
| 2026-07-27 | scan only | Scanned TestNodeResultsState, FileLogger, PropertyBag, MessageBus, DotnetTest HTTP transport, Analyzer hot paths — all already well-optimized |
| 2026-07-22 | scan only | Verified TestCaseExtensions fix in main; maintainer commit #10141 pools IPC string buffers independently |
| 2026-07-16 | branch pushed (landed in main) | Avoid string interpolation allocations in GetTestName/GetClassNameWhenFullyQualifiedNameStartsWith |
| 2026-07-10 | PR# TBD (branch efficiency/stacktrace-string-split — no longer needed, already in main) | StackTraceHelper already fixed in main |
| 2026-07-09 | bool.Parse now in main | Cache `bool.Parse` results already in RFC 018 commit; no separate PR needed |
| 2026-07-08 | #9712 comment | Energy impact of Azure.Identity dependency; recommended TokenCredential abstraction |
| 2026-07-10 | #9714 (closed) | Cache JsonSerializerOptions in PlainProcess + DotnetTestProcess; remove CA1869 pragmas |
| 2026-07-10 | #9713 (closed) | Issue: propose Scenario2 data-driven benchmark for perf runner — resolved by #9728 |
| 2026-07-07 | #9617 (merged) | Data-driven allocation fixes (CloneForDataDrivenIteration dict, TCS bridge, ReflectionTestMethodInfo wrapper caching) |
| 2026-07-05 | #9614 (merged) | Cache `GetParameters()` in `TestMethodInfo.ParameterTypes` |
| 2026-06-30 | #9514 (merged) | Cache `MethodInfo.GetParameters()` in `TestMethodInfo.ParameterTypes` |

## Previously Checked-Off Items (by Maintainer)

*(None recorded yet — track here if maintainer checks items in Monthly Summary)*

## Backlog Cursor

- Code scan cursor: All code through 2026-08-04 reviewed (commits up to ff9bca1). Backlog is EMPTY as of maintainer's 2026-08-03 consolidation comment on #10382 (fixed via #10397; rest won't-fix or needs-maintainer-decision).
- Issue comments cursor: #8824 ✅ (no new comments since 2026-07-14), #9712 ✅ — no new efficiency-labeled issues found as of 2026-08-04.
- Next code scan area: repo has been very active (dozens of commits/day from maintainer + Copilot coding agent) — re-scan diffs since ff9bca1 (2026-08-04) next run for new hot-path code.


## 2026-08-04 Run Notes

- Verified #10382 is the canonical August summary issue (has maintainer's consolidation comment). Updated it in place rather than creating a new issue — avoided repeating the duplicate-issue mistake from earlier runs.
- Reviewed recent commit history (2026-08-01 to 2026-08-04): all routine (dependency bumps, localization check-ins, coverage/CI infra, analyzer test coverage additions, AzDO reporter refinements). No new efficiency-relevant hot-path code changes spotted requiring action this run.
- Energy efficiency backlog is currently empty per maintainer disposition — next run should do a fresh Task 2 scan across newly merged features (AzureFoundry extension, JUnitReport, GitHubActionsReport) for any un-reviewed hot paths, and prioritise Task 3 (implementation) since backlog needs repopulating with concrete measurable items.
