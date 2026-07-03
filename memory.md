# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-07-03 (scanned TestFramework assertions, TelemetryCollector, TestMethodRunner, TestDataSourceUtilities — ReflectionTestMethodInfo per-row allocation found)
- Task 3 (Implement Improvement): 2026-07-03 (cache ReflectionTestMethodInfo + GetParameters() across data rows)
- Task 4 (Maintain PRs): 2026-07-03 (PR #aw_pr_paramsfix from 2026-07-02 was NEVER created; no open efficiency PRs found)
- Task 5 (Comment on Issues): 2026-06-30 (GitHub MCP 403 error)
- Task 6 (Measurement Infrastructure): 2026-06-27
- Task 7 (Monthly Summary): 2026-07-03 (closed June #9197; created July 2026 issue #aw_jul_pr1)

## Backlog Cursor
- IPC serializer series: COMPLETE
- Analyzers scan: DONE
- SourceGeneratedReflectionOperations: DONE (PR #9479)
- VideoRecorder PropertyBag.FirstOrDefault: DONE (PR #9488)
- GetParameters caching (TestMethodInfo.ParameterTypes + AssemblyEnumerator): DONE (PR #9514, merged 2026-06-30)
- VSTestBridge, JUnitReport, AzureDevOpsReport, MTP platform core, MTP extensions: all scanned — no new opportunities
- TestFramework assertions + TelemetryCollector: scanned 2026-07-03 — already optimized (interpolated string handlers, AggressiveInlining, Lazy<bool> guard)
- TestMethodRunner: scanned 2026-07-03 — ReflectionTestMethodInfo per-row allocation found and fixed
- TestDataSourceUtilities: scanned 2026-07-03 — ComputeDefaultDisplayName calls GetParameters() once per row; fixed via ReflectionTestMethodInfo caching
- Next scan areas: CtrfReport/HtmlReport report generators (low priority — end-of-run), Analyzers remaining

## Validated Commands

| Command | Purpose |
|---------|---------|
| `./build.sh` | Full restore + build |
| `./build.sh -test` | Run unit tests |
| `./build.sh -pack` | Build + produce NuGets |
| `./build.sh -pack -test -integrationTest` | Full pipeline incl. acceptance tests |

Notes:
- SDK: `.dotnet/dotnet` (Arcade-provisioned). Install via `./build.sh` first.
- `--no-restore` flag is broken; always use full restore.
- MSTestAdapter internal tests not run by MTP runner; handled by CI separately.
- NU1201 errors for net462/net472/net48 test assets are pre-existing on Linux (no .NET Framework).

## Monthly Activity Issue
- Issue #9197: June 2026 — CLOSED 2026-07-03
- Current: July 2026 issue — created 2026-07-03 (temporary_id #aw_jul_pr1); real number TBD

## Open PRs (Efficiency Improver)
- Branch `efficiency/cache-reflection-method-info-across-data-rows` (temporary_id #aw_jul_pr1): cache ReflectionTestMethodInfo and GetParameters() across data rows — PENDING REVIEW (created 2026-07-03)

## Work in Progress
None.

## Completed Work (PRs merged or applied)
- PR #9514: cache MethodInfo.GetParameters() in TestMethodInfo.ParameterTypes + AssemblyEnumerator.TryUnfoldITestDataSource — merged 2026-06-30
- PR #9488: PropertyBag.FirstOrDefault<T>() — merged 2026-06-29
- PR #9479: eliminate LINQ in SourceGeneratedReflectionOperations — merged 2026-06-28
- PR #9466: eliminate LINQ in MSTest Analyzer DerivesFrom() — merged 2026-06-28
- PR #9436: direct-allocate arrays in IPC CommandLineOption/FileArtifact deserializers — merged 2026-06-26
- PR #9408: direct-allocate arrays in TestResultMessagesSerializer — merged 2026-06-25
- PR #9380: single-pass PropertyBag collection in DotnetTestDataConsumer — merged 2026-06-24
- PR #9353: single-pass aggregation in AppendTestRunSummary — merged

## Efficiency Notes
- MethodInfo.GetParameters(): always returns a fresh array copy (CLR safety). Cache with ??= for repeated calls.
- C# 14 `field` keyword available (LangVersion=preview) — useful for lazy auto-property caching.
- ReflectionTestMethodInfo: internal class in TestFramework/Internal/. GetParameters() now cached.
- TestMethodRunner._cachedReflectionMethodInfo: reuses wrapper across N data rows since _testMethodInfo.MethodInfo and _test.DisplayName are immutable per TestMethodRunner lifetime.
- PropertyBag.FirstOrDefault<T>(): check _testNodeStateProperty fast path, then linked-list walk.
- TestContextImplementation._testResultFiles.ToList(): intentional defensive copy — do not cache.
- GitHub MCP tools: enterprise PAT policy may block search_issues. search_pull_requests and search_code work reliably.
- Note: PRs from 2026-07-02 (branch efficiency/use-cached-parametertypes-in-resolve-arguments) were NEVER created — safeoutputs did not create them. Discard that memory entry.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Infrastructure | Add server-mode (JSON-RPC) perf scenario to nightly pipeline + trend tracking | Issue #9480 open |
| LOW | Code-Level | OpenTelemetry: OfType<TestMetadataProperty>() in iterator method | Needs non-iterator refactor |
| LOW | Code-Level | TerminalTestReporter.TotalTests: calls _assemblies.Values.Sum() on every access | Rare caller, negligible |
| LOW | Code-Level | PlainProcess.cs: cache JsonSerializerOptions instance (CA1869) | Negligible |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — no new activity as of 2026-07-03
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — no new activity as of 2026-07-03
- Do not re-comment until new human activity appears

## Round-Robin Task Schedule
- This run (2026-07-03): Tasks 2 (scan assertions+TestMethodRunner), 3 (ReflectionTestMethodInfo fix), 4 (no open PRs), 7 (new July issue)
- Next run should prioritize: Task 4 (check new PR status), Task 5 (comment on issues), Task 6 (infra proposal), Task 7
