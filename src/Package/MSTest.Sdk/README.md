# MSTest SDK

`MSTest.Sdk` configures the MSTest framework, test adapter or self-contained runner, and optional Microsoft.Testing.Platform extensions.

## Quick start

```xml
<Project Sdk="MSTest.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Specify the SDK version in the `Sdk` attribute (`MSTest.Sdk/x.y.z`) or through the `msbuild-sdks` section of `global.json`.

## Runner modes

| Configuration | Runner |
| --- | --- |
| Default | ClassicEngine: self-contained MSTest runner built on Microsoft.Testing.Platform |
| `<PublishAot>true</PublishAot>` | NativeAOT MSTest runner |
| `<UseVSTest>true</UseVSTest>` | VSTest with `Microsoft.NET.Test.Sdk` and the selected adapter composition |

`UseVSTest=true` takes precedence over `PublishAot`. Set `<IsTestApplication>false</IsTestApplication>` to create a reusable test library instead of an executable test application.

## Windows application models

MSTest.Sdk uses Microsoft.Testing.Platform by default for unpackaged WinUI, packaged full-trust WinUI, AppContainer-configured WinUI, modern UWP (`UseUwpTools=true`), and classic `uap10.0` projects. All compositions share `MSTest.TestFramework`; UI APIs are supplied by the explicit `MSTest.Uwp` and `MSTest.WinUI` packages, not `MSTest.TestFramework.Extensions`.

| Application model | Adapter/UI package selected by the SDK |
| --- | --- |
| Ordinary .NET | `MSTest.TestAdapter` |
| Classic or modern UWP | `MSTest.Uwp` |
| WinUI | `MSTest.WinUI` |

Set `MSTestApplicationModel` to `Default`, `Uwp`, or `WinUI` to select a composition explicitly. An empty value infers the model from `UseWinUI=true`, `UseUwpTools=true`, `UseUwp=true` while `UseUwpTools` is unset, or a classic `uap` target framework; otherwise it selects `Default`. The explicit property controls package ownership before late desktop tooling imports, but does not enable or override that tooling. Unknown values and conflicting models fail with an MSBuild error. Modern UWP never selects WinUI.

`UseUwp=true` adds UWP XAML references. The desktop Visual Studio UWP targets can enable `UseUwpTools` after MSTest.Sdk declares its package references. MSTest.Sdk preserves the UWP composition during that earlier evaluation when `UseUwp=true` and `UseUwpTools` is still unset. Set `UseUwpTools=false` explicitly to use the references without UWP package activation; this selects the ordinary composition and direct MTP runner unless another application model is explicitly selected.

Selection applies to ClassicEngine, NativeAOT, and VSTest, including their source-generation hooks. Central package management receives a `PackageVersion` for the selected package, without requiring `VersionOverride`. Multi-targeted projects select independently for each inner framework. For example, a project whose UWP tooling imports are late can declare its package composition as follows:

```xml
<PropertyGroup>
  <TargetFrameworks>net11.0;net11.0-windows10.0.26100.0</TargetFrameworks>
  <MSTestApplicationModel Condition="'$(TargetFramework)' == 'net11.0-windows10.0.26100.0'">Uwp</MSTestApplicationModel>
</PropertyGroup>
```

Ordinary reusable helpers (`IsTestApplication=false`) reference only `MSTest.TestFramework` unless source generation requires adapter runtime hooks. UI helpers reference their matching UI package. Helper adapter references are private, do not register an application builder hook, and do not copy adapter payloads or satellites into the consuming application; the application supplies its own selected runtime composition. An ordinary source-generated helper can therefore be referenced by a UWP or WinUI test application without introducing `MSTest.TestAdapter` transitively.

For packaged applications the SDK references `Microsoft.Testing.Extensions.PackagedApp.MSBuild`, which supplies the full-trust app-model controller and shared launch targets. That controller registers the package and activates the exact manifest application by AUMID while retaining MTP-owned cancellation, reports, retries, and exit-code handling. The SDK itself does not duplicate the tool payload. The integration package ships in the aligned MTP release and can also be referenced explicitly by consumers of `MSTest.Uwp` or `MSTest.WinUI`.

Unsigned build-output layouts require Windows Developer Mode or sideloading. UWP builds still require the Visual Studio UWP/MSBuild workload, but they do not require `Microsoft.NET.Test.Sdk`, `vstest.console`, or the Visual Studio UWP test-host runtime provider. See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md).

Copy-ready projects are available for [modern UWP](../../../samples/public/UwpMtpApp),
[classic UWP](../../../samples/public/ClassicUwpMtpApp),
[packaged WinUI](../../../samples/public/WinUIMtpPackagedApp),
[unpackaged WinUI](../../../samples/public/WinUIMtpUnpackagedApp), and
[AppContainer WinUI](../../../samples/public/WinUIMtpAppContainerApp).

## ClassicEngine extension profiles

| `TestingExtensionsProfile` | Included extensions |
| --- | --- |
| `Default` | TrxReport and CodeCoverage |
| `AllMicrosoft` | `Default`, plus CrashDump, HangDump, HotReload, Retry, AzureDevOpsReport, GitHubActionsReport, HtmlReport, and Fakes |
| `None` | No extensions |

Individual extensions can be enabled or disabled with their `Enable*` MSBuild properties. See the [complete MSTest.Sdk property reference](https://github.com/microsoft/testfx/blob/main/docs/glossary.md#mstestsdk) for profiles, runner compatibility, test-library usage, NativeAOT restrictions, and advanced version controls.

### GitHub Actions reporting

The `--report-gh` option is provided by `Microsoft.Testing.Extensions.GitHubActionsReport`; it is not included in the `Default` profile. Enable it explicitly:

```xml
<PropertyGroup>
  <EnableMicrosoftTestingExtensionsGitHubActionsReport>true</EnableMicrosoftTestingExtensionsGitHubActionsReport>
</PropertyGroup>
```

Alternatively, select the `AllMicrosoft` profile. After the extension is included, `--help` lists `--report-gh` and its related options.
