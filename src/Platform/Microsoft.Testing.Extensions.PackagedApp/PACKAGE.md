# Microsoft.Testing.Extensions.PackagedApp

> [!IMPORTANT]
> This package is experimental and consumes the experimental `ITestHostLauncher` extension point of [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). The API may change or be removed in a future update.

`Microsoft.Testing.Extensions.PackagedApp` is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that deploys and launches Windows test hosts: a non-packaged (loose-layout) host (for example, unpackaged WinUI) is deployed into an isolated directory and launched from there, and — in its Windows build (`net*-windows`) — a packaged, full-trust MSIX desktop host is registered with the OS and activated by Application User Model ID (AUMID).

It is the consumer of the platform's `ITestHostLauncher` extension point for Windows test hosts. Packaged Windows apps require package identity and ship as MSIX; VSTest exposes a single `UwpTestHostRuntimeProvider` for the equivalent scenario, built on Visual-Studio-internal deployment components, whereas this extension uses only public, redistributable Windows APIs:

- **Deploy + launch loose layout**: a non-packaged (loose-layout) app — one without an `AppxManifest.xml`, such as unpackaged WinUI — is deployed to a deployment directory and the produced executable is launched from there.
- **Packaged AUMID activation** (Windows build): a packaged (MSIX) layout is registered in place with the `PackageManager` and the app is activated by AUMID via `IApplicationActivationManager` — the scenario tracked by [#9933](https://github.com/microsoft/testfx/issues/9933). This connect-back transport targets **full-trust packaged (MSIX) desktop hosts**; true UWP/AppContainer hosts are not supported, and registering an unsigned build-output layout requires Developer Mode (or sideloading). The plain `net8.0`/`net9.0` build rejects a packaged layout with an actionable error pointing at the Windows TFM.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.PackagedApp` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.PackagedApp
```

## About

This package extends Microsoft.Testing.Platform with:

- **Deployment + launch**: stages a non-packaged (loose-layout) Windows test host payload (for example, unpackaged WinUI) into an isolated directory and launches the deployed copy; the Windows build additionally registers and activates a packaged, full-trust MSIX desktop host by AUMID (see [#9933](https://github.com/microsoft/testfx/issues/9933)).
- **Mechanism-agnostic monitoring**: returns an `ITestHostHandle` that exposes only the lifecycle the platform needs (surfacing the activated process id for the packaged path, and none for the deployed loose-layout path).

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
