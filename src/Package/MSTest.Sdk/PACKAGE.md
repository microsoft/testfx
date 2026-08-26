# MSTest.Sdk

MSTest is Microsoft supported Test Framework.

This package includes a custom test SDK for writing tests with MSTest.

## Getting started

Create a test project by using `MSTest.Sdk` as the project SDK. Replace the example version with the version you want to use.

```xml
<Project Sdk="MSTest.Sdk/4.1.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Supported platforms:

- .NET 4.6.2+
- .NET 8.0+
- .NET 8.0 Windows.18362+ (WinUI)
- UWP 10.0.16299
- UWP 10.0.17763 with .NET 9+

## Documentation

For setup guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-getting-started>.

For SDK configuration options, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-sdk>.

### GitHub Actions reporting

In ClassicEngine mode, the `--report-gh` option is provided by `Microsoft.Testing.Extensions.GitHubActionsReport`; it is not included in the default extension profile. Enable it explicitly:

```xml
<PropertyGroup>
  <EnableMicrosoftTestingExtensionsGitHubActionsReport>true</EnableMicrosoftTestingExtensionsGitHubActionsReport>
</PropertyGroup>
```

Alternatively, set `TestingExtensionsProfile` to `AllMicrosoft`. After the extension is included, `--help` lists `--report-gh` and its related options. The extension is not supported in VSTest or NativeAOT mode.
