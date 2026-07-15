# Perf Improver — Repo Memory

## Validated Commands

```sh
./build.sh                    # restore + build (Debug)
./build.sh -test              # build + unit tests
./build.sh -pack              # produce NuGet packages
./build.sh -pack -test -integrationTest  # full suite (slow)

# Single test project
dotnet run --project <proj> -f net9.0 --no-build \
  -- --treenode-filter "*/*/MyTestClass/MyTestMethod"
```

## Task Schedule (last run dates)

| Task | Last Run     |
|------|-------------|
| 1    | 2026-07-14  |
| 2    | 2026-07-15  |
| 3    | 2026-07-15  |
| 4    | 2026-07-15  |
| 5    | 2026-07-08  |
| 6    | 2026-07-13  |
| 7    | 2026-07-15  |

Next priority: Tasks 5, 6 (oldest: 2026-07-08, 2026-07-13)

## Completed Work

| Date       | Item                                      | Notes                                                    |
|------------|-------------------------------------------|----------------------------------------------------------|
| 2026-07-15 | PR submitted: cache-test-method-identifier | Avoid PropertyBag scan in AddTrxResultProperties          |
| 2026-07-14 | PR submitted: skip-method-id-in-progress-2 | Skip TestMethodIdentifier for in-progress nodes (status unknown) |
| 2026-07-13 | PR #aw_pr_scenario4 submitted             | Scenario4 class-init overhead benchmark                  |
| 2026-07-11 | PR skip-method-id-in-progress submitted   | Old branch lost; re-submitted 2026-07-14                 |
| 2026-07-10 | PR #9800 merged (by Evangelink)           | Cache GetTestId on UnitTestElement                       |
| 2026-07-08 | PR #9728 merged                           | Scenario2 data-driven + JsonSerializerOptions caching    |
| 2026-07-08 | PR #9706 merged                           | Native MTP integration (RFC 018)                         |
| 2026-07-07 | PR #9617 merged                           | All 4 data-driven hot-path optimisations                 |
| 2026-07-07 | PR #9636 merged                           | TCS fast-path skip                                       |

## Work In Progress

- Branch `perf-assist/cache-test-method-identifier`: avoid `SingleOrDefault<TestMethodIdentifierProperty>()`
  bag scan in `AddTrxResultProperties` by threading the property from `CreateBaseTestNode`.
  PR submitted 2026-07-15.

## Monthly Activity Issue

- **July 2026**: #9604 (open)

## Performance Opportunities Backlog

Priority order (highest first):

1. `AntiTerminal.StopUpdate()` — `_stringBuilder.ToString()` on every flush.
   Blocked: IConsole abstraction + netstandard2.0 compat. Low priority.

2. `SilenceDrivenHeartbeatRenderer` — allocations on rare heartbeat paths. Low priority.

3. `ClassifyOutcome` in `TestResultCaptureHelper.cs` — `Array.IndexOf` fallback. Very low priority.

## Key Notes

- PR #9726 (open): removes VSTest support, makes MTP the default.
- GetTestId() caching is in place (PR #9800 merged). `CachedTestNodeUid` on UnitTestElement.
- `testMethod.DisplayName` is always non-null (constructor: `displayName ?? name`). Safe to use directly.
- Pre-existing CA1416 build errors in `FileLoggerTests.cs` on Linux prevent full ./build.sh -test.
  Product code builds fine.
- The "efficiency-improver" workflow is ALSO active on this repo, generating `efficiency/*` branches.

## Previously Closed/Actioned Items (do not re-suggest)

- PR #9617 — merged
- PR #9636 — merged
- PR #9728 — merged
- Issues #9602/#9603 — closed
- Issues #9713/#9714 — closed by Evangelink 2026-07-08
- PR #9800 — merged by Evangelink 2026-07-10 (GetTestId caching)
