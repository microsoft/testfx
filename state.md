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
- PR #9461 merged (2026-06-29): Defer class-cleanup TestContextImplementation allocation to last test only
- PR #9478 merged (2026-06-28): Skip Dictionary + ReadOnlyDictionary alloc in CaptureLifecycleProperties when empty
- PR #9486 merged (2026-06-29): Add dotnet test server-mode scenario to performance runner
- PR #9507 merged (2026-06-29): Avoid List<TestResult> allocation in RunTestMethodAsync non-data-driven fast path

## Open Work

- Branch perf-assist/skip-tcs-no-exec-context — PR submitted 2026-07-01 (number TBD)
  - Fast path in ExecuteTestAsync when no ExecutionContext (no AssemblyInitialize/ClassInitialize captured ctx)
  - Skips TaskCompletionSource<TestResult[]> + async-lambda closure + Action delegate allocs
  - ~160 B fewer allocations per test in the common case
  - Status: awaiting CI

## Optimization Backlog

Priority | Item
---------|-----
Done | PR #9159, #9257, #9299, #9311, #9348, #9433, #9450, #9461, #9478, #9486, #9507 merged
Pending | PR (2026-07-01) — skip TCS bridge in ExecuteTestAsync when ctx == null (awaiting CI)
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
- DotnetTestProcess step: TotalProcessorTime = parent dotnet only; ElapsedTime is the primary user-visible metric
- RunTestMethodAsync: fast path now covers both non-data-driven and null-ctx cases
- ExecuteTestAsync TCS bridge: only needed when TestClassInfo.ExecutionContext or TestAssemblyInfo.ExecutionContext is non-null (i.e., when [AssemblyInitialize]/[ClassInitialize] captured an ExecutionContext)
- GitHub MCP tools return 403 when run in this CI agent (token lifetime constraint) — use git log + local code analysis only

## Task Schedule (last run dates)

- Task 1 (Commands): 2026-06-25
- Task 2 (Identify): 2026-06-30
- Task 3 (Implement): 2026-07-01 ✓ this run
- Task 4 (Maintain PRs): 2026-06-30
- Task 5 (Comment issues): 2026-06-28
- Task 6 (Infra): 2026-06-28
- Task 7 (Monthly Summary): 2026-07-01 ✓ this run (closed #9258, created July issue)

## Monthly Activity Issue

Issue #9258: [perf-improver] Monthly Activity 2026-06 (CLOSED 2026-07-01)
July issue: [perf-improver] Monthly Activity 2026-07 (created 2026-07-01, number TBD)
Last updated: 2026-07-01 run 28524086228

## Backlog Cursor

TCS bridge fast path PR submitted 2026-07-01. Next priority tasks:
- Task 5 (Comment on performance issues) — last done 2026-06-28
- Task 6 (Perf infra) — last done 2026-06-28
- Task 4 (Maintain PRs) — check TCS bridge PR status next run
