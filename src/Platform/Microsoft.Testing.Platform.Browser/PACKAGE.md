# Microsoft.Testing.Platform.Browser

> **Experiment:** This package is a proof of concept for design validation. It is
> not a supported package or a shipping commitment.

`Microsoft.Testing.Platform.Browser` is an optional launcher package for running
pure-managed `browser-wasm` Microsoft Testing Platform applications through
`dotnet test`. It keeps browser dependencies and browser release cadence out of
core Microsoft.Testing.Platform.

## Usage

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TestingPlatformBrowserExecutable>PATH_TO_CHROMIUM</TestingPlatformBrowserExecutable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="VERSION" />
    <PackageReference Include="Microsoft.Testing.Platform.Browser" Version="0.1.0-alpha" />
  </ItemGroup>
</Project>
```

The canonical application contains only managed test code. MTP generates
`Main(string[] args)`, and the package injects the arguments prepared by
`dotnet test` before that entry point runs. Managed `HttpClient` sends discovery,
progress, and results directly from MTP to the SDK authenticated HTTP gateway.
User and test code need no HTML, JavaScript, Blazor `IJSRuntime`, or `[JSImport]`.

Browser WebAssembly still needs host JavaScript. The package supplies a minimal
page at `/_mtp/browser-host.html` and private boot supervisor that dynamically
imports the root `_framework/dotnet.js`, obtains the MTP arguments through a private Playwright
binding, invokes `runMain`, and reports only the terminal exit code or bootstrap
failure to the launcher. It does not discover tests, execute tests, parse
results, or relay the MTP HTTP protocol.

## Experiment contract

The package:

- wraps the browser framework's existing `ComputeRunArguments` result only when
  the caller sets the proof-of-concept marker `DotnetTestInvocation=true`;
- requires an explicit installed Chromium-family browser through
  `TestingPlatformBrowserExecutable`;
- launches that browser through Playwright's private transport in an isolated
  context, with no unauthenticated DevTools TCP endpoint;
- recognizes the WasmAppHost readiness line
  `App url: http://<loopback-address>/` and rejects non-loopback origins;
- makes argument and completion bindings available only to the expected
  top-level page at the exact host origin;
- keeps the SDK HTTP bearer token out of URLs and redacts it from bounded host,
  browser, and launcher diagnostics;
- owns bounded cleanup of the browser context, browser, and host process tree on
  completion, failure, timeout, or cancellation.

`TestingPlatformBrowserStartupTimeoutSeconds` defaults to 60 seconds and
`TestingPlatformBrowserCompletionTimeoutSeconds` defaults to 600 seconds. The
reserved `/_mtp/` path avoids colliding with a consumer-owned root page; no
public framework-page integration protocol is provided.

Ordinary desktop targets and unmarked `ComputeRunArguments` calls remain
unchanged.

This minimum proof of concept supports only the canonical
`Microsoft.NET.Sdk.WebAssembly` and WasmAppHost combination. The package keeps a
simple `AfterTargets="ComputeRunArguments"` hook; a project target that also
runs after `ComputeRunArguments` can subsequently replace the browser wrapper
and is unsupported by this experiment. A product preview must move launcher
selection into the .NET SDK rather than adding more package-level ordering
machinery.

## Current limitations

- Browser virtual-file-system artifacts are not exported. TRX, coverage,
  diagnostics, and other file reports may remain only in the browser VFS.
- User-authored `@response-file` arguments are not supported by this experiment.
  Only the final private SDK HTTP bootstrap response file is expanded.
- The SDK authenticated HTTP bootstrap and invocation marker are experimental
  cross-repository contracts. The current proof-of-concept caller sets
  `DotnetTestInvocation`; the shipping SDK does not yet own this selection.
- The package uses the framework-provided WasmAppHost and depends on its exact
  HTTP readiness output; shared external-host readiness is unresolved.
- Managed MTP does not yet receive graceful cancellation before the launcher
  starts bounded process cleanup.
- A permanently wedged Playwright driver can still leave its private process
  orphaned after the bounded call-site cleanup expires. A stable implementation
  needs explicit ownership of the transport process.
- The experimental package currently bundles Playwright's cross-platform Node
  driver payload. Package size, source-build, signing, platform validation, and
  servicing must be resolved before any preview or stable productization.
- The package is emitted to the repository's Shipping artifacts for acceptance
  testing, but is not included in the product shipment layout.

Microsoft.Testing.Platform is open source. You can find
`Microsoft.Testing.Platform.Browser` in the
[microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Documentation

For comprehensive Microsoft Testing Platform documentation, see
<https://aka.ms/testingplatform>.

## Feedback & contributing

Provide feedback or report issues in the
[microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
