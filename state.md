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
| 3    | 2026-07-09  |
| 4    | 2026-07-09  |
| 5    | 2026-07-08  |
| 6    | 2026-07-07  |
| 7    | 2026-07-09  |

Next priority: Tasks 5 and 6 (oldest)

## Completed Work

| Date       | Item                                  | Notes                                      |
|------------|---------------------------------------|--------------------------------------------|
| 2026-07-09 | PR #aw_pr_uid_cache (TBD number)      | Cache GetTestId on UnitTestElement (native MTP path) |
| 2026-07-08 | PR #9728 merged                       | Scenario2 data-driven + JsonSerializerOptions caching |
| 2026-07-08 | PR #9706 merged                       | Native MTP integration (RFC 018), experimental |
| 2026-07-07 | PR #9617 merged                       | All 4 data-driven hot-path optimisations   |
| 2026-07-07 | PR #9636 merged                       | TCS fast-path skip                         |
| 2026-07-07 | PR #9729 merged                       | TestMethodRunner split into partial classes|
| 2026-07-07 | Issues #9602/#9603 closed             | Resolved by PR #9617/#9636                 |
| 2026-07-08 | Issues #9713/#9714 closed             | Closed by Evangelink 2026-07-08            |

## Work In Progress

None.

## Monthly Activity Issue

- **July 2026**: #9604 (open)

## Performance Opportunities Backlog

Priority order (highest first):

1. `AntiTerminal.StopUpdate()` — `_stringBuilder.ToString()` on every flush.
   Blocked: IConsole abstraction + netstandard2.0 compat. Low priority.

2. `SilenceDrivenHeartbeatRenderer` — allocations on rare heartbeat paths. Low priority.

3. `ClassifyOutcome` in `TestResultCaptureHelper.cs` — `Array.IndexOf` fallback. Very low priority.

## Key Notes

- Native MTP path (MSTestTestFramework) is now THE default for all MTP runs —
  no more experimental flag. MSTestTestNodeConverter.CreateBaseTestNode calls
  GetTestId() for discovered/in-progress/result nodes.
- GetTestId() caching PR submitted 2026-07-09 (branch: perf-assist/cache-test-node-uid).
  Saves 1 hash+alloc per test per execution run (RecordStart+RecordResult share same element).
- The "efficiency-improver" workflow is ALSO active on this repo, generating `efficiency/*` branches.
  These are separate from `perf-assist/*` branches. Do not duplicate their work.
- TestMethodRunner was split into partial class files in PR #9729 (2026-07-07):
  TestMethodRunner.DataRow.cs, TestMethodRunner.Execution.cs, etc.
- Issues #9713 and #9714 (from efficiency-improver) closed by Evangelink 2026-07-08.

## Previously Closed/Actioned Items (do not re-suggest)

- PR #9617 (CloneForDataDrivenIteration + related) — merged
- PR #9636 (TCS fast-path) — merged
- Issues #9602/#9603 — closed (resolved by above PRs)
- Scenario2 data-driven perf scenario — merged as PR #9728
- JsonSerializerOptions caching in PlainProcess/DotnetTestProcess — merged as PR #9728
- Issues #9713/#9714 — closed by Evangelink 2026-07-08
