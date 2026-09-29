# Modern UWP tests with MSTest

This sample is a modern .NET UWP application (`UseUwp=true`) whose AppContainer process hosts
Microsoft.Testing.Platform. `MSTest.Sdk` supplies the sidecar controller and packaged-app bootstrap;
the UWP launch activation string is restored with
`PackagedAppExtensions.GetTestApplicationArguments`.

The application targets .NET 9 because MSTest 4.5's modern-UWP package assets and native-AOT
selection are validated for `net9.0`. The .NET 10 SDK supplies the release toolchain and MTP runner
selection, while UWP execution uses desktop MSBuild. Targeting `net10.0` currently builds a package
recipe whose executable selection is not supported by the MTP packaged-app launcher.

## Prerequisites

- Windows 11 with Windows SDK 10.0.26100 or later.
- .NET SDK 10 or later.
- Visual Studio 2026 or later with the Universal Windows Platform workload.
- Windows Developer Mode or another policy that permits registering unsigned package layouts.

## Build and run

Use a Developer PowerShell for Visual Studio so desktop MSBuild can load the UWP toolchain:

```powershell
msbuild UwpMtpApp.sln /restore /p:Configuration=Release /p:Platform=x64 /bl:UwpMtpApp-build.binlog
msbuild UwpMtpApp.csproj /t:InvokeTestingPlatform /p:Configuration=Release /p:Platform=x64 /p:TestingPlatformCommandLineArguments="--report-trx" /bl:UwpMtpApp-test.binlog
```

The second command registers the build-output package, activates the exact manifest application by
AUMID, runs the plain and UI-thread tests, publishes TRX, and exits the app. No
`Microsoft.NET.Test.Sdk`, `vstest.console`, `UwpTestHostRuntimeProvider`, or Visual Studio deployment
runtime is used.

One-shot activation and connect-back payloads are consumed from package `LocalState`. The
development package registration is retained for repeated runs; remove it manually when needed:

```powershell
Get-AppxPackage -Name MSTestUwpMtpSample | Remove-AppxPackage
```

See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for classic UWP,
AppContainer security, CI, cleanup, and troubleshooting guidance.
