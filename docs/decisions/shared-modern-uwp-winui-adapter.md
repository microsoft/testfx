# Shared modern UWP and WinUI adapter: research and decisions

## Problem and contract

Build one `MSTest.TestAdapter.dll` and one `MSTestAdapter.PlatformServices.dll`
for modern UWP and WinUI at Windows 18362, while keeping their existing package
identities and application models:

| Consumer | Package | Consumer TFM | Adapter asset path |
| --- | --- | --- | --- |
| Modern UWP | `MSTest.Uwp` | `net10.0-windows10.0.18362.0` | `buildTransitive/net10.0-windows10.0.18362/` |
| WinUI | `MSTest.WinUI` | `net10.0-windows10.0.18362.0` | `buildTransitive/net10.0-windows10.0.18362/` |

Both packages will contain identical adapter DLL bytes. The WinUI package keeps
its 18362 package asset folder and its Windows App SDK dependency. Modern UWP's
`lib` and `buildTransitive` assets move from 17763 to 18362: consumers targeting
17763 must raise their Windows target to at least 18362. Classic
UWP (`uap10.0.16299`) remains a separately compiled variant; the ordinary
`MSTest.TestAdapter` package continues to exclude all UI-targeted payloads.

The shared modern-UWP/WinUI assemblies will compile with the WinUI code paths.
This intentionally changes modern UWP behavior where its old compile-time
branch omitted the assembly resolver, configuration-backed settings, desktop
working-directory behavior, and WinUI-style packaged test-source resolution.
The existing UWP argument-unwrapping workaround remains enabled in the shared
binary. No new public API, package dependency, launcher, process protocol,
handshake, cancellation, or cleanup contract is introduced.

The producer is `MSTest.TestAdapter` plus its bundled
`MSTestAdapter.PlatformServices`. The consumers are the shipping
`MSTest.Uwp` and `MSTest.WinUI` NuGets, which pack those DLLs through
`PlatformPackage.targets`; versions remain repository-aligned through
`$(PackageVersion)`. Consumer discovery and execution continue to use each
package's existing build-transitive targets and application model. Classic UWP
is a non-goal for this unification. Mainline is the only implementation target;
release-branch servicing/backports remain undecided and are not enabled here.

## Research and experiments

### 2026-11-18: source inspection at `ba85050ce3075f6f8a5777e75fcede110baa1fa3`

- `Directory.Build.props` defines modern UWP at
  `net10.0-windows10.0.17763.0` and WinUI at
  `net10.0-windows10.0.18362.0`.
- Both adapter projects currently build both TFMs. Their UI package projects
  already copy the adapter outputs from their `TargetDir` into distinct
  package-specific build-transitive paths.
- `MSTest.TestAdapter` public API baselines for modern UWP and WinUI match.
  Their internal baselines differ because modern UWP compiles the classic-UWP
  connect-back `CommandLineOptionParser`; only the classic-UWP adapter code
  calls it.
- The asymmetric PlatformServices paths are compile-time differences in
  `TestSourceHost`, `FileOperations`, `TestSourceHandler`, assembly resolution,
  provider discovery, and settings. `AppModel.cs` uses Windows API P/Invokes
  and does not reference WinAppSDK types.
- The app-model pipeline has a package-contract test phase and separate real
  UWP/WinUI activation phases. Its Windows-capable/elevated prerequisites are
  not established by this source inspection.
- The Release baseline pack succeeded at the inspected source revision with
  SDK `12.0.100-alpha.1.26562.108`, package version `5.0.0-dev`, 0 warnings,
  and exit 0. `MSTest.Uwp.5.0.0-dev.nupkg` SHA-256:
  `61081F94CD5C3559CD6A51B8F1D8C76B13B4EE03DD393B376152C0EF114F396A`;
  `MSTest.WinUI.5.0.0-dev.nupkg` SHA-256:
  `962EF8F315C82C4011FDEAEF39B7F483297E5088952ACD2D8AEC6CBE063F55F9`.
- Baseline adapter payload hashes differ:
  `MSTest.TestAdapter.dll` modern UWP
  `12BFEB069241690E0674286926A2883F9402AF609923CA67AC7938BE9014EEE8`,
  WinUI `9F645C1539CCAE643042096D614938D148B4019E14A4F7658F47EA9ED64C0A2C`;
  `MSTestAdapter.PlatformServices.dll` modern UWP
  `4941E977CB9728C05CB7DCC77CC427D0D94FFE6EB4ED343F147E557B60F05E4E`,
  WinUI `34CAAE32C6C2C3EECE88B3954329B5B23772257F201AF87DCA3B2DB4C0AE89B3`.
