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
| 3    | 2026-07-19  |
| 4    | 2026-07-19  |
| 5    | 2026-07-08  |
| 6    | 2026-07-13  |
| 7    | 2026-07-19  |

Next priority: Tasks 5, 6 (oldest: 2026-07-08, 2026-07-13)

## Completed Work

| Date       | Item                                      | Notes                                                    |
|------------|-------------------------------------------|----------------------------------------------------------|
| 2026-07-19 | PR submitted: cache-supported-diagnostics | Cache SupportedDiagnostics in 2 analyzer outliers        |
| 2026-07-17 | PR #10032 merged (by community)           | Avoid per-test string allocations in TestCaseExtensions  |
| 2026-07-15 | PR submitted: avoid-propertybag-scan      | Avoid PropertyBag scan in AddTrxResultProperties (#aw_pr_methodid — bundle may be lost) |
| 2026-07-14 | PR submitted: skip-method-id-in-progress  | Skip TestMethodIdentifier for in-progress nodes          |
| 2026-07-10 | PR #9800 merged (by Evangelink)           | Cache GetTestId on UnitTestElement                       |
| 2026-07-08 | PR #9728 merged                           | Scenario2 data-driven + JsonSerializerOptions caching    |

## Work In Progress

- Branch `perf-assist/cache-supported-diagnostics`: Cache SupportedDiagnostics in CollectionAssertToAssertAnalyzer and StringAssertToAssertAnalyzer.
  PR submitted 2026-07-19 as #aw_pr_cache_diag. Fixes #10055.

## Monthly Activity Issue

- **July 2026**: #9604 (open)

## Performance Opportunities Backlog

Priority order (highest first):

1. `AntiTerminal.StopUpdate()` — `_stringBuilder.ToString()` on every flush.
   Blocked: IConsole abstraction + netstandard2.0 compat. Low priority.

2. `SilenceDrivenHeartbeatRenderer` — allocations on rare heartbeat paths. Low priority.

3. `ClassifyOutcome` in `TestResultCaptureHelper.cs` — `Array.IndexOf` fallback. Very low priority.

## Key Notes

- GetTestId() caching is in place (PR #9800 merged). `CachedTestNodeUid` on UnitTestElement.
- 15 of 17 MSTest analyzers already use auto-initialized SupportedDiagnostics. Fixed the 2 outliers in 2026-07-19 run.
- PR #10032 (merged 2026-07-17): Avoid per-test string allocations in TestCaseExtensions — not from perf-improver but related work.
- The "efficiency-improver" workflow is ALSO active on this repo, generating `efficiency/*` branches.

## Previously Closed/Actioned Items (do not re-suggest)

- PR #9617 — merged
- PR #9636 — merged
- PR #9728 — merged
- Issues #9602/#9603 — closed
- Issues #9713/#9714 — closed by Evangelink 2026-07-08
- PR #9800 — merged by Evangelink 2026-07-10 (GetTestId caching)
- PR #10032 — merged 2026-07-17 (string allocs in TestCaseExtensions)
