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

The Visual Studio components are build-time requirements. Execution uses the MTP integration package's
sidecar and does not use `vstest.console`, `UwpTestHostRuntimeProvider`, or the Visual Studio
deployment runtime.

## Build and run

Use a Developer PowerShell for Visual Studio:

```powershell
$run = [Guid]::NewGuid().ToString('N')
msbuild ClassicUwpMtpApp.sln /restore /p:Configuration=Release /p:Platform=x64 "/bl:ClassicUwpMtpApp-build-$run.binlog"
if ($LASTEXITCODE -ne 0) { throw "UWP build failed: $LASTEXITCODE" }
msbuild ClassicUwpMtpApp.csproj /t:InvokeTestingPlatform /p:Configuration=Release /p:Platform=x64 "/p:TestingPlatformCommandLineArguments=--report-trx --report-trx-filename ClassicUwpMtpApp-$run.trx" "/bl:ClassicUwpMtpApp-test-$run.binlog"
if ($LASTEXITCODE -ne 0) { throw "UWP execution failed: $LASTEXITCODE" }
```

The second command registers and activates the package by AUMID, runs the tests, publishes TRX,
and exits the app. One-shot activation and connect-back payloads are consumed from package `LocalState`.

## Packaging and localization checks

The packaged run must contain these **six passed results**, not just exit successfully:

| Test result | Contract |
| --- | --- |
| `PackageIdentityMatchesManifest` | The test really runs in this AppContainer package. |
| `UITestMethodRunsOnTheCoreWindowDispatcher` | The selected UWP extension dispatches a real `Windows.UI.Xaml.Controls.Grid` to the CoreWindow thread, not a WinUI dispatcher. |
| `AdapterFailureMessages_AreLocalized_English` | Public `TestContext.AddResultFile("")` throws `ArgumentException` for `fileName` with the English platform-services message. |
| `AdapterFailureMessages_UseNeutralFallbackForFrench` | Under French UI, the same API retains the English fallback required by the existing native UWP MTP satellite suppression; the previous UI culture is restored. |
| `ApplicationResources_AreLocalized_English` | The application PRI resolves the manifest's `AppDescription` to `MSTest classic UWP sample`. |
| `ApplicationResources_AreLocalized_French` | An explicit per-call French resource context resolves `Exemple UWP classique MSTest`. |

`Strings/*/Resources.resw` supplies a real manifest description. These checks do not change the
machine's language or application-wide resource policy. The existing explicit framework DLL copies
and `AppxGeneratePrisForPortableLibrariesEnabled=false` are retained baseline compatibility settings,
not universal fixes for package-layout changes. Application-owned PRI generation must still work.
The adapter-message guard exercises its real ResourceManager through public MSTest APIs, but does
not require an adapter translation that the baseline intentionally suppresses. If a candidate
starts exposing French satellites, the fallback assertion flags a policy difference for review,
not proof that either layout is a product bug.

The build records compile references and effective SDK/application-model/cache settings in
`obj/**/resolved-mstest-assets.txt`, with copy-local inputs in
`obj/**/resolved-mstest-runtime-assets.txt`. Inspect these alongside the generated manifest, package
layout, fresh TRX, and binlogs; a copied DLL's existence alone does not prove translation loading.

### Testing freshly packed repository packages

This public sample pins released SDK/packages in `global.json` and the shared sample properties.
A repository build does **not** retarget those pins. For repeatable local-package comparisons,
use its existing generated acceptance counterpart
`ClassicUwpTests.ClassicUwp_ConsumesUapAssets_AndRunsPlainAndUiTestsThroughMtp`;
see the [local-package validation commands](../README.md#local-package-uwp-validation).
It exercises MSTest.Sdk, MSTest, and explicit adapter/framework references in fresh generated
consumers with isolated package caches and unique registration identities. It also checks all six
child result identities/outcomes and counters. Its classic compatibility target is Windows SDK
16299; the public sample above uses SDK 26100.

These are readiness guards, not proof that an experimental adapter layout has been validated.
A missing UWP/XAML/AppX toolchain, skipped outer test, missing classic payload, or zero/inconclusive
results leaves native package validation blocked.

The development package registration is retained for repeated runs. Remove it manually when needed:

```powershell
Get-AppxPackage -Name MSTestClassicUwpMtpSample | Remove-AppxPackage
```

See [Testing UWP and WinUI apps with MSTest](../../../docs/winui-testing.md) for the UWP bootstrap,
AppContainer pipe authorization, CI, cleanup, and troubleshooting guidance.
