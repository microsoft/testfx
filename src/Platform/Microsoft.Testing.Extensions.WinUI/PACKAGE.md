# Microsoft.Testing.Extensions.WinUI

> [!IMPORTANT]
> This package is experimental and consumes the experimental `ITestHostLauncher` extension point of [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). The API may change or be removed in a future update.

`Microsoft.Testing.Extensions.WinUI` is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that deploys a WinUI test host into an isolated directory and launches it from there, instead of starting the test host in place.

It is the consumer of the platform's `ITestHostLauncher` extension point for the WinUI scenario:

- **Unpackaged WinUI** (implemented): the app's loose layout is deployed to a deployment directory and the produced executable is launched from there.
- **Packaged WinUI / MSIX** (planned): the loose layout is registered with the `PackageManager` and the app is activated by Application User Model ID (AUMID) via `IApplicationActivationManager` — the scenario behind [#2784](https://github.com/microsoft/testfx/issues/2784).

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.WinUI` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.WinUI
```

## About

This package extends Microsoft.Testing.Platform with:

- **Deployment + launch**: stages the WinUI test host payload into an isolated directory and launches the deployed copy.
- **Mechanism-agnostic monitoring**: returns an `ITestHostHandle` that exposes only the lifecycle the platform needs and no local process id.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
