# Modern UWP tests with MSTest

This sample is a modern .NET UWP application (`UseUwp=true`) whose AppContainer process hosts
Microsoft.Testing.Platform. `MSTest.Sdk` supplies the sidecar controller and packaged-app bootstrap;
the UWP launch activation string is restored with
`PackagedAppExtensions.GetTestApplicationArguments`.

The application targets .NET 10. The .NET 10 SDK supplies the target framework and resolves
`MSTest.Sdk`; desktop MSBuild from Visual Studio loads the UWP and MSIX toolchain and runs the
`InvokeTestingPlatform` target.

## Prerequisites

- Windows 11 with Windows SDK 10.0.26100 or later.
- .NET SDK 10 or later.
- Visual Studio 2026 or later with the Universal Windows Platform workload.
- Windows Developer Mode or another policy that permits registering unsigned package layouts.

## Build and run

For a quick command-line validation, run:

```powershell
dotnet test
```

This builds the code-only UWP application without requiring the Visual Studio XAML compiler. The
test host and adapter-message checks run directly; tests that require package identity, the UWP
dispatcher, or application PRI resources are reported as inconclusive because native `dotnet test`
launches the test module outside AppContainer. This path is not native package validation.

Use a Developer PowerShell for Visual Studio so desktop MSBuild can load the UWP toolchain:

```powershell
$run = [Guid]::NewGuid().ToString('N')
msbuild UwpMtpApp.sln /restore /p:Configuration=Release /p:Platform=x64 /p:EnableMicrosoftTestingExtensionsPackagedApp=true "/bl:UwpMtpApp-build-$run.binlog"
if ($LASTEXITCODE -ne 0) { throw "UWP build failed: $LASTEXITCODE" }
msbuild UwpMtpApp.csproj /t:InvokeTestingPlatform /p:Configuration=Release /p:Platform=x64 /p:EnableMicrosoftTestingExtensionsPackagedApp=true "/p:TestingPlatformCommandLineArguments=--report-trx --report-trx-filename UwpMtpApp-$run.trx" "/bl:UwpMtpApp-test-$run.binlog"
if ($LASTEXITCODE -ne 0) { throw "UWP execution failed: $LASTEXITCODE" }
```

The second command registers the build-output package, activates the exact manifest application by
AUMID, runs the plain and UI-thread tests, publishes TRX, and exits the app. No
`Microsoft.NET.Test.Sdk`, `vstest.console`, `UwpTestHostRuntimeProvider`, or Visual Studio deployment
runtime is used.

## Packaging and localization checks

Require these **seven passed results** from the packaged run, with zero skipped/inconclusive
results; a successful process exit is insufficient:

| Test result | Contract |
| --- | --- |
| `TestHostRuns` | The expected test assembly executes. |
| `PackageIdentityMatchesManifest` | The host runs in this AppContainer package. |
| `UITestMethodRunsOnTheCoreWindowDispatcher` | The UWP extension dispatches a real `Windows.UI.Xaml.Controls.Grid` to the CoreWindow thread, not a WinUI dispatcher. |
| `AdapterFailureMessages_AreLocalized_English` | Public `TestContext.AddResultFile("")` throws `ArgumentException` for `fileName` with the English platform-services message. |
| `AdapterFailureMessages_UseNeutralFallbackForFrench` | Under French UI, the same API retains the English fallback required by the existing native UWP MTP satellite suppression; the previous UI culture is restored. |
| `ApplicationResources_AreLocalized_English` | The application PRI resolves the manifest's `AppDescription` to `MSTest modern UWP sample`. |
| `ApplicationResources_AreLocalized_French` | An explicit per-call French resource context resolves `Exemple UWP moderne MSTest`. |

`Strings/*/Resources.resw` supplies a real manifest description and is compiled for the packaged
path. No `SatelliteResourceLanguages` restriction or application-wide PRI suppression is needed by
these tests. Runtime lookup, rather than a particular satellite filename, is the localization
contract; Native AOT can change the physical representation.
The packaged adapter-message rows pin the existing native UWP MTP suppression policy, not a
requirement to ship previously suppressed translations. A candidate exposing French satellites
will flag a policy difference for review, not prove a product bug. The unpackaged quick path
instead includes `AdapterFailureMessages_AreLocalized_French`, which expects
`Le paramètre ne doit pas être null ou vide.` because that path copies adapter translations.

The build records compile references and effective SDK/application-model/cache settings in
`obj/**/resolved-mstest-assets.txt`, with copy-local inputs in
`obj/**/resolved-mstest-runtime-assets.txt`. Inspect these alongside the generated manifest, package
layout, fresh TRX, and binlogs.

### Testing freshly packed repository packages

This public sample pins released SDK/packages in `global.json` and the shared sample properties.
A repository build does **not** retarget those pins. For repeatable local-package comparisons,
use its existing generated acceptance counterpart
`ModernUwpTests.ModernUwp_ConsumesUwpAssets_AndRunsPlainAndUiTestsThroughMtp`;
see the [local-package validation commands](../README.md#local-package-uwp-validation).
It executes net10/net11 x64 consumers against fresh local packages with isolated caches and unique
registration identities. Each generated consumer has six child cases (the public sample's
`TestHostRuns` is not duplicated); each name, outcome and counter is checked independently of the
process exit. `ModernUwp_BuildsMtpPackageForArchitecture` additionally guards x86/ARM64 build layouts
and application PRI presence, but does not prove execution on those architectures.

These are readiness guards, not proof that an experimental adapter layout has been validated.
A missing UWP/XAML/AppX toolchain, skipped outer test, or zero/inconclusive results leaves native
package validation blocked.

One-shot activation and connect-back payloads are consumed from package `LocalState`. The
development package registration is retained for repeated runs; remove it manually when needed:

```powershell
Get-AppxPackage -Name MSTestUwpMtpSample | Remove-AppxPackage
```

See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for classic UWP,
AppContainer security, CI, cleanup, and troubleshooting guidance.
