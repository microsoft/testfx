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
    <PackageReference Include="MSTest.TestFramework" Version="VERSION" />
    <PackageReference Include="MSTest.TestAdapter" Version="VERSION" />
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

- appends its launcher target to the SDK-owned
  `ComputeTestingPlatformRunArgumentsDependsOn` extension point and writes
  dedicated `TestingPlatformRunCommand`,
  `TestingPlatformRunArguments`, and `TestingPlatformRunWorkingDirectory`
  values without modifying the canonical `dotnet run` properties;
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
- launches Playwright in a package-private worker behind a startup barrier and
  owns that worker plus its host, Node driver, and Chromium descendants through
  a Windows kill-on-close job or a POSIX process group;
- owns bounded cleanup on completion, failure, timeout, cancellation, or parent
  process loss.

`TestingPlatformBrowserStartupTimeoutSeconds` defaults to 60 seconds and
`TestingPlatformBrowserCompletionTimeoutSeconds` defaults to 600 seconds. The
reserved `/_mtp/` path avoids colliding with a consumer-owned root page; no
public framework-page integration protocol is provided.

The package bundles Playwright's upstream Node.js driver payloads for
`win32-x64`, `linux-x64`, `linux-arm64`, `darwin-x64`, and `darwin-arm64` by
default. Source-build or distribution builds can pack with
`TestingPlatformBrowserBundlePlaywrightNode=false` and consumers can point the
launcher at an approved host Node.js executable through
`TestingPlatformBrowserNodeExecutable`. This is a packaging seam, not a claim
that source-build or offline distribution validation is complete.

Ordinary desktop targets and canonical `ComputeRunArguments` calls remain
unchanged. The package requires an SDK that advertises
`TestingPlatformRunArgumentsVersion=1`; there is no legacy ordering or caller
marker fallback.

This minimum proof of concept supports only the canonical
`Microsoft.NET.Sdk.WebAssembly` and WasmAppHost combination. The .NET SDK owns
launcher selection and invokes the optional browser extension target after the
framework's canonical `ComputeRunArguments` target has completed.

## Validated proof-of-concept matrix

The composed product has been validated with source-matched SDK and TestFX
packages on:

- Windows x64 with an installed Chromium-family browser.
- Debian Linux x64 with native Chromium 154.0.8037.92.

Both environments cover run, discovery, matching and zero-result filters,
project-configured arguments, managed cancellation, artifact export and
integrity, quota rejection, exit-code propagation, and process cleanup.
macOS, Linux arm64, and Windows arm64 have package-layout or compile coverage
only; this experiment does not claim runtime support for those hosts.

## Packaging, source-build, and signing boundary

- The default package is self-contained for the private Playwright transport:
  it includes the managed Playwright assembly, required JavaScript modules,
  Node.js license/notice data, and the host-specific Node.js payloads listed
  above. The browser executable remains an explicit external prerequisite; the
  package never downloads a browser.
- A distribution can omit bundled Node.js and use an approved system Node.js
  executable. A full source-build policy still needs an approved source or
  binary provenance decision for Microsoft.Playwright, Node.js, and the chosen
  browser, plus offline restore/build validation in the target distribution.
- Unused Playwright UI/type/font payloads are excluded. Signing policy exempts
  only the exact package-owned and vendored JavaScript filenames; there is no
  repository-wide JavaScript or font bypass.
- Arcade validation has passed with `SignType=real` and `DryRun=true`. That
  proves signing policy and container repacking only. It is not official
  signing, macOS notarization, package publication, or release approval.

## Current limitations

- Browser virtual-file-system artifacts are exported only when the SDK
  negotiates `FileArtifactTransfer=chunked-v1`. The SDK owns destination paths
  and transfer limits; an older peer receives a clear warning and browser-local
  paths are not reported as host artifacts.
- Browser and WASI hosts deliberately do not advertise artifact postprocessors:
  an SDK host-filesystem manifest cannot be consumed inside their virtual
  filesystem. Exported original artifacts are preserved, while a future
  host-side postprocessing contract remains productization work.
- User-authored `@response-file` arguments are not supported by this experiment.
  Only the final private SDK HTTP bootstrap response file is expanded.
- The SDK authenticated HTTP bootstrap, dedicated run-argument target, HTTP
  cancellation capability, and artifact-transfer capability are experimental
  cross-repository contracts.
- This experiment assumes a trusted test project and package-owned host page.
  Top-frame and exact-origin binding checks prevent accidental cross-frame or
  cross-origin use; they do not authenticate another script already executing
  in the same page. The SDK gateway pins the first accepted Origin (including
  preflight) rather than accepting launcher preregistration.
- The package uses the framework-provided WasmAppHost and depends on its exact
  HTTP readiness output; shared external-host readiness is unresolved.
- Managed MTP receives SDK cancellation through capability-gated authenticated
  HTTP long polls and uses its existing graceful-stop capability before the
  SDK's grace-period force termination.
- The supervisor owns the worker's process tree, but no user-mode implementation
  can guarantee cleanup if external tooling simultaneously sends an
  uncatchable kill to every supervisor and worker process.
- Preview or stable release still requires official signing and publishing
  approval, third-party license/SBOM and source-build approval, dependency flow
  of the matching SDK protocol/launcher contract, runtime validation for every
  advertised OS/architecture, and an explicit servicing owner for Playwright,
  Node.js, and browser cadence.
- The package is emitted to the repository's Shipping artifacts for acceptance
  testing, but is not included in the product shipment layout. Enabling
  publication is a separate human release decision.

Microsoft.Testing.Platform is open source. You can find
`Microsoft.Testing.Platform.Browser` in the
[microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Documentation

For comprehensive Microsoft Testing Platform documentation, see
<https://aka.ms/testingplatform>.

## Feedback & contributing

Provide feedback or report issues in the
[microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
