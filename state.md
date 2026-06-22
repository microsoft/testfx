# Perf Improver — State for microsoft/testfx

_Last updated: 2026-06-25 (run 27963241204)_

## Completed Work

| PR | Description | Status |
|---|---|---|
| #9159 | perf: single-pass PropertyBag walk in TerminalOutputDevice | Merged by Evangelink 2026-06-16 |
| #9257 | perf: replace per-test Queue/Stack with List-based index iteration | Merged by Evangelink 2026-06-19 |
| #9299 | perf: Array.IndexOf(GetType()) → is pattern in TestApplicationResult, AbortForMaxFailedTests, RetryDataConsumer | Merged by Evangelink 2026-06-22 |
| #9311 | perf: extend MSTest.Performance.Runner timing to Linux/macOS + ClassLevel variant | Merged by Evangelink 2026-06-22 |
| #9312 (issue) | Proposal: Phase 1 nightly perf-timing CI workflow | Closed — Evangelink implemented via PR #9325 |

## Open Work

| PR | Description | Status |
|---|---|---|
| #aw_pr_uid (TBD) | perf: avoid redundant TestNodeUid allocation in PopulateTestNodeStatistics | Created 2026-06-25, awaiting CI/review |

## Optimization Backlog

1. `AnsiTerminal.StopUpdate()` — `_stringBuilder.ToString()` on every flush. Blocked by IConsole limitation. Low priority.
2. `SilenceDrivenHeartbeatRenderer` — allocations only on rare heartbeat paths. Low priority.
3. `ClassifyOutcome` in `TestResultCaptureHelper.cs` — add explicit `CancelledTestNodeStateProperty` arm. Very low impact (obsolete type, rarely used). Backlog only.
4. Look for non-server-mode hot paths in MSTest execution (RunSingleTestAsync, reflection attribute caching, etc.)

## Monthly Activity Issue

Issue #9258 — open, "[perf-improver] Monthly Activity 2026-06"
- Updated 2026-06-25 with new PR and corrected history

## Task Schedule (round-robin)

| Task | Last Run |
|---|---|
| T1: Discover commands | 2026-06-19 (validated) |
| T2: Identify opportunities | 2026-06-25 |
| T3: Implement improvement | 2026-06-25 (PR for TestNodeUid) |
| T4: Maintain PRs | 2026-06-25 (no open PRs) |
| T5: Comment on issues | 2026-06-25 (no issues needed) |
| T6: Measurement infrastructure | 2026-06-21 |
| T7: Monthly summary | 2026-06-25 ✅ |

## Checked-off items (by maintainer) — do NOT re-suggest

- (none yet — first issue, maintainer hasn't checked off anything)

## Notes

- Repository uses pinned SDK 11.0.100-preview — local build/test not possible in CI agent; verified logic manually + CI will confirm
- No `AGENTS.md` found in repo (as of 2026-06-15)
- `efficiency-improver` agent also operates on this repo — check its comments before commenting on same issues (anti-spam)
- Issue #5348 (in-progress/passed duplicates in server mode): efficiency-improver already posted detailed proposal — do NOT comment again unless new human comments appear
- PropertyBag SingleOrDefault<TestNodeStateProperty>() is O(1) via _testNodeStateProperty fast-path — already optimal
- TestNodeUid: reference type (sealed class), value equality via .Value string; using original instance vs new wrapper is semantically identical for dict operations
