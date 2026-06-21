# Perf Improver State — microsoft/testfx

## Last Updated
2026-06-21 (run 27907017916)

## Validated Commands

```sh
# Build all (Debug, with StyleCop/analyzers as errors)
./build.sh

# Build + unit tests
./build.sh -test

# Pack NuGets (required before acceptance tests and performance runner)
./build.sh -pack

# Acceptance integration tests (needs pack first)
./build.sh -pack -test -integrationTest

# Run performance timing scenarios (needs pack first; cross-platform after PR #aw_pr_xplat)
.dotnet/dotnet run --project test/Performance/MSTest.Performance.Runner \
  -- execute --pipelineNameFilter "*PlainProcess*"

# Run single MTP/Extension test project
.dotnet/dotnet run --project <path> -f net9.0 --no-build
  -- --treenode-filter "/*/*/*/ClassName/MethodName"
```

Notes:
- StyleCop and Roslyn analyzers are treated as errors
- `CancelledTestNodeStateProperty` is `[Obsolete]` → wrap in `#pragma warning disable CS0618, MTP0001`
- Put `#pragma restore` on same line as or after `)` to avoid SA1009
- `--filter-uid` and `--treenode-filter` are both available on MTP-based test hosts
- 24 pre-existing failures in MSTestAdapter.PlatformServices.UnitTests are deployment-path tests unrelated to our changes
- dotnet binary at .dotnet/dotnet (not in PATH initially; build.sh installs it)

## Task Round-Robin (last run per task)

- Task 1 (Discover commands): 2026-06-15
- Task 2 (Identify opportunities): 2026-06-20
- Task 3 (Implement improvements): 2026-06-20
- Task 4 (Maintain PRs): 2026-06-21
- Task 5 (Comment on issues): 2026-06-17
- Task 6 (Measurement infra): 2026-06-21
- Task 7 (Monthly summary): 2026-06-21

**Next run priority**: Task 5 (comment on issues), Task 3 (implement improvements), Task 2 (identify opportunities)

## Monthly Activity Issue

- Issue #9258 open: "[perf-improver] Monthly Activity 2026-06"
- Updated: 2026-06-21
- No maintainer instructions found in comments

## Open PRs (ours)

- PR #9299 (open, CI green): "Replace Array.IndexOf(GetType()) with 'is' pattern matching in hot paths"
  branch: perf-assist/is-pattern-replace-array-indexof
- PR #aw_pr_xplat (new, 2026-06-21): "Extend performance runner timing scenarios to Linux/macOS and add ClassLevel variant"
  branch: perf-assist/crossplatform-timing-scenarios

## Open Issues (ours)

- Issue #aw_issue_ci (new, 2026-06-21): "Track performance regressions in CI: integrate MSTest.Performance.Runner timing output"

## Completed Work

| Date | PR | Description |
|---|---|---|
| 2026-06-16 | #9159 (merged) | Single-pass PropertyBag walk in TerminalOutputDevice |
| 2026-06-19 | #9257 (merged) | Replace per-test Queue/Stack with List in TestMethodInfo lifecycle |
| 2026-06-20 | #9299 (open, green) | Replace Array.IndexOf(GetType()) with 'is' patterns in hot paths |
| 2026-06-21 | #aw_pr_xplat (open) | Cross-platform timing scenarios in perf runner; ClassLevel variant |

## Backlog (prioritized)

1. **[Done ✅]** Single-pass PropertyBag walk — PR #9159 merged
2. **[Done ✅]** Eliminate Queue/Stack per-test alloc in TestMethodInfo — PR #9257 merged
3. **[In PR #9299]** Replace Array.IndexOf(GetType()) with `is` pattern in TestApplicationResult, AbortForMaxFailedTests, RetryDataConsumer
4. **[In PR #aw_pr_xplat]** Cross-platform PlainProcess/DotnetTrace timing pipelines; ClassLevel scenario
5. `TrxTestResultExtractor.MapOutcome` + `TestResultCaptureHelper.ClassifyOutcome`: switch catch-all `_ when Array.IndexOf(...)` — can add explicit `CancelledTestNodeStateProperty` arm; low impact (only on failed tests)
6. `AnsiTerminal.StopUpdate()` — StringBuilder.ToString() allocation on every flush; blocked on netstandard2.0 target (StreamWriter.Write(StringBuilder) is NET8+ only)
7. `SilenceDrivenHeartbeatRenderer` — allocations only on rare heartbeat paths; not hot
8. **[Issue #aw_issue_ci]** CI integration for performance regression tracking (proposal only)

## Checked-off Items (by maintainer)

None yet

## Performance Infrastructure (from Task 6 audit, 2026-06-21)

MSTest.Performance.Runner (test/Performance/MSTest.Performance.Runner/):
- 5 Windows-only profiling pipelines: PerfView, DotnetTrace, VSDiagnostics x2, ConcurrencyVisualizer
- 1 cross-platform timing pipeline: PlainProcess (after PR #aw_pr_xplat)
- 1 cross-platform trace pipeline: DotnetTrace (after PR #aw_pr_xplat)
- Scenario1: 100 classes × 100 methods, MTP mode (EnableMSTestRunner=true)
- PlainProcess records: ElapsedTime, TotalProcessorTime, ProcessorCount, TotalAvailableMemoryBytes → Result.json
- No CI integration (tracked in issue #aw_issue_ci)
- Requires -pack to produce MSTest NuGet packages before running

## Performance Notes

- `TestNodePropertiesCategories.WellKnownTestNodeXxx` arrays hold `Type` objects and are used by Array.IndexOf → O(n) scan + GetType() call per invocation
- `ConsumeAsync` in TestApplicationResult fires for EVERY test node update including InProgress
- `CancelledTestNodeStateProperty` is obsolete (`[Obsolete]`) — must use pragma wrapper
- SA1009: closing `)` must not be on its own line preceded by space; put `)` at end of last condition line or use local bool variable
- HangDump pattern: put `#pragma disable` before the line with `CancelledTestNodeStateProperty` in the `is` chain, then `#pragma restore` on the next line; works as long as `)` follows on a different line with more conditions
- PropertyBag `SingleOrDefault()` already has O(1) `_testNodeStateProperty` fast-path for single-state nodes
- TestMethodInfo.RunTestAsync uses index iteration not queue/stack since PR #9257