- Added `PackedModernUwpAndWinUI_ContainIdenticalAdapterAssemblies` and ran it
  against those baseline packages. Discovery found one test; it executed and
  failed the intended equality assertion on `MSTest.TestAdapter.dll` (outer
  exit 2, 1 failed, 0 skipped). The test build succeeded with 0 warnings.

## Alternatives and decision

- **Keep separate adapter TFMs:** preserves today's compile-time behavior, but
  continues to produce two independently compiled DLL pairs.
- **Unify on the common behavior intersection:** would disable WinUI-supported
  behavior and is not acceptable.
- **Unify at 17763 using WinUI paths:** initially implemented and validated
  below, then superseded by the user's request to use 18362.
- **Unify at 18362 using WinUI paths:** selected. It gives the packages one
  adapter build and aligns modern UWP's framework and adapter asset folders
  with the WinUI target. The accepted tradeoff is that modern
  UWP gains the WinUI path behavior listed above; real app-model validation is
  required before treating that behavior as release-ready.

Reconsider this choice if the packed modern-UWP consumer cannot use the WinUI
paths or if its app-model tests show a compatibility regression.

## Validation and demonstration

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Modern UWP and WinUI packages carry the same adapter binaries under 18362 | `PackedModernUwpAndWinUI_ContainIdenticalAdapterAssemblies` compares each adapter DLL from the current `MSTest.Uwp` and `MSTest.WinUI` packages. Run after a Release pack and verify exact planned/executed test identity. | Nonvisual package contract; inspect physical `.nupkg` entries and hashes. |
| Each package still resolves its expected adapter composition | Existing `PackedUIFramework_OwnsItsPayloadWithoutAnotherAdapter` and Windows application-model package contract tests. | No visual surface. |
| Shared assemblies build and existing behavior remains supported | Build the adapter projects and run focused adapter/platform-services tests for their Windows target. | Real modern-UWP and packaged/unpackaged WinUI activation is the relevant runtime surface. |
| Modern UWP's newly enabled WinUI paths work in the shipping package | Existing real-UWP and WinUI app-model acceptance cases where applicable. | If the local host lacks the app-model prerequisites, report that activation and the modern-UWP behavior change remain unverified. |

The acceptance producer/consumer path is the Release-packed adapter projects,
then `MSTest.Uwp` / `MSTest.WinUI` package payloads and their existing consumer
TFMs. The app-model pipeline selects package-contract tests separately from
real activation tests. Any checks not executable on this host remain explicit
gaps; package hashes and unit tests do not substitute for activation.

Baseline commands from the worktree root:

```powershell
.\build.cmd -pack -c Release -bl
.\build.cmd -c Release -projects test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -bl
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --list-tests text --filter "FullyQualifiedName~WindowsApplicationModelPackageTests.PackedModernUwpAndWinUI_ContainIdenticalAdapterAssemblies" -p:UsingDotNetTest=true -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments=
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~WindowsApplicationModelPackageTests.PackedModernUwpAndWinUI_ContainIdenticalAdapterAssemblies" -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\uwp-winui-unify-red-d89425dd243743969fad4316245ad09a -bl:artifacts\TestResults\Release\uwp-winui-unify-red-d89425dd243743969fad4316245ad09a\execution.binlog --show-test-results all
```

The test-only counterfactual is the current adapter production code with the
new package equality test retained. Discovery and execution logs, TRX, and
binlogs are under
`artifacts\TestResults\Release\uwp-winui-unify-red-d89425dd243743969fad4316245ad09a`.

## Initial 17763 implementation and validation (2026-11-18)

The change is implemented on top of
`ba85050ce3075f6f8a5777e75fcede110baa1fa3`. The validated code-and-test diff
(excluding this record) SHA-256 is
`4969851986376CABE3796A46E92738D2D91EAB936EB4721C9C81D496DB5B19D7`.
Both adapter projects now build the shared UI pair at
`net10.0-windows10.0.17763.0` with `WIN_UI`. Classic UWP remains its own
`uap10.0.16299` build with `WINDOWS_UWP`; the UI packages still place the
shared pair under their existing, distinct asset paths.

The post-change Release pack succeeded with 0 warnings and 0 errors. The
package contract test verified byte equality for both assemblies:

| Assembly | SHA-256 in both packages |
| --- | --- |
| `MSTest.TestAdapter.dll` | `DA9A9E42754E35D7EEA76A38FEF731A3C6AD6434170CC10E930C1AE979C0B420` |
| `MSTestAdapter.PlatformServices.dll` | `B6424CA392D3FE02651C5E27D91D7861669046F18386E3985D6B3D05955ADD52` |

