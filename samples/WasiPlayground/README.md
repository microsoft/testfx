# WasiPlayground

Demonstrates hosting [Microsoft.Testing.Platform](https://aka.ms/testingplatform/) on the WebAssembly System Interface (WASI).

## Build & run

Prerequisites:

- The repo-local .NET SDK at `.dotnet\dotnet.exe`. Bootstrap it once by running
  `.\build.cmd` (Windows) or `./build.sh` (Linux/macOS) from the repo root;
  this also installs the `wasi-experimental-net10` workload required to build
  the sample (see [`eng/restore-toolset.ps1`](../../eng/restore-toolset.ps1)
  / [`eng/restore-toolset.sh`](../../eng/restore-toolset.sh)).
- The `wasm-tools-net10` workload, which is only needed for `dotnet publish`
  (not by the repo's `dotnet build`), so install it manually:

  ```cmd
  .\.dotnet\dotnet.exe workload install wasm-tools-net10
  ```

- [wasmtime](https://docs.wasmtime.dev/cli-install.html) on `PATH`.

> All commands below use `.\.dotnet\dotnet.exe` so that the `.dotnet\packs`
> lookup in step 2 resolves. If you prefer a machine-installed SDK, swap
> `dotnet` in and replace `.dotnet\packs` with the corresponding `packs`
> folder reported by `dotnet --info` (look for *.NET SDKs installed*).

Then:

1. From the repo root, publish the sample:

   ```cmd
   .\.dotnet\dotnet.exe publish samples\WasiPlayground\WasiPlayground.csproj -c Debug -f net10.0
   ```

2. The pre-built `dotnet.wasm` does not embed the ICU data file, so copy it
   next to the bundle. First list the installed runtime-pack version (a
   single folder name such as `10.0.8`), then substitute it into the copy
   command:

   ```cmd
   dir /b .dotnet\packs\Microsoft.NETCore.App.Runtime.Mono.wasi-wasm

   copy .dotnet\packs\Microsoft.NETCore.App.Runtime.Mono.wasi-wasm\<runtime-version>\runtimes\wasi-wasm\native\icudt.dat ^
        artifacts\bin\WasiPlayground\Debug\net10.0\wasi-wasm\AppBundle\
   ```

3. Switch to the bundle directory and invoke `wasmtime` (the `-S http` flag is required because the runtime imports `wasi:http`):

   ```cmd
   cd artifacts\bin\WasiPlayground\Debug\net10.0\wasi-wasm\AppBundle
   wasmtime run -S http --dir . -- dotnet.wasm WasiPlayground
   ```

## Status

After publishing, Microsoft.Testing.Platform boots on `wasi-wasm` (the banner
reads `[wasi-wasm - net10.0]`) thanks to [#7137](https://github.com/microsoft/testfx/pull/7137),
and MSTest tests now **run to completion** end-to-end (issue
[#2196](https://github.com/microsoft/testfx/issues/2196)):

```console
MSTest v4.4.0-dev (UTC ...)
...
/managed/WasmTestProject.dll (net10.0|wasi-wasm)
Test run summary: Passed!
  total: 2
  failed: 0
  succeeded: 2
  skipped: 0
```

### Why it works now

A user-authored `async Task Main` is fine on single-threaded `wasi-wasm`: the
compiler-synthesized entry point only blocks (`GetAwaiter().GetResult()`) if the
awaited task has not already completed, so as long as the pipeline never hops to
a background thread it completes synchronously and the wait is a no-op.

The real constraint is **threads**, not `async`/`await`. `wasi-wasm` links
wasi-emulated / synthetic pthreads, so there is no thread pool: `Task.Run`
continuations never execute and blocking waits throw
`PlatformNotSupportedException`. The platform and the MSTest adapter detect this
(`RuntimeFeatureHelper.IsMultiThreaded` / `RuntimeContext.IsMultiThreaded`,
derived from `OperatingSystem.IsWasi()`; .NET 11 will expose
`RuntimeFeature.IsMultithreadingSupported`, see
[dotnet/runtime#77541](https://github.com/dotnet/runtime/issues/77541)) and fall
back to inline/synchronous execution for the handful of thread-dependent spots on
the run path — the message-bus consumers, the shutdown watchdog, the telemetry
ingest loop, the countdown-event wait, and the adapter's per-test task factory.

This scenario is covered, in automated form, by the gated
`WasmExecutionTests.WasmExecution_RunsTestsUnderWasmtime` acceptance test
(see [`test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/WasmExecutionTests.cs`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/WasmExecutionTests.cs)).
That test publishes a minimal MSTest `wasi-wasm` project and runs it under
`wasmtime`; it is skipped automatically when the `wasm-tools` workload or
`wasmtime` is not available, and otherwise asserts the tests actually run. A
companion always-on `WasmBuild_GeneratesTestingPlatformEntryPoint` test guards
the build/entry-point plumbing on every CI leg.

## Build configuration notes

`WasiPlayground.csproj` ships with a few non-obvious switches:

| Property | Reason |
| --- | --- |
| `UsingWasiRuntimeWorkload=true` | Workaround for an SDK manifest bug in `11.0.100-preview.5` where `$(UsingWasiRuntimeWorkload)` never resolves to `true` for net10.0 projects, so the WASI Sdk targets are never imported and no `dotnet.wasm` is produced. |
| `WasmSingleFileBundle=false` | Single-file bundling requires the [wasi-sdk](https://github.com/WebAssembly/wasi-sdk) (clang) toolchain to relink the native runtime. Keeping the managed assemblies on disk avoids that requirement. |
| `InvariantGlobalization=true` | Prevents the trimmer from crashing during publish, but the pre-built `dotnet.wasm` still loads ICU at runtime, so `icudt.dat` must still be staged next to the bundle (see step 2). |
