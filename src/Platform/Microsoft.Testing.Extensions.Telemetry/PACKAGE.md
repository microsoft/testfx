# Microsoft.Testing.Extensions.Telemetry

Microsoft.Testing.Extensions.Telemetry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that collects usage telemetry to help the Microsoft.Testing.Platform team improve the product.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.Telemetry` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.Telemetry) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Telemetry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Usage telemetry**: collects anonymous usage data to help the Microsoft.Testing.Platform team understand how the platform is used
- **Opt-out support**: telemetry can be disabled via the `DOTNET_CLI_TELEMETRY_OPTOUT` environment variable or the `--no-telemetry` command line option
- **Privacy-first**: no personally identifiable information (PII) is collected

This package is typically **not referenced directly**. Instead, test framework packages (such as [MSTest](https://www.nuget.org/packages/MSTest)) reference it automatically.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
