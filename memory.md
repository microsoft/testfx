# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-05-29

## Build/Test Commands
- Build: `./build.sh` (Linux/macOS), `.\build.cmd` (Windows)
- Test: `./build.sh -test`
- Build + Pack: `./build.sh -pack`
- Build + Pack + Acceptance Tests: `./build.sh -pack -test -integrationTest`
- Local dotnet SDK: `.dotnet/dotnet` (auto-installed by build.sh)
- NOTE: `--no-restore` flag is broken (MSBuild unknown switch); always run with restore

## Tasks Last Run (round-robin cursor)
- 2026-05-29: Task 1 (discover commands), Task 2 (identify opportunities), Task 3 (implement), Task 7 (monthly summary)

## Completed Work
- PR: perf: reduce redundant UTF-8 string encoding in IPC BaseSerializer
  - Branch: efficiency/ipc-serializer-reduce-string-encoding
  - Change: WriteField(ushort, string?) now calls WriteString() once instead of WriteStringSize()+WriteStringValue()
  - Also fixed WriteStringSize in #else path to use GetByteCount instead of GetBytes
  - Impact: -1 string scan in NETCOREAPP; -1 allocation + scan in netstandard2.0 path
  - Hot path: ~54+ WriteField calls per test result message in IPC serializers

## Optimisation Backlog
| Priority | Focus Area | Opportunity | Estimated Impact |
|----------|------------|-------------|------------------|
| MEDIUM | Code-Level | DynamicDataShouldBeValidAnalyzer.cs:189 - Count() > 1 causes full IEnumerable enumeration, then FirstOrDefault() enumerates again. Use Take(2).ToList() or Skip(1).Any() pattern | Low per call, runs on every [DynamicData] attr at compile time |
| MEDIUM | Code-Level | TcpMessageHandler.WriteRequestAsync: GetByteCount(messageStr) scans string separately from Write; string is scanned twice. Could encode to bytes first for both header and body | Only in server mode |
| LOW | Code-Level | ToolsTestHost.cs:55 - GroupBy().Where(x => x.Count() > 1).ToList().ForEach() - startup code, low priority |

## Efficiency Notes
- Platform uses ArrayPool<byte> in NETCOREAPP path (good)
- IPC uses named pipes / TCP for inter-process communication
- Serializers are hot path for test result delivery
- Build requires arcade toolset (GenerateFileFromTemplate task); can't build individual projects without full build first
- TreeNodeFilter uses compiled Regex (already optimized)
- No BenchmarkDotNet benchmarks in repo; performance tests use PerfView
