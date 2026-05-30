# Efficiency Improver Memory — microsoft/testfx

## Last Updated
2026-05-30

## Build/Test Commands
- Build: `./build.sh` (Linux/macOS), `.\build.cmd` (Windows)
- Test: `./build.sh -test`
- Build + Pack: `./build.sh -pack`
- Build + Pack + Acceptance Tests: `./build.sh -pack -test -integrationTest`
- Local dotnet SDK: `.dotnet/dotnet` (auto-installed by build.sh)
- NOTE: `--no-restore` flag is broken (MSBuild unknown switch); always run with restore

## Tasks Last Run (round-robin cursor)
- 2026-05-29: Task 1 (discover commands), Task 2 (identify opportunities), Task 3 (implement), Task 7 (monthly summary)
- 2026-05-30: Task 3 (implement), Task 4 (maintain PRs — PR #8692 all CI green), Task 7 (monthly summary)

## Completed Work
- PR #8692: perf: reduce redundant UTF-8 string encoding in IPC BaseSerializer (open, all CI green)
- PR #aw_pr2: perf: avoid double IEnumerable enumeration in DynamicDataShouldBeValidAnalyzer
  - Branch: efficiency/analyzer-avoid-double-enumeration
  - Change: IEnumerable.Count()>1 + FirstOrDefault() → ToImmutableArray() + .Length + indexer
  - Impact: eliminates one full IEnumerable traversal per [DynamicData] attribute at compile time

## Optimisation Backlog
| Priority | Focus Area | Opportunity | Estimated Impact |
|----------|------------|-------------|------------------|
| MEDIUM | Code-Level | TcpMessageHandler.WriteRequestAsync: GetByteCount(messageStr) scans string separately from Write; string is scanned twice. Could encode to bytes first for both header and body | Only in server mode |
| LOW | Code-Level | ToolsTestHost.cs:55 - GroupBy().Where(x => x.Count() > 1).ToList().ForEach() - startup code, low priority |

## Efficiency Notes
- Platform uses ArrayPool<byte> in NETCOREAPP path (good)
- IPC uses named pipes / TCP for inter-process communication
- Serializers are hot path for test result delivery
- Build requires arcade toolset; can't build individual projects without full build first
- TreeNodeFilter uses compiled Regex (already optimized)
- No BenchmarkDotNet benchmarks in repo; performance tests use PerfView
