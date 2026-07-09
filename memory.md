# Efficiency Improver — Persistent Memory for microsoft/testfx

## Last Updated
2026-07-09 UTC

## Round-Robin Schedule

Tasks run this session: **2, 3, 7**
Last run before this: Tasks 5, 7 (2026-07-08)
Last run before that: Tasks 2, 3, 6, 7 (2026-07-10)
Next run should prioritise: Tasks 1 (validate), 4 (maintain PRs), 5 (issue comments), 7 (always)

## Build / Test / Benchmark Commands

| Command | Purpose | Validated |
|---------|---------|-----------|
| `./build.sh` | Full restore + build (installs SDK to `.dotnet/` first) | ✅ |
| `./build.sh -test` | Run unit tests | ✅ |
| `./build.sh -pack` | Build + produce NuGet packages | ✅ |
| `./build.sh -pack -test -integrationTest` | Full pipeline incl. acceptance tests | ✅ |

Notes:
- Repo-local SDK at `.dotnet/dotnet` (Arcade-provisioned). Must run `./build.sh` first to install.
- Required SDK version: `11.0.100-preview.7.26359.110`
- `--no-restore` flag is broken; always run with full restore.
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

## Open PRs / Issues Created by Efficiency Improver

- **branch `efficiency/cache-bool-parse-invoke-task`** pushed 2026-07-09 — Cache `bool.Parse` results in `InvokeTestingPlatformTask` (hot path: every output line). PR pending assignment of number.
- Previous work:
  - #9713 (Scenario2 proposal) — closed as completed by Evangelink, resolved by #9728
  - #9714 (JsonSerializerOptions caching) — closed as completed by Evangelink

## Monthly Summary Issue

- Issue #9594 — `[efficiency-improver] Monthly Activity 2026-07` — open

## Issue Comments (Task 5)

- **#8824** — commented 2026-06-24 (energy analysis of LLM output proposals). No new human comments since.
- **#9712** — commented 2026-07-08 (energy impact of Azure.Identity dependency, energy-proportionality recommendation).

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| LOW | Code-Level | OpenTelemetry: `Properties.OfType()` in `yield` — needs non-iterator helper | Not worth changing without profiling |
| LOW | Code-Level | `TerminalTestReporter.TotalTests`: `_assemblies.Values.Sum()` on every access | Negligible — called only for display |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |

## Completed Work

| Date | PR/Issue | Summary |
|------|----------|---------|
| 2026-07-09 | branch pushed (PR# TBD) | Cache `bool.Parse` results in `InvokeTestingPlatformTask`; eliminate repeated string scanning on every output line |
| 2026-07-08 | #9712 comment | Energy impact of Azure.Identity dependency; recommended TokenCredential abstraction |
| 2026-07-10 | #9714 (closed) | Cache JsonSerializerOptions in PlainProcess + DotnetTestProcess; remove CA1869 pragmas |
| 2026-07-10 | #9713 (closed) | Issue: propose Scenario2 data-driven benchmark for perf runner — resolved by #9728 |
| 2026-07-07 | #9617 (merged) | Data-driven allocation fixes (CloneForDataDrivenIteration dict, TCS bridge, ReflectionTestMethodInfo wrapper caching) |
| 2026-07-05 | #9614 (merged) | Cache `GetParameters()` in `TestMethodInfo.ParameterTypes` |
| 2026-06-30 | #9514 (merged) | Cache `MethodInfo.GetParameters()` in `TestMethodInfo.ParameterTypes` |

## Previously Checked-Off Items (by Maintainer)

*(None recorded yet — track here if maintainer checks items in Monthly Summary)*

## Backlog Cursor

- Code scan cursor: CtrfReport ✅, HtmlReport ✅, Adapter/ ✅, TestFramework/ ✅, Platform/ hot paths ✅, VSTestBridge ✅, AzureDevOps extensions ✅, MSBuild tasks ✅
- Issue comments cursor: #8824 ✅, #9712 ✅ — next: scan for new efficiency issues
- Next code scan area: `src/Platform/Microsoft.Testing.Extensions.TrxReport/` — TRX streaming serialiser
