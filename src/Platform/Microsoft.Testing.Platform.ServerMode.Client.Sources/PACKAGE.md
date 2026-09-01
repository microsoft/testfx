# Microsoft.Testing.Platform.ServerMode.Client.Sources

A **source-only** client for the Microsoft Testing Platform (MTP) server-mode JSON-RPC protocol.

This package ships no assembly. When you reference it, its C# files are injected into your project and
compiled (as `internal` types) into your own assembly. That means:

- **No runtime dependency** and no extra DLL to deploy.
- **Native-AOT friendly** and dependency-free serialization: the vendored `Jsonite` JSON engine on
  .NET Framework / `netstandard2.0`, and in-box `System.Text.Json` (no reflection) on .NET.
- **Wire-compatible by construction**: the protocol types and serialization are the *same* source
  files the platform server compiles, shipped from the protocol owner (testfx).

## What you get

- `MtpServerClient` / `IMtpServerClient` — launch an MTP test app in server mode and drive it:
  `InitializeAsync`, `DiscoverTestsAsync`, `RunTestsAsync`, `ExitAsync`, plus a `TestNodesUpdated`
  event.
- Two launch paths: an **external process** (`LaunchAsync(path)`) for IDE and desktop tooling, and an
  **in-process host** (`LaunchInProcessAsync(callback)`) for embedded runners (MAUI, Android/iOS test
  apps) that cannot spawn a child process.
- The launch/transport layer (loopback TCP listener the app dials back to, LSP-style
  `Content-Length` framing) and the strongly-typed protocol records.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Platform.ServerMode.Client.Sources
```

## Usage

The injected client types use the `Microsoft.Testing.Platform.ServerMode.Client` namespace:

```csharp
using Microsoft.Testing.Platform.ServerMode.Client;

using IMtpServerClient client = await MtpServerClient.LaunchAsync(testApplicationPath);
client.TestNodesUpdated += (_, update) => Console.WriteLine(update.RunId);

await client.InitializeAsync();
await client.DiscoverTestsAsync();
MtpRunResult result = await client.RunTestsAsync();
await client.ExitAsync();
```

### Embedded hosts (no child process)

`LaunchInProcessAsync` runs the test application in **your own process**. You supply only "how to run
the application"; the client still owns the loopback listener, the server-mode arguments, the connect
race, the serializer/formatter/transport setup and the shutdown sequence:

```csharp
using IMtpServerClient client = await MtpServerClient.LaunchInProcessAsync(
    async (serverArgs, token) =>
    {
        // serverArgs is the complete server-mode argument array
        // (--server jsonrpc --client-host … --client-port … --no-banner).
        // Forward it verbatim; do not filter or reorder it.
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(serverArgs);
        builder.AddMSTest(() => testAssemblies);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    },
    options,
    cancellationToken);

await client.InitializeAsync();
await client.DiscoverTestsAsync();
await client.RunTestsAsync();
await client.ExitAsync();

