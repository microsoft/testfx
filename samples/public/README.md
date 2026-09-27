# Public samples

## Observability

| Scenario | Sample |
| --- | --- |
| Use application-owned OpenTelemetry providers from `HostApplicationBuilder` to collect MTP diagnostics and custom test activities while preserving the application's resource identity | [`MTPOTel`](MTPOTel) |

## Windows application testing

The sample name identifies the Windows application model, test host, and packaging mode.

| Application model | Test host | Packaging | Sample | Status |
| --- | --- | --- | --- | --- |
| UWP | VSTest | Packaged | [`UwpVSTestApp`](UwpVSTestApp) | Legacy supported configuration |
| UWP | Microsoft.Testing.Platform (MTP) | Packaged | See [`docs/winui-testing.md`](../../docs/winui-testing.md) | Supported and the MSTest.Sdk default |
| WinUI 3 | VSTest | Packaged | [`WinUIVSTestApp`](WinUIVSTestApp) | Supported |
| WinUI 3 | VSTest | Unpackaged | — | Not supported; VSTest's WinUI provider requires an AppX manifest |
| WinUI 3 | MTP | Packaged | [`WinUIMtpPackagedApp`](WinUIMtpPackagedApp) | Supported |
| WinUI 3 | MTP | Unpackaged | [`WinUIMtpUnpackagedApp`](WinUIMtpUnpackagedApp) | Supported |

UWP is always packaged. For more detail about packaging, trust levels, launch behavior, and UI-thread
tests, see [Testing UWP and WinUI apps with MSTest](../../docs/winui-testing.md).

Run commands from a sample directory and select a concrete architecture:

The MTP `dotnet test --project` syntax and the per-sample `global.json` runner selection require
.NET SDK 10 or later. The projects target .NET 8, but that target framework does not determine the
SDK version used by `dotnet test`.

```powershell
# MTP samples
dotnet build -p:Platform=x64 -bl:{{}}
dotnet run --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64

# UWP VSTest sample
dotnet test UwpVSTestApp.csproj -p:Platform=x64

# WinUI VSTest sample
dotnet test WinUIVSTestApp.csproj -p:Platform=x64
```

UWP builds require Visual Studio with the Universal Windows Platform workload. Only the explicit legacy VSTest samples require the Visual Studio test tools/runtime provider.
