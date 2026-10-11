# Unpackaged WinUI 3 tests with MSTest

This sample is an unpackaged WinUI 3 application whose process hosts Microsoft.Testing.Platform.
It has no MSIX identity or `Package.appxmanifest`; `dotnet run` and `dotnet test` start the generated
executable directly. It carries the Windows App SDK with the app so it does not depend on a
machine-wide Windows App Runtime installation. The tests verify the absence of package identity,
Windows App SDK initialization, and WinUI dispatcher access.

## Prerequisites

- Windows 10 version 2004 (10.0.19041) or later.
- .NET SDK 10 or later for the native Microsoft.Testing.Platform runner and `dotnet test` CLI.
- .NET 8 Desktop Runtime for the selected architecture to run this framework-dependent
  `net8.0-windows10.0.19041.0` application.
- Visual Studio 2022 or later with the Windows application development tools.

## Run

From this directory:

```powershell
dotnet build -p:Platform=x64 -bl:{{}}
dotnet run --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64
```

Both run commands must finish without manually closing the window. The .NET 10 SDK runs the native
Microsoft.Testing.Platform CLI, but it does not supply the .NET 8 Desktop Runtime required by this
framework-dependent .NET application. `WindowsAppSDKSelfContained=true` applies only to the Windows
App SDK: it avoids a machine-wide Windows App Runtime dependency and prevents its bootstrapper from
blocking unattended runs when that runtime is missing.

No package registration or package `LocalState` cleanup is required. See
[Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for dispatcher, bootstrap,
CI, and troubleshooting guidance.
