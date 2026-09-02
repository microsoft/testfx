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
- Task 1 (Discover Commands): 2026-07-30 (still valid)
- Task 2 (Identify Opportunities): 2026-08-30 (no new hot-path findings; backlog unchanged)
- Task 4 (Maintain PRs): 2026-08-29 (no open perf-improver PRs before this run's new PR)
- Task 5 (Comment Issues): 2026-08-29 (no open performance-labeled issues)
- Task 6 (Infrastructure): 2026-08-29 (added TestDataSourceUtilitiesBenchmarks.cs, new draft PR)
- Task 7 (Monthly Summary): 2026-08-29
- Task 2 (Identify Opportunities): 2026-08-13 (explore-agent scan of Retry, CrashDump/HangDump, VSTestBridge, MSTestAdapter.PlatformServices, TestFramework.Extensions — found ObjectModelConverters.FixUpTestCase LINQ Any(lambda) delegate alloc per test case; fixed same run. Other findings: PrivateObject generic-method cache rebuild (net-framework-only, medium risk, not fixed), TestExecutionManager.ParallelExecution per-test array wrapping (medium risk, not fixed), RetryDataConsumer SingleOrDefault already uses optimized PropertyBag method not LINQ (no action needed))
- Task 2 (previous): 2026-08-11 (explore-agent scan of Assert*.cs, MSTest.TestAdapter/Execution, Platform Hosts/Requests, DataRow/DynamicData attrs, Analyzers hot paths — found TelemetryCollector.TrackAssertionCall ConcurrentDictionary.AddOrUpdate contention, fixed same run; secondary: Assert.HasCount non-generic overload Cast<object>() not fast-pathing ICollection.Count, low priority/not fixed)
- Task 3 (Implement): 2026-08-13 (PR: "Avoid LINQ Any() delegate allocation in VSTestBridge FixUpTestCase" — branch perf-assist/fixup-testcase-any-loop; manual foreach replaces Any(lambda) over testCase.Properties, per-test-case hot path in VSTest bridge; microbenchmark 5M calls over 4-item list: 252ms/440MB alloc -> 225ms/0 alloc; VSTestBridge.UnitTests 70/70 pass)
- Task 3 (previous): 2026-08-12 (PR: "Add ICollection fast path to non-generic Assert.HasCount/IsEmpty" — branch perf-assist/hascount-icollection-fastpath; avoids Cast<object>()+LINQ Count() when collection is ICollection; microbenchmark 2M iters over 1000-item ArrayList: 20371ms->15ms (~1350x); TestFramework.UnitTests 1506/1506 pass)
- Task 3 (previous): 2026-08-11 (PR: "Reduce TelemetryCollector.TrackAssertionCall contention on the assertion hot path" — replaced ConcurrentDictionary<string,long>.AddOrUpdate with ConcurrentDictionary<string,StrongBox<long>>.GetOrAdd + Interlocked.Increment; microbenchmark 2M iters: 197ms->23ms single-threaded (~8.6x), 168ms->34ms under 4-thread contention (~5x); TestFramework.UnitTests 1506/1506 pass)
- Task 4 (Maintain PRs): 2026-08-13 (checked open PRs 10560 "Reduce assertion telemetry contention" and 10575 "Optimize non-generic collection count assertions" — these correspond to previously-tracked perf-improver work, both already under human review with state/needs-review label, no CI failures observed needing action from this run; no push made)
- Task 4 (previous): 2026-08-10 (no open perf-improver PRs at that time; PR #10543 "Reduce MTP command-line validation allocations" from a human/other contributor also open, not perf-improver's)
- Task 5 (Comment Issues): 2026-08-10 (no open performance-labeled issues found via search_issues)
- Task 6 (Infrastructure): 2026-08-05 (reviewed perf-timing-nightly.yml + MSTest.Performance.Runner; infra already solid — PlainProcess timing, cross-platform)
- Task 7 (Monthly Summary): 2026-08-12

## Monthly Activity Issue
- Issue #10381 (August 2026, open) — kept updated; no suggested actions pending

## Work In Progress
None. New PR created 2026-08-13: "Avoid LINQ Any() delegate allocation in VSTestBridge FixUpTestCase" (branch perf-assist/fixup-testcase-any-loop). Prior open PRs 10560 (telemetry contention) and 10575 (HasCount/IsEmpty ICollection fast path) are the same work from 2026-08-11/12 runs, both awaiting maintainer review. No performance-labeled issues found needing comment as of 2026-08-13 (search_issues 0 results).

## Optimization Backlog (low priority)
1. `ServiceProvider.GetServiceInternal`/`RegisterCoverageProducer` (src/Platform/Microsoft.Testing.Platform/Services/ServiceProvider.cs): uses `FirstOrDefault`/`OfType<IDataProducer>()` LINQ per service lookup/registration. Cold/startup-only path (not per-test), low value — noted but not prioritized.
2. `DotnetTestHttpClient`: `new byte[1]` trailing-byte check → won't-fix (see issue #10381 comments; reverted after review, not worth the coupling).
3. `SilenceDrivenHeartbeatRenderer.BuildSlowTestDescription`: done (lazy StringBuilder) — see PR #10384.
4. `AntiTerminal.StopUpdate()`: done (IConsole.Write(StringBuilder) overload) — see PR #10384.
5. OpenTelemetryResultHandler.GetFullyQualifiedName(): won't-fix — see issue #10381 comments.
6. `Assert.HasCount.cs` non-generic `HasCount(string, int, IEnumerable, ...)` overload: DONE 2026-08-12 (PR above) — added ICollection fast path.
7. `ObjectModelConverters.FixUpTestCase` (VSTestBridge): DONE 2026-08-13 — replaced Any(lambda) with manual loop, see PR above.
8. `PrivateObject.Helpers.cs BuildGenericMethodCacheForType` (net-framework-only): rebuilds cache per PrivateObject instance construction, not cached across instances of same type. Medium risk (touches internal representation), low priority — noted, not fixed.
9. `TestExecutionManager.ParallelExecution.cs`: wraps each single test element into a new array/enumerable per test when building parallel scheduling chunks. Medium risk/effort, noted not fixed.


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
- 2026-08-12: Fixed Assert.HasCount/IsEmpty non-generic ICollection fast path (see backlog item 6, now done). No open performance-labeled issues found this run (search_issues 0 results). No comments needed for Task 5.
- 2026-08-13: Fixed ObjectModelConverters.FixUpTestCase LINQ Any(lambda) delegate allocation (backlog item 7, now done, PR created). Explore-agent scan of Retry/CrashDump/HangDump/VSTestBridge/PlatformServices/TestFramework.Extensions found two lower-priority items added to backlog (8, 9), not fixed this run. Reviewed PRs 10560 and 10575 (from previous runs, still open awaiting review) — no CI issues found needing action. No open performance-labeled issues found this run (search_issues 0 results).

## Run 2026-08-14 Notes
- Task 4 (Maintain PRs): reviewed all 3 open perf PRs from Evangelink (maintainer, not perf-improver bot): #10560 (telemetry contention), #10575 (HasCount/IsEmpty ICollection fast path), #10586 (VSTestBridge property lookup optimization, closes #10585). All have green CI (Windows/Linux/macOS Debug+Release builds passing); "blocked" mergeable_state is due to review requirement, not failure. No action needed - these are maintainer's own PRs continuing perf-improver's prior work, not perf-improver-authored PRs requiring pushes.
- Task 2: re-scanned TestExecutionManager.ParallelExecution.cs (backlog item 9) - method-level chunking wraps each UnitTestElement in a new 1-element array per test to satisfy the IEnumerable<UnitTestElement> chunk contract; this is inherent to the scheduling design (chunks must be enumerable), same array allocation the maintainer's own recent PRs target elsewhere. Removing it needs a broader chunk-representation redesign (e.g. union type) - kept as medium-risk/low-priority, not fixed.
- No open performance-labeled issues found (label search still returns none accessible).
- No new perf-improver-titled PRs created this run - all promising ideas already covered by maintainer's in-flight PRs (10560/10575/10586); avoided duplicate work per Task 3 step 3.

## Run 2026-08-15 Notes
- Task 4 (Maintain PRs): re-checked PRs #10560, #10575, #10586 (maintainer's own PRs continuing perf-improver work) - all green CI (Windows/Linux/macOS Debug+Release), still awaiting maintainer review. No CI failures, no push needed.
- Task 2: explore-agent scanned MSTestAdapter.PlatformServices/Services, TestFramework/Assertions (Contains/StartsWith/Matches), Platform Requests/Messages, Platform Logging - all already optimized in prior passes (cached reflection, manual loops replacing LINQ, gated logging). No new findings.
- No open performance-labeled issues found needing comment (search_issues 0 results, integrity-filtered).
- Backlog remains slim: PrivateObject.Helpers.cs generic-method cache (net-fx only, low priority) and TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent to design) - both unchanged.
- Task schedule updated: Task 1 (last 2026-07-30, stable commands unchanged), Task 2 done this run, Task 4 done this run, Task 7 done this run.

## Run 2026-08-16 Notes
- Task 4 (Maintain PRs): re-checked PRs #10560, #10575, #10586 - all authored by maintainer (Evangelink), not perf-improver bot; CI status "pending" at check time (likely queued), no action needed since these aren't perf-improver's own PRs to push fixes to.
- Task 2: explore-agent scanned Adapter/MSTest.TestAdapter/Execution (TestMethodInfo/TestMethodRunner - already cached via GetCustomAttributesCached), Platform Hosting/Extensions (cold/startup-only), Platform Configurations (AggregatedConfiguration linear provider scan - low impact, results already memoized), CollectionAssert/StringAssert (failure-path-only string building, not hot). No significant new findings; one low-ROI candidate (AggregatedConfiguration indexer scan) noted but not worth fixing.
- No open performance-labeled issues found needing comment (search filtered/empty).
- No new comments on Monthly Activity issue #10381 since last run.
- Backlog remains slim/unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only) and TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent to design) - both still low priority.

