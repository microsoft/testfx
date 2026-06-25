# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-06-25
- Task 3 (Implement Improvement): 2026-06-25
- Task 4 (Maintain PRs): 2026-06-25
- Task 5 (Comment on Issues): 2026-06-24
- Task 6 (Measurement Infrastructure): 2026-06-08
- Task 7 (Monthly Summary): 2026-06-25

## Backlog Cursor
- IPC serializer series: COMPLETE (all 6 serializers now use direct-array allocation)
- Next scan area: Task 6 (Measurement Infrastructure) + src/Analyzers/ + broader codebase scan

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
- Last updated: 2026-06-25

## Work in Progress
- PR #aw_pr_ipc2 (submitted 2026-06-25, branch efficiency/direct-array-alloc-remaining-ipc-serializers)
  - Change: CommandLineOptionMessagesSerializer + FileArtifactMessagesSerializer
  - Pattern: List<T> + [.. spread] → read length first, new T[length], populate directly
  - Completes IPC serializer series
  - CI pending

## Completed Work (PRs merged or applied)
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
  - DiscoveredTestMessages: already correct (reference pattern)
  - TestResultMessages: fixed in #9408 (merged)
  - TestInProgress: was already correct before this series started
  - TestSessionEvent: no list fields (scalar only)
  - CommandLineOptionMessages: fixed in #aw_pr_ipc2 (this run)
  - FileArtifactMessages: fixed in #aw_pr_ipc2 (this run)
  - HandshakeMessage: uses Dictionary, different pattern, not applicable
- perf-improver workflow (#9258) is a separate bot; recent PRs: #9299, #9311, #9348, #9376, #9399. Avoid duplicating.
- `TerminalTestReporter.TotalTests` property calls `_assemblies.Values.Sum()` on every access, but is rarely called — low priority.
- `GroupBy().Where(Count()>1)` in `ToolsTestHost.cs:55` runs only once at startup — negligible.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Code-Level | Scan src/Analyzers/ for hot-path inefficiencies (OfType<T>, LINQ in hot paths) | Not yet scanned |
| MEDIUM | Infrastructure | Task 6: Assess benchmark coverage for IPC/reporter paths — do benchmarks exist for these serializers? | No local SDK; propose via issue |
| LOW | Code-Level | `TerminalTestReporter.cs:68` TotalTests prop calls `Sum()` on every access | Rare caller, negligible |
| LOW | Code-Level | `ToolsTestHost.cs:55` GroupBy at startup | Startup only, negligible |
| LOW | Infrastructure | Add output-byte-count tracking as CI health metric (suggested in #8824 comment) | Needs maintainer discussion first |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — energy framing, ClassInit waste quantification, measurement suggestion, GSF Demand Shaping
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — energy-impact prioritisation table, stack-frame filtering as highest win
- Do not re-comment on these until new human activity appears

## Round-Robin Task Schedule
- Next run should prioritize: Task 6 (Measurement Infrastructure) + Task 5 (Issue comments, check for new activity on #8894 and #8824) + Task 2 (scan Analyzers)
- IPC series is complete; backlog cursor moves to Analyzers + infrastructure
