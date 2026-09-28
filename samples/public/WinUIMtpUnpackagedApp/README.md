# Unpackaged WinUI 3 tests with MSTest

This sample is an unpackaged WinUI 3 application whose process hosts Microsoft.Testing.Platform.
It has no MSIX identity or `Package.appxmanifest`; `dotnet run` and `dotnet test` start the generated
executable directly. The tests verify the absence of package identity, Windows App SDK bootstrap,
and WinUI dispatcher access.

## Prerequisites

- Windows 10 version 2004 (10.0.19041) or later.
- .NET SDK 10 or later.
- Visual Studio 2022 or later with the Windows application development tools.

## Run

From this directory:

```powershell
dotnet build -p:Platform=x64 -bl:{{}}
dotnet run --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64
```

Both run commands must finish without manually closing the window. `dotnet test` requires .NET SDK
10 or later because `global.json` selects the native Microsoft.Testing.Platform runner.

No package registration or package `LocalState` cleanup is required. See
[Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for dispatcher, bootstrap,
CI, and troubleshooting guidance.
