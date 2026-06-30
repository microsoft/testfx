# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-06-30 (scanned OTel, TerminalTestReporter, TrxReport, TestResultCaptureHelper)
- Task 3 (Implement Improvement): 2026-06-29
- Task 4 (Maintain PRs): 2026-06-29 (no open efficiency PRs; all merged)
- Task 5 (Comment on Issues): 2026-06-30 (GitHub MCP 403 error; enterprise PAT policy blocked search)
- Task 6 (Measurement Infrastructure): 2026-06-27
- Task 7 (Monthly Summary): 2026-06-30

## Backlog Cursor
- IPC serializer series: COMPLETE
- Analyzers scan: DONE
- SourceGeneratedReflectionOperations: DONE (PR #9479, merged 2026-06-28)
- VideoRecorder PropertyBag.FirstOrDefault: DONE (PR #9488, merged 2026-06-29)
- GetParameters caching: DONE (PR #9514, merged 2026-06-30)
- OpenTelemetry handler: scanned 2026-06-30 — LOW priority only
- TerminalTestReporter: scanned 2026-06-30 — LOW priority only
- TrxReport (TrxReportEngine, TestResultCaptureHelper): scanned 2026-06-30 — all optimized
- Next scan area: VSTestBridge conversion path, JUnitReport, AzureDevOpsReport extensions

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
- Last updated: 2026-06-30

## Open PRs (Efficiency Improver)
None. All efficiency-improver PRs merged.

## Work in Progress
None.

## Completed Work (PRs merged or applied)
- PR #9514: cache MethodInfo.GetParameters() in TestMethodInfo.ParameterTypes + AssemblyEnumerator.TryUnfoldITestDataSource — merged 2026-06-30
- PR #9488: add PropertyBag.FirstOrDefault<T>() — zero-allocation linked-list walk — merged 2026-06-29
- PR #9479: eliminate LINQ iterator allocations in SourceGeneratedReflectionOperations — merged 2026-06-28
- PR #9466: eliminate LINQ iterator allocations in MSTest Analyzer DerivesFrom() — merged 2026-06-28
- PR #9436: direct-allocate arrays in IPC CommandLineOption and FileArtifact deserializers — merged 2026-06-26
- PR #9408: direct-allocate arrays in IPC TestResultMessagesSerializer — merged 2026-06-25
- PR #9380: single-pass PropertyBag collection in DotnetTestDataConsumer — merged 2026-06-24
- PR #9353: single-pass aggregation in AppendTestRunSummary — merged
- PR #9300, #9274, #9196, #9162, #9159, #9018, #8692–#8975: all merged

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
- GitHub MCP tools: enterprise PAT policy (>8 days) blocks search_issues and search_pull_requests intermittently. Use search_pull_requests (works more reliably) for PR status checks.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Infrastructure | Add server-mode (JSON-RPC) perf scenario to nightly pipeline + trend tracking | Issue #9480 open |
| LOW | Code-Level | OpenTelemetry: OfType<TestMetadataProperty>() in iterator method | Needs non-iterator refactor |
| LOW | Code-Level | TerminalTestReporter.TotalTests: calls _assemblies.Values.Sum() on every access | Rare caller, negligible |
| LOW | Code-Level | PlainProcess.cs: cache JsonSerializerOptions instance (CA1869) | Negligible |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — no new activity as of 2026-06-30
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — no new activity as of 2026-06-30
- Do not re-comment until new human activity appears

## Round-Robin Task Schedule
- This run (2026-06-30): Tasks 2, 5 (partial, blocked), 7
- Next run should prioritize: Task 3 (consider OpenTelemetry non-iterator refactor or VSTestBridge scan), Task 6 (infra), Task 4 (check for new PRs), Task 7