Focused build and execution results:

| Validation | Result |
| --- | --- |
| `.\build.cmd -c Release -projects test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -bl` | Passed, 0 warnings/errors. |
| PlatformServices tests at `net10.0-windows10.0.18362.0` | 1,350 passed, 0 failed, 0 skipped. |
| `.\build.cmd -c Release -projects test\UnitTests\MSTestAdapter.UnitTests\MSTestAdapter.UnitTests.csproj -bl` | Passed, 0 warnings/errors. |
| Adapter tests at `net10.0-windows10.0.18362.0` | 170 passed, 0 failed, 0 skipped. |
| `PackedModernUwpAndWinUI_ContainIdenticalAdapterAssemblies` (1), `PackedUIFramework_OwnsItsPayloadWithoutAnotherAdapter` (3), and `ModernUwp_ResolvedAssetsRequireMatchingFrameworkAndSpecializedPayloads` (2) | 6 passed, 0 failed, 0 skipped; the fresh TRX was checked against the exact data-row identities. |
| Real app-model acceptance: modern UWP .NET 10, .NET 11, unsupported deployment, and unpackaged WinUI UI-dispatcher run | 4 passed, 0 failed, 0 skipped. |
| Packaged WinUI full-trust registration, activation, connect-back, UI test, and cleanup | 1 passed, 0 failed, 0 skipped. |

Reproduction commands from the repository root (each build and test process
exited 0):

```powershell
.\build.cmd -pack -c Release -bl
.\build.cmd -c Release -projects test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -bl
.\.dotnet\dotnet.exe run --project test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -f net10.0-windows10.0.18362.0 -c Release --no-build -- --results-directory artifacts\TestResults\Release\uwp-winui-platformservices-unit-18362 --show-test-results all
.\build.cmd -c Release -projects test\UnitTests\MSTestAdapter.UnitTests\MSTestAdapter.UnitTests.csproj -bl
.\.dotnet\dotnet.exe run --project test\UnitTests\MSTestAdapter.UnitTests\MSTestAdapter.UnitTests.csproj -f net10.0-windows10.0.18362.0 -c Release --no-build -- --results-directory artifacts\TestResults\Release\uwp-winui-adapter-unit-18362 --show-test-results failed
.\build.cmd -c Release -projects test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -bl
.\eng\pipelines\test-windows-app-model-preflight.ps1 -Mode Preflight
$env:TESTFX_RUN_WINDOWS_APP_MODEL_TESTS = '1'
.\.dotnet\dotnet.exe test test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~WindowsApplicationModelPackageTests.PackedModernUwpAndWinUI_ContainIdenticalAdapterAssemblies|FullyQualifiedName~WindowsApplicationModelPackageTests.PackedUIFramework_OwnsItsPayloadWithoutAnotherAdapter|FullyQualifiedName~WindowsApplicationModelPackageTests.ModernUwp_ResolvedAssetsRequireMatchingFrameworkAndSpecializedPayloads" -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\uwp-winui-unify-green-bfb585f955d442089aad04f74c680bf8 -bl:artifacts\TestResults\Release\uwp-winui-unify-green-bfb585f955d442089aad04f74c680bf8\execution.binlog --show-test-results all
.\.dotnet\dotnet.exe test test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~ModernUwpTests.ModernUwp_ConsumesUwpAssets_AndRunsPlainAndUiTestsThroughMtp|FullyQualifiedName~UnpackagedWinUITests.SelfHostedUnpackagedWinUI_RunsTests_AndProcessExits" -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\uwp-winui-real-activation -bl:artifacts\TestResults\Release\uwp-winui-real-activation\execution.binlog --show-test-results all
.\build.cmd -c Release -projects test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -bl
$env:TESTFX_RUN_WINDOWS_APP_MODEL_TESTS = '1'
.\.dotnet\dotnet.exe test test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~PackagedWinUITests.PackagedFullTrustWinUI_RegistersActivatesConnectsBackRunsUiTestsAndCleansUp" -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\uwp-winui-packaged-winui-activation -bl:artifacts\TestResults\Release\uwp-winui-packaged-winui-activation\execution.binlog --show-test-results all
```

The Windows app-model preflight passed on an interactive Windows 11 host and
the real activation tests ran successfully. The six package-contract TRX and
the real-activation TRX are under
`artifacts\TestResults\Release\uwp-winui-unify-green-bfb585f955d442089aad04f74c680bf8`
and
`artifacts\TestResults\Release\uwp-winui-real-activation`; packaged WinUI
evidence is under
`artifacts\TestResults\Release\uwp-winui-packaged-winui-activation`.
The app-model runs were focused selections, not the complete Windows
application-model pipeline. The activation coverage exercises the shipping
MSTest packages through MTP; the adapter itself was separately built and
unit-tested under the WinUI TFM.

