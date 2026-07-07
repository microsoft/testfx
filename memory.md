# Efficiency Improver — Persistent Memory for microsoft/testfx

## Last Updated
2026-07-10 UTC

## Round-Robin Schedule

Tasks run this session: **2, 3, 6, 7**
Last run before this: Tasks 5, 3, 7 (2026-07-09)
Next run should prioritise: Tasks 1 (validate commands), 4 (PR maintenance), 5 (issue comments), 7 (always)

## Build / Test / Benchmark Commands

| Command | Purpose | Validated |
|---------|---------|-----------|
| `./build.sh` | Full restore + build (installs SDK to `.dotnet/` first) | ✅ |
| `./build.sh -test` | Run unit tests | ✅ |
| `./build.sh -pack` | Build + produce NuGet packages | ✅ |
| `./build.sh -pack -test -integrationTest` | Full pipeline incl. acceptance tests | ✅ |

Notes:
- Repo-local SDK at `.dotnet/dotnet` (Arcade-provisioned). Must run `./build.sh` first to install.
- Required SDK version: `11.0.100-preview.5.26302.115`
- `--no-restore` flag is broken; always run with full restore.
- Performance runner: `test/Performance/MSTest.Performance.Runner/`
  - Build: `.dotnet/dotnet build test/Performance/MSTest.Performance.Runner/MSTest.Performance.Runner.csproj -f net9.0 -c Debug`

## Efficiency Notes

- **Hot paths already optimised**: `TestMethodRunner`, `TestMethodInfo`, `ReflectionTestMethodInfo` — data-driven allocation paths covered by #9514 + #9617
- **MSTest.Performance.Runner**: Custom pipeline-based perf tool; `PlainProcess` (direct process), `DotnetTestProcess` (dotnet test server-mode). Both now have cached `JsonSerializerOptions`.
- **Only 1 scenario exists**: `Scenario1` (100×100 plain methods). No data-driven scenario — proposed in issue #aw_sc2issue (Task 6).
- **Report generators well-optimized**: CtrfReport uses custom `Utf8JsonWriter`-based streaming serialiser; HtmlReport is single-pass. No significant opportunities found.
- **OpenTelemetry `Properties.OfType()`** in yield iterator — LOW priority, not worth changing without profiling evidence.

## Open PRs

- **#aw_pr_ca1869** (created 2026-07-10): `efficiency/cache-json-serializer-options` — cache `JsonSerializerOptions` in `PlainProcess` and `DotnetTestProcess`; removed CA1869 pragmas. Draft PR.

## Monthly Summary Issue

- Issue #9594 — `[efficiency-improver] Monthly Activity 2026-07` — open, updated this run.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| LOW | Code-Level | OpenTelemetry: `Properties.OfType()` in `yield` — needs non-iterator helper | Not worth changing without profiling |
| LOW | Code-Level | `TerminalTestReporter.TotalTests`: `_assemblies.Values.Sum()` on every access | Negligible — called only for display |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |
| LOW | Infrastructure | Scenario2: data-driven benchmark — proposed in issue #aw_sc2issue | Needs maintainer input |

## Completed Work

| Date | PR/Issue | Summary |
|------|----------|---------|
| 2026-07-10 | #aw_pr_ca1869 | Cache JsonSerializerOptions in PlainProcess + DotnetTestProcess; remove CA1869 pragmas |
| 2026-07-10 | #aw_sc2issue | Issue: propose Scenario2 data-driven benchmark for perf runner |
| 2026-07-07 | #9617 (merged by Evangelink) | Data-driven allocation fixes (CloneForDataDrivenIteration dict, TCS bridge, ReflectionTestMethodInfo wrapper caching) |
| 2026-07-05 | #9614 (merged) | Cache `GetParameters()` in `TestMethodInfo.ParameterTypes` |
| 2026-06-30 | #9514 (merged) | Cache `MethodInfo.GetParameters()` in `TestMethodInfo.ParameterTypes` |

## Previously Checked-Off Items (by Maintainer)

*(None recorded yet — track here if maintainer checks items in Monthly Summary)*

## Backlog Cursor

- Code scan cursor: CtrfReport ✅, HtmlReport ✅, Adapter/ ✅, TestFramework/ ✅, Platform/ hot paths ✅
- Issue comments cursor: #8824 commented 2026-07-09
- Next code scan area: `src/Platform/Microsoft.Testing.Platform/` extensions (MSBuild, VSTestBridge, Telemetry)
