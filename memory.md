# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-06-07

## Build/Test Commands
- Build: `./build.sh` (Linux/macOS), `.\build.cmd` (Windows)
- Test: `./build.sh -test`
- Build + Pack: `./build.sh -pack`
- Build + Pack + Acceptance Tests: `./build.sh -pack -test -integrationTest`
- Local dotnet SDK: `.dotnet/dotnet` (auto-installed by build.sh)
- NOTE: `--no-restore` flag is broken (MSBuild unknown switch); always run with restore
- NOTE: Build requires full restore even for single project (Arcade SDK tasks needed)

## Tasks Last Run (round-robin cursor)
- 2026-05-29: Task 1, Task 2, Task 3, Task 7
- 2026-05-30: Task 3, Task 4, Task 7
- 2026-05-31: Task 3, Task 7
- 2026-06-01: Task 3 (TrxReportEngine single-pass), Task 7
- 2026-06-02: Task 2, Task 3 (SimpleAnsiTerminal cache), Task 7
- 2026-06-03: Task 3 (SerializerUtilities single-pass), Task 7
- 2026-06-04: Task 3 (SimpleAnsiTerminal cache - actually submitted), Task 7
- 2026-06-05: Task 2, Task 3 (DotnetTestDataConsumer - cache ExecutionId + single-pass property scan), Task 7
- 2026-06-06: Task 2, Task 3 (AzureDevOps OfType→SingleOrDefault), Task 4, Task 7
- 2026-06-07: Task 3 (OpenTelemetry OfType→SingleOrDefault stdout/stderr), Task 7

## Completed Work
- PR #8692: perf: reduce redundant UTF-8 string encoding in IPC BaseSerializer (MERGED 2026-05-31)
- PR #8705: perf: avoid double IEnumerable enumeration in DynamicDataShouldBeValidAnalyzer (MERGED 2026-05-31)
- PR #8720: perf: encode JSON body once in TcpMessageHandler.WriteRequestAsync (MERGED 2026-06-01)
- PR #8743: perf: single-pass TRX message partitioning in TrxReportEngine (MERGED 2026-06-02)
- PR #8806: perf: single-pass TestNode property serialization in SerializerUtilities (MERGED 2026-06-04)
- PR #8834: perf: cache newline+color string in SimpleAnsiTerminal (MERGED 2026-06-05)
- PR #8866: perf: cache ExecutionId + single-pass property scan in DotnetTestDataConsumer (MERGED 2026-06-07)
- PR #8884: perf: SingleOrDefault instead of OfType().FirstOrDefault() in AzureDevOps report (MERGED 2026-06-07)
- PR (efficiency/otel-singleordefault-stdout-stderr): perf: use SingleOrDefault instead of OfType().Select() for stdout/stderr in OpenTelemetryResultHandler (submitted 2026-06-07, awaiting merge)

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
- TrxReportEngine.Results.cs: trxMessages was enumerated 6x per test result; now 1x (2026-06-01)
- SerializerUtilities TestNode: Properties was enumerated twice; now single-pass (2026-06-03)
- SimpleAnsiTerminal: SetColorPerLine cached "\n"+_foregroundColor to avoid per-call allocation (2026-06-04)
- SimpleAnsiTerminal used in CI environments (Azure DevOps, GitHub Actions) with ANSI but no cursor control
- DotnetTestDataConsumer: ExecutionId was computed property (env var lookup + string alloc per test); now readonly field (2026-06-05)
- DotnetTestDataConsumer: GetTestNodeDetails did 3 separate PropertyBag linked-list walks per test; now single pass via GetStructEnumerator() (2026-06-05)
- PropertyBag.GetStructEnumerator() returns internal struct (zero alloc); accessible within Microsoft.Testing.Platform assembly
- PropertyBag.OfType<T>() allocates TProperty[] even for single-element results; use SingleOrDefault<T>() when only first match is needed (2026-06-06)
- AzureDevOpsTestResultsPublisher.BuildAttachmentsFromTestNode had OfType<T>().FirstOrDefault() for stdout/stderr; now SingleOrDefault<T>() (2026-06-06)
- OpenTelemetryResultHandler.HandleTestResult: OfType<T>().Select() in string.Join for stdout/stderr; now SingleOrDefault<T>()?.Prop ?? string.Empty (2026-06-07)
- Removed _environment field from OpenTelemetryResultHandler (only needed for NewLine separator in the string.Join, now unnecessary) (2026-06-07)
- Backlog mostly exhausted for easy PropertyBag/LINQ wins; next run should scan for new opportunities
