# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-07-02 (scanned MSTest execution path, TestDataSourceHelpers, TerminalTestReporter, MTP extensions)
- Task 3 (Implement Improvement): 2026-07-02 (cache GetParameters in ResolveArguments + ReflectionTestMethodInfo)
- Task 4 (Maintain PRs): 2026-07-02 (no open efficiency PRs before this run)
- Task 5 (Comment on Issues): 2026-06-30 (GitHub MCP 403 error; enterprise PAT policy blocked search)
- Task 6 (Measurement Infrastructure): 2026-06-27
- Task 7 (Monthly Summary): 2026-07-02 (created new July 2026 issue #aw_jul2026b — previous run issue not recoverable)

## Backlog Cursor
- IPC serializer series: COMPLETE
- Analyzers scan: DONE
- SourceGeneratedReflectionOperations: DONE (PR #9479, merged 2026-06-28)
- VideoRecorder PropertyBag.FirstOrDefault: DONE (PR #9488, merged 2026-06-29)
- GetParameters caching (TestMethodInfo.ParameterTypes + AssemblyEnumerator): DONE (PR #9514, merged 2026-06-30)
- VSTestBridge conversion path: scanned 2026-07-01 — all optimized (single-pass, pre-sized lists, struct enumerators)
- JUnitReport: scanned 2026-07-01 — all optimized
- AzureDevOpsReport: scanned 2026-07-01 — all optimized (already GetStructEnumerator)
- MTP platform core (Terminal, PropertyBag, AsynchronousMessageBus): scanned 2026-07-01 — end-of-run code, no hot-path LINQ
- MSTest adapter execution path (TestMethodInfo.ArgumentResolution): FIX SUBMITTED (branch efficiency/use-cached-parametertypes-in-resolve-arguments, PR #aw_pr_paramsfix, 2026-07-02)
- ReflectionTestMethodInfo.GetParameters() caching: FIX SUBMITTED (same PR as above, 2026-07-02)
- TestExecutionManager parallelization LINQ: scanned 2026-07-02 — all setup-time, not hot path
- TestContextImplementation: scanned 2026-07-02 — _testResultFiles.ToList() is intentional defensive copy, LOW priority
- MTP extensions (Retry, CrashDump, HangDump, HotReload, Telemetry, VSTestBridge): scanned 2026-07-02 — no new hot-path patterns found
- Next scan area: TestFramework assertions hot paths, Analyzers remaining scans, CtrfReport/HtmlReport

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
- GitHub MCP tools: enterprise PAT policy (>8 days) blocks ALL list/read/search_issues operations. Only search_pull_requests and search_code work reliably. Cannot verify issue numbers via API.

## Monthly Activity Issue
- Issue #9197: `[efficiency-improver] Monthly Activity 2026-06` — CLOSED 2026-07-01
- Current: July 2026 issue — created 2026-07-02 (temporary_id #aw_jul2026b)
- Label: `efficiency`

## Open PRs (Efficiency Improver)
- Branch `efficiency/use-cached-parametertypes-in-resolve-arguments` (temporary_id #aw_pr_paramsfix): cache GetParameters() in ResolveArguments + ReflectionTestMethodInfo — PENDING REVIEW (created 2026-07-02)

## Work in Progress
None after PR creation.

## Completed Work (PRs merged or applied)
- PR branch efficiency/use-cached-parametertypes-in-resolve-arguments: cache GetParameters() in ResolveArguments + ReflectionTestMethodInfo — created 2026-07-02 (pending)
- PR #9514: cache MethodInfo.GetParameters() in TestMethodInfo.ParameterTypes + AssemblyEnumerator.TryUnfoldITestDataSource — merged 2026-06-30
- PR #9488: add PropertyBag.FirstOrDefault<T>() — zero-allocation linked-list walk — merged 2026-06-29
- PR #9479: eliminate LINQ iterator allocations in SourceGeneratedReflectionOperations — merged 2026-06-28
- PR #9466: eliminate LINQ iterator allocations in MSTest Analyzer DerivesFrom() — merged 2026-06-28
- PR #9436: direct-allocate arrays in IPC CommandLineOption and FileArtifact deserializers — merged 2026-06-26
- PR #9408: direct-allocate arrays in IPC TestResultMessagesSerializer — merged 2026-06-25
- PR #9380: single-pass PropertyBag collection in DotnetTestDataConsumer — merged 2026-06-24
- PR #9353: single-pass aggregation in AppendTestRunSummary — merged

## Efficiency Notes (Key Insights)
- PropertyBag internal structure: _testNodeStateProperty (fast path, O(1)) + linked-list for other props
- PropertyBag.FirstOrDefault<T>(): same pattern as SingleOrDefault — check fast path, IsAssignableFrom guard, linked-list walk with early exit. Zero allocation.
- OfType<T>() always allocates TProperty[] — avoid in hot paths.
- IPC serializer series: COMPLETE (all 6 serializers use direct T[length] allocation)
- nightly perf pipeline: only measures plain-process scenarios; issue #9480 tracks server-mode gap.
- PlainProcess.cs: new JsonSerializerOptions per run (CA1869 suppressed) — LOW priority, nightly-only code.
- perf-improver workflow: separate bot (PR #9486) — avoid duplicating its work.
- OpenTelemetry: OfType in yield method — cannot use struct enumerator; needs non-iterator refactor.
- MethodInfo.GetParameters(): always returns a fresh array copy (CLR safety). Use field ??= caching or hoist outside loops for data-driven paths.
- C# 14 `field` keyword available (LangVersion=preview). Useful for lazy-initialized auto properties.
- TestResultCaptureHelper.ExtractProperties: already uses single-pass GetStructEnumerator with switch pattern — fully optimized.
- TrxReportEngine: processes at end-of-run only (report generation), not in hot path.
- VSTestBridge / JUnitReport / AzureDevOpsReport extensions: all already optimized; no new hotpaths found.
- GitHub MCP tools: enterprise PAT policy (>8 days) blocks search_issues and all list/read operations. Only search_pull_requests and search_code work.
- ReflectionTestMethodInfo: created per ITestDataSource attribute in TryUnfoldITestDataSources (not per row). Adding _cachedParameters field makes per-row GetDisplayName() calls free (return cached array).
- TestMethodInfo.ResolveArguments: ParameterTypes (cached) vs MethodInfo.GetParameters() (always allocates). Now fixed.
- TestContextImplementation._testResultFiles.ToList(): intentional defensive copy (returns list, then clears). LOW priority, end-of-test-only.
- TestExecutionManager parallelization GroupBy patterns: all setup-time (once per test run), not per-test hot path.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Infrastructure | Add server-mode (JSON-RPC) perf scenario to nightly pipeline + trend tracking | Issue #9480 open |
| LOW | Code-Level | OpenTelemetry: OfType<TestMetadataProperty>() in iterator method | Needs non-iterator refactor |
| LOW | Code-Level | TerminalTestReporter.TotalTests: calls _assemblies.Values.Sum() on every access | Rare caller, negligible |
| LOW | Code-Level | PlainProcess.cs: cache JsonSerializerOptions instance (CA1869) | Negligible |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |
| LOW | Code-Level | TestFramework assertions hot paths — scan for string allocation patterns | Next scan target |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — no new activity as of 2026-07-02
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — no new activity as of 2026-07-02
- Do not re-comment until new human activity appears

## Round-Robin Task Schedule
- This run (2026-07-02): Tasks 2 (MSTest execution scan), 3 (cache GetParameters), 4 (no open PRs), 7 (new July issue)
- Next run should prioritize: Task 5 (comment on issues — try again), Task 6 (infra proposal for server-mode perf), Task 4 (check new PR status), Task 7
