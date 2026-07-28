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
| `<UseVSTest>true</UseVSTest>` | VSTest with `Microsoft.NET.Test.Sdk` and `MSTest.TestAdapter` |

`UseVSTest=true` takes precedence over `PublishAot`. Set `<IsTestApplication>false</IsTestApplication>` to create a reusable test library instead of an executable test application.

## ClassicEngine extension profiles

| `TestingExtensionsProfile` | Included extensions |
| --- | --- |
| `Default` | TrxReport and CodeCoverage |
| `AllMicrosoft` | `Default`, plus CrashDump, HangDump, HotReload, Retry, AzureDevOpsReport, GitHubActionsReport, HtmlReport, and Fakes |
| `None` | No extensions |

Individual extensions can be enabled or disabled with their `Enable*` MSBuild properties. See the [complete MSTest.Sdk property reference](https://github.com/microsoft/testfx/blob/main/docs/glossary.md#mstestsdk) for profiles, runner compatibility, test-library usage, NativeAOT restrictions, and advanced version controls.
