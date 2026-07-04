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
NOTE: MSTestAdapter.PlatformServices.UnitTests only builds on Windows (requires .NET Framework TFMs).
NOTE: NonWindowsTests.slnf covers only MTP/Analyzer unit tests on Linux.

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

- Branch perf-assist/skip-clone-dict-alloc — PR #aw_clone_alloc submitted 2026-07-04
  - Skip intermediate Dictionary allocation in CloneForDataDrivenIteration
  - Pass _properties directly to ctor; ctor copies in its null/null branch
  - Saves 1 Dictionary alloc + O(n) copy per data-driven test iteration
  - Build: awaiting CI
  - Status: awaiting CI

- Branch perf-assist/skip-tcs-no-exec-context — PR #aw_tcs_fast submitted 2026-07-04
  - Fast path in ExecuteTestAsync when capturedContext == null
  - Skips TaskCompletionSource<TestResult[]> + async-lambda closure + Action delegate allocs
  - ~3 heap allocs fewer per test in the common case
  - Build: awaiting CI
  - Status: awaiting CI

## Optimization Backlog

Priority | Item
---------|-----
Done | PR #9159, #9257, #9299, #9311, #9348, #9433, #9450, #9461, #9478, #9486, #9507 merged
Submitted | PR (2026-07-04) — skip intermediate dict alloc in CloneForDataDrivenIteration
Submitted | PR (2026-07-04) — skip TCS bridge in ExecuteTestAsync when ctx==null
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
- ExecuteTestAsync TCS bridge: only needed when TestClassInfo.ExecutionContext or TestAssemblyInfo.ExecutionContext is non-null (i.e., when [AssemblyInitialize]/[ClassInitialize] captured an ExecutionContext)
- GitHub MCP tools return 403/token lifetime error in CI agent — use git log + local code analysis only
- CloneForDataDrivenIteration: ctor null/null branch always copies via [with(properties)]; no snapshot needed
- IDE0008 rule: use explicit types for new locals (not var) — unless csharp_style_var_when_type_is_apparent applies (e.g. new Foo() on right side)
- csharp_style_var_elsewhere = false:warning — do NOT use var for method results/local assignments where type not apparent
- 2026-07-03 run PRs never created due to id-token:read bug in agentic workflow (fixed by PR #9574/9577)

## Task Schedule (last run dates)

- Task 1 (Commands): 2026-06-25
- Task 2 (Identify): 2026-06-30
- Task 3 (Implement): 2026-07-04 ✓ (2 PRs submitted)
- Task 4 (Maintain PRs): 2026-07-04 ✓ (confirmed no open PRs, re-submitted)
- Task 5 (Comment issues): 2026-06-28
- Task 6 (Infra): 2026-06-28
- Task 7 (Monthly Summary): 2026-07-04 ✓ (closed June issue #9258, created July #aw_jul2607)

## Monthly Activity Issue

Issue #9258: [perf-improver] Monthly Activity 2026-06 (CLOSED 2026-07-04)
July issue: #aw_jul2607 (created 2026-07-04 run 28708586488)

## Backlog Cursor

Two new PRs submitted. Next priority tasks:
- Task 4 (Maintain PRs) — check both pending PRs next run
- Task 5 (Comment on performance issues) — last done 2026-06-28 (oldest)
- Task 6 (Perf infra) — last done 2026-06-28 (oldest)
