# Perf Improver — Repo Memory

## Validated Commands

```sh
./build.sh                    # restore + build (Debug)
./build.sh -test              # build + unit tests
./build.sh -pack              # produce NuGet packages
./build.sh -pack -test -integrationTest  # full suite (slow)

# Perf runner (requires -pack first)
.dotnet/dotnet run --project test/Performance/MSTest.Performance.Runner \
  -- execute --pipelineNameFilter "*PlainProcess*"

# Single test project
.dotnet/dotnet run --project <proj> -f net9.0 --no-build \
  -- --treenode-filter "*/*/MyTestClass/MyTestMethod"
```

## Task Schedule (last run dates)

| Task | Last Run     |
|------|-------------|
| 1    | 2026-07-04  |
| 2    | 2026-07-08  |
| 3    | 2026-07-13  |
| 4    | 2026-07-11  |
| 5    | 2026-07-08  |
| 6    | 2026-07-13  |
| 7    | 2026-07-13  |

Next priority: Tasks 1, 4, 5 (oldest: 2026-07-04, 2026-07-11, 2026-07-08)

## Completed Work

| Date       | Item                                  | Notes                                      |
|------------|---------------------------------------|--------------------------------------------|
| 2026-07-13 | PR #aw_pr_scenario4 submitted         | Scenario4 class-init overhead benchmark    |
| 2026-07-11 | PR #aw_pr_skip_inprog submitted       | Skip TestMethodIdentifierProperty for in-progress nodes |
| 2026-07-10 | PR #9800 merged (by Evangelink)       | Cache GetTestId on UnitTestElement         |
| 2026-07-10 | WIP branch lost (lazy testfullname)   | safe-output bundle was lost; no PR created |
| 2026-07-08 | PR #9728 merged                       | Scenario2 data-driven + JsonSerializerOptions caching |
| 2026-07-08 | PR #9706 merged                       | Native MTP integration (RFC 018)           |
| 2026-07-07 | PR #9617 merged                       | All 4 data-driven hot-path optimisations   |
| 2026-07-07 | PR #9636 merged                       | TCS fast-path skip                         |

## Work In Progress

- Branch `perf-assist/skip-method-id-in-progress`: skip AddTestMethodIdentifier
  (ParseManagedMethodName + TestMethodIdentifierProperty alloc) for in-progress nodes.
  Saves ~10,000 parses per 10,000-test run. PR submitted 2026-07-11. Status: unknown (verify next run).
- Branch `perf-assist/scenario4-class-init-overhead-1783953091`: Scenario4 class-init benchmark.
  PR submitted 2026-07-13 (#aw_pr_scenario4). Purely additive to performance runner.

## Monthly Activity Issue

- **July 2026**: #9604 (open)

## Performance Opportunities Backlog

Priority order (highest first):

1. `AntiTerminal.StopUpdate()` — `_stringBuilder.ToString()` on every flush.
   Blocked: IConsole abstraction + netstandard2.0 compat. Low priority.

2. `SilenceDrivenHeartbeatRenderer` — allocations on rare heartbeat paths. Low priority.

3. `ClassifyOutcome` in `TestResultCaptureHelper.cs` — `Array.IndexOf` fallback. Very low priority.

## Key Notes

- PR #9726 (open): removes VSTest support, makes MTP the default. MSTestTestNodeConverter
  is now the primary execution path for all MTP runs.
- GetTestId() caching is in place (PR #9800 merged). `CachedTestNodeUid` on UnitTestElement.
- `testMethod.DisplayName` is always non-null (constructor: `displayName ?? name`). Safe to use directly.
- Pre-existing CA1416 build errors in `FileLoggerTests.cs` on Linux prevent full ./build.sh -test.
  Product code builds fine; unit tests for adapter fail due to net48/net462 NU1201 restore issues.
- The "efficiency-improver" workflow is ALSO active on this repo, generating `efficiency/*` branches.
- Issue #5348 (in-progress + result batch dedup): Efficiency Improver already commented (June 2026).
  No new human activity since then. Not a good candidate for Perf Improver comment.

## Previously Closed/Actioned Items (do not re-suggest)

- PR #9617 — merged
- PR #9636 — merged
- PR #9728 — merged
- Issues #9602/#9603 — closed
- Issues #9713/#9714 — closed by Evangelink 2026-07-08
- PR #9800 — merged by Evangelink 2026-07-10 (GetTestId caching)
