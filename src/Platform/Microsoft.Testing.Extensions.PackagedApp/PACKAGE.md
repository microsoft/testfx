# Microsoft.Testing.Extensions.PackagedApp

> [!IMPORTANT]
> This package is experimental and consumes the experimental `ITestHostLauncher` extension point of [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). The API may change or be removed in a future update.

`Microsoft.Testing.Extensions.PackagedApp` is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that deploys a packaged Windows test host (UWP or packaged WinUI) into an isolated directory and launches it from there, instead of starting the test host in place.

It is the consumer of the platform's `ITestHostLauncher` extension point for packaged Windows apps. UWP and packaged WinUI ship as MSIX and share the same launch mechanism, which is why VSTest exposes a single `UwpTestHostRuntimeProvider` for both:

- **Deploy + launch** (implemented): the app's loose layout is deployed to a deployment directory and the produced executable is launched from there.
- **Packaged AUMID activation** (planned): the loose layout is registered with the `PackageManager` and the app is activated by Application User Model ID (AUMID) via `IApplicationActivationManager` — the scenario behind [#2784](https://github.com/microsoft/testfx/issues/2784).

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.PackagedApp` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.PackagedApp
```

## About

This package extends Microsoft.Testing.Platform with:

- **Deployment + launch**: stages the packaged Windows (UWP/WinUI) test host payload into an isolated directory and launches the deployed copy.
- **Mechanism-agnostic monitoring**: returns an `ITestHostHandle` that exposes only the lifecycle the platform needs and no local process id.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