// Non-blocking teardown. The trailing Dispose from the `using` is then a no-op.
await client.ShutdownAsync();
Console.WriteLine(client.ServerExitCode);
```

Things to know:

- **Ownership.** The returned client owns the hosted application. Tearing it down closes the transport —
  which is how a server-mode application is asked to stop — and then waits for your callback. Call
  `ExitAsync` first for a protocol-level shutdown.
- **Prefer `ShutdownAsync()` over `Dispose()`.** `Dispose()` performs that wait **synchronously on the
  calling thread**, so on a platform with a responsiveness watchdog (Android ANR, the iOS watchdog) it can
  trip it. `await client.ShutdownAsync()` does the same work without blocking; a following `Dispose()`
  returns immediately. Both are idempotent.
- **Bounded shutdown.** Teardown waits at most `MtpServerClientOptions.ServerShutdownTimeout`
  (default 30 seconds), then cancels the token passed to your callback and waits a further fixed
  5 seconds. A callback still running after that is abandoned rather than hanging your app; its failure
  is reported through `MtpServerClientOptions.Logger`. A *failed launch* skips the graceful wait
  entirely — the callback's token is canceled immediately and only the fixed 5-second grace applies —
  so an unwinding caller never waits for `ServerShutdownTimeout`.
- **Cancellation.** The `cancellationToken` passed to `LaunchInProcessAsync` scopes the *launch* only.
  Once the client exists, canceling it no longer affects the hosted application. Cancellation is bounded
  rather than immediate: the unwind waits up to 5 seconds for the callback to stop before abandoning it.
  Per-request cancellation is unchanged — canceling a `RunTestsAsync`/`DiscoverTestsAsync` token sends
  `$/cancelRequest`.
- **Exit code.** `client.ServerExitCode` carries the value your callback returned (typically
  `TestApplication.RunAsync`'s exit code) once teardown has completed.
- **Failures before connection.** If the callback throws, is canceled, or returns before dialing back,
  the launch fails with `MtpServerConnectionClosedException` and your exception is preserved as the
  inner exception (instead of surfacing as a misleading connection timeout).
- **Failures after connection.** `ShutdownAsync` finishes tearing down the connection and then rethrows the
  callback's original exception. `Dispose` remains non-throwing and reports the same failure through
  `MtpServerClientOptions.Logger`, so cleanup cannot mask an exception already leaving your code.
- **Threading.** The callback is invoked on the thread pool, so the launch never blocks the caller and
  the callback never inherits the caller's synchronization context. There is no synchronous
  `LaunchInProcess` overload on purpose.
- **`EnvironmentVariables` is ignored** on this path: the application shares your process's
  environment. Set the variables before starting the host.
- **Platform limits.** Both launch paths use loopback TCP. On browser/WASM there is no listening
  socket, so `LaunchInProcessAsync` throws `PlatformNotSupportedException`. This package does **not**
  enable server-mode testing in the browser.

## Consumer requirements

Because the source is compiled into your assembly, your project must provide the ambient pieces the
shared source expects (the three first-party consumers — vstest, VSUnitTesting, C# Dev Kit — already
do):

- **C# language version**: the package's build targets set `LangVersion=12.0` for you when your
  project hasn't pinned one, so normally you need to do nothing. If you *do* pin `LangVersion`
  explicitly, it must be **C# 12** or newer.
- On `net462` / `netstandard2.0`: the package **ships the internal polyfills it needs itself**
  (nullable attributes, `IsExternalInit`, required-member/compiler-feature attributes, and a small set
  of runtime helpers), each `internal` and self-guarded. If your project already defines one, add the
  corresponding opt-out constant:
  `MTP_CLIENT_EXCLUDE_CALLER_ARGUMENT_EXPRESSION_ATTRIBUTE`,
  `MTP_CLIENT_EXCLUDE_COMPILER_FEATURE_REQUIRED_ATTRIBUTE`,
  `MTP_CLIENT_EXCLUDE_EXPERIMENTAL_ATTRIBUTE`,
  `MTP_CLIENT_EXCLUDE_IS_EXTERNAL_INIT`,
  `MTP_CLIENT_EXCLUDE_NULLABLE_ATTRIBUTES`,
  `MTP_CLIENT_EXCLUDE_REQUIRED_MEMBER_ATTRIBUTE`, or
  `MTP_CLIENT_EXCLUDE_UNREACHABLE_EXCEPTION`.
  Benign source-vs-imported polyfill warnings are suppressed only inside the generated package sources;
  the package does not alter the consumer project's warnings.

The injected types are `internal`; delete any previous hand-written MTP client in your repo when you
adopt this package to avoid duplicate symbols.

## Documentation

For the server-mode JSON-RPC protocol, see <https://github.com/microsoft/testfx/blob/main/docs/mstest-runner-protocol/001-protocol-intro.md>.

For comprehensive Microsoft.Testing.Platform documentation, see <https://aka.ms/testingplatform>.
