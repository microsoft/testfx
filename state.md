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
- Task 2 (Identify Opportunities): 2026-08-11 (explore-agent scan of Assert*.cs, MSTest.TestAdapter/Execution, Platform Hosts/Requests, DataRow/DynamicData attrs, Analyzers hot paths — found TelemetryCollector.TrackAssertionCall ConcurrentDictionary.AddOrUpdate contention, fixed same run; secondary: Assert.HasCount non-generic overload Cast<object>() not fast-pathing ICollection.Count, low priority/not fixed)
- Task 3 (Implement): 2026-08-11 (PR: "Reduce TelemetryCollector.TrackAssertionCall contention on the assertion hot path" — replaced ConcurrentDictionary<string,long>.AddOrUpdate with ConcurrentDictionary<string,StrongBox<long>>.GetOrAdd + Interlocked.Increment; microbenchmark 2M iters: 197ms->23ms single-threaded (~8.6x), 168ms->34ms under 4-thread contention (~5x); TestFramework.UnitTests 1506/1506 pass)
- Task 4 (Maintain PRs): 2026-08-10 (no open perf-improver PRs at that time; PR #10543 "Reduce MTP command-line validation allocations" from a human/other contributor also open, not perf-improver's)
- Task 5 (Comment Issues): 2026-08-10 (no open performance-labeled issues found via search_issues)
- Task 6 (Infrastructure): 2026-08-05 (reviewed perf-timing-nightly.yml + MSTest.Performance.Runner; infra already solid — PlainProcess timing, cross-platform)
- Task 7 (Monthly Summary): 2026-08-11

## Monthly Activity Issue
- Issue #10381 (August 2026, open) — kept updated; no suggested actions pending

## Work In Progress
None. New PR created 2026-08-11: "Reduce TelemetryCollector.TrackAssertionCall contention on the assertion hot path" (branch perf-assist/telemetry-collector-counter). No other open perf-improver PRs or performance-labeled issues as of 2026-08-11.

## Optimization Backlog (low priority)
1. `ServiceProvider.GetServiceInternal`/`RegisterCoverageProducer` (src/Platform/Microsoft.Testing.Platform/Services/ServiceProvider.cs): uses `FirstOrDefault`/`OfType<IDataProducer>()` LINQ per service lookup/registration. Cold/startup-only path (not per-test), low value — noted but not prioritized.
2. `DotnetTestHttpClient`: `new byte[1]` trailing-byte check → won't-fix (see issue #10381 comments; reverted after review, not worth the coupling).
3. `SilenceDrivenHeartbeatRenderer.BuildSlowTestDescription`: done (lazy StringBuilder) — see PR #10384.
4. `AntiTerminal.StopUpdate()`: done (IConsole.Write(StringBuilder) overload) — see PR #10384.
5. OpenTelemetryResultHandler.GetFullyQualifiedName(): won't-fix — see issue #10381 comments.
6. `Assert.HasCount.cs` non-generic `HasCount(string, int, IEnumerable, ...)` overload: falls through to `collection.Cast<object>()` then LINQ `Count()` without an `ICollection.Count` fast path first. Only hit for non-generic-collection overloads (minor), low priority — noted 2026-08-11, not fixed yet.


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
- 2026-08-09: Scanned Platform Extensions (TrxReport/HtmlReport/AzureDevOpsReport/CrashDump/HangDump), Configurations, Services/DI (ServiceProvider), MSTest.TestAdapter execution path, Analyzers, TestFramework Assert/DataRow. Found and fixed `TestDataSourceUtilities.ComputeDefaultDisplayName`/`GetHumanizedArguments`: LINQ `Select`+`Join` and `Cast<object>()` boxing enumeration replaced with a direct loop over a reused `StringBuilder`. Runs once per data-driven ([DataRow]/[DynamicData]) test case. Microbenchmark (200k iters, mixed-type array): 371ms/103MB alloc -> 223ms/84MB alloc (~40% faster, ~18% less alloc). Output verified byte-identical to old implementation. DataRowAttributeTests (17/17) pass. `ServiceProvider.GetServiceInternal`/`RegisterCoverageProducer` also use LINQ but are startup/cold-path only — added to backlog as low-value, not fixed this run.
- 2026-08-10: PR #10528 (the display-name allocation fix) is now closed. Re-scanned CommandLine/*.cs LINQ usage (Where/GroupBy/Select on CLI options — all one-shot at startup, not hot), TreeNodeFilter regex (already per-ValueExpression cached, RegexOptions.Compiled), CursorProgressRenderer (500ms tick interval, no per-frame allocation concerns), TerminalTestReporter.*.cs string.Format call sites (Coverage/FlakyTests/Summary/TestDiscovery — all cold reporting paths, not per-test hot loops). No new optimization targets found. Backlog remains empty; no open perf-labeled issues or perf-improver PRs.

## Checked-off by Maintainer (do not re-suggest)
(none yet for August 2026)
