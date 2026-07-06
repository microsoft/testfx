# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 3 (Implement Improvement): 2026-07-09 (pass cached ParameterTypes to GetInvokeResultAsync; branch efficiency/pass-params-to-invoke; PR created this run)
- Task 5 (Comment on Issues): 2026-07-09 (#8894 and #9480 confirmed closed; #8824 no new activity — skipped)
- Task 7 (Monthly Summary): 2026-07-09 (updated July 2026 issue #9594)
- Task 4 (Check PR Status): 2026-07-08 (PR #9617 still open/dirty — merge conflict)
- Task 2 (Identify Opportunities): 2026-07-08 (found GetInvokeResultAsync uncached GetParameters() call site)
- Task 6 (Measurement Infrastructure): 2026-06-27

## Backlog Cursor
- GetInvokeResultAsync uncached params: DONE (branch efficiency/pass-params-to-invoke, PR created 2026-07-09)
- Next scan areas: CtrfReport/HtmlReport report generators (low priority — end-of-run), Analyzers remaining

## Validated Commands

| Command | Purpose |
|---------|---------|
| `./build.sh` | Full restore + build (installs SDK to .dotnet/ first time) |
| `./build.sh -test` | Run unit tests |
| `./build.sh -pack` | Build + produce NuGets |
| `./build.sh -pack -test -integrationTest` | Full pipeline incl. acceptance tests |
| `.dotnet/dotnet build <project> -f net8.0 -c Debug` | Build single project after SDK restored |

Notes:
- SDK: `.dotnet/dotnet` (Arcade-provisioned at 11.0.100-preview.5.26302.115). Install via `./build.sh` first.
- `--no-restore` fails before assets.json exists; always restore first.
- MSTestAdapter internal tests not run by MTP runner; handled by CI separately.
- NU1201 errors for net462/net472/net48 test assets are pre-existing on Linux (no .NET Framework).
- Warnings are treated as errors in CI (via /warnaserror from Arcade). Fix all new warnings.
- Using `?.` on a non-nullable ParameterInfo[] parameter causes spurious CS8604 — remove ?. operators.

## Monthly Activity Issue
- Issue #9197: June 2026 — CLOSED 2026-07-03
- Current: July 2026 issue #9594 (created 2026-07-03, updated 2026-07-09)

## Open PRs (Efficiency Improver)
- Branch `efficiency/pass-params-to-invoke`: thread TestMethodInfo.ParameterTypes through GetInvokeResultAsync + ConstructGenericMethod; eliminates O(N) GetParameters() allocs per invocation — PR created 2026-07-09 (check PR number from safeoutputs)

## Items Checked Off by Maintainer (do not re-add to Suggested Actions)
- (none yet)

## Issues Closed Since Last Run
- #8894 (ITestFilter): CLOSED 2026-06-29 — remove from Suggested Actions
- #9480 (server-mode perf scenario): CLOSED 2026-06-29 — remove from Suggested Actions

## Work in Progress
None.

## Completed Work (PRs merged or applied)
- Branch efficiency/pass-params-to-invoke: PR created 2026-07-09 (thread ParameterTypes to GetInvokeResultAsync + ConstructGenericMethod)
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
- Nullable warning gotcha: using ?. on a non-nullable ParameterInfo[] parameter causes CS8604 on downstream calls. Fix by removing the ?. operators.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| LOW | Code-Level | OpenTelemetry: OfType<TestMetadataProperty>() in iterator method | Needs non-iterator refactor |
| LOW | Code-Level | TerminalTestReporter.TotalTests: calls _assemblies.Values.Sum() on every access | Rare caller, negligible |
| LOW | Code-Level | PlainProcess.cs: cache JsonSerializerOptions instance (CA1869) | Negligible |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |
| LOW | Code-Level | CtrfReport/HtmlReport report generators: scan for inefficiencies | Low priority — end-of-run |

## Issue Comments Posted (Task 5)
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — no new activity as of 2026-07-09; do not re-comment
- #8894 (ITestFilter): CLOSED — no action needed
- Do not re-comment unless new human activity appears

## Round-Robin Task Schedule
- Last run (2026-07-09): Tasks 5, 3, 7
- Next run should prioritize: Task 6 (infra proposal), Task 2 (scan CtrfReport/HtmlReport), Task 7
