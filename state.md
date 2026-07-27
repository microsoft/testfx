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
| 3    | 2026-07-27  |
| 4    | 2026-07-27  |
| 5    | 2026-07-27  |
| 6    | 2026-07-13  |
| 7    | 2026-07-27  |

Next priority: Task 6 (oldest: 2026-07-13)

## Completed Work

| Date       | Item                                           | Notes                                                       |
|------------|------------------------------------------------|-------------------------------------------------------------|
| 2026-07-27 | PR submitted: sync-log-upper-case-name         | GetUpperCaseName in InternalSyncLog (avoid Enum.ToString()) |
| 2026-07-27 | PR #10243 merged (by Evangelink)               | Cache ManagedTypeName on TestMethod                         |
| 2026-07-26 | PR #10230 merged (by Evangelink)               | Cache FullyQualifiedName on TestMethod                      |
| 2026-07-21 | PR submitted: ipc-serializer-stackalloc        | stackalloc + BinaryPrimitives for BaseSerializer (closed)   |
| 2026-07-19 | PR submitted: cache-supported-diagnostics      | Cache SupportedDiagnostics (closed)                         |
| 2026-07-17 | PR #10032 merged (by community)                | Avoid per-test string allocations in TestCaseExtensions     |
| 2026-07-15 | PR submitted: avoid-propertybag-scan           | Avoid PropertyBag scan in AddTrxResultProperties            |
| 2026-07-14 | PR submitted: skip-method-id-in-progress       | Skip TestMethodIdentifier for in-progress nodes             |
| 2026-07-10 | PR #9800 merged (by Evangelink)                | Cache GetTestId on UnitTestElement                          |
| 2026-07-08 | PR #9728 merged                                | Scenario2 data-driven + JsonSerializerOptions caching       |

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

## Performance Notes

- `TestMethod.FullyQualifiedName` (PR #10230) and `ManagedTypeName` (new PR) both use C# 13 field-backed
  property `field ??= ...` with `[field: NonSerialized]` guard on NETFRAMEWORK.
- Perf runner uses PlainProcess pipelines cross-platform; PerfView/VSDiagnostics are Windows-only.
- `./build.sh -test` is the single step that builds + runs all unit tests.
