# Perf Improver — State for microsoft/testfx

## Validated Commands

```sh
# Build all projects (Debug)
./build.sh

# Build + run unit tests
./build.sh -test

# Pack NuGet packages (required before acceptance tests and performance runner)
./build.sh -pack

# Run acceptance integration tests (requires -pack first)
./build.sh -pack -test -integrationTest

# Run performance timing scenarios (requires -pack first)
.dotnet/dotnet run --project test/Performance/MSTest.Performance.Runner \
  -- execute --pipelineNameFilter "*PlainProcess*"

# Run a single test project
.dotnet/dotnet run --project <project-path> -f net9.0 --no-build -- --treenode-filter "*/*/MyTestClass/*"
```

NOTE: Local SDK (.dotnet/) is NOT available in CI agent. Build/test must go through CI.
NOTE: AwesomeAssertions (FluentAssertions-style) is used in MSTestAdapter.PlatformServices.UnitTests — NOT MSTest Assert.

## Completed Work

- PR #9159 merged: perf: single-pass PropertyBag walk in TerminalOutputDevice
- PR #9257 merged: perf: replace per-test Queue/Stack allocation in TestMethodInfo lifecycle
- PR #9299 merged: Replace Array.IndexOf(GetType()) with is pattern matching in TestApplicationResult etc.
- PR #9311 merged: Extend performance runner to Linux/macOS; add ClassLevel variant
- PR #9348 merged: Avoid redundant TestNodeUid allocation in server mode
- PR #9433 merged (2026-06-26): Skip unused TestContextImplementation allocs in RunSingleTestAsync (assembly/class init fast path)
- PR #9450 merged (2026-06-26): Add Linux job to nightly perf-timing workflow

## Open Work

- Branch perf-assist/defer-class-cleanup-context-alloc — PR submitted 2026-06-26
  - Defers testContextForClassCleanup allocation inside if(isLastTestInClass) block
  - Saves C*(K-1) TestContextImplementation allocs per run (one dict copy + CancellationTokenRegistration each)
  - Files: UnitTestRunner.cs + UnitTestRunnerTests.cs
  - Safety: ShouldRunEndOfAssemblyCleanup can only be true when isLastTestInClass is true (MarkClassComplete only called in that block)

## Optimization Backlog

Priority | Item
---------|-----
Done | PR #9159, #9257, #9299, #9311, #9348, #9433, #9450 merged
Pending | defer-class-cleanup-context-alloc (PR submitted 2026-06-26)
Low | AntiTerminal.StopUpdate() _stringBuilder.ToString() on flush (blocked on IConsole/netstandard2.0)
Low | SilenceDrivenHeartbeatRenderer — only heartbeat/slow-test path
Very Low | ClassifyOutcome in TestResultCaptureHelper.cs — Array.IndexOf fallback for CancelledTestNodeStateProperty

## Performance Notes

- TestContextImplementation ctor copies testContextProperties dict + registers CancellationTokenRegistration — non-trivial per-test cost
- TestablePlatformServiceProvider.GetTestContextCallCount tracks allocation counts in unit tests
- MarkClassComplete is only called inside if(isLastTestInClass) — ShouldRunEndOfAssemblyCleanup invariant
- efficiency-improver bot also operates on this repo — check for duplicate opportunities before creating PRs
- Acceptance tests need -pack first; unit tests do not

## Task Schedule (last run dates)

- Task 1 (Commands): 2026-06-25
- Task 2 (Identify): 2026-06-26 ✓ this run
- Task 3 (Implement): 2026-06-26 ✓ this run
- Task 4 (Maintain PRs): 2026-06-26 ✓ this run
- Task 5 (Comment issues): 2026-06-24
- Task 6 (Infra): 2026-06-21
- Task 7 (Monthly Summary): 2026-06-26 ✓ this run (issue #9258)

## Monthly Activity Issue

Issue #9258: [perf-improver] Monthly Activity 2026-06 (open)
Last updated: 2026-06-26 run 28243694549

## Backlog Cursor

Scanned all hot paths in UnitTestRunner.cs, TestMethodRunner.cs, ClassCleanupManager.cs.
Next: consider TestMethodInfo.Lifecycle.cs for remaining alloc opportunities (verify nothing missed).
