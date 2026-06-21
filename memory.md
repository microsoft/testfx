# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-06-21

## Build/Test Commands
- Build: `./build.sh` (Linux/macOS), `.\build.cmd` (Windows)
- Test: `./build.sh -test`
- Build + Pack: `./build.sh -pack`
- Build + Pack + Acceptance Tests: `./build.sh -pack -test -integrationTest`
- Local dotnet SDK: `.dotnet/dotnet` (auto-installed by build.sh)
- NOTE: `--no-restore` flag is broken (MSBuild unknown switch); always run with full restore
- NOTE: Build requires full restore even for single project (Arcade SDK tasks needed)
- NOTE: `--treenode-filter` not available on Microsoft.Testing.Platform.UnitTests (extension not registered); run all tests and verify none fail
- NOTE: Only .NET 11.0 preview runtime is available in this environment; build succeeds but test runner needs net8.0/net9.0
- NOTE: MSTestAdapter.PlatformServices.UnitTests uses internal test framework (UseInternalTestFramework=true); not run by MTP runner, run via separate CI pass
- NOTE: global.json requires dotnet 11.0.100-preview SDK; environment only has 8.x/9.x/10.x; build requires ./build.sh bootstrapping

## Tasks Last Run (round-robin cursor)
- 2026-06-10: Task 3 (JUnitReport TestResultCapture single-pass), Task 7
- 2026-06-15: Task 3 (DiscoveredTestsJsonSerializer single-pass), Task 7
- 2026-06-16: Task 2 (scan), Task 3 (AzureDevOpsReporter defer GetTestName), Task 7
- 2026-06-19: Task 2 (scan MSTest core, Adapter, Platform), Task 3 (single-pass GroupBy in TestExecutionManager.Parallelization), Task 4 (verified #9196 merged), Task 7
- 2026-06-20: Task 3 (TerminalTestReporter.Summary.cs GroupBy.Any() fix), Task 4 (confirmed #9274 merged), Task 7
- 2026-06-21: Task 2 (scan for new opportunities), Task 3 (CommandLineParseResult IsOptionSet/TryGetOptionArgumentList optimization), Task 7

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
- PR #9274: perf: single-pass GroupBy partition in TestExecutionManager parallelization (MERGED 2026-06-20)
- PR #9300: perf: avoid GroupBy lazy evaluation for empty-check in AppendTestRunSummary (MERGED 2026-06-21)
- PR (branch efficiency/fix-parseoption-hot-loops): perf: hoist Trim and single-pass loop in CommandLineParseResult hot methods (SUBMITTED 2026-06-21, pending merge)
  - IsOptionSet: hoist optionName.Trim(OptionPrefix) outside lambda — was called N times per invocation, now called once
  - TryGetOptionArgumentList: replace lazy Where+Any+SelectMany double-enumeration with single foreach — avoids 2nd full pass over Options on every option-found call

## Optimisation Backlog
| Priority | Focus Area | Opportunity | Estimated Impact |
|----------|------------|-------------|------------------|
| LOW | Code-Level | `TerminalTestReporter.Summary.cs:45-48` — 4 separate Sum() calls on `assemblies` (TotalTests, FailedTests, SkippedTests, PassedTests); single foreach would do one pass. End-of-run, assemblies usually small. | LOW (end-of-run, small list) |
| LOW | Code-Level | `ToolsTestHost.cs:55` — `GroupBy().Where(Count()>1)` at startup only | Negligible |

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
- Backlog is very sparse; new scan (2026-06-21) found only low-priority items remaining
- TestExecutionManager.Parallelization.cs: GroupBy fix reduces 2 full passes to 1 (merged #9274)
- TerminalTestReporter.Summary.cs: artifactGroups.Any() → _artifacts.Count > 0 fix (merged #9300)
- 4x Sum() calls on assemblies in Summary.cs could be 1-pass foreach; LOW priority, end-of-run, small list
- ITerminal interface has no ReadOnlySpan<char>/int overloads; int.ToString() allocations in render loop cannot be avoided without interface changes
- MSTestAdapter tests (UseInternalTestFramework=true) not run by MTP runner in CI; covered separately
- Another perf-focused workflow ("perf-improver") is also active in this repo (PR #9299: replace Array.IndexOf with is pattern, PR #9311: extend perf runner to Linux/macOS)
- CommandLineParseResult: IsOptionSet was O(N) Trim calls, now 1; TryGetOptionArgumentList was 2-pass, now 1-pass (submitted 2026-06-21)

## Monthly Activity Issue
- 2026-06 issue: #9197 (open)
