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

MSTest.Sdk uses Microsoft.Testing.Platform by default for unpackaged WinUI, packaged full-trust WinUI, AppContainer-configured WinUI, modern UWP (`UseUwpTools=true`), and classic `uap10.0` projects. Every composition uses the same two packages: `MSTest.TestFramework`, plus `MSTest.TestAdapter` for test applications. Both packages select their UWP or WinUI assemblies from the project's `UseWinUI`, `UseUwpTools`, or `UseUwp` properties, or from a classic `uap` target framework. No UI-specific MSTest package or `MSTest.TestFramework.Extensions` reference is needed.

`UseUwp=true` adds UWP XAML references. The desktop Visual Studio UWP targets can enable `UseUwpTools` after MSTest.Sdk evaluates, so `UseUwp=true` with `UseUwpTools` still unset is treated as UWP. Set `UseUwpTools=false` explicitly to use the references without UWP package activation; this selects the ordinary adapter assemblies and direct MTP runner. A project cannot use both the WinUI and UWP application models.

Reusable helpers (`IsTestApplication=false`) reference only `MSTest.TestFramework`, which also supplies the UWP or WinUI UI-thread APIs, unless source generation requires adapter runtime hooks. Helper adapter references are private, do not register an application builder hook, and do not copy adapter payloads or satellites into the consuming application. A UWP or WinUI helper therefore stays a library rather than becoming a test application.

For packaged applications the SDK references `Microsoft.Testing.Extensions.PackagedApp.MSBuild`, which supplies the full-trust app-model controller and shared launch targets. That controller registers the package and activates the exact manifest application by AUMID while retaining MTP-owned cancellation, reports, retries, and exit-code handling. The SDK itself does not duplicate the tool payload. The integration package ships in the aligned MTP release and can also be referenced explicitly by `MSTest.TestAdapter` consumers.

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
