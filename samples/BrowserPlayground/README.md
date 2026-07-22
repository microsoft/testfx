# BrowserPlayground

Demonstrates hosting [Microsoft.Testing.Platform](https://aka.ms/testingplatform/) on
`browser-wasm` — the WebAssembly runtime that runs inside a browser (and, headlessly,
under [Node.js](https://nodejs.org/) via the same `dotnet.js` loader).

This is the browser counterpart to the `wasi-wasm` acceptance coverage in
[`WasmExecutionTests.cs`](../../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/WasmExecutionTests.cs)
(and its MSTest sibling under `MSTest.Acceptance.IntegrationTests`). Both `wasi-wasm` and
`browser-wasm` run on a single-threaded WebAssembly runtime; the platform work that makes
that possible is shared (see [#2196](https://github.com/microsoft/testfx/issues/2196)). The
only real difference is the **host**: `wasi-wasm` runs headless under `wasmtime`, whereas
`browser-wasm` boots through a small JavaScript module (`wwwroot/main.js`) that loads
`dotnet.js` and invokes the sample's `Program.Main` (defined by the top-level statements
in `Program.cs`).

## Build & run

> **Not part of `TestFx.slnx`.** `browser-wasm` needs the `wasm-tools` workload even to
> *build*. The repo bootstrap (`.\build.cmd` / `./build.sh`) installs that workload via
> `eng/restore-toolset`, so a bootstrapped build already has it. It is kept out of the
> solution to protect **builds that bypass the bootstrap** — most notably opening
> `TestFx.slnx` in an IDE on a fresh clone, or any environment where the workload has not
> been installed — from failing the whole solution build. So `BrowserPlayground` is built
> on demand with the explicit `dotnet publish` below rather than as part of the solution.

Prerequisites:

- The repo-local .NET SDK (`.dotnet\dotnet.exe` on Windows, `.dotnet/dotnet` on
  Linux/macOS). Bootstrap it once by running `.\build.cmd` (Windows) or `./build.sh`
  (Linux/macOS) from the repo root. The bootstrap also installs the `wasm-tools-net10`
  workload this sample needs (see
  [`eng/restore-toolset.ps1`](../../eng/restore-toolset.ps1) /
  [`eng/restore-toolset.sh`](../../eng/restore-toolset.sh)). If you use a machine-installed
  SDK instead, install the workload matching that SDK:

  ```sh
  # .NET 10 SDK
  dotnet workload install wasm-tools

  # Newer SDK cross-targeting .NET 10
  dotnet workload install wasm-tools-net10
  ```

- **.NET 10 is sufficient.** A .NET 11 preview SDK is not required to build or run MSTest
  on `browser-wasm`; use a .NET 10 SDK with the `wasm-tools` workload. This repository's
  newer repo-local SDK uses the `wasm-tools-net10` cross-targeting workload, but that is a
  repository toolchain choice rather than an MSTest browser-WASM prerequisite.

- For the headless run: [Node.js](https://nodejs.org/) on `PATH`.
  For the browser run: any static web server (see step 2b).

> The commands below use the repo-local SDK so the `.dotnet/packs` lookup resolves — that
> is `.\.dotnet\dotnet.exe` on Windows and `./.dotnet/dotnet` on Linux/macOS (shown as
> `dotnet` below for brevity). Swap in a machine-installed `dotnet` if you prefer. The paths
> below use forward slashes, which work on all platforms; on Windows you can also use `\`.

### 1. Publish

```sh
dotnet publish samples/BrowserPlayground/BrowserPlayground.csproj -c Debug -f net10.0
```

The published app bundle lands under
`artifacts/bin/BrowserPlayground/Debug/net10.0/browser-wasm/AppBundle` and contains
`_framework/dotnet.js`, the managed assemblies, `index.html`, `main.js`, and
`runtests.mjs`.

### 2a. Run headlessly under Node

From the bundle directory:

```sh
cd artifacts/bin/BrowserPlayground/Debug/net10.0/browser-wasm/AppBundle
node runtests.mjs
```

`runtests.mjs` boots the same bundle under Node (no DOM required — Microsoft.Testing.Platform
never touches the DOM), forwards stdout/stderr, and maps the .NET exit code onto the Node
process exit code. This is the mode the `BrowserWasmExecutionTests` acceptance test uses.

### 2b. Run in a browser

Serve the bundle over HTTP (WebAssembly cannot be loaded from `file://`) and open it. Any
static file server works; two options that need no extra provisioning:

```sh
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

## Host an existing MSTest assembly

`BrowserPlayground` uses a tiny custom test framework to demonstrate the raw platform. A
real application normally has an existing MSTest project and a small `browser-wasm` host
that tells MSTest which assembly to discover. The test project can remain a class library:

```xml
<!-- ExistingTests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="VERSION" />
  </ItemGroup>
</Project>
```

Create a separate host project beside it and copy this sample's `wwwroot/main.js`,
`wwwroot/runtests.mjs`, and `wwwroot/index.html` into the host:

```xml
<!-- ExistingTests.BrowserHost.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <SelfContained>true</SelfContained>

    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>

    <WasmMainJSPath>wwwroot\main.js</WasmMainJSPath>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="VERSION" />
    <ProjectReference Include="..\ExistingTests\ExistingTests.csproj" />
  </ItemGroup>

  <ItemGroup>
    <WasmExtraFilesToDeploy Include="wwwroot\runtests.mjs" />
    <WasmExtraFilesToDeploy Include="wwwroot\index.html" />
  </ItemGroup>
</Project>
```

The host owns its entry point and registers the existing test assembly explicitly:

```csharp
using ExistingTests;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => [typeof(SomeExistingTestClass).Assembly]);

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
```

The two MSBuild properties are both intentional:

- `EnableMSTestRunner=true` selects Microsoft.Testing.Platform and disables the
  `AutoGeneratedProgram.Main` supplied by the `Microsoft.NET.Test.Sdk` that the `MSTest`
  metapackage references transitively.
- `GenerateTestingPlatformEntryPoint=false` disables MTP's generated entry point because
  this host supplies its own top-level `Program.cs`.

Together they avoid compiler warning CS7022 without suppressing it. The host needs only the
`MSTest` metapackage; do not add explicit `MSTest.TestAdapter`,
`Microsoft.NET.Test.Sdk`, or `Microsoft.Testing.Platform` references. Keep every MSTest
package in the project graph on the **same version**: use the same `VERSION` for the host's
`MSTest` and the test library's `MSTest.TestFramework` (or update an existing test project's
`MSTest` reference to that version). Mixing an older framework/adapter with a newer host can
produce restore-time version conflicts or runtime incompatibilities; adding another explicit
adapter reference only masks the misalignment.

Publish and run the host exactly like the playground:

```sh
dotnet publish ExistingTests.BrowserHost/ExistingTests.BrowserHost.csproj -c Debug -f net10.0
cd ExistingTests.BrowserHost/bin/Debug/net10.0/browser-wasm/AppBundle

# Discover names before running a large suite.
node runtests.mjs --list-tests

# Run a small slice with per-test output and a run-level safety limit.
node runtests.mjs --output Detailed \
  --treenode-filter "/*/*/*/SomeExistingTestClass/SomeTestMethod" \
  --timeout 10m
```

Use `--list-tests json` when a script needs stable discovery output. Start with a class or
method filter and widen it gradually to isolate a hang; `--filter-uid` is also available when
you already know test UIDs. `--timeout` limits the whole run and requires a unit suffix such
as `30s`, `10m`, or `1h`. It can cancel asynchronous/cooperative work, but browser-WASM is
single-threaded: code that blocks the runtime thread without yielding cannot be preempted, so
filtering/bisection remains the reliable way to identify that kind of hang.

## Debugging: JavaScript host versus managed code

`browser-wasm` does not run the test assembly in the `dotnet` process that launches the
application. This explains the behavior reported in
[#2196](https://github.com/microsoft/testfx/issues/2196#issuecomment-5032231745): there are
separate layers:

1. `dotnet run` starts the .NET SDK's `WasmAppHost`.
2. `WasmAppHost` starts Node or serves the bundle to a browser.
3. `_framework/dotnet.js` loads the Mono runtime compiled to WebAssembly, and Mono executes
   the managed test assemblies inside the JavaScript host.

Consequently, attaching Visual Studio's managed debugger to the outer `dotnet` process only
debugs `WasmAppHost`. It neither follows the child Node process nor establishes the Mono
WebAssembly debugger-agent/proxy connection needed to resolve C# portable PDBs and managed
frames. Similarly, a Node or browser JavaScript debugger can inspect the loader, promises,
event loop, and JavaScript/WebAssembly frames, but that alone does not make C# breakpoints work.

| Host and debugger | What it can debug | Support for this sample |
| --- | --- | --- |
| Node inspector, Chrome/Edge DevTools, or the Visual Studio/VS Code JavaScript debugger | `runtests.mjs`, `dotnet.js`, JavaScript interop, and V8/WebAssembly state | Supported. This is JavaScript debugging, not managed C# debugging. |
| Visual Studio or VS Code managed debugger attached to Node | C# executing inside Mono WebAssembly | No supported .NET 10 workflow is documented for a `browser-wasm` app hosted by Node. |
| Browser developer tools against the statically served bundle | JavaScript, console output, network activity, and browser WebAssembly state | Supported. The static server used above does not start the .NET managed-debug proxy. |
| Visual Studio or VS Code through the .NET browser debug proxy | Managed C# in Mono WebAssembly, including breakpoints, stepping, locals, and managed/JavaScript call stacks | Supported by the .NET 10 browser tooling, but the proxy must be part of the launch. It is not provided by `node runtests.mjs`, `http-server`, or `python -m http.server`. |

### Inspect the headless Node host

To diagnose the JavaScript side of a hanging headless run, start Node's inspector and pause
before the module loads:

```sh
cd artifacts/bin/BrowserPlayground/Debug/net10.0/browser-wasm/AppBundle
node --inspect-brk runtests.mjs
```

Attach with `chrome://inspect` / `edge://inspect`, the
[VS Code Node.js debugger](https://code.visualstudio.com/docs/nodejs/nodejs-debugging), or
[Visual Studio's JavaScript debugger for Node.js](https://learn.microsoft.com/en-us/visualstudio/javascript/debug-nodejs?view=visualstudio).
Use `--inspect` instead of `--inspect-brk` when the test run should start immediately. These
tools are useful for finding an unresolved JavaScript promise, a blocked host callback, or a
loader/interop problem. They cannot step through the managed test method.

### Debug managed C# in a real browser

.NET's managed browser debugger uses a proxy between the IDE/browser DevTools protocol and the
Mono debugger agent. A plain static server is therefore insufficient. The supported .NET 10
reference flows are:

- [Debug ASP.NET Core Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/debug?view=aspnetcore-10.0)
  with Visual Studio or VS Code. The documented `inspectUri` tells the IDE to connect through
  the framework's debug proxy.
- The official .NET 10
  [`wasmbrowser` template launch profile](https://github.com/dotnet/runtime/blob/release/10.0/src/mono/wasm/templates/templates/browser/Properties/launchSettings.json),
  which contains that proxy `inspectUri`, together with the
  [`Microsoft.NET.Sdk.WebAssembly` project](https://github.com/dotnet/runtime/blob/release/10.0/src/mono/wasm/templates/templates/browser/browser.0.csproj).
- The runtime's
  [`WasmAppHost --debug` support](https://github.com/dotnet/runtime/blob/release/10.0/src/mono/wasm/host/README.md),
  which starts the browser debug server when a browser host configuration is used.

By contrast, the official .NET 10
[`wasmconsole` Node template](https://github.com/dotnet/runtime/tree/release/10.0/src/mono/wasm/templates/templates/console)
has no managed-debug launch profile. BrowserPlayground intentionally keeps its small,
host-neutral publish/run setup, so it does not check in an IDE launch configuration that would
pretend to enable managed debugging under Node. If managed breakpoints are required, reproduce
the failing run through a debug-proxy-enabled browser project rather than attaching to the outer
`dotnet` process.

The open draft [dotnet/sdk#55389](https://github.com/dotnet/sdk/pull/55389) is related to
launching WebAssembly test hosts from `dotnet test`, not to managed debugger attachment. Its
current draft uses standalone execution and assembly exit codes while richer reporting remains
gated, so do not depend on it for per-test progress, live transport, or Node-hosted C# debugging.

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
| (no `InvariantGlobalization`) | On browser-wasm, `InvariantGlobalization=true` forces `WasmBuildNative=true` (an emscripten relink), so it is intentionally left unset — the browser bundle ships ICU (`icudt*.dat`) by default. This differs from the `wasi-wasm` acceptance asset (`WasmExecutionTests.cs`), which keeps `InvariantGlobalization=true` and stages `icudt.dat` manually via `WasmRuntime.StageIcuData`. |
| `WasmExtraFilesToDeploy` | Copies the node runner (`runtests.mjs`) and `index.html` into the published `AppBundle` next to `main.js` (the `WasmMainJSPath` module). Plain `CopyToOutputDirectory` would only reach the build output folder, not the `AppBundle`. |
