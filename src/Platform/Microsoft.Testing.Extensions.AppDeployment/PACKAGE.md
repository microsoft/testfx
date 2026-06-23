# Microsoft.Testing.Extensions.AppDeployment

> [!IMPORTANT]
> This package is experimental and consumes the experimental `ITestHostLauncher` extension point of [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). The API may change or be removed in a future update.

`Microsoft.Testing.Extensions.AppDeployment` is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that deploys the test host into an isolated directory and launches it from there, instead of starting it in place.

It is a reference implementation of a custom `ITestHostLauncher`: a launch mechanism that does more than `Process.Start` and that does not surface a local process id, demonstrating the platform's mechanism-agnostic launch contract (the same shape an AUMID-activated packaged app, a container, or a remote launch would use).

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.AppDeployment` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.AppDeployment
```

## About

This package extends Microsoft.Testing.Platform with:

- **Deployment + launch**: stages the test host payload into an isolated directory and launches the deployed copy.
- **Mechanism-agnostic monitoring**: returns an `ITestHostHandle` that exposes only the lifecycle the platform needs and no local process id.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
