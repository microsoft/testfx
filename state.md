# Perf Improver State — microsoft/testfx

## Last Updated
2026-06-20 (run 27873665540)

## Validated Commands

```sh
# Build all (Debug, with StyleCop/analyzers as errors)
./build.sh

# Build + unit tests
./build.sh -test

# Pack NuGets (required before acceptance tests)
./build.sh -pack

# Acceptance integration tests (needs pack first)
./build.sh -pack -test -integrationTest

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

## Task Round-Robin (last run per task)

- Task 1 (Discover commands): 2026-06-15
- Task 2 (Identify opportunities): 2026-06-20
- Task 3 (Implement improvements): 2026-06-20
- Task 4 (Maintain PRs): 2026-06-17
- Task 5 (Comment on issues): 2026-06-17
- Task 6 (Measurement infra): not run yet ← PRIORITY
- Task 7 (Monthly summary): 2026-06-20

**Next run priority**: Task 6 (Measurement infra), Task 4/5 (maintain PR / comment)

## Monthly Activity Issue

- Issue #9258 open: "[perf-improver] Monthly Activity 2026-06"
- Updated: 2026-06-20
- No maintainer instructions found in comments

## Open PRs (ours)

- PR created this run (2026-06-20): "Replace Array.IndexOf(GetType()) with 'is' pattern matching in hot paths"
  branch: perf-assist/is-pattern-replace-array-indexof
  Files: TestApplicationResult.cs, AbortForMaxFailedTestsExtension.cs, RetryDataConsumer.cs

## Completed Work

| Date | PR | Description |
|---|---|---|
| 2026-06-16 | #9159 (merged) | Single-pass PropertyBag walk in TerminalOutputDevice |
| 2026-06-19 | #9257 (merged) | Replace per-test Queue/Stack with List in TestMethodInfo lifecycle |
| 2026-06-20 | (new, open) | Replace Array.IndexOf(GetType()) with 'is' patterns in hot paths |

## Backlog (prioritized)

1. **[Done ✅]** Single-pass PropertyBag walk — PR #9159 merged
2. **[Done ✅]** Eliminate Queue/Stack per-test alloc in TestMethodInfo — PR #9257 merged
3. **[In PR]** Replace Array.IndexOf(GetType()) with `is` pattern in TestApplicationResult, AbortForMaxFailedTests, RetryDataConsumer
4. `TrxTestResultExtractor.MapOutcome` + `TestResultCaptureHelper.ClassifyOutcome`: switch catch-all `_ when Array.IndexOf(...)` — can add explicit `CancelledTestNodeStateProperty` arm; low impact (only on failed tests)
5. `AnsiTerminal.StopUpdate()` — StringBuilder.ToString() allocation on every flush; blocked on netstandard2.0 target (StreamWriter.Write(StringBuilder) is NET8+ only)
6. `SilenceDrivenHeartbeatRenderer` — allocations only on rare heartbeat paths; not hot

## Checked-off Items (by maintainer)

None yet

## Performance Notes

- `TestNodePropertiesCategories.WellKnownTestNodeXxx` arrays hold `Type` objects and are used by Array.IndexOf → O(n) scan + GetType() call per invocation
- `ConsumeAsync` in TestApplicationResult fires for EVERY test node update including InProgress
- `CancelledTestNodeStateProperty` is obsolete (`[Obsolete]`) — must use pragma wrapper
- SA1009: closing `)` must not be on its own line preceded by space; put `)` at end of last condition line or use local bool variable
- HangDump pattern: put `#pragma disable` before the line with `CancelledTestNodeStateProperty` in the `is` chain, then `#pragma restore` on the next line; works as long as `)` follows on a different line with more conditions
- PropertyBag `SingleOrDefault()` already has O(1) `_testNodeStateProperty` fast-path for single-state nodes
- TestMethodInfo.RunTestAsync uses index iteration not queue/stack since PR #9257
