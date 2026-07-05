# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 4 (Check PR Status): 2026-07-08 (PR #9614 merged; PR #9617 still open/dirty)
- Task 3 (Implement Improvement): 2026-07-08 (pass cached ParameterTypes to GetInvokeResultAsync; eliminate double-alloc in ConstructGenericMethod; branch efficiency/pass-cached-params-to-invoke; issue #aw_pr_jul3)
- Task 7 (Monthly Summary): 2026-07-08 (updated July 2026 issue #9594)
- Task 2 (Identify Opportunities): 2026-07-08 (found GetInvokeResultAsync uncached GetParameters() call site)
- Task 5 (Comment on Issues): 2026-06-30 (GitHub MCP 403 error)
- Task 6 (Measurement Infrastructure): 2026-06-27

## Backlog Cursor
- IPC serializer series: COMPLETE
- Analyzers scan: DONE
- SourceGeneratedReflectionOperations: DONE (PR #9479)
- VideoRecorder PropertyBag.FirstOrDefault: DONE (PR #9488)
- GetInvokeResultAsync uncached params: DONE (branch efficiency/pass-cached-params-to-invoke; issue #aw_pr_jul3; 2026-07-08) — threads ParameterTypes cache to invocation call; eliminates double-alloc for generic methods
- ReflectionTestMethodInfo caching + ArgumentResolution fix: DONE (incorporated in PR #9617 by Evangelink)
- GetInvokeResultAsync uncached params: DONE (branch efficiency/pass-cached-params-to-invoke, issue #aw_pr_jul3, 2026-07-08)
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
- Current: July 2026 issue #9594 (created 2026-07-03, updated 2026-07-08)

## Open PRs (Efficiency Improver)
- Branch `efficiency/pass-cached-params-to-invoke` (temporary_id #aw_pr_jul3): thread TestMethodInfo.ParameterTypes through GetInvokeResultAsync + ConstructGenericMethod; eliminates O(N) GetParameters() allocs per invocation — PENDING REVIEW (created 2026-07-08)

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
- GetInvokeResultAsync: add overload accepting ParameterInfo[] to avoid re-calling GetParameters() on every test invocation. ConstructGenericMethod also called GetParameters() a second time for generic methods — pass cached array there too.
- C# 14 `field` keyword available (LangVersion=preview) — useful for lazy auto-property caching.
- ReflectionTestMethodInfo: cache _parameters field (GetParameters() result).
- TestMethodRunner._cachedReflectionMethodInfo: reuses wrapper across N data rows since _testMethodInfo.MethodInfo and _test.DisplayName are immutable per TestMethodRunner lifetime.
- TestMethodInfo.ResolveArguments(): use ParameterTypes (cached) not MethodInfo.GetParameters() directly.
- PropertyBag.FirstOrDefault<T>(): check _testNodeStateProperty fast path, then linked-list walk.
- TestContextImplementation._testResultFiles.ToList(): intentional defensive copy — do not cache.
- GitHub MCP tools: enterprise PAT policy may block search_issues. search_pull_requests and search_code work reliably.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Infrastructure | Add server-mode (JSON-RPC) perf scenario to nightly pipeline + trend tracking | Issue #9480 open |
| LOW | Code-Level | OpenTelemetry: OfType<TestMetadataProperty>() in iterator method | Needs non-iterator refactor |
| LOW | Code-Level | TerminalTestReporter.TotalTests: calls _assemblies.Values.Sum() on every access | Rare caller, negligible |
| LOW | Code-Level | PlainProcess.cs: cache JsonSerializerOptions instance (CA1869) | Negligible |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — no new activity as of 2026-07-04
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — no new activity as of 2026-07-04
- Do not re-comment until new human activity appears

## Round-Robin Task Schedule
- This run (2026-07-08): Tasks 4 (check PR status), 2 (identify opportunities), 3 (implement), 7 (update July issue)
- Next run should prioritize: Task 5 (comment on issues — check for new activity on #8894, #8824), Task 6 (infra proposal for #9480), Task 7
