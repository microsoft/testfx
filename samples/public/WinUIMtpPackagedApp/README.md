# Packaged WinUI 3 tests with MSTest

This sample is a full-trust WinUI 3 application whose process hosts Microsoft.Testing.Platform.
`MSTest.Sdk` registers the unsigned build-output layout and activates the manifest application by
Application User Model ID (AUMID). The tests verify the real package identity, AUMID, process
location, and WinUI dispatcher.

## Prerequisites

- Windows 10 version 2004 (10.0.19041) or later.
- .NET SDK 10 or later.
- Visual Studio 2022 or later with the Windows application development tools.
- Windows Developer Mode or another policy that permits registering unsigned package layouts.

## Run

From this directory:

```powershell
dotnet build -p:Platform=x64 -bl:{{}}
dotnet run --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64 --filter "FullyQualifiedName~PackageIdentityAndAumidMatchManifest" --report-trx --report-trx-filename filtered.trx
```

Both run commands must finish without manually closing the window. `dotnet test` requires .NET SDK
10 or later because `global.json` selects the native Microsoft.Testing.Platform runner.

The filtered command must produce exactly one passed result named
`PackageIdentityAndAumidMatchManifest` in `TestResults\filtered.trx`; the UI test must
not run. The application-model acceptance suite builds this sample with the current
packed MSTest/MTP packages and checks this selection through both native `dotnet test`
and `InvokeTestingPlatform`, rather than relying on the sample's published package pins.
The filter example requires a matching MSTest/MTP release containing packaged-app
sidecar filter support; published sample pins must be advanced together after that release.

The development package registration is intentionally retained so repeated runs can reuse the same
layout. To remove it manually:

```powershell
Get-AppxPackage -Name 27a818e1-af01-4177-9e34-ad49120c15ed | Remove-AppxPackage
```

One-shot activation and connect-back payloads are consumed from package `LocalState` during startup.
See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for package activation,
security, CI, and troubleshooting guidance.
