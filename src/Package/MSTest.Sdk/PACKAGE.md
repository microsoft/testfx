# MSTest.Sdk

MSTest is Microsoft supported Test Framework.

This package includes a custom test SDK for writing tests with MSTest.

## Getting started

Create a test project by using `MSTest.Sdk` as the project SDK. Replace the example version with the version you want to use.

```xml
<Project Sdk="MSTest.Sdk/4.1.0">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Supported platforms:

- .NET 4.6.2+
- .NET 10.0+
- .NET 10.0 Windows.18362+ (WinUI)
- UWP 10.0.16299
- UWP 10.0.17763 with .NET 10+

The SDK uses one shared `MSTest.TestFramework` and selects exactly one adapter
composition: `MSTest.TestAdapter` for ordinary .NET, `MSTest.Uwp` for classic or
modern UWP, or `MSTest.WinUI` for WinUI. The UI packages replace
`MSTest.TestFramework.Extensions`; modern UWP does not use WinUI.

`MSTestApplicationModel` accepts `Default`, `Uwp`, or `WinUI`. Leave it empty to
infer from the current target framework and UI tooling properties, or set it
explicitly when desktop tooling imports arrive after restore-time package
selection. `UseUwp=true` with `UseUwpTools=false` remains ordinary unless another
model is explicitly selected. Conflicting or unknown models are rejected.
Selection and central package versions are per inner target framework.

Reusable helpers use `IsTestApplication=false`. Ordinary helpers remain
core-only unless source generation needs runtime hooks; UI helpers use their
matching UI package. Their adapter dependencies stay private and do not copy
application payloads into a referencing project.

For package identity, AUMID activation, unpackaged WinUI, UWP launch activation, UI dispatchers, and
AppContainer security guidance, see
[Testing UWP and WinUI apps with MSTest](https://github.com/microsoft/testfx/blob/main/docs/winui-testing.md).

For packaged Windows applications, the SDK references
`Microsoft.Testing.Extensions.PackagedApp.MSBuild` from the aligned MTP release.
That separately shipped package owns the full-trust controller, shared launch
targets and classic UWP bootstrap; the SDK does not embed a second controller
payload. Direct `MSTest.Uwp` and `MSTest.WinUI` consumers can reference the same
integration explicitly with `EnableMSTestRunner=true`.

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
