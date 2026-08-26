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
