# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-06-26
- Task 3 (Implement Improvement): 2026-06-26
- Task 4 (Maintain PRs): 2026-06-26
- Task 5 (Comment on Issues): 2026-06-24
- Task 6 (Measurement Infrastructure): 2026-06-08
- Task 7 (Monthly Summary): 2026-06-26

## Backlog Cursor
- IPC serializer series: COMPLETE (all 6 serializers now use direct-array allocation)
- Analyzers scan: DONE — found and fixed DerivesFrom() LINQ iterator allocations
- Next scan area: Task 6 (Measurement Infrastructure) + broader Platform scan for remaining OfType<T> on PropertyBag

## Validated Commands

| Command | Purpose |
|---------|---------|
| `./build.sh` | Full restore + build |
| `./build.sh -test` | Run unit tests |
| `./build.sh -pack` | Build + produce NuGets |
| `./build.sh -pack -test -integrationTest` | Full pipeline incl. acceptance tests |

Notes:
- SDK: `.dotnet/dotnet` (Arcade-provisioned). `/usr/bin/dotnet` is non-functional for SDK commands.
- `--no-restore` flag is broken; always use full restore.
- MSTestAdapter internal tests not run by MTP runner; handled by CI separately.
- Build environment for this agent: no `.dotnet/` bootstrapped; PRs validated via GitHub Actions CI.

## Monthly Activity Issue
- Issue #9197: `[efficiency-improver] Monthly Activity 2026-06` (open)
- Label: `efficiency`
- Last updated: 2026-06-26

## Work in Progress
None (IPC #9436 merged 2026-06-26)

## Completed Work (PRs merged or applied)
- PR #9436: direct-allocate arrays in IPC CommandLineOption and FileArtifact deserializers — merged 2026-06-26
- PR #9408: direct-allocate arrays in IPC TestResultMessagesSerializer — merged 2026-06-25
- PR #9380: single-pass PropertyBag collection in DotnetTestDataConsumer — merged 2026-06-24
- PR #9353: single-pass aggregation in AppendTestRunSummary — merged
- CommandLineParseResult.IsOptionSet + TryGetOptionArgumentList: hoisted Trim(), single-pass foreach (changes in main already)
- PR #9300: replace `artifactGroups.Any()` with `_artifacts.Count > 0` in `AppendTestRunSummary`
- PR #9274: single-pass GroupBy partition in `TestExecutionManager.Parallelization.cs`
- PR #9196: defer `GetTestName()` + avoid `OfType<>` in `AzureDevOpsReporter`
- PR #9162: single-pass PropertyBag walk in `DiscoveredTestsJsonSerializer`
- PR #9159: single-pass PropertyBag walk in `TerminalOutputDevice` + `SimplifiedConsoleOutputDeviceBase`
- PR #9018: single-pass PropertyBag walk in `JUnitReport TestResultCapture`
- Earlier PRs #8692–#8975: UTF-8 encoding, IPC serialization, TRX/OTel/TrxReport/AzureDevOps/SerializerUtilities PropertyBag walks, AnsiTerminal string caching (all merged)

## Efficiency Notes (Key Insights)
- PropertyBag hot-path series COMPLETE: all OfType<T>() → GetStructEnumerator() conversions done across all report extensions
- IPC serializer series: COMPLETE — all 6 serializers now use direct T[length] allocation pattern
- Analyzers: DerivesFrom() fix (this run) — ImmutableArray<INamedTypeSymbol>.OfType<ITypeSymbol>() + optional Select() + Contains() replaced by direct foreach. Called ~36 times per symbol analysis.
- perf-improver workflow (#9258) is a separate bot; recent PRs: #9299, #9311, #9348, #9376, #9399, #9433. Avoid duplicating.
- VideoRecorderSessionHandler.cs: still uses OfType<TestNodeStateProperty>().FirstOrDefault() — could use SingleOrDefault<TestNodeStateProperty>(). Low priority (rarely-used extension).
- OpenTelemetryResultHandler.cs:126: OfType<TestMetadataProperty>() in iterator method — cannot use GetStructEnumerator() in yield methods. Low priority.
- `TerminalTestReporter.TotalTests` property calls `_assemblies.Values.Sum()` on every access, but is rarely called — low priority.
- `GroupBy().Where(Count()>1)` in `ToolsTestHost.cs:55` runs only once at startup — negligible.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Infrastructure | Task 6: Assess benchmark coverage for IPC/reporter paths — do benchmarks exist for these serializers? | No local SDK; propose via issue |
| LOW | Code-Level | VideoRecorder: `Properties.OfType<TestNodeStateProperty>().FirstOrDefault()` → `Properties.SingleOrDefault<TestNodeStateProperty>()` | Rarely-used extension, low impact |
| LOW | Code-Level | OpenTelemetry: `Properties.OfType<TestMetadataProperty>()` in iterator method | Cannot use struct enumerator in yield; would need refactor |
| LOW | Code-Level | `TerminalTestReporter.cs:68` TotalTests prop calls `Sum()` on every access | Rare caller, negligible |
| LOW | Code-Level | `ToolsTestHost.cs:55` GroupBy at startup | Startup only, negligible |
| LOW | Infrastructure | Add output-byte-count tracking as CI health metric (suggested in #8824 comment) | Needs maintainer discussion first |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — energy framing, ClassInit waste quantification, measurement suggestion, GSF Demand Shaping
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — energy-impact prioritisation table, stack-frame filtering as highest win
- Do not re-comment on these until new human activity appears

## Round-Robin Task Schedule
- Tasks 2+3 done this run (DerivesFrom fix)
- Next run should prioritize: Task 6 (Measurement Infrastructure) + Task 5 (check for new activity)
- Backlog cursor: infrastructure gaps (Task 6 is overdue — last run 2026-06-08)
