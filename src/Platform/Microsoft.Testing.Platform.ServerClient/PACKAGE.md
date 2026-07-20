# Microsoft.Testing.Platform.ServerClient.Source

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

## Consumer requirements

Because the source is compiled into your assembly, your project must provide the ambient pieces the
shared source expects (the three first-party consumers — vstest, VSUnitTesting, C# Dev Kit — already
do):

- C# language version 9 or later.
- On `net462` / `netstandard2.0`: the usual polyfills (nullable attributes, `IsExternalInit`,
  index/range, `System.HashCode`, `ValueTask`) and framework references (`System.Memory`,
  `System.Threading.Tasks.Extensions`). This package intentionally does **not** ship polyfills, to
  avoid duplicate-type collisions with the polyfills consumers already have.

The injected types are `internal`; delete any previous hand-written MTP client in your repo when you
adopt this package to avoid duplicate symbols.
