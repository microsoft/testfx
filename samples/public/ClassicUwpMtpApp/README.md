# Classic UWP tests with MSTest

This sample shows an existing `uap10.0` project importing `MSTest.Sdk` and running through
Microsoft.Testing.Platform. It intentionally preserves the classic UWP project shape: desktop
MSBuild, `MSBuild.Sdk.Extras`, the classic `uap10.0` framework, and the Visual Studio UWP build
extension SDK. It targets the current Windows SDK while retaining the classic project model.

## Prerequisites

- Windows 10 or later.
- Visual Studio with the Universal Windows Platform workload, Windows SDK 10.0.26100, and the
  matching `TestPlatform.Universal` extension SDK.
- .NET SDK 10 or later for the MSTest 4.5 toolchain.
- Windows Developer Mode or another policy that permits registering unsigned package layouts.

The Visual Studio components are build-time requirements. Execution uses the SDK-shipped MTP
sidecar and does not use `vstest.console`, `UwpTestHostRuntimeProvider`, or the Visual Studio
deployment runtime.

## Build and run

Use a Developer PowerShell for Visual Studio:

```powershell
msbuild ClassicUwpMtpApp.sln /restore /p:Configuration=Release /p:Platform=x64 /bl:ClassicUwpMtpApp-build.binlog
msbuild ClassicUwpMtpApp.csproj /t:InvokeTestingPlatform /p:Configuration=Release /p:Platform=x64 /p:TestingPlatformCommandLineArguments="--report-trx" /bl:ClassicUwpMtpApp-test.binlog
```

The second command registers and activates the package by AUMID, runs the plain and UI-thread tests,
publishes TRX, and exits the app. One-shot activation and connect-back payloads are consumed from
package `LocalState`.

The development package registration is retained for repeated runs. Remove it manually when needed:

```powershell
Get-AppxPackage -Name MSTestClassicUwpMtpSample | Remove-AppxPackage
```

See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for the UWP bootstrap,
AppContainer pipe authorization, CI, cleanup, and troubleshooting guidance.