The acceptance-run verifier initially rejected an expected-test plan that
omitted data-row display names; correcting the plan to use the exact TRX names
produced a passing verification of all six selected cases. A separate
`--list-tests` attempt was rejected because the acceptance project's default
report generators conflict with discovery; it ran zero tests and is not
counted as validation.

### Test-value evidence

The focused package equality test was executed against the baseline packages
before the production change: 1 test executed and failed on the expected
`MSTest.TestAdapter.dll` hash mismatch. After unifying the build, the same
assertion passed against the newly packed packages, and the six selected
package-contract cases plus the targeted app-model activation cases passed.
This proves the package-level byte-identity contract and the tested app-model
paths; it does not claim that every Windows app-model case or a packaged
VSTest-host activation path was exercised.

## 18362 target revision (2026-11-18)

The user requested 18362 instead of 17763 after publication of
`25e7a6097095079560666a48af9c5d188d9a5d15`. The shared adapter now selects
`WinUiMinimum`; `ModernUwpMinimum` aliases that same TFM. The UWP package must
move its framework and build-transitive assets too, because NuGet cannot
resolve a project reference from a 17763 framework to an 18362 adapter.
The PlatformServices OS annotation returns to 18362. Classic UWP, ordinary
.NET targets, WinUI package dependencies, and all launch/cleanup protocols are
unchanged. No release-branch rollout is part of this mainline revision.

The existing package/layout expectations and real-UWP resolved-asset checks
are updated together. `PackedConsumersUseSharedCoreAndSelectedUiPackage`
also covers a modern-UWP consumer at exactly 18362, in addition to the existing
higher-target .NET 10/.NET 11 consumers. The tests retain their existing
parallel scheduling: package checks only read the packed files, generated
consumers own isolated directories/caches, and real UWP/WinUI activation keeps
its existing exclusions and unique package identities.

Before changing production, the baseline revision was repacked successfully.
With the updated test retained, one package-equality test executed and failed
at the intended assertion: `MSTest.Uwp must contain
buildTransitive/net10.0-windows10.0.18362/MSTest.TestAdapter.dll.`
The outer process exited 2 (1 failed, 0 skipped); this is missing-package-asset
evidence, not a compilation or launch failure.
Logs, the test-only patch, and copied baseline packages are retained under
`artifacts\TestResults\Release\shared-ui-18362`.

The first 18362 pack compiled successfully but failed the package-shape gate
because `eng/package-shapes/MSTest.Uwp.txt` still listed 17763. Running
`.\eng\verify-nupkgs.ps1 -configuration Release -updateBaselines` changed only
that package baseline. The confirming Release pack passed with zero warnings
and errors. No verification gate was disabled.

The final tested state is
`25e7a6097095079560666a48af9c5d188d9a5d15` plus the non-record patch SHA-256
`D1BEEBE2B6E0A6D202186F42CDC453B20BD6EB96F76BEEBB2493B4A68B890D82`.
The SDK remains `12.0.100-alpha.1.26562.108`; the outer acceptance hosts target
`net12.0`, consuming freshly packed `MSTest.*` `5.0.0-dev` and MTP
`3.0.0-dev` packages.

| 18362 validation | Executed result |
| --- | --- |
| Package entries, shared adapter hashes, sealed UI attributes, ordinary-adapter exclusion and resolved-asset helper | 10 passed, 0 failed/skipped. Includes the unchanged red-phase package-equality assertion. |
| `PackedConsumersUseSharedCoreAndSelectedUiPackage` | 8 passed, including `ModernUwpMinimum` at 18362 and existing .NET 10/.NET 11 cases. Each generated consumer checks restored package hashes, compile/runtime assembly paths and copied DLL hashes against the current pack before executing. |
| `MSTestSdk_MixedOrdinaryAndUiTargetFrameworks_SelectOneCompositionPerInnerFramework` | 4 passed: UWP/WinUI with central package management on/off. |
| Modern UWP .NET 10/.NET 11/unsupported-deployment and unpackaged WinUI | 4 passed, 0 failed/skipped. Real UWP resolves the new 18362 payload, runs plain/UI tests, verifies final TRX outcomes, and cleans up its unique registration. |
| Packaged full-trust WinUI | 1 passed, 0 failed/skipped: registration, activation, connect-back, UI execution and cleanup. |

