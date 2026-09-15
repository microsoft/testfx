# Microsoft.Testing.Platform.Browser

`Microsoft.Testing.Platform.Browser` is an optional launcher package for running
`browser-wasm` Microsoft Testing Platform applications through `dotnet test`.

The package keeps browser dependencies and browser release cadence out of core
Microsoft.Testing.Platform. It contains:

- build-transitive assets that provide a browser boot page and JavaScript supervisor;
- an MSBuild `ComputeRunArguments` hook that selects the browser launcher only for
  `browser-*` runtime identifiers;
- an independently versioned .NET launcher that starts a configured WebAssembly host, uses
  Playwright's private browser transport to launch an installed Chromium-family browser in
  an isolated context, injects the Microsoft Testing Platform arguments before the runtime
  starts, captures bounded diagnostics, and cleans up the browser and host process trees.

Test discovery and results are not parsed or relayed by this package. The browser test
application connects directly to the authenticated HTTP gateway created by the .NET SDK.

## Prerequisites

- A .NET SDK that supplies the authenticated `dotnettestcli` HTTP bootstrap for
  `browser-wasm`.
- Microsoft Edge, Google Chrome, Chromium, or a compatible executable selected with
  `TestingPlatformBrowserExecutable`.
- A browser-WASM host. The package wraps the command, arguments, and working directory
  produced by the project's original `ComputeRunArguments` target. It recognizes the
  current `Now listening on: <URL>` output. A shared host can instead write the versioned
  launch-info file named by the
  `TESTINGPLATFORM_BROWSER_LAUNCH_INFO_FILE` environment variable:

  ```json
  { "version": 1, "url": "http://127.0.0.1:12345/" }
  ```

  The launcher creates a fresh private directory for each run and passes a file path inside
  it. On Unix the directory is mode `0700`; on Windows it has a protected current-user-only
  DACL. The host must create the file atomically at that exact path with owner-only
  permissions and must not replace the containing directory. The launcher rejects
  symbolic links/reparse points, validates Unix permissions on the opened file handle, and
  prefers this contract over console parsing.

## Usage

```xml
<PropertyGroup>
  <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Testing.Platform.Browser" Version="0.1.0-alpha" />
</ItemGroup>
```

Then run:

```dotnetcli
dotnet test
dotnet test -- --list-tests
```

The launcher integration is a preview contract with the .NET SDK. The package wraps
`ComputeRunArguments` only when the SDK marks that ProjectInstance with both
`DotnetTestInvocation=true` and `DotnetTestHttpBootstrapVersion=1`, and supplies a unique
32-character hexadecimal `DotnetTestInvocationId`. The ID isolates launcher configuration
files when the SDK evaluates the same project concurrently. Ordinary `dotnet run` and
standalone `ComputeRunArguments` queries retain the framework-provided host command. An SDK
that supplies a missing or unsupported bootstrap version or invocation ID receives an
actionable MSBuild error instead of silently launching an incompatible browser host.

Useful properties:

| Property | Purpose |
| --- | --- |
| `TestingPlatformBrowserEnabled` | Enables or disables the package. It defaults to `true` only for `browser-*` runtime identifiers and can be set to `false` in the project or on the command line. |
| `TestingPlatformBrowserGenerateHostAssets` | Controls whether the package supplies its default `index.html` and JavaScript supervisor. Set to `false` when a UI framework owns the page and integrates the launcher bootstrap/completion contract itself. |
| `TestingPlatformBrowserExecutable` | Overrides browser discovery with an explicit Chromium-family executable path. |
| `TestingPlatformBrowserHostCommand` | Overrides the command that starts the external browser-WASM host. When unset, the package wraps the `RunCommand` produced by the project's original `ComputeRunArguments`. |
| `TestingPlatformBrowserHostArguments` | Overrides the arguments for the host command. When unset, the original computed `RunArguments` are preserved, including empty and quoted arguments. |
| `TestingPlatformBrowserHostWorkingDirectory` | Overrides the working directory for the host process. When unset, the original computed `RunWorkingDirectory` is used. |
| `TestingPlatformBrowserUrlPath` | Path opened relative to the host URL. Defaults to `/`. |
| `TestingPlatformBrowserStartupTimeoutSeconds` | Host/browser startup timeout. Defaults to 60 seconds. |
| `TestingPlatformBrowserCompletionTimeoutSeconds` | Test completion timeout. Defaults to 600 seconds. |
| `TestingPlatformBrowserAdditionalArguments` | Additional Chromium command-line arguments. |

