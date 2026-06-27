# Efficiency Improver Memory — microsoft/testfx

## Tasks Last Run
- Task 2 (Identify Opportunities): 2026-06-27
- Task 3 (Implement Improvement): 2026-06-27
- Task 4 (Maintain PRs): 2026-06-27
- Task 5 (Comment on Issues): 2026-06-27 (no new activity; no re-engagement)
- Task 6 (Measurement Infrastructure): 2026-06-27
- Task 7 (Monthly Summary): 2026-06-27

## Backlog Cursor
- IPC serializer series: COMPLETE
- Analyzers scan: DONE — DerivesFrom() fix (PR #9466)
- SourceGeneratedReflectionOperations: DONE — LINQ chains fixed (PR submitted this run: efficiency/eliminate-linq-in-source-gen-attribute-lookup)
- Next scan area: broader Platform scan for remaining OfType<T> on PropertyBag / hot paths in MSTestAdapter

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
- Last updated: 2026-06-27

## Open PRs (Efficiency Improver)
- PR #9466: `[efficiency-improver] perf: eliminate LINQ iterator allocations in MSTest Analyzer DerivesFrom interface check` — open draft, CI passing ✅
- PR (branch: efficiency/eliminate-linq-in-source-gen-attribute-lookup): `perf: replace LINQ iterator chains with direct foreach in SourceGeneratedReflectionOperations` — submitted 2026-06-27, awaiting PR number

## Work in Progress
None — SourceGeneratedReflectionOperations optimization submitted this run.

## Completed Work (PRs merged or applied)
- PR #9436: direct-allocate arrays in IPC CommandLineOption and FileArtifact deserializers — merged 2026-06-26
- PR #9408: direct-allocate arrays in IPC TestResultMessagesSerializer — merged 2026-06-25
- PR #9380: single-pass PropertyBag collection in DotnetTestDataConsumer — merged 2026-06-24
- PR #9353: single-pass aggregation in AppendTestRunSummary — merged
- PR #9300, #9274, #9196, #9162, #9159, #9018, #8692–#8975: all merged

## Efficiency Notes (Key Insights)
- PropertyBag hot-path series COMPLETE across all report extensions
- IPC serializer series: COMPLETE (all 6 serializers use direct T[length] allocation)
- Analyzers: DerivesFrom() fix (PR #9466) — ImmutableArray OfType + optional Select + Contains → direct foreach
- SourceGeneratedReflectionOperations: DONE — 3 attribute helpers (IsAttributeDefined, GetFirstAttributeOrDefault, GetSingleAttributeOrDefault) had LINQ chains allocating 1–3 iterator state machines per call; replaced with direct foreach loops (zero allocs). GetAttributes<T> left as-is (returns IEnumerable<T> by contract).
- nightly perf pipeline (perf-timing-nightly.yml): only measures plain-process scenarios; server-mode (JSON-RPC) path has NO benchmark coverage. Issue created (#aw_issue_perfgap) to track this gap.
- PlainProcess.cs: new JsonSerializerOptions per run (CA1869 suppressed) — LOW priority, nightly-only code.
- perf-improver workflow: separate bot — avoid duplicating its work.
- VideoRecorderSessionHandler.cs: OfType<TestNodeStateProperty>().FirstOrDefault() — low priority (rarely-used extension).
- OpenTelemetry: OfType in yield method — cannot use struct enumerator; needs refactor.

## Optimisation Backlog

| Priority | Focus Area | Opportunity | Notes |
|----------|------------|-------------|-------|
| MEDIUM | Infrastructure | Add server-mode (JSON-RPC) perf scenario to nightly pipeline + trend tracking | Issue created (#aw_issue_perfgap) this run |
| LOW | Code-Level | VideoRecorder: OfType<TestNodeStateProperty>().FirstOrDefault() → direct foreach | Rarely-used extension |
| LOW | Code-Level | OpenTelemetry: OfType<TestMetadataProperty>() in iterator method | Needs non-iterator refactor |
| LOW | Code-Level | TerminalTestReporter.TotalTests: calls _assemblies.Values.Sum() on every access | Rare caller, negligible |
| LOW | Code-Level | ToolsTestHost.cs:55: GroupBy at startup only | Negligible |
| LOW | Code-Level | PlainProcess.cs: cache JsonSerializerOptions instance (CA1869) | Negligible |
| LOW | Infrastructure | Output-byte-count CI health metric (suggested in #8824 comment) | Needs maintainer discussion |

## Issue Comments Posted (Task 5)
- #8894 (ITestFilter): commented 2026-06-24 — no new activity as of 2026-06-27
- #8824 (LLM-efficient output RFC): commented 2026-06-24 — no new activity as of 2026-06-27
- Do not re-comment until new human activity appears

## Round-Robin Task Schedule
- This run (2026-06-27): Tasks 2, 3, 4, 5, 6, 7
- Next run should prioritize: Task 1 (validate commands — check if SDK available) + Task 2 (broader scan) + Task 7
