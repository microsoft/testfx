# Explicit MSTest packages: layer 2

## Problem and contract

This is the package-extraction layer replacing
[#12435](https://github.com/microsoft/testfx/pull/12435), following
[#7225](https://github.com/microsoft/testfx/issues/7225),
[#12412](https://github.com/microsoft/testfx/pull/12412) and
[#12428](https://github.com/microsoft/testfx/pull/12428). The
[shared-core prerequisite](shared-core-ui-contract.md), delivered separately in
[#12446](https://github.com/microsoft/testfx/pull/12446), owns the runtime changes;
this layer does not repeat or extend them.

`TestFramework.csproj` becomes the `MSTest.TestFramework` package owner.
`MSTest.Uwp` owns the existing classic/modern UWP UI attribute and matching
adapter/services; `MSTest.WinUI` owns the existing WinUI attributes and matching
adapter/services. Each UI package pins core to its exact package version, uses
ordinary `lib/<tfm>` assets, and carries its own build assets and satellites.
Modern net10.0 Windows assets also support compatible net11.0 consumers. Neither
UI package depends on the ordinary adapter or the other UI flavor.

Extensions is retired, not retained as a live compatibility stub. Its existing
API records are byte-preserving `.txt.history` moves, outside release promotion.
Consumers must update UI package/assembly references and recompile at this
intentional next-major boundary. Ordinary and net462 resolver/configuration
behavior remain unchanged. `AppModel` moves unchanged into PlatformServices,
where its actual callers already live.

The SDK automatically selects the existing application model using `UseWinUI`,
`UseUwpTools`, early `UseUwp`, and classic `uap` inference. An explicit
`UseUwpTools=false` remains an opt-out from early modern UWP inference.
Selection is shared by ClassicEngine, NativeAOT, VSTest, existing generated-code
helper injections, and central package management. This layer introduces no
public override, new diagnostics, helper privacy/copy-local policy or builder-hook
policy. Existing launch, activation, cancellation, handshake, exit aggregation
and cleanup ownership are unchanged.

Native/Bazel edits are limited to live source/type-owner references made invalid
by deleting Extensions. New native UI archives, exact-range machinery and
expanded validation belong to layer 4. SDK policy/composition coverage belongs
to layer 3. No release-branch enablement or backport is made here; the series
coordinator owns dependency flow, rollout and servicing decisions.

## Research and decision

2026-11-13: the initial clean parent and remote ref both identify
`e22e899febaeee44ea3af385403e1c65c18164bb`. The frozen source ref and live
#12435 head both identify `00428d48c212cf3630dcf6fa31d131c5ae745c98`.
The carving input is
`8dd78e1347417e400c3ef8d786059a8bb346162f...refs/pr-stack/source/12435`.
Parent changes must be reconciled, not replaced with old snapshots.

Keeping Extensions would preserve the hidden UI asset-swapping package design.
Copying the full source SDK/native policy would defeat the smaller review.
The chosen boundary extracts the source's package owners and small exact-version
packing hook, retaining existing runtime and helper policy. Ordinary adapter
restore/build still expose UI variants; pack must narrow both NuGet's public
framework list and Sdk.Extras' cached flavor matrix so UI payloads and dependency
groups cannot leak.

## Validation and demonstration

| Requirement | Existing automated evidence | Surface / limitation |
| --- | --- | --- |
| Core/UI archive ownership, exact core dependency, no flavor leaks | `WindowsApplicationModelPackageTests` and physical package inspection | Physical nupkg entries and nuspec groups, not inferred project metadata |
| Ordinary/direct/metapackage consumers use the shared core | `CoreFrameworkAssemblyTests`, `FrameworkTargetingTests` | Packed/cached/resolved/copied hashes and loaded identities |
| Automatic SDK selection preserves existing applications | Existing UI and helper cases in `SdkTests` | Real packed SDK; expanded policy matrix deferred |
| Classic/modern UWP and packaged/unpackaged WinUI still execute | Existing `ClassicUwpTests`, `ModernUwpTests`, `WinUITests`, `UnpackagedWinUITests`, `PackagedWinUITests` | Existing environment gates and owned activation lifecycle retained |
| AppModel relocation and ordinary net462 behavior are preserved | `WinUIExtensionTests.UnpackagedProcessHasNoPackageIdentityOrPath`, existing net462 consumer | Reflection owner follows the actual assembly; no native baseline repair |

Validation used Release outputs at shipping revision
`f9708f5edbd37215b7e53cef840ef1e29ddf3f93`, SDK
`12.0.100-alpha.1.26556.115` and desktop MSBuild `18.13.0`. The later evidence
commit changes this record only. The populated producer cache was input, never
cleared or copied; same-version consumers used fresh task-owned caches. Pack
preceded consumers. Historical results from the unsplit source are not credited.

The acceptance assemblies retain method-level, CPU-count parallelization.
Generated consumers use per-invocation assets and isolated cache state; existing
class exclusions for shared mutable app assets remain unchanged. The internal
TestContainer engine is sequential. No scheduling declarations change.

Package ownership is nonvisual; the archive/consumer transcript below is more
useful than a staged screenshot. Real UI activation was exercised separately.

The first pinned-script invocation did not start because the bounded PATH omitted
Windows PowerShell; correcting that command-local path fixed setup. The clean
baseline build then passed all four declared framework-test TFMs, and
`WinUIExtensionTests.UnpackagedProcessHasNoPackageIdentityOrPath` executed
**1 passed, 0 failed, 0 skipped** before relocation.

The 36-project actual-source pack filter contained the ten MSTest package owners,
their reference closure, the pack-time global-config generator, and packages
unconditionally required by the existing acceptance fixture. The command
`.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer2-pack.slnf -bl`
exited **0**, with **0 warnings and 0 errors**, including the package content and
metadata verifier. Core built four TFMs, each adapter/service still built all six
Windows variants, and both classic/modern UWP plus WinUI package owners built.

The native adaptation is separate from the substantive package extraction:
15 existing source/layout/probe files and one matching unit-test fixture lose or
replace obsolete owner references. Existing labels, target matrices, rule
machinery, Hosting behavior and append-only app-model artifacts are retained.
No native UI package production, range machinery or expanded validation is added.

### Executed requirements

Independent discovery and the strict TRX execution gate reconciled every primary
selection: **71 executed, 71 passed, 0 failed, 0 skipped** (70 acceptance cases
and one TestContainer case). These are not added to historical counts.

| Requirement | Exact cases / observed result |
| --- | --- |
| Shared core and selected UI package, including net11 and explicit modern UWP opt-out | Six `CoreFrameworkAssemblyTests.PackedConsumersUseSharedCoreAndSelectedUiPackage` rows; packed/cached/resolved/copied hashes and loaded core/UI/resource paths matched. |
| Ordinary net10/net11 runtime selection | Two `FrameworkTargetingTests.PackedPackages_UseMatchingFrameworkAssetsAndRuntime` rows passed. |
| Physical archives, API availability, exact pin and flavor isolation | 21 selected `WindowsApplicationModelPackageTests` rows passed, including all three `PackedUIFramework_OwnsItsPayloadWithoutAnotherAdapter` rows. |
| Unchanged runtime guards from layer 1 | `WinUITests.SimpleWinUITestCase` and `Deployment_Reflection`, `Deployment_Rooting`, `Deployment_ReflectionFree` passed. Each negative child produced its four associated deployment failures and one metadata-provider pass. |
| Existing SDK selection, satellites, helper default and net462/configuration behavior | 25 selected SDK/net462 cases passed, including `MSTestSdk_UwpXamlReferencesWithoutUwpTools_BuildsAndRunsDirectMtpRunner`, `NetFrameworkCleanupFailureMessagesRetainBothMethodIdentities` and `TestDataSourceFromAppConfig`. The latter two intentionally assert failing children; their outer cases passed. |
| Real classic UWP via SDK, metapackage and direct Uwp package | Three `ClassicUwp_ConsumesUapAssets_AndRunsPlainAndUiTestsThroughMtp` rows passed. Each recovered TRX had the exact plain/UI identities, 2 executed and 2 passed. |
| Real modern UWP net10/net11 and unsupported deployment | `ModernUwp_Net10`, `ModernUwp_Net11`, `ModernUwp_UnsupportedDeployment` passed. Normal children had 2/2 passes; the negative child had 2 passes and the 2 expected inherited-deployment failures, with no body execution. |
| Real unpackaged WinUI | `SelfHostedUnpackagedWinUI_RunsTests_AndProcessExits` passed. |
| Real packaged SDK/direct WinUI | `PackagedFullTrustWinUI_RegistersActivatesConnectsBackRunsUiTestsAndCleansUp` and `PackagedAppContainerWinUI_ExplicitIntegrationPublishesTrx` passed. The direct AppContainer TRX associated all five expected identities with Passed. |
| Existing generated-code helper flow and CPM | `MSTestSdk_SourceGenerationOptIn_DoesNotFlowTransitively` and both `JUnitReport_UsesPlatformVersion_WhenOptedIn` rows passed. The helper retains its existing adapter dependency; no layer-3 privacy policy is introduced. |
| Unchanged AppModel implementation, new reflection owner | `UnpackagedProcessHasNoPackageIdentityOrPath`: 1/1 green before and after relocation. The source blob remains `9efd65874c58227c59de033a4568f7501aa085cf`. |

Real consumer manifests selected classic `lib/netstandard2.0` core and
`MSTest.Uwp/lib/uap10.0.16299`; modern net10/net11 core came from the corresponding
ordinary lib folder with Uwp's net10 Windows assets; WinUI used its own normalized
net10 Windows folder. Cached core/Uwp/WinUI/SDK/metapackage archive hashes matched
the current producer. All 28 archived API files were byte-identical moves.

The initial real-app attempt used an unnecessarily long task cache: XAML
references reached 277 characters and the controller loader also failed to open
present files. Fresh short caches reduced the longest inspected WinUI reference
to 240 characters. The same real-app selections then passed without any product
or test-source change. These setup failures are not behavioral red proof.
Preflight and the final registration leak check both exited 0; no reserved test
package registration remained. Owned diagnostics and caches are retained.

### Commands and retained inputs

All commands ran from their owning worktree with a bounded command-local PATH.
The exact generated filters, independent discovery, run manifests, binlogs and
TRX are retained under `artifacts/validation` and `artifacts/TestResults/Release`.
The generated pack filter enumerates actual `TestFx.slnx` projects, not source
globs or globally overridden target frameworks. The test filter names the three
actual framework/MSTest/MTP acceptance owners.

```powershell
.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer2-pack.slnf -bl
.\build.cmd -c Release -msbuildEngine vs -projects artifacts\validation\layer2-tests.slnf -bl
.\build.cmd -c Release -msbuildEngine vs -projects test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj -bl
```

Each exited 0 with no warnings/errors. The first command was repeated after the
shipping checkpoint was pushed. Each acceptance selection used this command,
with its independently discovered filter and its own result/binlog path:

```powershell
.\.dotnet\dotnet.exe test --project $project -c Release --no-build --filter $filter -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" "-bl:$binlog" --show-test-results all
```

The MSTest acceptance selections were contract (33), SDK/net462 (25), real UWP
and unpackaged WinUI (7), generated-code helper (1), and CPM/non-CPM (2). The MTP
acceptance selection was packaged SDK/direct WinUI (2). All exited 0. The direct
framework host used
`--filter-uid Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions.WinUIExtensionTests.UnpackagedProcessHasNoPackageIdentityOrPath --report-trx`
before and after; both exited 0. Packed SDK XML inspection also verified that
ClassicEngine, NativeAOT, VSTest and existing helper injections use the same
private package-id property for both PackageReference and CPM PackageVersion.
This static check is not a new NativeAOT/composition execution matrix.

### Test-value evidence

The outside-repository comparison retained the same test source blob
`68740964bb4d22979e5a197dc9b46dffd4e55a35` and source revision
`f9708f5edbd37215b7e53cef840ef1e29ddf3f93`. Its only mutation removed the eight-line
`_AlignMSTestPlatformPackageCoreVersion` packing target; patch SHA-256:
`782C978E864EE5D000D0C9EF4645FFCB16BD7F4F033057747872C89934910676`.
The primary worktree was never mutated for this experiment.

Both counterfactual and restored states rebuilt/repacked with the same 36-project
pack command above (exit 0). The unchanged acceptance owner also rebuilt (exit 0).
The same selector was used in both states:

```powershell
$filter = 'FullyQualifiedName~WindowsApplicationModelPackageTests.PackedUIFramework_OwnsItsPayloadWithoutAnotherAdapter'
```

| Contract | Without behavior | Correct implementation |
| --- | --- | --- |
| Exact core pin, classic/modern Uwp and WinUI | Executed: detected. 3 selected/executed/failed, 0 skipped, outer exit 2. Every failure was the exact-range assertion: expected `[5.0.0-dev]`, actual `5.0.0-dev`. | Executed: 3 selected/executed/passed, 0 skipped, outer exit 0, after restoration and repack. |
| AppModel relocation | Preservation baseline: 1 passed. | 1 passed with the reflection owner changed to PlatformServices. |
| Other archive/routing/runtime contracts | Separate behavior-removal mutations were not executed. | The named real consumers and archive checks above passed; no broader mutation claim is made. |

Red archives, both paired reports, fresh run-owned caches, the exact mutation,
commands and package hashes are retained. The comparison source was restored
cleanly. The negative run did not rely on compilation/setup failures or skips.

### Primary package identity

All four packages are `5.0.0-dev`; the aligned MTP runtime family is `3.0.0-dev`.

| Package | Physical files | SHA-256 |
| --- | ---: | --- |
| MSTest.TestFramework | 67 | `D2EE5D08A2CBB07F1B121BF45638D91B379A9786DF97600BFEDB35683E6AF6EF` |
| MSTest.TestAdapter | 54 | `D92345C1F377777D90211009BEAD6F2357DC0A22C1BA3AAF0EABF7542CED416C` |
| MSTest.Uwp | 73 | `CACA4C1F125823A65098CAE676F154529BD945B358400E6A7D4B3C6D990B0EC2` |
| MSTest.WinUI | 53 | `71499BFDCF652259B4A09CFA9CCAA7E1E80CB5DA743FF0F1CECBC713127A484C` |

## Open questions and deferred proof

No shipping-source fix was needed after the pushed checkpoint. Live CI execution,
full native Bazel execution, x86/ARM64 activation, native shipping .NET 10 SDK
runner interoperability and release-branch/backport validation are not claimed.
The known native net462 PlatformServices System.Reflection.Metadata failure has
only static baseline attribution and was not repaired or experimentally chased.
The coordinator retains downstream rollout/servicing ownership; layers 3 and 4
retain their explicitly deferred policy/native-package responsibilities.

## Native parity slice: layer 4

2026-11-13: carve only the residual native rules, probes and validators from
`00428d48c212cf3630dcf6fa31d131c5ae745c98` onto the verified SDK parent
`17b8abac7c8959e73413e01f2c92b0998b590551`. Shipping source, package policy,
runtime behavior and the lower layers' evidence remain unchanged.

Native modern `MSTest.Uwp` and `MSTest.WinUI` archives must use canonical
`lib`/`buildTransitive` paths, matching adapters/services/satellites, and an exact
`[matching core version]` dependency without ordinary/other-flavor payload leaks.
Desktop and both UI library consumers must load the same ordinary core bytes.
An absent ordinary-core target-framework attribute remains supported; declared
wrong/Windows metadata is rejected, with hash comparisons keyed by the planned
TFM. Reuse the narrow UI-only range-tool mode, not a new metadata producer.
Native classic UAP production remains unsupported.

Keeping flavored cores or copying the whole unsplit PR would violate this
boundary. The chosen native delta follows the actual source inputs, preserves
Hosting/core alignment and append-only evidence, and updates portable fixtures
only where required by the existing `net10.0;net11.0` contract. SDK test scheduling
stays method-level/CPU-count, with per-test temporary paths and bounded joined
child processes; shared producer inputs remain read-only.

Planned evidence is `//:sdk_package_tests`, the five staged native Windows library
probes plus targeting/asset tests, and the two modern UI archive targets checked
with the maintained `Assert-PackageLayout` logic. These are nonvisual library and
archive contracts, not UI activation or full ordinary native-consumer proof.
The known native net462 PlatformServices metadata-reference failure, CI,
other architectures, classic UAP and release-branch validation remain explicit
gaps; rollout/servicing ownership stays with the series coordinator.

Executed native evidence: the 19 native inputs and patch remained byte-identical
before/after all runs. The native patch SHA-256 against the parent above is
`8A5A3F20DCE2FEBA1FE187D43C574013B5B108ED9CBFEE5304C7C02D090E35C1`;
`src/`, the Hosting/core rules and producer patches were not changed. Bazel
`9.1.1` used pinned SDK `11.0.100-rc.3.26522.148` on Windows x64.

| Requirement | Command / executed evidence |
| --- | --- |
| SDK/meta-package contracts | `eng\bazel.ps1 test //:sdk_package_tests --jobs=2 --test_output=errors`: exit **0**, TRX **15 passed, 0 failed, 0 skipped**. `Generate_SdkPreservesTemplatesAndComponentVersions`, `Generate_MetaPackagePreservesDependencyAndAssetFlow` and all 13 `Generate_RejectsContractDriftBeforeWritingOutputs` rows executed, including `meta-retired-framework`, `meta-ui-reference`, and `meta-ui-condition`. |
| Independent SDK discovery | `eng\bazel.ps1 run //:sdk_package_tests_app --noshow_progress '--' --list-tests`: exit **0**, the same 15 source-planned identities. Direct `.bat` discovery lacked runfiles; the test wrapper also rejects discovery with its execution-only minimum/TRX defaults. The application target is the correct discovery entry. |
| Shared ordinary core and isolated UI libraries | `eng\bazel\windows\validate.ps1 -ArtifactsDirectory artifacts\validation\native-parity-e4c16b\windows`: exit **0**; five staged consumers (`windows_probe_packaged_net10`, `windows_probe_packaged_net11`, `windows_probe_desktop`, `windows_probe_uwp`, `windows_probe_winui`) and both executed XML cases (`windows_targeting_test`, `windows_asset_selection_test`) passed. Runtime versions were `10.0.14` and `11.0.0`. |
| Actual modern UI archives | `eng\bazel.ps1 build //:mstest_uwp_package //:mstest_winui_package --jobs=2 --noshow_progress --show_result=0`: exit **0**. AST-loaded maintained `Assert-PackageLayout` checks passed for those two cquery-resolved archives; DLLs/satellites matched the staged native probes and build assets matched current source. This was scoped UI validation, not a full validator/ordinary consumer run. |

Both UI packages are `5.0.0-preview.bazel` and declare exactly
`[5.0.0-preview.bazel]` for core. UWP has 43 entries/26 satellites; WinUI has
56 entries/39 satellites. Their archive SHA-256 values are respectively
`1F518CFD8CDF3B02B8667625FBBAB4C8E3F72CA62D0584773E95F6C0504DC248` and
`17BEE8E98003EAF43C6EFB373CC1CDCEA36F748E931993F80B71F6783EF08BB4`.
Desktop/UWP/WinUI loaded ordinary core SHA-256
`D392BF09B480D19856C94B7DD47C092CA348B5B89168152D0E45FDCCE93873B1`,
with no declared target-framework/Windows metadata.

**Test-value evidence:** replacing each exact core pin with a compatible range
in owned archive copies triggered the maintained exact-pin assertion twice.
Injecting the actual UWP attribute DLL into an owned WinUI publish copy made the
unchanged `LibraryProbe` reject the other flavor (exit **-532462766**); removing
only that injected file restored exit **0** with the same core hash. These are
executed output/layout mutations, not pristine-baseline runs. The earlier
flavored-core configuration and SDK guard removals were not executed as
counterfactuals. Logs, TRX/XML, input hashes and package/probe manifests are
retained under the task-owned `artifacts\validation\native-parity-e4c16b`.

The full native closure was deliberately not rerun: the previously observed
net462 PlatformServices `CS0234` for missing `System.Reflection.Metadata` /
`PortableExecutable` references remains outside scope. Its prerequisite
attribution is static, not an executed pristine-baseline proof. CI, non-x64,
classic UAP production, real UI activation and release-branch proof remain
unavailable; no downstream enablement or backport is made.

Owned mutation layouts and bad archive copies were removed. The isolated Bazel
server (PID `92900`) shut down with exit **0**; no app registration, shared-cache
cleanup or other-worktree cleanup was performed.

Before publication, integrated SDK parent
`06ee2b01d972423dd9d4f06b8ef5c8256cda9e3c`. Its only change from the tested
parent was the SDK decision/evidence document; native/product inputs and the
native patch hash above are unchanged.
