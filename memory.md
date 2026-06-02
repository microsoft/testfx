# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-06-02

## Build/Test Commands
- Build: `./build.sh` (Linux/macOS), `.\build.cmd` (Windows)
- Test: `./build.sh -test`
- Build + Pack: `./build.sh -pack`
- Build + Pack + Acceptance Tests: `./build.sh -pack -test -integrationTest`
- Local dotnet SDK: `.dotnet/dotnet` (auto-installed by build.sh)
- NOTE: `--no-restore` flag is broken (MSBuild unknown switch); always run with restore

## Tasks Last Run (round-robin cursor)
- 2026-05-29: Task 1, Task 2, Task 3, Task 7
- 2026-05-30: Task 3, Task 4, Task 7
- 2026-05-31: Task 3, Task 7
- 2026-06-01: Task 3 (TrxReportEngine single-pass), Task 7
- 2026-06-02: Task 2, Task 3 (SimpleAnsiTerminal cache), Task 7

## Completed Work
- PR #8692: perf: reduce redundant UTF-8 string encoding in IPC BaseSerializer (MERGED 2026-05-31)
- PR #8705: perf: avoid double IEnumerable enumeration in DynamicDataShouldBeValidAnalyzer (MERGED 2026-05-31)
- PR #8720: perf: encode JSON body once in TcpMessageHandler.WriteRequestAsync (MERGED 2026-06-01)
- PR #8743: perf: single-pass TRX message partitioning in TrxReportEngine (MERGED 2026-06-02)
- PR (efficiency/simple-ansi-terminal-cache-newline-color): perf: cache newline+color string in SimpleAnsiTerminal (submitted 2026-06-02)

## Optimisation Backlog
| Priority | Focus Area | Opportunity | Estimated Impact |
|----------|------------|-------------|------------------|
| MEDIUM | Code-Level | SerializerUtilities.cs + Json.cs: Properties enumerated twice per TestNode (OfType<TestMetadataProperty> + foreach). Merging into single pass requires careful ordering. | Per-TestNode PropertyBag traversal in server mode |
| LOW | Code-Level | ToolsTestHost.cs:55 - GroupBy().Where(x => x.Count() > 1).ToList().ForEach() - startup code, negligible |

## Efficiency Notes
- Platform uses ArrayPool<byte> in NETCOREAPP path (good)
- IPC uses named pipes / TCP for inter-process communication
- Serializers are hot path for test result delivery
- Build requires arcade toolset; can't build individual projects without full build first
- TreeNodeFilter uses compiled Regex (already optimized)
- No BenchmarkDotNet benchmarks in repo; performance tests use PerfView
- TcpMessageHandler is server mode only (--server flag, IDE-driven test runs)
- TrxReportEngine.Results.cs: trxMessages was enumerated 6x per test result; now 1x (2026-06-01)
- SimpleAnsiTerminal: used for CI-mode color output; every Append/AppendLine call with color active calls SetColorPerLine (2026-06-02)
