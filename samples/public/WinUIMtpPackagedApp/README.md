# Packaged WinUI 3 with all SDK extensions

This sample is a full-trust WinUI 3 application whose process hosts Microsoft.Testing.Platform.
`MSTest.Sdk` registers the unsigned build-output layout and activates the manifest application by
Application User Model ID (AUMID). The tests verify the real package identity, AUMID, process
location, and WinUI dispatcher.

The project enables `TestingExtensionsProfile=AllMicrosoft`, plus the opt-in CTRF,
JUnit, and OpenTelemetry packages. This covers all SDK-managed MTP extensions,
not every optional package or workload in this repository. Referencing/enabling
an extension does **not** imply that its options work through the packaged sidecar,
or that an exporter, external service, licensed Fakes scenario, crash, or hang has
actually been exercised.

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

## Probe extension support

Build the x64 layout, then run the probe with a dotnet installation containing
SDK **10.0.401**:

```powershell
dotnet build -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -bl:{{}}
powershell.exe -NoProfile -File .\Probe-Extensions.ps1 -DotnetPath "C:\Program Files\dotnet\dotnet.exe"
```

The script invokes that SDK's CLI directly, so installing a newer SDK does not
silently change the transport being tested. It runs native `dotnet test --help`
on the activated app, then executes ten independent CLI probes. It verifies all
14 SDK-managed extension packages in the built layout and prints a table that
also marks the package-activation check and the three unexercised scenarios. It
writes `TestResults\Extensions\extensions.json`, actual-host help, per-probe logs,
TRX, and available artifacts. Each probe uses a fresh directory to prevent stale
reports from looking like success.

| Result | Meaning |
| --- | --- |
| `Verified` | The command succeeds and exactly the selected test passes in TRX; any required artifact/retry assertions also pass. Read `Detail` for the verification boundary. |
| `Unavailable` | The actual host advertises the option, but the sidecar rejects it as unknown. This is a known composition gap, not a missing package or successful feature execution. |
| `Partial` | The command succeeds but its output does not prove the feature worked. The current coverage probe produces Cobertura with no covered sample lines. The report is retained and the script exits nonzero. |
| `Failed` | Missing host help/options, another nonzero exit, incorrect selection, or missing artifacts. The script finishes writing the report and exits nonzero. |
| `NotExercised` | The package is included, but this script does not exercise its interactive, licensed, or exporter-dependent scenario. |

The current controller only composes filter, code coverage, HangDump, Retry, and
TRX. CrashDump, HTML, CTRF, JUnit, GitHub Actions, and Azure DevOps options can
therefore appear in actual-host help while failing controller-backed execution.
The probe checks both surfaces instead of treating help output as proof.

Verification deliberately has boundaries:

- Coverage requires covered lines from the sample, not merely a nonempty Cobertura file or its misleading aggregate `line-rate`. Current packaged execution records no sample coverage and is reported as `Partial`; the script intentionally exits nonzero after writing all results.
- HangDump is enabled during a passing run; no hang is induced and no dump is claimed.
- Retry selects `RetryFailsFirstAttempt`, which fails attempt 1 and passes attempt 2; both attempt TRXs are checked.
- Hot Reload is installed but its interactive watch/restart behavior is not exercised.
- Fakes is included by the profile; no shim assembly is generated or executed, and real Fakes usage requires the applicable Visual Studio Enterprise tooling/license.
- OpenTelemetry's diagnostics producer is enabled, but no exporter/backend is configured. See [MTPOTel](../MTPOTel) for an exporter composition sample.
- No external CI service publishing, screen capture, browser, Aspire, or AI workload is exercised.

The dedicated application-model acceptance job also runs this script against the
actual sample sources with freshly packed, version-aligned MSTest/MTP packages.
Its expectations distinguish supported probes from the known controller gaps.
When a gap is fixed, update the support assertions and this documentation together.
Published sample pins remain a separate release-flow concern.

For contributor validation **before those fixes reach the published sample pins**,
run the current-package acceptance check from the repository root:

```powershell
.\build.cmd -pack
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:TESTFX_RUN_WINDOWS_APP_MODEL_TESTS = '1'
$env:TESTFX_DOTNET_10_PATH = 'C:\Program Files\dotnet\dotnet.exe' # Includes SDK 10.0.401
.\.dotnet\dotnet.exe artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Debug\net11.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter "FullyQualifiedName~PublicPackagedWinUISample_AllExtensions_ExposeControllerSupport" --progress off
```

That check builds an isolated copy of this sample against the freshly packed
packages, runs the default probe invocation, asserts the current `Partial` and
`Unavailable` results, and removes only its unique test package registration.
The acceptance check passes when those results are correctly surfaced, even though
the probe itself exits nonzero for the known coverage gap. Reports, logs, binlogs,
and evidence are retained under `artifacts\log\Debug\PackagedWinUI\...\extension-probes`.

The development package registration is intentionally retained so repeated runs can reuse the same
layout. To remove it manually:

```powershell
Get-AppxPackage -Name 27a818e1-af01-4177-9e34-ad49120c15ed | Remove-AppxPackage
```

One-shot activation and connect-back payloads are consumed from package `LocalState` during startup.
See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for package activation,
security, CI, and troubleshooting guidance.
