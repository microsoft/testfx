# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-06-16

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

## Tasks Last Run (round-robin cursor)
- 2026-06-10: Task 3 (JUnitReport TestResultCapture single-pass), Task 7
- 2026-06-15: Task 3 (DiscoveredTestsJsonSerializer single-pass), Task 7
- 2026-06-16: Task 2 (scan), Task 3 (AzureDevOpsReporter defer GetTestName), Task 7

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
- PR #9159: perf: single-pass PropertyBag walk in TerminalOutputDevice and SimplifiedConsoleOutputDeviceBase (MERGED 2026-06-16, submitted by perf-improver workflow)
- PR #9162: perf: single-pass PropertyBag walk in DiscoveredTestsJsonSerializer (MERGED 2026-06-16)
- PR (efficiency/azdo-reporter-defer-getname): perf: defer GetTestName() to failure branches and avoid OfType<> alloc in AzureDevOpsReporter (submitted 2026-06-16, awaiting merge)
  - Defers GetTestName() call to failure branches only (passing tests no longer call it)
  - Replaces OfType<SerializableKeyValuePairStringProperty>().FirstOrDefault(predicate) with zero-alloc struct enumerator + early-exit key search
  - Saves: 1 PropertyBag walk + 1 SerializableKeyValuePairStringProperty[] alloc per passing test

## Optimisation Backlog
| Priority | Focus Area | Opportunity | Estimated Impact |
|----------|------------|-------------|------------------|
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
- AzureDevOpsReporter ConsumeAsync: last remaining sub-optimal pattern now fixed (2026-06-16)
- Backlog is now very sparse; next scan should focus on MSTest core (src/TestFramework) and Analyzers for new categories of work

## Monthly Activity Issue
- 2026-06 previous issue: #8939 (closed by Evangelink 2026-06-14 as "completed")
- 2026-06 new issue: to be created in this run