The three outer runs exited 0. Independent discovery was reconciled with source
rows, and `verify_execution.py` verified planned/selected/executed/passed counts
of 22, 4 and 1 against their fresh TRX files. Method-level scheduling with
CPU-count workers and existing real-activation exclusions were preserved.

The two NuGets have SHA-256
`D1ADDBDAC7E5FB1DE5D9BF237661517D84C41E355B79F34AE35C56A60C2A4DE5`
(`MSTest.Uwp`) and
`0F06E874B846964941DE7DD4B07B64C2BA97030DBF2CE0D2F63036C3697A6F1A`
(`MSTest.WinUI`). Their shared adapter payload hashes are:

| Assembly under `buildTransitive/net10.0-windows10.0.18362/` | SHA-256 in both packages |
| --- | --- |
| `MSTest.TestAdapter.dll` | `72B3F0953701CAB7C65CA278917206FFA1286D7955021BC012F9C75190695022` |
| `MSTestAdapter.PlatformServices.dll` | `0F89A1D93AF5BCADC851665D6FD2D6BDC3127FF9C1777B48ABCBFB82DBF50715` |

Exact successful build/execution commands (from the worktree root):

```powershell
.\build.cmd -pack -c Release -bl
.\build.cmd -c Release -projects test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -bl
.\eng\pipelines\test-windows-app-model-preflight.ps1 -Mode Preflight
$results = Join-Path $PWD 'artifacts\TestResults\Release\shared-ui-18362'
$filter = 'FullyQualifiedName~WindowsApplicationModelPackageTests.PackedModernUwpAndWinUI_ContainIdenticalAdapterAssemblies|FullyQualifiedName~WindowsApplicationModelPackageTests.PackedUIFramework_OwnsItsPayloadWithoutAnotherAdapter|FullyQualifiedName~WindowsApplicationModelPackageTests.ModernUwp_ResolvedAssetsRequireMatchingFrameworkAndSpecializedPayloads|FullyQualifiedName~WindowsApplicationModelPackageTests.PackedUIFramework_SealsConcreteUIAttributes|FullyQualifiedName~WindowsApplicationModelPackageTests.PackedMSTestTestAdapter_ContainsOnlyOrdinaryAssets|FullyQualifiedName~CoreFrameworkAssemblyTests.PackedConsumersUseSharedCoreAndSelectedUiPackage|FullyQualifiedName~SdkTests.MSTestSdk_MixedOrdinaryAndUiTargetFrameworks_SelectOneCompositionPerInnerFramework'
.\.dotnet\dotnet.exe test test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter $filter -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results\green" "-bl:$results\green\execution.binlog" --show-test-results all
$env:TESTFX_RUN_WINDOWS_APP_MODEL_TESTS = '1'
.\.dotnet\dotnet.exe test test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter 'FullyQualifiedName~ModernUwpTests.ModernUwp_ConsumesUwpAssets_AndRunsPlainAndUiTestsThroughMtp|FullyQualifiedName~UnpackagedWinUITests.SelfHostedUnpackagedWinUI_RunsTests_AndProcessExits' -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results\activation" "-bl:$results\activation\execution.binlog" --show-test-results all
.\build.cmd -c Release -projects test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -bl
.\.dotnet\dotnet.exe test test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release --no-build --filter 'FullyQualifiedName~PackagedWinUITests.PackagedFullTrustWinUI_RegistersActivatesConnectsBackRunsUiTestsAndCleansUp' -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results\packaged-winui" "-bl:$results\packaged-winui\execution.binlog" --show-test-results all
```

The red command used the same package-equality selector and assertions, with
`ArtifactsTestResultsDir` and the binlog under `$results\red`, against the
repacked 17763 baseline. It executed one test, failed the missing-18362-entry
assertion and exited 2. Source-bound logs, expected-test JSON, TRX and binary
logs remain under the result directories above; these are local artifacts, not
uploaded CI evidence. The tables and hashes here are the reviewer-visible
evidence. Screenshots are inapplicable to this nonvisual targeting change.

**Test-value evidence:** executed detection of the missing 18362 package asset,
followed by the same test passing with the raised target. Current package
provenance and 27 passing composed/package cases establish the requested
target change; the original DLL-unification red/green experiment remains
historical evidence for the earlier revision, not a new experiment here.
Not rerun: the full Windows app-model matrix, classic-UWP activation, packaged
VSTest-host activation, standalone sample applications and adapter unit suites.
No Windows 18362 OS machine or PR CI execution was verified; the 18362-targeted
consumer and real app activation ran on the preflight-approved Windows 11 host.

## Open questions

- Which servicing branches, if any, need this change after mainline validation?
