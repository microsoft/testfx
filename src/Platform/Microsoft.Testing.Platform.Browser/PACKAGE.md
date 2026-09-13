# Microsoft.Testing.Platform.Browser

`Microsoft.Testing.Platform.Browser` is an optional launcher package for running
`browser-wasm` Microsoft Testing Platform applications through `dotnet test`.

The package keeps browser dependencies and browser release cadence out of core
Microsoft.Testing.Platform. It contains:

- build-transitive assets that provide a browser boot page and JavaScript supervisor;
- an MSBuild `ComputeRunArguments` hook that selects the browser launcher only for
  `browser-*` runtime identifiers;
- a dependency-free .NET launcher that starts a configured WebAssembly host, launches an
  installed Chromium-family browser in an isolated profile, injects the Microsoft Testing
  Platform arguments before the runtime starts, captures bounded diagnostics, and cleans up
  the browser and host process trees.

Test discovery and results are not parsed or relayed by this package. The browser test
application connects directly to the authenticated HTTP gateway created by the .NET SDK.

## Prerequisites

- A .NET SDK that supplies the authenticated `dotnettestcli` HTTP bootstrap for
  `browser-wasm`.
- Microsoft Edge, Google Chrome, Chromium, or a compatible executable selected with
  `TestingPlatformBrowserExecutable`.
- A browser-WASM host. The proof of concept defaults to `dotnet run --no-build` and
  recognizes the current `Now listening on: <URL>` output. A shared host can instead write
  the versioned launch-info file named by the
  `TESTINGPLATFORM_BROWSER_LAUNCH_INFO_FILE` environment variable:

  ```json
  { "version": 1, "url": "http://127.0.0.1:12345/" }
  ```

  The host must create the file atomically and with owner-only permissions. The launcher
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

Useful properties:

| Property | Purpose |
| --- | --- |
| `TestingPlatformBrowserEnabled` | Enables or disables the package. It defaults to `true` only for `browser-*` runtime identifiers and can be set to `false` in the project or on the command line. |
| `TestingPlatformBrowserExecutable` | Overrides browser discovery with an explicit Chromium-family executable path. |
| `TestingPlatformBrowserHostCommand` | Command that starts the external browser-WASM host. |
| `TestingPlatformBrowserHostArguments` | Arguments for the host command. |
| `TestingPlatformBrowserHostWorkingDirectory` | Working directory for the host process. |
| `TestingPlatformBrowserUrlPath` | Path opened relative to the host URL. Defaults to `/`. |
| `TestingPlatformBrowserStartupTimeoutSeconds` | Host/browser startup timeout. Defaults to 60 seconds. |
| `TestingPlatformBrowserCompletionTimeoutSeconds` | Test completion timeout. Defaults to 600 seconds. |
| `TestingPlatformBrowserAdditionalArguments` | Additional Chromium command-line arguments. |

The SDK bearer token is read from its owner-only response file, retained in memory, injected
into the browser runtime through the DevTools protocol, and redacted from launcher, host, and
browser diagnostics. It is never added to the browser URL.

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
