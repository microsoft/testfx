# BrowserPlayground

Demonstrates hosting [Microsoft.Testing.Platform](https://aka.ms/testingplatform/) on
`browser-wasm` — the WebAssembly runtime that runs inside a browser (and, headlessly,
under [Node.js](https://nodejs.org/) via the same `dotnet.js` loader).

This is the browser counterpart to [`samples/WasiPlayground`](../WasiPlayground/README.md).
Both run on a single-threaded WebAssembly runtime; the platform work that makes that
possible is shared (see [#2196](https://github.com/microsoft/testfx/issues/2196)). The
only real difference is the **host**: `wasi-wasm` runs headless under `wasmtime`, whereas
`browser-wasm` boots through a small JavaScript module (`wwwroot/main.js`) that loads
`dotnet.js` and invokes the generated `Program.Main`.

## Build & run

> **Not part of `TestFx.slnx`.** `browser-wasm` needs the `wasm-tools` workload even to
> *build*. The repo bootstrap installs that workload (see `eng/restore-toolset`), but
> adding this project to the solution would still break the default `.\build.cmd` on any
> environment where the workload is missing, so `BrowserPlayground` is intentionally
> excluded from the solution and built on demand with the explicit `dotnet publish` below.

Prerequisites:

- The repo-local .NET SDK (`.dotnet\dotnet.exe` on Windows, `.dotnet/dotnet` on
  Linux/macOS). Bootstrap it once by running `.\build.cmd` (Windows) or `./build.sh`
  (Linux/macOS) from the repo root. The bootstrap also installs the `wasm-tools-net10`
  workload this sample needs (see
  [`eng/restore-toolset.ps1`](../../eng/restore-toolset.ps1) /
  [`eng/restore-toolset.sh`](../../eng/restore-toolset.sh)). If you use a machine-installed
  SDK instead, install the workload manually:

  ```
  dotnet workload install wasm-tools-net10
  ```

- For the headless run: [Node.js](https://nodejs.org/) on `PATH`.
  For the browser run: any static web server (see step 2b).

> The commands below use the repo-local SDK so the `.dotnet/packs` lookup resolves — that
> is `.\.dotnet\dotnet.exe` on Windows and `./.dotnet/dotnet` on Linux/macOS (shown as
> `dotnet` below for brevity). Swap in a machine-installed `dotnet` if you prefer. Windows
> examples use `\` path separators; on Linux/macOS use `/` (e.g.
> `artifacts/bin/BrowserPlayground/Debug/net10.0/browser-wasm/AppBundle`).

### 1. Publish

```
dotnet publish samples/BrowserPlayground/BrowserPlayground.csproj -c Debug -f net10.0
```

The published app bundle lands under
`artifacts/bin/BrowserPlayground/Debug/net10.0/browser-wasm/AppBundle` and contains
`_framework/dotnet.js`, the managed assemblies, `index.html`, `main.js`, and
`runtests.mjs`.

### 2a. Run headlessly under Node

From the bundle directory:

```
cd artifacts/bin/BrowserPlayground/Debug/net10.0/browser-wasm/AppBundle
node runtests.mjs
```

`runtests.mjs` boots the same bundle under Node (no DOM required — Microsoft.Testing.Platform
never touches the DOM), forwards stdout/stderr, and maps the .NET exit code onto the Node
process exit code. This is the mode the `BrowserWasmExecutionTests` acceptance test uses.

### 2b. Run in a browser

Serve the bundle over HTTP (WebAssembly cannot be loaded from `file://`) and open it. Any
static file server works; two options that need no extra provisioning:

```
cd artifacts/bin/BrowserPlayground/Debug/net10.0/browser-wasm/AppBundle

# Using Node (installed for step 2a):
npx --yes http-server -o

# ...or using Python 3:
python3 -m http.server 8080
```

Then browse to the served URL (`http-server` opens it automatically; for the Python server
open <http://localhost:8080/>). The test run output appears in the browser's developer-tools
**Console**. Pass Microsoft.Testing.Platform options through the page query string, e.g.
`index.html?arg=--minimum-expected-tests&arg=1`.

## Status

Microsoft.Testing.Platform boots on `browser-wasm` (the banner reads
`[browser-wasm - net10.0]`) and tests run to completion, exactly as on `wasi-wasm`.
Running this sample — which hosts a `DummyFramework` with a single passing test —
produces:

```console
Test run summary: Passed!
  total: 1
  failed: 0
  succeeded: 1
  skipped: 0
```

MSTest itself (not just the raw platform) also runs end-to-end on `browser-wasm`; that
is covered by the gated `BrowserWasmExecutionTests.BrowserWasmExecution_RunsTestsUnderNode`
acceptance test (see
[`test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserWasmExecutionTests.cs`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserWasmExecutionTests.cs)),
which publishes a real MSTest project and runs it under Node.

### Why it works

The single-threaded WebAssembly constraint is the same as `wasi-wasm`: there is no
thread pool, so `Task.Run` continuations never execute and blocking waits throw
`PlatformNotSupportedException`. The platform and the MSTest adapter detect this
(`RuntimeFeatureHelper.IsMultiThreaded` / `RuntimeContext.IsMultiThreaded`, derived from
`OperatingSystem.IsBrowser()`) and fall back to inline/synchronous execution on the run
path — the message-bus consumers, the shutdown watchdog, the telemetry ingest loop, the
countdown-event wait, and the adapter's per-test task factory.

Console output is routed through `BrowserOutputDevice`, which forwards to the browser's
`globalThis.console.*` APIs via `[JSImport]`.

### Feature matrix (what is *not* available on browser-wasm)

Several extensions/capabilities are intentionally unavailable in the browser sandbox and
are guarded off by `OperatingSystem.IsBrowser()` in the platform:

| Feature | Reason |
| --- | --- |
| TRX report (`--report-trx`) | `TrxDataConsumer` creates a `TrxResultStreamingStore` whose background writer uses `BlockingCollection<T>` and `ITask.RunLongRunning`, both unsupported on browser; the TRX lifecycle handlers are gated by `OperatingSystem.IsBrowser()`. |
| Hang dump / crash dump | Rely on `System.Diagnostics.Process`, unsupported in the browser (see [#8557](https://github.com/microsoft/testfx/issues/8557)). |
| Azure DevOps report | Its `HttpClient` sets `AutomaticDecompression`, unsupported by the browser `HttpClientHandler`. |
| Server mode / `dotnet test` pipe | Depends on TCP/named-pipe IPC, unavailable in the browser. |
| `--exit-on-process-exit`, wait-for-debugger | No host process model in the browser. |
| Synchronous file-logger flush | Not supported in the browser (throws `PlatformNotSupportedException`). |

That is why `Program.cs` registers only the telemetry provider (`AddAppInsightsTelemetryProvider`).

## Build configuration notes

`BrowserPlayground.csproj` ships with a few non-obvious switches:

| Property | Reason |
| --- | --- |
| `SelfContained=true` | Publishing browser-wasm must be self-contained so the `Microsoft.NETCore.App.Runtime.Mono.browser-wasm` runtime pack is resolved. Targeting `net10.0` under an `11.0` SDK does not infer this on its own, so publish fails with an empty `MicrosoftNetCoreAppRuntimePackDir` without it. |
| `WasmBuildNative=false` | Use the pre-built `dotnet.native.wasm`. A native relink needs the emscripten toolchain (pulled in by the `wasm-tools` workload) and is slower; the pre-built runtime is enough to boot MTP. |
| (no `InvariantGlobalization`) | On browser-wasm, `InvariantGlobalization=true` forces `WasmBuildNative=true` (an emscripten relink), so it is intentionally left unset — the browser bundle ships ICU (`icudt*.dat`) by default. This differs from `WasiPlayground`, which keeps `InvariantGlobalization=true` and stages `icudt.dat` manually. |
| `WasmExtraFilesToDeploy` | Copies the node runner (`runtests.mjs`) and `index.html` into the published `AppBundle` next to `main.js` (the `WasmMainJSPath` module). Plain `CopyToOutputDirectory` would only reach the build output folder, not the `AppBundle`. |

