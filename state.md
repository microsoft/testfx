# Perf Improver — State for microsoft/testfx

_Last updated: 2026-06-25 (run 28176552850)_

## Completed Work

| PR | Description | Status |
|---|---|---|
| #9159 | perf: single-pass PropertyBag walk in TerminalOutputDevice | Merged by Evangelink 2026-06-16 |
| #9257 | perf: replace per-test Queue/Stack with List-based index iteration | Merged by Evangelink 2026-06-19 |
| #9299 | perf: Array.IndexOf(GetType()) → is pattern in TestApplicationResult, AbortForMaxFailedTests, RetryDataConsumer | Merged by Evangelink 2026-06-22 |
| #9311 | perf: extend MSTest.Performance.Runner timing to Linux/macOS + ClassLevel variant | Merged by Evangelink 2026-06-22 |
| #9312 (issue) | Proposal: Phase 1 nightly perf-timing CI workflow | Closed — Evangelink implemented via PR #9325 |
| #9348 | perf: avoid redundant TestNodeUid allocation in PopulateTestNodeStatistics | Merged by Evangelink 2026-06-23 |

## Open Work

| Branch | Description | Status |
|---|---|---|
| perf-assist/skip-unused-init-contexts | perf: skip unused TestContextImplementation allocs for repeat assembly/class init | PR submitted 2026-06-25 (draft), awaiting CI/review |

**Note**: The Linux perf-timing job PR (branch: perf-assist/add-linux-perf-timing-job) was NEVER actually created (safe-outputs failed silently in runs 28032837283 and 28105007003). Removed from tracking.

## Optimization Backlog

1. `AnsiTerminal.StopUpdate()` — `_stringBuilder.ToString()` on every flush. Blocked by IConsole limitation. Low priority.
2. `SilenceDrivenHeartbeatRenderer` — allocations only on rare heartbeat paths. Low priority.
3. `ClassifyOutcome` in `TestResultCaptureHelper.cs` — add explicit `CancelledTestNodeStateProperty` arm. Very low impact. Backlog only.

## Monthly Activity Issue

Issue #9258 — open, "[perf-improver] Monthly Activity 2026-06"
- Updated 2026-06-25 with new PR (skip unused TestContextImplementation allocs)

## Task Schedule (round-robin)

| Task | Last Run |
|---|---|
| T1: Discover commands | 2026-06-19 (validated) |
| T2: Identify opportunities | 2026-06-25 |
| T3: Implement improvement | 2026-06-25 (PR: skip-unused-init-contexts) |
| T4: Maintain PRs | 2026-06-24 |
| T5: Comment on issues | 2026-06-25 |
| T6: Measurement infrastructure | 2026-06-24 |
| T7: Monthly summary | 2026-06-25 ✅ |

## Checked-off items (by maintainer) — do NOT re-suggest

- (none yet)

## Notes

- Repository uses pinned SDK 11.0.100-preview — local build/test not possible in CI agent
- No `AGENTS.md` found in repo
- `efficiency-improver` agent also operates on this repo — check before commenting (anti-spam)
- PropertyBag SingleOrDefault<TestNodeStateProperty>() is O(1) via _testNodeStateProperty fast-path
- TestContextImplementation ctor: copies testContextProperties dict + registers CancellationTokenRegistration
- TryGetClonedCachedClassInitializeResult(): changed from private to internal in TestClassInfo.Initializer.cs
- The SetOutcome(testContextForClassInit.Context.CurrentTestOutcome) call on class-init fast-path was a no-op (value was always InProgress) — safe to skip when context is null
- efficiency-improver merged DotnetTestDataConsumer single-pass PropertyBag (#9380) on 2026-06-24
