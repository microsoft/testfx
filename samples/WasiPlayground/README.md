# WasiPlayground

To run this project:

1. Run `dotnet workload install wasi-experimental`
1. Run `dotnet build`
1. Install wasmtime. See docs at <https://docs.wasmtime.dev/cli-install.html>.
1. Open command-line in AppBundle directory (`artifacts\bin\WasiPlayground\Debug\net10.0\wasi-wasm\AppBundle`)
1. Run `wasmtime run --wasi http --dir . -- dotnet.wasm WasiPlayground`

## Status

As of today, this will produce this exception (it's not yet supported by MTP).

```
Unhandled Exception:
System.PlatformNotSupportedException: Arg_PlatformNotSupported
   at System.Threading.Tasks.Task.InternalWaitCore(Int32 millisecondsTimeout, CancellationToken cancellationToken)
   at System.Threading.Tasks.Task.InternalWait(Int32 millisecondsTimeout, CancellationToken cancellationToken)
   at Program.<Main>(String[] args)
```
