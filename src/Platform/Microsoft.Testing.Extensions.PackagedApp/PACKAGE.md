# Microsoft.Testing.Extensions.PackagedApp

> [!IMPORTANT]
> This package is experimental and consumes the experimental `ITestHostLauncher` extension point of [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). The API may change or be removed in a future update.

`Microsoft.Testing.Extensions.PackagedApp` is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that deploys a non-packaged (loose-layout) Windows test host (for example, unpackaged WinUI) into an isolated directory and launches it from there, instead of starting the test host in place.

It is the consumer of the platform's `ITestHostLauncher` extension point for Windows test hosts. Packaged Windows apps — UWP and packaged WinUI — require package identity and ship as MSIX; they share a single launch mechanism, which is why VSTest exposes a single `UwpTestHostRuntimeProvider` for them:

- **Deploy + launch loose layout** (implemented): a non-packaged (loose-layout) app — one without an `AppxManifest.xml`, such as unpackaged WinUI — is deployed to a deployment directory and the produced executable is launched from there.
- **Packaged AUMID activation** (planned): a genuinely packaged (MSIX) layout — UWP or packaged WinUI — is registered with the `PackageManager` and the app is activated by Application User Model ID (AUMID) via `IApplicationActivationManager` — the scenario behind [#9933](https://github.com/microsoft/testfx/issues/9933). Until then, a packaged layout is rejected with an actionable error rather than silently failing at launch.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.PackagedApp` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.PackagedApp
```

## About

This package extends Microsoft.Testing.Platform with:

- **Deployment + launch**: stages a non-packaged (loose-layout) Windows test host payload (for example, unpackaged WinUI) into an isolated directory and launches the deployed copy. Genuinely packaged (MSIX) hosts — UWP and packaged WinUI — are not launched yet (see [#9933](https://github.com/microsoft/testfx/issues/9933)).
- **Mechanism-agnostic monitoring**: returns an `ITestHostHandle` that exposes only the lifecycle the platform needs and no local process id.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
