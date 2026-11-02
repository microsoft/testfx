# MSTest performance benchmarks

The performance suite has two complementary layers:

- `MSTest.Performance.Runner` measures complete test processes, including startup,
  discovery, execution, and shutdown.
- `MSTest.Performance.Benchmarks` uses BenchmarkDotNet for stable in-process hot
  paths and allocation measurements.

Neither layer is a pull-request timing gate. Hosted-agent timings vary too much
for reliable pass/fail thresholds; scheduled runs publish machine-readable
artifacts for trend analysis instead.

## End-to-end process benchmarks

Build the repository packages before running the generated test assets:

```powershell
.\build.cmd -pack -c Release
.\.dotnet\dotnet.exe run --project test\Performance\MSTest.Performance.Runner -c Release -- execute --pipelineNameFilter "*PlainProcess*"
```

Each pipeline performs one warmup and five measured runs. A run is accepted only
when the process exits successfully and reports the expected number of passing
tests. `Result.json` contains every sample plus median and interquartile timing,
CPU time, peak working set, target framework, build configuration, effective
worker count, runner runtime, CI image, and machine metadata.

CPU and peak-working-set values are reported only for standalone test-host
processes. They are `null` for `dotnet test`, because those values would describe
only the parent CLI process and omit the spawned test host.

The scenario matrix covers:

- MTP standalone and `dotnet test` server mode;
- VSTest through `dotnet test`;
- reflection, `Rooting`, and `ReflectionFree` discovery;
- NativeAOT;
- plain, data-driven, per-test lifecycle, and per-class lifecycle workloads;
- method-level and class-level parallel execution.

Compare results only when the OS and CI image, architecture, processor count,
runner runtime, target framework, configuration, worker count, and scenario all
match. The rolling baseline enforces these environment dimensions. Prefer
median and interquartile range over a single elapsed-time sample.

Set `MSTEST_PERFORMANCE_SKIP_ARCHIVE=1` to keep only the stable
`Results/<pipeline>/Result.json` outputs and skip compression of generated test
assets. Scheduled collection uses this mode.

## Allocation microbenchmarks

Run all microbenchmarks:

```powershell
.\.dotnet\dotnet.exe run --project test\Performance\MSTest.Performance.Benchmarks -c Release -- --filter "*" --buildTimeout 600 --generateBinLog
```

For a quick smoke run:

```powershell
.\.dotnet\dotnet.exe run --project test\Performance\MSTest.Performance.Benchmarks -c Release -- --filter "*" --job short --buildTimeout 600 --generateBinLog
```

Scheduled collection uses BenchmarkDotNet's normal job rather than `ShortRun`;
the latter is intended only to validate that a benchmark builds and executes.

Add a microbenchmark only for a stable, isolated hot path. Use the process
runner when the behavior includes process startup, assembly loading, test-host
protocols, NativeAOT, or publishing.
