# Perf Improver State

## Validated Commands
```sh
./build.sh                              # restore + build (Debug)
./build.sh -test                        # build + unit tests
./build.sh -pack                        # produce NuGet packages
./build.sh -pack -test -integrationTest # full suite (slow)
# SDK: 11.0.100-preview.7.26376.106 (bootstrapped via ./build.sh into .dotnet/)
# Note: MSTestAdapter.UnitTests and other net48-targeting tests can't run in Linux env (no .NET Framework SDK)
```

## Task Schedule (last run dates)
- Task 1 (Discover Commands): 2026-07-30
- Task 2 (Identify Opportunities): 2026-08-01
- Task 3 (Implement): 2026-08-01
- Task 4 (Maintain PRs): 2026-07-31
- Task 5 (Comment Issues): 2026-07-27
- Task 6 (Infrastructure): 2026-07-28
- Task 7 (Monthly Summary): 2026-08-01

## Monthly Activity Issue
- Issue #9604 (July 2026, open) — transition to August issue next run

## Work In Progress
None

## Optimization Backlog (low priority)
1. `DotnetTestHttpClient`: `new byte[1]` trailing-byte check → `ReadByte()`. Very low priority.
2. `SilenceDrivenHeartbeatRenderer.BuildSlowTestDescription`: `new StringBuilder()` per slow-test event. Very low priority.
3. `AntiTerminal.StopUpdate()`: `_stringBuilder.ToString()` on every flush. Blocked by IConsole + netstandard2.0 compat.
4. OpenTelemetryResultHandler: `GetFullyQualifiedName()` allocates string per test result - only matters for OTel users; low priority.

## Performance Notes
- TestMethod: FullyQualifiedName, ManagedTypeName use C# 13 `field ??=` caching
- ReflectionOperations: has `_attributeCache` via ConcurrentDictionary
- PropertyBag: well-optimized with struct enumerators; internal `GetStructEnumerator()` for zero-alloc iteration
- IPC BaseSerializer: already uses ArrayPool and stackalloc
- Static readonly fields in this codebase: PascalCase (SA1311); collection expression `[]` preferred (IDE0028)
- MSTestTestNodeConverter: ParsedManagedName cached per TestMethod via ConditionalWeakTable; TestMethodIdentifierProperty cached in ParsedManagedName for parameterless case
- Backlog is now very slim — codebase is well-optimized for hot paths

## Completed Work (this month)
### July 2026
- PR #10032 merged: avoid per-test string allocations in TestCaseExtensions
- PR #10230 merged: cache FullyQualifiedName in TestMethod
- PR #10243 merged: cache ManagedTypeName on TestMethod
- PR #10265 merged: GetUpperCaseName in InternalSyncLog (avoid Enum.ToString())
- PR #10366 merged: cache TestMethodIdentifierProperty per TestMethod in MSTestTestNodeConverter (ParsedManagedName ConditionalWeakTable)
- PR #10074 merged: cache analyzer SupportedDiagnostics
- PR #10201 merged: eliminate per-test closure allocations in IPC deserializers
- PR #10089 merged: replace Array.IndexOf state checks with direct type patterns
### August 2026
- PR #aw_pr_paramless created 2026-08-01: cache TestMethodIdentifierProperty in ParsedManagedName for parameterless tests
  Branch: perf-assist/cache-parameterless-test-method-identifier-property

## Checked-off by Maintainer (do not re-suggest)
(none yet for August 2026)