The SDK bearer token is read from its owner-only response file, retained in memory, injected
into the browser runtime through Playwright's launcher-private transport, and redacted from
launcher, host, and browser diagnostics. It is never added to the browser URL or exposed
through an unauthenticated DevTools TCP listener. User browser arguments cannot override
Playwright's debugging transport or isolated profile.

### Preview option limitations

The browser preview supports ordinary execution/discovery options such as `--help`,
`--list-tests`, `--filter`, and `--filter-uid`. It rejects options that read or write host
files because the browser virtual file system is not exported to the host yet:

- configuration and host paths: `--config-file`, `--settings`,
  `--diagnostic-output-directory`, `--diagnostic-file-prefix`, and
  `--results-directory`;
- file diagnostics: `--diagnostic`;
- report artifacts: `--report-trx`, `--report-trx-filename`, `--report-html`,
  `--report-html-filename`, `--report-junit`, `--report-junit-filename`,
  `--report-ctrf`, and `--report-ctrf-filename`;
- coverage artifacts: `--coverage`, `--coverage-output`, `--coverage-output-format`,
  and `--coverage-settings`.

The launcher rejects these before starting the browser and names the unsupported option.
They can be enabled after a browser artifact sink exports their inputs and outputs.

Cancellation delivered after browser startup is linked to the completion wait. The launcher
therefore exits that wait immediately and enters bounded browser/host cleanup rather than
waiting for `TestingPlatformBrowserCompletionTimeoutSeconds`.

## Browser page API

The launcher installs a versioned API on the top-level page only when its origin exactly
matches the loopback host origin:

```js
globalThis.testingPlatformBrowser = {
    contractVersion: 1,
    getArguments(): string[],
    complete(exitCode: number): void,
    reportFatalError(error: string): void
};
```

- `contractVersion` is `1`. A framework-owned page must reject versions it does not support.
- `getArguments()` returns a new frozen array containing the Microsoft Testing Platform
  arguments prepared by `dotnet test`, including the authenticated HTTP transport
  bootstrap. The page must pass the array directly to the managed test application; it
  must not log, persist, put into a URL, or relay those arguments.
- `complete(exitCode)` reports the managed application's final exit code to the
  launcher. It is one-shot and must be called exactly once. Failures should be written
  to `console.error` before completion so the launcher captures their diagnostics. Test discovery and test
  results do not flow through this method; MTP sends them directly to the SDK HTTP
  gateway.
- `reportFatalError(error)` terminates the launcher immediately when the page cannot
  negotiate the contract or cannot report normal completion. It is also one-shot. This
  is only for fatal page/framework integration failures; ordinary test or application
  exceptions must still be represented by the managed exit code passed to `complete`.

The package-owned JavaScript supervisor implements this API contract automatically. A UI
framework that owns its browser page can set
`TestingPlatformBrowserGenerateHostAssets=false`, provide its own `WasmMainJSPath` and
page, then integrate the API:

```js
import { dotnet } from './_framework/dotnet.js';

const api = globalThis.testingPlatformBrowser;
if (api?.contractVersion !== 1) {
    throw new Error('testingPlatformBrowser contract version 1 is required.');
}

let exitCode;
let failure;
try {
    const { runMain } = await dotnet
        .withApplicationArguments(...api.getArguments())
        .create();
    exitCode = await runMain();
}
catch (error) {
    failure = error;
    exitCode = 1;
    console.error(error instanceof Error ? error.stack ?? error.message : String(error));
}

api.complete(exitCode);
if (failure !== undefined) {
    throw failure;
}
```

The Playwright binding used underneath `complete` is a private launcher transport detail
and is not part of the browser page API.

The optional package carries Playwright and its Node-based driver so that browser cadence
can be serviced independently of core Microsoft.Testing.Platform and the .NET SDK. This
introduces package-size, platform, offline/source-build, and Node security servicing
considerations that must be resolved before productization.

The current proof of concept intentionally leaves physical browser virtual-file-system
artifact export to a future artifact sink and leaves host implementation to the shared
browser-WASM host effort.

Microsoft.Testing.Platform is open source. You can find
`Microsoft.Testing.Platform.Browser` in the
[microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Provide feedback or report issues in the
[microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