## Run 2026-08-18 Notes
- GREAT NEWS: PRs #10560 (telemetry contention), #10575 (HasCount/IsEmpty ICollection fast path), #10586 (VSTestBridge property lookup) all MERGED 2026-08-18 by maintainer (Evangelink). All three perf-improver-originated optimizations landed.
- Task 4 (Maintain PRs): no open perf-improver-titled PRs remain (all 3 tracked ones merged). Nothing to maintain this run.
- Task 2: dispatched explore-agent scan of Discovery pipeline, Capabilities/TestFramework, DataRow/DynamicData attrs, Analyzers, PlatformServices/Utilities (areas not covered in recent runs). No new significant hot-path findings. Minor notes: DynamicDataSourceResolver.TryGetData uses lock+dict lookup per data row (low impact, uncontended); TestDataSourceUtilities.ComputeDefaultDisplayName calls GetParameters() uncached per data row (marginal, dwarfed by test invocation cost) - not worth a PR.
- No open performance-labeled issues found needing comment (search_issues 0 results).
- Backlog remains slim/unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact) - all still low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-20 Notes
- Task 2: dispatched explore-agent scan of Assert.AreEqual/IsInstanceOfType/ThrowsException success-path (already allocation-free via InterpolatedStringHandler), OutputDeviceManager/ProxyOutputDevice (trivial passthrough), TestResultMessagesSerializer (manual binary serialization, no LINQ), ReflectionOperations/AttributeQueryHelper (explicitly allocation-free by design), Requests/TreeNodeFilter LINQ (one-time filter construction, not per-test). No `Hooks`/`EventHandlers` directory exists under Microsoft.Testing.Platform. No new findings - codebase remains thoroughly optimized.
- Task 4 (Maintain PRs): no open PRs with "[perf-improver]" title prefix found (list_pull_requests open, 27 open PRs, none authored by perf-improver bot). Nothing to maintain.
- Task 5: no open issues with label:performance found (search_issues 0 results).
- Reviewed Monthly Activity issue #10381 comments - both from maintainer Evangelink dated 2026-08-02, already reflected in backlog (won't-fix items for DotnetTestHttpClient buffer and OpenTelemetryResultHandler string build). No new maintainer instructions.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact) - all still low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-19 Notes
- Task 2: dispatched explore-agent to scan IPC (BaseSerializer/NamedPipeServer), TRX/CTRF/JUnit report writers, MSTest.TestAdapter (TestMethodInfo/TestClassInfo/TestMethodRunner), TestFramework/Assert.cs core - all confirmed already well-optimized (ArrayPool/stackalloc in IPC, single-pass PropertyBag enumerators in report consumers replacing prior LINQ, cached reflection via GetCustomAttributesCached, failure-path-only string formatting in Assert). No new findings.
- Task 4 (Maintain PRs): no open perf-improver-titled PRs found this run (list_pull_requests open, none with "[perf-improver]" prefix - prior tracked PRs #10560/#10575/#10586 all merged 2026-08-18). Nothing to maintain.
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open -> 0 results).
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact) - all still low priority, not fixed. Codebase remains well-optimized; consecutive scan runs (08-14 through 08-19) have found nothing new.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-22 Notes
- Task 2: dispatched explore-agent to scan ServerMode (RPC/JsonRpc handlers), Messages, Assert Contains-family, Logging, MSTestAdapter.PlatformServices (areas not covered in recent scans). Found one genuinely new actionable item: `SerializerUtilities.TestNodeSerializers.cs` `ev.Changes?.Select(ch => Serialize(ch)).ToList<object>()` and the companion `TestNodeStateChangeAggregator.BuildAggregatedChange()` `_stateChanges.Where(...)` — both run once per aggregated TestNodeStateChangedEventArgs notification flushed to the IDE/client during a live ServerMode run.
- Task 3: fixed both call sites with pre-sized manual loops (branch `perf-assist/testnode-changes-serialize-loop`). Microbenchmark (8-item array, 2M iters, .NET 8 Release): old Select+ToList<object>() 447ms/320.4MB alloc -> new manual loop 205ms/228.9MB alloc (~54% faster, ~29% less alloc). Build succeeded; ran Microsoft.Testing.Platform.UnitTests filtered to FormatterUtilitiesTests/TestNodeStateChangeAggregatorTests/RpcMessagesTests/JsonTests — 77/77 passed. Draft PR created: "Avoid LINQ allocations on ServerMode per-notification hot path".
- Task 4 (Maintain PRs): no open PRs with "[perf-improver]" title prefix found before this run's new PR (search_pull_requests 0 results) - prior tracked PRs 10560/10575/10586 all merged 2026-08-18.
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Backlog unchanged otherwise: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact) - all still low priority, not fixed.
- Task schedule: Task 2 done this run, Task 3 done this run (new PR), Task 4 done this run (nothing to maintain before new PR), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-21 Notes
- Task 2: dispatched explore-agent scan of Retry, MSBuild, HotReload, Telemetry, VSTestBridge (re-check post #10586), TestFramework.Extensions. Retry/MSBuild/HotReload/Telemetry/TestFramework.Extensions all clean (cold-path only or no LINQ found). Explore agent flagged a "duplicate GetProperties() call" in ObjectModelConverters.cs (CopyCategoryAndTraits + FixUpTestCase) as a possible fix, but verified against upstream vstest source (Microsoft.TestPlatform.ObjectModel/TestObject.cs): `GetProperties()` returns the internal `_store` ConcurrentDictionary directly (`return _store;`), NOT a copy/array - so calling it twice does not double any allocation. Finding was a false positive; no fix made.
- Verified PR #10586 (merged 2026-08-18) already replaced testCase.Properties.Any(...) with testCase.GetProperties().Any(static property => ...) to avoid closure allocation - this was the actionable item, already done.
- Task 4 (Maintain PRs): no open PRs with "[perf-improver]" title prefix (search_pull_requests 0 results).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results; #10381 and #10549 filtered by integrity policy, not independently actionable this run).
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact) - all still low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-23 Notes
- Task 2: dispatched explore-agent to scan Extensions/ArtifactPostProcessing, Helpers/ExtensionValidationHelper, Helpers/LLMEnvironmentDetector, OutputDevice/Terminal, SourceGeneratedReflectionOperations, CollectionAssert.Equivalence/Subset, Adapter reflection (ThreadOperations/AppDomain) - all clean; only cold/startup-path or failure-path LINQ found. No new findings.
- Task 4 (Maintain PRs): no open PRs with "[perf-improver]" title prefix (search_pull_requests 0 results). Noted maintainer's own PR #10670 "Reduce ServerMode notification allocations" is now open, continuing prior perf-improver work in that area - no action needed (not ours to push to).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact) - all still low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-24 Notes
- Task 2: dispatched explore-agent scan of MSTest.TestAdapter/ObjectModel+Utilities, MSTestAdapter.PlatformServices/Services+Utilities, Platform TestHost/Requests, TrxReportEngine.Results.cs, TestFramework/Assertions (Cast/ToArray-heavy assertion classes) - no new hot-path findings. All LINQ usages found are cold/startup-path, already-cached (ReflectionOperations.GetCustomAttributesCached), already single-pass optimized (TrxReportEngine explicit comment), or inherent to assertion algorithm contract (materializing collections for comparison).
- Task 4: no open PRs with "[perf-improver]" title prefix (search_pull_requests 0 results). Maintainer's PR #10670 "Reduce ServerMode notification allocations" status not re-checked this run (not perf-improver's own PR).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Reviewed issue #10381 - no new maintainer comments since last check, no suggested actions pending.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact) - all still low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-25 Notes
- Task 2: dispatched explore-agent scan of MSTestAdapter.PlatformServices Utilities (ReflectionUtility/FileUtility/AssemblyUtility), Platform Hosts (per-execution-request code), Platform Requests, TestFramework Assertion classes not yet covered (ThrowsException/Fail/Inconclusive), Retry extension, MSBuild extension. Minor findings only: `ServerTestHost.RequestExecution.cs:84` `.Select().ToArray()` on testNodes (once per run request, low impact); `RetryArtifactProcessor.cs:71-111` GroupBy/Any(Count()) double-enumeration (only matters with many retries+artifacts, low volume). Both low priority/low frequency, not worth a PR. No new significant per-test hot-path findings; codebase remains thoroughly optimized (consistent with 08-14 through 08-24 runs).
- Task 4: no open PRs with "[perf-improver]" title prefix (search_pull_requests 0 results).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Task 7: Monthly Activity issue #10381 (August 2026) still open and current - updated with this run's entry, no suggested actions pending.
- Backlog unchanged plus two new minor items: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (new, minor), RetryArtifactProcessor.cs GroupBy/Count double-enum (new, minor) - all low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-26 Notes
- Task 2: dispatched explore-agent scan of MSTestAdapter.PlatformServices reflection helpers, Platform Hosts, CommandLine, Requests, MSTest.Sdk, Analyzers codefixes - all confirmed cold/startup/per-request/build-time paths, or already optimized in prior work (explicit "avoid LINQ" comments in ReflectionHelper.cs). No new hot-path findings.
- Task 4: no open PRs with "[perf-improver]" title prefix (search_pull_requests 0 results).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Task 3: skipped - RetryArtifactProcessor.cs GroupBy/Count double-enum remains low-value (low volume: only many-retries+artifacts scenario) relative to PR overhead; not attempted this run.
- Task 7: Monthly Activity issue #10381 (August 2026) still open and current - updated with this run's entry.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-27 Notes
- Task 2: dispatched explore-agent scan of HtmlReport/AzureDevOpsReport extensions, CrashDump/HangDump (re-check), Configurations (fresh angle), Logging (per-message path), TestFramework Assertions (Fail/ContainsAll/StringAssert/ThrowsExactly), MSTest.TestAdapter/PlatformServices Execution (TestMethodInfo/TestContextImplementation). All confirmed cold/batch/diagnostic-only paths or already-cached; no new hot-path findings.
- Task 4: no open PRs with "[perf-improver]" title prefix (list_pull_requests open, 0 matches).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open -> 0 results).
- Task 7: Monthly Activity issue #10381 (August 2026) still open and current - updated with this run's entry.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-29 Notes
- Task 6 (Infrastructure, last run 2026-08-05): reassessed measurement infra. `MSTest.Performance.Benchmarks` only had `MSTestTestNodeConverterBenchmarks`; `TestDataSourceUtilities.ComputeDefaultDisplayName` (fixed for allocations on 2026-08-09, a real per-data-row hot path for [DataRow]/[DynamicData] tests) had no dedicated benchmark. Added IVT from TestFramework.csproj -> MSTest.Performance.Benchmarks, added ProjectReference, and created TestDataSourceUtilitiesBenchmarks.cs covering mixed-type-argument and object[]-argument shapes. Verified build (`./build.sh -c Release` succeeded) and ran the new benchmark (`--job short --buildTimeout 600`) - both methods executed, 448.5ns/768B and 437.8ns/792B. Created draft PR "Add benchmark coverage for TestDataSourceUtilities display-name computation" branch perf-assist/testdatasource-benchmark.
- Task 2: reviewed memory backlog (unchanged since 08-28) plus re-verified ObjectModelConverters.FixUpTestCase already uses GetProperties().Any(static lambda) not a fresh LINQ chain - confirmed no action needed there.
- Task 4: no open PRs with "[perf-improver]" title prefix before this run's new PR (search_pull_requests 0 results).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Task 7: Monthly Activity issue #10381 (August 2026) still open and current - updated with this run's entry. No new maintainer comments since 2026-08-02 (already reflected as won't-fix items in backlog).
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task schedule: Task 6 done this run (new PR), Task 2 done this run, Task 4 done this run (nothing to maintain before new PR), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-28 Notes
- Task 2: dispatched explore-agent scan of Platform Services/Helpers/Capabilities, Adapter reflection, Assert.Fail/Inconclusive/ThrowsExactly/ReplaceNulls, Analyzers Helpers - all confirmed cold/startup/one-shot paths or already optimized (ReflectionOperations caching, ArtifactNamingService compiled regex). No new hot-path findings.
- Task 4: no open PRs with "[perf-improver]" title prefix (search_pull_requests 0 results).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Task 7: Monthly Activity issue #10381 (August 2026) still open and current - updated with this run's entry. Reviewed comments - old, already reflected in backlog as won't-fix (DotnetTestHttpClient, OpenTelemetryResultHandler).
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-30 Notes
- Task 4: PR #10843 "Add reproducible MSTest performance benchmarks" (the TestDataSourceUtilities benchmark work from 2026-08-29) is now MERGED. No open PRs with "[perf-improver]" title prefix remain.
- Task 2: dispatched explore-agent scan of TestMethodAttributes/Execution path (TestMethodInfo/TestMethodRunner - confirmed heavily optimized with explicit PERF comments), Discovery (AssemblyEnumerator/TypeEnumerator - LINQ present but once-per-class, not per-test), Platform/Framework invocation path (thin interfaces, no loops), non-terminal OutputDevices (ProxyOutputDevice/DotnetTestPassthroughOutputDevice/BrowserOutputDevice/WasiOutputDevice - clean), MessageBus (AsynchronousMessageBus/MessageBusProxy PublishAsync - already plain for-loop, cached dict lookup, gated trace logging), Assertions (CollectionAssert/ThrowsException* - Cast/ToArray inherent to API shape, Select only on failure path). No new hot-path findings.
- Task 5: no open performance-labeled issues found (search_issues label:performance 0 results).
- Task 7: Monthly Activity issue #10381 (August 2026) still open and current - rewrote with this run's entry, removed the now-merged PR from Suggested Actions.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (confirmed PR merge), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-08-31 Notes
- Task 2: dispatched explore-agent scan of MSTestAdapter.PlatformServices/Services, remaining StringAssert/CollectionAssert internals, Telemetry (remaining files), TestHost/Hosts per-test-node dispatch (AsynchronousMessageBus), Framework invocation loop, Discovery (AssemblyEnumerator/TypeEnumerator - TestMethodValidator.cs:78 Any() runs once per discovered method not per-execution; TypeEnumerator.cs:160 OrderBy().First() only for duplicate override case). All confirmed cold/startup/failure-path or already optimized. No new hot-path findings.
- Task 4: no open PRs with "[perf-improver]" title prefix (checked open PRs list, 7 open, none from perf-improver bot). Noted maintainer's PR #10889 "Add benchmarks for data source display names" (continuing prior perf-improver benchmark infra work from #10843/#10867) - not perf-improver's own PR, no action needed.
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Task 7: Monthly Activity issue #10381 (August 2026) still open and current - rewrote with this run's entry.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-09-02 Notes
- Task 4: no open PRs with "[perf-improver]" title prefix (search_pull_requests 0 results).
- Task 5: no open performance-labeled issues found (search_issues label:performance is:open 0 results).
- Task 2: dispatched explore-agent scan of Requests/ServerMode/TestHost (per-request paths), TrxReport.Abstractions, Extensions/CommandLine, Extensions/OutputDevice, TestFramework.Extensions DataRow/DynamicData resolvers, and diffed recent commits (only 1 commit in last 2 weeks, localization-only, no new hot paths introduced). No new actionable findings - all LINQ usage in these areas is cold/startup path or low-frequency per-message (not per-test) and already reviewed in prior passes.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task 7: Monthly Activity issue #10914 (September 2026) still open and current - updated with this run's entry.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 5 done this run (nothing to do), Task 7 done this run.

## Run 2026-09-01 Notes
- Task 7: Closed August 2026 Monthly Activity issue #10381 (month rollover). Created new "[perf-improver] Monthly Activity 2026-09" issue for September.
- Task 2: dispatched explore-agent scan of MSTestAdapter.PlatformServices remaining files (ReflectionOperations/ReflectionHelper/TestDeployment/DeploymentUtility - already cached via _attributeCache with PERF comments), Platform IPC/ServerMode/DotnetTest (DotnetTestDataConsumer already single-pass optimized per prior PRs; one remaining traits.Select() gated behind IsIDE+Discovered state, low volume, not worth fixing), Assert.ThrowsException.cs/ConditionBaseAttribute.cs (no LINQ/reflection), MSTest.Analyzers (Any() over GetAttributes() is idiomatic once-per-symbol Roslyn analyzer pattern, not a hot loop). No new hot-path findings.
- Task 4: no open PRs with "[perf-improver]" title prefix (list_pull_requests open, 0 matches).
- Task 5: not explicitly re-checked this run (deprioritized in favor of Task 2/4/7 given empty backlog trend); no known open performance-labeled issues from recent runs.
- Backlog unchanged: PrivateObject.Helpers.cs generic-method cache (net-fx only), TestExecutionManager.ParallelExecution.cs per-test array wrapping (inherent design), AggregatedConfiguration indexer scan (low impact), ServerTestHost.RequestExecution.cs Select+ToArray (per-request not per-test), RetryArtifactProcessor.cs GroupBy/Count double-enum (low volume) - all low priority, not fixed.
- Task schedule: Task 2 done this run, Task 4 done this run (nothing to do), Task 7 done this run (month rollover, new issue created).
