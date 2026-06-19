# Perf Improver State — microsoft/testfx

## Last Updated
2026-06-17

## Last Run
- Date: 2026-06-17
- Run ID: 27831457042
- Tasks done: Task 4 (no open PRs), Task 5 (no new perf issues), Task 3 (new PR), Task 7 (new monthly issue)
- Next run priority: Task 1/2 (re-scan backlog), Task 6 (measurement infra), Task 7

## Validated Commands

```sh
# Build (Debug, all projects)
./build.sh

# Build + unit tests
./build.sh -test

# Pack NuGet
./build.sh -pack

# Integration tests (needs -pack first)
./build.sh -pack -test -integrationTest

# Direct test run (single project, single TFM)
.dotnet/dotnet run --project <path> -f net9.0 --no-build -p:TargetFrameworks=net9.0 -- --treenode-filter "*/*/ClassName/*"
```

Notes:
- SDK is installed by build.sh into .dotnet/ (v11 preview)
- `global.json` enforces SDK version; system `dotnet` is a stub
- MSTestAdapter.PlatformServices.UnitTests requires `-p:TargetFrameworks=net9.0` to filter out net462/net48 TFMs
- 24 pre-existing failures in MSTestAdapter.PlatformServices.UnitTests (deployment-path tests)

## Open Work in Progress

None — PR created for per-test Queue/Stack allocation fix.

## Optimization Backlog

1. [IN PR] Per-test Queue/Stack allocation in TestMethodInfo.Lifecycle.cs
   - Branch: perf-assist/list-backed-base-method-queues
   - Change Queue→List; foreach for cleanup, backward for-loop for init
   - Affects every test with base-class TestInitialize/TestCleanup

2. AnsiTerminal.StopUpdate() — StringBuilder.ToString() allocation
   - Blocked: IConsole has Write(string?) only; StreamWriter.Write(StringBuilder) is NET8+, project targets netstandard2.0
   - Low priority

3. SilenceDrivenHeartbeatRenderer — allocations only on heartbeat/slow-test rare path
   - Not hot path, low priority

## Completed Work

| PR | Title | Status |
|---|---|---|
| #9159 | perf: single-pass PropertyBag walk in TerminalOutputDevice | MERGED 2026-06-16 by Evangelink |
| perf-assist/list-backed-base-method-queues | perf: replace per-test Queue/Stack with List index iteration | OPEN (created 2026-06-17) |

## Performance Notes

- PropertyBag.SingleOrDefault<TestNodeStateProperty>() is O(1) — _testNodeStateProperty field fast path prevents list walk. All current callers of SingleOrDefault<TestNodeStateProperty>() are already on the fast path.
- BaseTestInitializeMethodsQueue/BaseTestCleanupMethodsQueue: items added in parent-first order (direct parent = index 0). Cleanup iterates forward (parent-first); initialize iterates backward (grandparent-first).
- MSTestAdapter.PlatformServices.UnitTests uses TestContainer framework + AwesomeAssertions (MSTest Assert banned).

## Backlog Cursor

Next areas to scan:
- HtmlReport extension (rendering hot paths)
- TrxReport extension (per-test string allocations)
- Retry extension (state tracking allocations)
- MSTest TestContext — string dictionary allocations
