# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-06-04

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

## Completed Work
- PR #8692: perf: reduce redundant UTF-8 string encoding in IPC BaseSerializer (MERGED 2026-05-31)
- PR #8705: perf: avoid double IEnumerable enumeration in DynamicDataShouldBeValidAnalyzer (MERGED 2026-05-31)
- PR #8720: perf: encode JSON body once in TcpMessageHandler.WriteRequestAsync (MERGED 2026-06-01)
- PR #8743: perf: single-pass TRX message partitioning in TrxReportEngine (MERGED 2026-06-02)
- PR #8806: perf: single-pass TestNode property serialization in SerializerUtilities (MERGED 2026-06-04)
- PR (efficiency/simple-ansi-terminal-cache-newline-color): perf: cache newline+color string in SimpleAnsiTerminal (submitted 2026-06-04, awaiting merge)

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
