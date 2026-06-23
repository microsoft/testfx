# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-06-23
- Task 3 (Implement Improvement): 2026-06-23
- Task 4 (Maintain PRs): 2026-06-23
- Task 5 (Comment on Issues): 2026-06-10
- Task 6 (Measurement Infrastructure): 2026-06-08
- Task 7 (Monthly Summary): 2026-06-23

## Backlog Cursor
- Last scanned: `src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.Summary.cs`
- Next scan area: `src/Adapter/` and `src/TestFramework/` for any remaining inefficiencies

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
- Last updated: 2026-06-22

## Work in Progress
- PR branch `efficiency/single-pass-dotnet-test-consumer` (submitted 2026-06-23, #aw_pr_dtc)
  - Change: `DotnetTestDataConsumer.cs` — extended GetStructEnumerator walk to collect FileArtifactProperty and TestMetadataProperty, removing 2 extra OfType<T>() passes
  - Proxy metric: PropertyBag linked-list iteration count (2–3× → 1× per test update)

## Completed Work (PRs merged or applied)
- PR #9353: single-pass aggregation in AppendTestRunSummary (TerminalTestReporter.Summary.cs) — merged
- CommandLineParseResult.IsOptionSet + TryGetOptionArgumentList: hoisted Trim(), single-pass foreach
  - Branch `efficiency/fix-parseoption-hot-loops` was never pushed but changes ARE in main already
- PR #9300: replace `artifactGroups.Any()` with `_artifacts.Count > 0` in `AppendTestRunSummary`
- PR #9274: single-pass GroupBy partition in `TestExecutionManager.Parallelization.cs`
- PR #9196: defer `GetTestName()` + avoid `OfType<>` in `AzureDevOpsReporter`
- PR #9162: single-pass PropertyBag walk in `DiscoveredTestsJsonSerializer`
- PR #9159: single-pass PropertyBag walk in `TerminalOutputDevice` + `SimplifiedConsoleOutputDeviceBase`
- PR #9018: single-pass PropertyBag walk in `JUnitReport TestResultCapture`
- Earlier PRs #8692–#8975: UTF-8 encoding, IPC serialization, TRX/OTel/TrxReport/AzureDevOps/SerializerUtilities PropertyBag walks, AnsiTerminal string caching (all merged)

## Efficiency Notes (Key Insights)
- PropertyBag hot-path series COMPLETE: all OfType<T>() → GetStructEnumerator() conversions done across all report extensions
- TestResultCaptureHelper.cs (shared core from #9330) already uses GetStructEnumerator — no changes needed
- perf-improver workflow (#9258) is a separate bot; recent PRs: #9299 (Array.IndexOf → is pattern), #9311 (cross-platform perf runner), #9348 (avoid redundant TestNodeUid alloc). Avoid duplicating.
- `TerminalTestReporter.TotalTests` property (line 68 of TerminalTestReporter.cs) calls `_assemblies.Values.Sum()` on every access, but is rarely called from outside — low priority.
- `GroupBy().Where(Count()>1)` in `ToolsTestHost.cs:55` runs only once at startup — negligible.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| LOW | Code-Level | `TerminalTestReporter.cs:68` TotalTests prop calls `Sum()` on every access | Rare caller, negligible |
| LOW | Code-Level | `ToolsTestHost.cs:55` GroupBy at startup | Startup only, negligible |

## Round-Robin Task Schedule
- Next run should prioritize: Task 5 (Comment on efficiency issues) + Task 6 (Measurement infrastructure)
- Task 3: WIP PR pending CI validation (#aw_pr_dtc). If merged, scan src/Adapter/ and src/TestFramework/ for next opportunity.
- DotnetTestDataConsumer PropertyBag series: FileArtifactProperty+TestMetadataProperty added to single pass (2026-06-23). All 5 property types now collected in one walk.
