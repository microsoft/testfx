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

- **C# language version**: the package's build targets set `LangVersion=latest` for you when your
  project hasn't pinned one, so normally you need to do nothing. If you *do* pin `LangVersion`
  explicitly, it must be new enough to parse the shipped source: on `net462` / `netstandard2.0` that
  currently means **C# 14** (the down-level `OperatingSystem` polyfill uses the C# 14 extension-member
  syntax); on .NET a lower version compiles. Using `latest` is the safe choice.
- On `net462` / `netstandard2.0`: the package **ships the internal polyfills it needs itself**
  (nullable attributes, `IsExternalInit`, required-member/compiler-feature attributes, and a small set
  of runtime helpers), each `internal`, self-guarded (they no-op on modern .NET), and opt-out-able via
  `EXCLUDE_*` compilation constants. It also references the usual down-level framework assemblies
  through its packaged build targets. If your project already defines one of these polyfills, either
  define the matching `EXCLUDE_*` constant or rely on the packaged targets, which `NoWarn` the benign
  `CS0436` source-vs-imported-type collision for the direct consumer.

The injected types are `internal`; delete any previous hand-written MTP client in your repo when you
adopt this package to avoid duplicate symbols.
