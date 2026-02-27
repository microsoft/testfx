# Microsoft.Testing.Extensions.Telemetry

Microsoft.Testing.Extensions.Telemetry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that collects usage telemetry to help the Microsoft.Testing.Platform team improve the product.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.Telemetry` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.Telemetry) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Telemetry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Usage telemetry**: collects usage data to help understand product usage and prioritize improvements
- **Opt-out support**: telemetry can be disabled via the `TESTINGPLATFORM_TELEMETRY_OPTOUT` or `DOTNET_CLI_TELEMETRY_OPTOUT` environment variables
- **Disclosure**: telemetry information is shown on first run, with opt-out guidance

This package is an optional, opt-in extension. To enable telemetry when using Microsoft.Testing.Platform (including when running tests with [MSTest](https://www.nuget.org/packages/MSTest)), you must explicitly reference the `Microsoft.Testing.Extensions.Telemetry` package from your test project or from your own test framework or tooling package.

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
