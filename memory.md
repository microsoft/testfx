# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-06-24
- Task 3 (Implement Improvement): 2026-06-24
- Task 4 (Maintain PRs): 2026-06-23
- Task 5 (Comment on Issues): 2026-06-24
- Task 6 (Measurement Infrastructure): 2026-06-08
- Task 7 (Monthly Summary): 2026-06-24

## Backlog Cursor
- Last scanned: `src/Adapter/`, `src/TestFramework/`, `src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/IPC/Serializers/`
- Next scan area: Task 6 (Measurement Infrastructure) + `src/Analyzers/` + any remaining IPC serializers

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
- Last updated: 2026-06-24

## Work in Progress
- PR branch `efficiency/direct-array-alloc-ipc-serializer` (submitted 2026-06-24, #aw_pr_ipc)
  - Change: `TestResultMessagesSerializer.cs` — 3 intermediate List<T> + 3 [.. spread] copies → 3 pre-sized direct arrays
  - Proxy metric: managed allocation count + O(N) array-copy operations per result batch
  - CI pending

## Completed Work (PRs merged or applied)
- PR #9380: single-pass PropertyBag collection in DotnetTestDataConsumer — merged 2026-06-24
- PR #9353: single-pass aggregation in AppendTestRunSummary (TerminalTestReporter.Summary.cs) — merged
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
- IPC serializer series: DiscoveredTestMessagesSerializer already used direct-array pattern; TestResultMessagesSerializer now fixed too
- TestResultCaptureHelper.cs (shared core from #9330) already uses GetStructEnumerator — no changes needed
- perf-improver workflow (#9258) is a separate bot; recent PRs: #9299, #9311, #9348, #9376, #9399. Avoid duplicating.
- `TerminalTestReporter.TotalTests` property (line 68 of TerminalTestReporter.cs) calls `_assemblies.Values.Sum()` on every access, but is rarely called from outside — low priority.
- `GroupBy().Where(Count()>1)` in `ToolsTestHost.cs:55` runs only once at startup — negligible.
- Remaining IPC serializers (CommandLineOption, Handshake, TestSessionEvent, TestInProgress, FileArtifact) should be checked for List<T> + spread patterns next run.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Code-Level | Check remaining IPC serializers for List<T>+spread patterns (CommandLineOptionMessages, TestInProgressMessages, FileArtifactMessages, TestSessionEvent) | Low impact per call but consistent with the series |
| LOW | Code-Level | `TerminalTestReporter.cs:68` TotalTests prop calls `Sum()` on every access | Rare caller, negligible |
| LOW | Code-Level | `ToolsTestHost.cs:55` GroupBy at startup | Startup only, negligible |
| LOW | Infrastructure | Task 6: Add output-byte-count tracking as CI health metric (suggested in #8824 comment) | Needs maintainer discussion first |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — energy framing, ClassInit waste quantification, measurement suggestion, GSF Demand Shaping
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — energy-impact prioritisation table, stack-frame filtering as highest win
- Do not re-comment on these until new human activity appears

## Round-Robin Task Schedule
- Next run should prioritize: Task 6 (Measurement Infrastructure) + Task 4 (PR maintenance)
- Task 3: Check if #aw_pr_ipc (TestResultMessagesSerializer) is merged; if so, scan remaining IPC serializers
