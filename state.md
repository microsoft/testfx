# Perf Improver State

## Validated Commands
```sh
./build.sh                              # restore + build (Debug)
./build.sh -test                        # build + unit tests
./build.sh -pack                        # produce NuGet packages
./build.sh -pack -test -integrationTest # full suite (slow)
# SDK: 11.0.100-preview.7.26376.106 (bootstrapped via ./build.sh into .dotnet/)
```

## Task Schedule (last run dates)
- Task 1 (Discover Commands): 2026-07-30
- Task 2 (Identify Opportunities): 2026-07-30
- Task 3 (Implement): 2026-07-31
- Task 4 (Maintain PRs): 2026-07-31
- Task 5 (Comment Issues): 2026-07-27
- Task 6 (Infrastructure): 2026-07-28
- Task 7 (Monthly Summary): 2026-07-31

## Monthly Activity Issue
- Issue #9604 (July 2026, open)

## Work In Progress
- PR #aw_pr_cache created 2026-07-31: cache TestMethodIdentifierProperty per TestMethod in MSTestTestNodeConverter
  Branch: perf-assist/cache-test-method-identifier-property

## Optimization Backlog (low priority)
1. `DotnetTestHttpClient`: `new byte[1]` trailing-byte check → `ReadByte()`. Very low priority.
2. `SilenceDrivenHeartbeatRenderer.BuildSlowTestDescription`: `new StringBuilder()` per slow-test event. Very low priority.
3. `AntiTerminal.StopUpdate()`: `_stringBuilder.ToString()` on every flush. Blocked by IConsole + netstandard2.0 compat.

## Performance Notes
- TestMethod: FullyQualifiedName, ManagedTypeName use C# 13 `field ??=` caching with `[field: NonSerialized]` guard on NETFRAMEWORK
- ReflectionOperations: has `_attributeCache` via ConcurrentDictionary
- PropertyBag: already well-optimized with struct enumerators
- IPC BaseSerializer: already uses ArrayPool and stackalloc
- Static readonly fields in this codebase: PascalCase (SA1311); collection expression `[]` preferred (IDE0028)
- MSTestTestNodeConverter: now caches TestMethodIdentifierProperty via ConditionalWeakTable

## Completed Work (this month)
- PR #10032 merged: avoid per-test string allocations in TestCaseExtensions
- PR #10230 merged: cache FullyQualifiedName in TestMethod
- PR #10243 merged: cache ManagedTypeName on TestMethod
- PR #10265 merged: GetUpperCaseName in InternalSyncLog (avoid Enum.ToString())
- PR #aw_pr_cache submitted: cache TestMethodIdentifierProperty in MSTestTestNodeConverter

## Checked-off by Maintainer (do not re-suggest)
(none yet for July 2026)
