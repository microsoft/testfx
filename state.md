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
- Task 2 (Identify Opportunities): 2026-08-08 (explore-agent scan of ServerMode/JsonRpc, Retry extension, MSTestAdapter.PlatformServices, TestFramework.Extensions — no new hot-path issues found; all either already cached/optimized or low-frequency error/config paths)
- Task 3 (Implement): 2026-08-03 (no viable target found — codebase well-optimized)
- Task 4 (Maintain PRs): 2026-08-08 (no open perf-improver PRs)
- Task 5 (Comment Issues): 2026-08-07 (no open performance-labeled issues found; issue #3495 "Show slowest tests" is effectively implemented via --show-slowest-tests, left as-is since discussion has since pivoted to a broader declarative-duration RFC)
- Task 6 (Infrastructure): 2026-08-05 (reviewed perf-timing-nightly.yml + MSTest.Performance.Runner; infra already solid — PlainProcess timing, cross-platform)
- Task 7 (Monthly Summary): 2026-08-08

## Monthly Activity Issue
- Issue #10381 (August 2026, open) — kept updated; no suggested actions pending

## Work In Progress
None

## Optimization Backlog (low priority)
1. `DotnetTestHttpClient`: `new byte[1]` trailing-byte check → `Memory<byte>` on .NET. Very low priority (cold path).
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
- HumanReadableDurationFormatter: has NET8+ fast path for common case (< 1 hour, no ms) using string.Create
- TestNodeResultsState: caches formatted "N tests running" strings to avoid per-frame re-format
- SingleConsumerUnboundedChannel: well-optimized lock-based channel with early-exit fast path
- Backlog is now very slim — codebase is well-optimized for hot paths
- 2026-08-04: Confirmed ReflectHelper/TypeCache attribute caching already centralized (ReflectionOperations._attributeCache); StackTraceHelper already uses [GeneratedRegex] with static cache fallback for non-source-gen targets. No fresh targets found.
- 2026-08-05: Scanned VSTest discovery pipeline (UnitTestDiscoverer, MSTestDiscovererHelpers) — no LINQ/List allocation hot spots found. TcmTestPropertiesProvider Dictionary pre-sized (capacity:15) already. JsonRpc SerializerUtilities.Select().ToArray() on ev.Attachments is per-event-with-attachments (rare path, not hot). No open perf-labeled issues or perf-improver PRs found this run. Reviewed nightly perf-timing workflow (PlainProcess scenario, Windows+Linux) — solid, no gaps identified.
- 2026-08-07: Scanned recently-merged CtrfReportMerger.cs/.JsonHelpers.cs/.RetryCollapsing.cs and JUnitReportMerger.cs (post-processing/artifact-merge feature, PRs #10506/#10507 area). BuildCommonEnvironment uses O(n²) `environments.All(...)` per field across inputs — acceptable since it's a one-shot CLI merge step over a handful of report files, not a hot per-test path. CtrfReportEngine.JsonSerializer.BuildCtrfJson uses Utf8JsonWriter directly (no intermediate string), already efficient. TestProgressState.GetSlowestTests/RecordTestDuration (backing --show-slowest-tests, issue #3495) is properly gated (`if (_options.SlowestTestsCount > 0)`) so a run without the flag pays zero bookkeeping cost — no perf issue there. No new optimization targets found; backlog remains empty.
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
- 2026-08-03: Deep scan of hot paths; codebase continues to be well-optimized. No new opportunities identified.
- 2026-08-08: Explore-agent scan of ServerMode/Retry/PlatformServices/TestFramework.Extensions — no new targets found; backlog remains empty.

## Checked-off by Maintainer (do not re-suggest)
(none yet for August 2026)
