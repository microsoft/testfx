# WasiPlayground

Demonstrates hosting [Microsoft.Testing.Platform](https://aka.ms/testingplatform/) on the WebAssembly System Interface (WASI).

## Build & run

Prerequisites:

- .NET 10 (or newer) SDK matching `global.json` at the repo root.
- The `wasi-experimental-net10` and `wasm-tools-net10` SDK workloads:

  ```cmd
  dotnet workload install wasi-experimental-net10 wasm-tools-net10
  ```

- [wasmtime](https://docs.wasmtime.dev/cli-install.html) on `PATH`.

Then:

1. From the repo root, publish the sample:

   ```cmd
   dotnet publish samples\WasiPlayground\WasiPlayground.csproj -c Debug -f net10.0
   ```

2. The pre-built `dotnet.wasm` does not embed the ICU data file, so copy it next to the bundle:

   ```cmd
   copy .dotnet\packs\Microsoft.NETCore.App.Runtime.Mono.wasi-wasm\10.0.7\runtimes\wasi-wasm\native\icudt.dat ^
        artifacts\bin\WasiPlayground\Debug\net10.0\wasi-wasm\AppBundle\
   ```

3. Switch to the bundle directory and invoke `wasmtime` (the `-S http` flag is required because the runtime imports `wasi:http`):

   ```cmd
   cd artifacts\bin\WasiPlayground\Debug\net10.0\wasi-wasm\AppBundle
   wasmtime run -S http --dir . -- dotnet.wasm WasiPlayground
   ```

## Status

After publishing, Microsoft.Testing.Platform now boots on `wasi-wasm` (the
banner reads `[wasi-wasm - net10.0]`) thanks to [#7137](https://github.com/microsoft/testfx/pull/7137).
Test execution itself currently fails with:

```console
Microsoft.Testing.Platform v2.3.0-dev (UTC ...) [wasi-wasm - net10.0]
...
Unhandled Exception:
System.PlatformNotSupportedException: Arg_PlatformNotSupported
   at System.Threading.Tasks.Task.InternalWaitCore(...)
   at System.Threading.Tasks.Task.InternalWait(...)
   at Program.<Main>(String[] args)
```

The exception comes from the C# compiler's synthetic `Main` wrapper for
`async Task Main`, which calls `Task.GetAwaiter().GetResult()` &rarr;
`Task.Wait()`. On single-threaded `wasi-wasm` (no thread pool) this throws
`PlatformNotSupportedException`. Tracked separately so this sample can act as
the canonical repro.

## Build configuration notes

`WasiPlayground.csproj` ships with a few non-obvious switches:

| Property | Reason |
| --- | --- |
| `UsingWasiRuntimeWorkload=true` | Workaround for an SDK manifest bug in `11.0.100-preview.5` where `$(UsingWasiRuntimeWorkload)` never resolves to `true` for net10.0 projects, so the WASI Sdk targets are never imported and no `dotnet.wasm` is produced. |
| `WasmSingleFileBundle=false` | Single-file bundling requires the [wasi-sdk](https://github.com/WebAssembly/wasi-sdk) (clang) toolchain to relink the native runtime. Keeping the managed assemblies on disk avoids that requirement. |
| `InvariantGlobalization=true` | Trimmed builds otherwise crash on startup trying to load `icudt.dat`. |

