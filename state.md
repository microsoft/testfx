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

- Branch perf-assist/defer-class-cleanup-context-alloc — PR #9461 (submitted 2026-06-26)
  - Defers testContextForClassCleanup allocation inside if(isLastTestInClass) block
  - Saves C*(K-1) TestContextImplementation allocs per run (one dict copy + CancellationTokenRegistration each)
  - Linux Debug CI failed — code invariant analyzed as correct; likely pre-existing flaky test
  - Status: awaiting maintainer review

- Branch perf-assist/avoid-empty-snapshot-alloc — PR submitted 2026-06-27 (number TBD)
  - CaptureLifecycleProperties now returns null when no non-label properties captured (common case)
  - Avoids 2 allocations (Dictionary + ReadOnlyDictionary) + 2 lock acquisitions per test
  - Files: TestContextImplementation.cs + TestContextImplementationTests.cs + TestAssemblyInfoTests.cs + TestClassInfoTests.cs
  - Safety: PostAssemblyInitProperties/PostClassInitProperties already nullable; MergeProperties(null) returns early

## Optimization Backlog

Priority | Item
---------|-----
Done | PR #9159, #9257, #9299, #9311, #9348, #9433, #9450 merged
Pending | PR #9461 — defer class-cleanup alloc (awaiting review)
Pending | PR (2026-06-27) — skip CaptureLifecycleProperties allocs when empty
Low | AntiTerminal.StopUpdate() _stringBuilder.ToString() on flush (blocked on IConsole/netstandard2.0)
Low | SilenceDrivenHeartbeatRenderer — only heartbeat/slow-test path
Very Low | ClassifyOutcome in TestResultCaptureHelper.cs — Array.IndexOf fallback for CancelledTestNodeStateProperty

## Performance Notes

- TestContextImplementation ctor copies testContextProperties dict + registers CancellationTokenRegistration — non-trivial per-test cost
- TestablePlatformServiceProvider.GetTestContextCallCount tracks allocation counts in unit tests
- MarkClassComplete is only called inside if(isLastTestInClass) — ShouldRunEndOfAssemblyCleanup invariant
- CaptureLifecycleProperties: internal method on TestContextImplementation; return type now nullable (null = no user properties set)
- efficiency-improver bot also operates on this repo — check for duplicate opportunities before creating PRs
- Acceptance tests need -pack first; unit tests do not

## Task Schedule (last run dates)

- Task 1 (Commands): 2026-06-25
- Task 2 (Identify): 2026-06-27 ✓ this run
- Task 3 (Implement): 2026-06-27 ✓ this run
- Task 4 (Maintain PRs): 2026-06-27 ✓ this run
- Task 5 (Comment issues): 2026-06-24
- Task 6 (Infra): 2026-06-21
- Task 7 (Monthly Summary): 2026-06-27 ✓ this run (issue #9258)

## Monthly Activity Issue

Issue #9258: [perf-improver] Monthly Activity 2026-06 (open)
Last updated: 2026-06-27 run 28291508097

## Backlog Cursor

Scanned TestContextImplementation, TestAssemblyInfo, TestClassInfo, UnitTestRunner hot paths.
Next: consider Task 5 (Comment performance issues) or Task 6 (infra) — both overdue since 2026-06-21/24.
