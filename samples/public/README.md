# Public samples

## Observability

| Scenario | Sample |
| --- | --- |
| Use an application-owned `HostApplicationBuilder` to supply configuration and logging, collect MTP diagnostics with OpenTelemetry, and preserve the application's resource identity and service ownership | [`MTPOTel`](MTPOTel) |

## Application hosting

| Scenario | Sample |
| --- | --- |
| Generate an MTP entry point that starts a real ASP.NET Core application, runs MSTest against its endpoint, and reuses the web host's configuration, logging, DI, and OpenTelemetry pipeline | [`MTPHostIntegration/AspNetCoreTests`](MTPHostIntegration/AspNetCoreTests) |
| Generate an MTP entry point for an Aspire AppHost test while the process uses the same ServiceDefaults pattern and owns a single host/provider pipeline | [`MTPHostIntegration/AspireTests`](MTPHostIntegration/AspireTests) |

## Windows application testing

The sample name identifies the Windows application model, test host, and packaging mode.

| Application model | Test host | Packaging | Sample | Status |
| --- | --- | --- | --- | --- |
| UWP | VSTest | Packaged | [`UwpVSTestApp`](UwpVSTestApp) | Legacy supported configuration |
| Modern UWP (`UseUwp=true`) | Microsoft.Testing.Platform (MTP) | Packaged AppContainer | [`UwpMtpApp`](UwpMtpApp) | Supported and the MSTest.Sdk default |
| Classic UWP (`uap10.0`) | Microsoft.Testing.Platform (MTP) | Packaged AppContainer | [`ClassicUwpMtpApp`](ClassicUwpMtpApp) | Supported for existing classic projects |
| WinUI 3 | VSTest | Packaged | [`WinUIVSTestApp`](WinUIVSTestApp) | Supported |
| WinUI 3 | VSTest | Unpackaged | — | Not supported; VSTest's WinUI provider requires an AppX manifest |
| WinUI 3 | MTP | Packaged | [`WinUIMtpPackagedApp`](WinUIMtpPackagedApp) | Supported |
| WinUI 3 | MTP | Unpackaged | [`WinUIMtpUnpackagedApp`](WinUIMtpUnpackagedApp) | Supported |
| WinUI 3 `packagedClassicApp` | MTP | Packaged AppContainer | [`WinUIMtpAppContainerApp`](WinUIMtpAppContainerApp) | Supported; run non-elevated |

UWP is always packaged. For more detail about packaging, trust levels, launch behavior, and UI-thread
tests, see [Testing UWP and WinUI apps with MSTest](../../docs/winui-testing.md).

Run commands from a sample directory and select a concrete architecture:

The MTP `dotnet test --project` syntax used by the WinUI samples and the per-sample `global.json`
runner selection require .NET SDK 10 or later. The WinUI projects target the supported .NET 8
minimum and the modern UWP sample targets .NET 9; those target frameworks do not determine the SDK
version used by `dotnet test`.

```powershell
# Full-trust and unpackaged WinUI MTP samples
dotnet build -p:Platform=x64 -bl:{{}}
dotnet run --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64

# UWP VSTest sample
dotnet test UwpVSTestApp.csproj -p:Platform=x64

# WinUI VSTest sample
dotnet test WinUIVSTestApp.csproj -p:Platform=x64
```

Modern and classic UWP builds require desktop MSBuild from Visual Studio with the Universal Windows
Platform workload. Follow the commands in [`UwpMtpApp/README.md`](UwpMtpApp/README.md) or
[`ClassicUwpMtpApp/README.md`](ClassicUwpMtpApp/README.md). Only the explicit legacy VSTest samples
require the Visual Studio test runtime provider.

AppContainer WinUI uses the sidecar's `InvokeTestingPlatform` MSBuild target rather than native
`dotnet test`; follow [`WinUIMtpAppContainerApp/README.md`](WinUIMtpAppContainerApp/README.md).
