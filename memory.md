# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-06-19

## Build/Test Commands
- Build: `./build.sh` (Linux/macOS), `.\build.cmd` (Windows)
- Test: `./build.sh -test`
- Build + Pack: `./build.sh -pack`
- Build + Pack + Acceptance Tests: `./build.sh -pack -test -integrationTest`
- Local dotnet SDK: `.dotnet/dotnet` (auto-installed by build.sh)
- NOTE: `--no-restore` flag is broken (MSBuild unknown switch); always run with restore
- NOTE: Build requires full restore even for single project (Arcade SDK tasks needed)
- NOTE: `--treenode-filter` not available on Microsoft.Testing.Platform.UnitTests (extension not registered); run all tests and verify none fail
- NOTE: Only .NET 11.0 preview runtime is available in this environment; build succeeds but test runner needs net8.0/net9.0
- NOTE: MSTestAdapter.PlatformServices.UnitTests uses internal test framework (UseInternalTestFramework=true); not run by MTP runner, run via separate CI pass

## Tasks Last Run (round-robin cursor)
- 2026-06-10: Task 3 (JUnitReport TestResultCapture single-pass), Task 7
- 2026-06-15: Task 3 (DiscoveredTestsJsonSerializer single-pass), Task 7
- 2026-06-16: Task 2 (scan), Task 3 (AzureDevOpsReporter defer GetTestName), Task 7
- 2026-06-19: Task 2 (scan MSTest core, Adapter, Platform), Task 3 (single-pass GroupBy in TestExecutionManager.Parallelization), Task 4 (verified #9196 merged), Task 7

## Completed Work
- PR #8692: perf: reduce redundant UTF-8 string encoding in IPC BaseSerializer (MERGED 2026-05-31)
- PR #8705: perf: avoid double IEnumerable enumeration in DynamicDataShouldBeValidAnalyzer (MERGED 2026-05-31)
- PR #8720: perf: encode JSON body once in TcpMessageHandler.WriteRequestAsync (MERGED 2026-06-01)
- PR #8743: perf: single-pass TRX message partitioning in TrxReportEngine (MERGED 2026-06-02)
- PR #8806: perf: single-pass TestNode property serialization in SerializerUtilities (MERGED 2026-06-04)
- PR #8834: perf: cache newline+color string in SimpleAnsiTerminal (MERGED 2026-06-05)
- PR #8866: perf: cache ExecutionId + single-pass property scan in DotnetTestDataConsumer (MERGED 2026-06-07)
- PR #8884: perf: SingleOrDefault instead of OfType().FirstOrDefault() in AzureDevOps report (MERGED 2026-06-07)
- PR #8908: perf: SingleOrDefault instead of OfType().Select() for stdout/stderr in OpenTelemetryResultHandler (MERGED 2026-06-08)
- PR #8938: perf: single-pass PropertyBag walk in OpenTelemetryResultHandler.HandleTestResult (MERGED 2026-06-09)
- PR #8975: perf: single-pass PropertyBag walk in TrxTestResultExtractor (MERGED 2026-06-10)
- PR #9018: perf: single-pass PropertyBag walk in JUnitReport TestResultCapture (MERGED 2026-06-11)
- PR #9159: perf: single-pass PropertyBag walk in TerminalOutputDevice and SimplifiedConsoleOutputDeviceBase (MERGED 2026-06-16)
- PR #9162: perf: single-pass PropertyBag walk in DiscoveredTestsJsonSerializer (MERGED 2026-06-16)
- PR #9196: perf: defer GetTestName() to failure branches and avoid OfType<> alloc in AzureDevOpsReporter (MERGED 2026-06-19)
- PR (efficiency/single-pass-groupby-parallel-split): perf: single-pass GroupBy partition in TestExecutionManager parallelization (SUBMITTED 2026-06-19, pending merge)
  - Lazy GroupBy evaluated twice (once per FirstOrDefault) → 2 full passes over testsToRun; fixed to 1 pass
  - Saves: ~n calls to GetPropertyValue + 1 Lookup<bool,TestCase> alloc per parallelized test assembly

## Optimisation Backlog
| Priority | Focus Area | Opportunity | Estimated Impact |
|----------|------------|-------------|------------------|
| MEDIUM | Code-Level | `TerminalTestReporter.Summary.cs:14` — `_artifacts.GroupBy(...).Any()` followed by `foreach ... GroupBy(...)` — double-enumeration of _artifacts; fix: use `_artifacts.Count > 0` | LOW-MEDIUM (summary path, ~1x per run, O(n) savings for large artifact counts) |
| LOW | Code-Level | `ToolsTestHost.cs:55` — `GroupBy().Where(Count()>1).ToList().ForEach()` — startup-only code | Negligible |

## Efficiency Notes
- Platform uses ArrayPool<byte> in NETCOREAPP path (good)
- IPC uses named pipes / TCP for inter-process communication
- Serializers are hot path for test result delivery
- Build requires arcade toolset; can't build individual projects without full restore first (Arcade MSBuild tasks required)
- TreeNodeFilter uses compiled Regex (already optimized)
- No BenchmarkDotNet benchmarks in repo; performance tests use PerfView
- TcpMessageHandler is server mode only (--server flag, IDE-driven test runs)
- PropertyBag.GetStructEnumerator() returns internal struct (zero alloc); accessible within Microsoft.Testing.Platform and all report extensions (TrxReport, JUnitReport, CtrfReport, HtmlReport, AzureDevOpsReport, MSBuild, OTel) via direct project reference
- PropertyBag.OfType<T>() allocates TProperty[] even for single-element results; use SingleOrDefault<T>() or GetStructEnumerator() depending on need
- Hot-path PropertyBag optimization series is substantially complete across all report generators
- AzureDevOpsReporter: GetTestName() was called eagerly for every test; now only called for 4 failure-state types (2026-06-16)
- CtrfReport, HtmlReport, JUnitReport, TrxTestResultExtractor, OTel, MSBuildConsumer, AzureDevOpsSummaryReporter, AzureDevOpsTestResultsPublisher.ResultFactory, DiscoveredTestsJsonSerializer, TerminalOutputDevice, SimplifiedConsoleOutputDeviceBase: all now use single-pass GetStructEnumerator()
- RetryDataConsumer: only 1 SingleOrDefault → no multi-walk to optimize
- VSTestBridge ObjectModelConverters: mostly PropertyBag.Add() calls, not reads; not a hot-path read scenario
- AzureDevOpsReporter ConsumeAsync: last remaining sub-optimal pattern fixed (2026-06-16)
- Backlog is very sparse after PropertyBag series; new scan (2026-06-19) found 2 new opportunities (GroupBy double-enum in Parallelization.cs + TerminalTestReporter.Summary.cs)
- TestExecutionManager.Parallelization.cs: GroupBy fix reduces 2 full passes to 1 in the parallelized code path (DoNotParallelize partitioning)
- TerminalTestReporter.Summary.cs: `_artifacts.GroupBy(...).Any()` double-enumerates; fix with `_artifacts.Count > 0` (1-liner, LOW priority, called once per run)
- ITerminal interface has no ReadOnlySpan<char>/int overloads; int.ToString() allocations in render loop cannot be avoided without interface changes
- MSTestAdapter tests (UseInternalTestFramework=true) not run by MTP runner in CI; covered separately

## Monthly Activity Issue
- 2026-06 issue: #9197 (open)
