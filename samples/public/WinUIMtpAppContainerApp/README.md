# AppContainer WinUI 3 tests with MSTest

This sample is a packaged WinUI 3 `packagedClassicApp` whose manifest explicitly sets
`TrustLevel="appContainer"`. `MSTest.Sdk` launches it through the full-trust sidecar controller,
authorizes only this package identity on the MTP named pipe, and recovers TRX and diagnostic
artifacts from package-owned storage. The tests verify package identity, AppContainer token
isolation, and WinUI dispatcher access.

## Prerequisites

- Run non-elevated on Windows 10 version 2004 (10.0.19041) or later.
- .NET SDK 10 or later.
- Visual Studio 2022 or later with the Windows application development tools.
- Windows Developer Mode or another policy that permits registering unsigned package layouts.

## Run

From this directory:

```powershell
dotnet build -p:Platform=x64 -bl:{{}}
$results = Join-Path $PWD TestResults
$arguments = "--report-trx --report-trx-filename appcontainer.trx --results-directory $results"
dotnet msbuild .\WinUIMtpAppContainerApp.csproj -t:InvokeTestingPlatform -p:Platform=x64 "-p:TestingPlatformCommandLineArguments=$arguments" -bl:{{}}
```

Use the `InvokeTestingPlatform` target for this AppContainer-hosted sample so the full-trust sidecar
can recover TRX from package `LocalState` to the absolute results directory. Native `dotnet test`
execution-ID handoff is demonstrated by the full-trust
[`WinUIMtpPackagedApp`](../WinUIMtpPackagedApp); it is not currently supported for this restricted
`packagedClassicApp` host.

Do not grant `ALL APPLICATION PACKAGES`, add a loopback exemption, or run the controller elevated.
MTP authorizes only the package SID required by this sample. One-shot activation and connect-back
payloads are consumed from package `LocalState`; reports are copied back to the requested results
directory.

The development package registration is retained for repeated runs. Remove it manually when needed:

```powershell
Get-AppxPackage -Name MSTestWinUIAppContainerSample | Remove-AppxPackage
```

See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for the security model,
CI requirements, artifact handoff, and troubleshooting guidance.
