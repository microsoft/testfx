# Explicit MSTest packages: layer 2

> **Status (2026-11-20):** the `MSTest.Uwp` and `MSTest.WinUI` package IDs
> described here were removed before shipping. Their UI framework assemblies now
> ship in `MSTest.TestFramework` and their adapter builds in
> `MSTest.TestAdapter`, as `buildTransitive` payloads. See
> [core packages only](mstest-adapter-lib-assets.md#core-packages-only-2026-11-20).
> This record is kept as history.

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

The old Extensions implementation and package-owner project are retired. Its
existing API records are byte-preserving `.txt.history` moves, outside release
promotion. A forwarding-only compatibility assembly preserves the non-UI types
already moved to core, including the upstream Playwright binary-compatibility
repair. It contains no UI definitions, resources, or selection logic. Consumers
must still update UI package/assembly references and recompile: one facade cannot
forward the same UI identity to both new packages. Ordinary and net462
resolver/configuration behavior remain unchanged. `AppModel` moves unchanged
into PlatformServices, where its actual callers already live.

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
[#12435](https://github.com/microsoft/testfx/pull/12435) head both identify
`00428d48c212cf3630dcf6fa31d131c5ae745c98`.
The carving input is
`8dd78e1347417e400c3ef8d786059a8bb346162f...refs/pr-stack/source/12435`.
Parent changes must be reconciled, not replaced with old snapshots.

Keeping the original Extensions project would preserve the hidden UI
asset-swapping package design.
Copying the full source SDK/native policy would defeat the smaller review.
The chosen boundary extracts the source's package owners and small exact-version
packing hook, retaining existing runtime and helper policy. Ordinary adapter
restore/build still expose UI variants; pack must narrow both NuGet's public
framework list and Sdk.Extras' cached flavor matrix so UI payloads and dependency
groups cannot leak.

### 2026-11-16 maintenance and compatibility follow-through

The maintenance rebase uses verified parent
`984c2c635742d3d9699f488af36d25dff48e0f85`, which includes main
`ddb33a558d5200030053e571dba7a65de476f317`. Main now requires the existing
`MSTest.TestFramework.Extensions` non-UI type forwarders: removing that assembly
would regress already-compiled `Microsoft.Playwright.MSTest.v4` consumers.
This supersedes the earlier blanket clean-break decision for non-UI types only.

The compatibility project has one dependency, on core. Core packing collects its
same-TFM DLL and XML after the repository solution's complete Build phase, with
an explicit missing-output error. The compatibility project is non-packable and
is included in the actual solution/filter inputs. There is no reverse project
reference, nested restore/build, new package, or UI asset selection in the facade.
The existing exact package-shape verifier is retained and its affected baselines
will be regenerated from the repacked products, not replaced with file counts.

| Preserved contract | Focused evidence planned for this revision |
| --- | --- |
| Forwarded non-UI identity, no duplicate implementation | Existing `PackedExtensionsForwardMovedTypesToCore` rows, adapted to ordinary core assets and checking facade type definitions |
| Already-compiled external consumers | Existing `PlaywrightSdkTests` under MTP and VSTest |
| Exact core pin, UI isolation and consumed binaries | Existing package-content and `PackedConsumersUseSharedCoreAndSelectedUiPackage` cases, plus representative real UI consumers |
| WinUI startup guard tests remain executable | Existing four `UITestMethodStartupWaitTests` cases; conditional new-owner reference and friend access only |

The original Linux pack binlog records `NU5129` with the core entry item's
`PackagePath=buildTransitive\`. The item now specifies its complete package
filename, avoiding platform-dependent trailing-directory path handling.
The original Windows unit build was reproduced: only the WinUI TFM failed with
`CS0234` for `AppContainer`. The conditional new-owner reference and matching
friend grant then built all five declared unit-test TFMs with zero warnings or
errors. Markdown `MD018` was reproduced and corrected with an explicit PR link.
These build/lint observations are not behavioral red-phase test evidence.

Azure build 1683532 at `852e5580048a8f5aa0e74c1bd362bc3de76b2265`
also exposed nine SDK evaluation rows restoring `MSTest.WinUI` on Linux, where
`NonWindowsTests.slnf` intentionally does not produce that package. Only the six
package-consuming test methods now require Windows-produced packed assets.
All nine rows and assertions remain, including the explicit Unix MSBuild
OS-property simulation executed on Windows. The console control stays
cross-platform; no synthetic UI package or SDK selection policy is introduced.
The acceptance-owner-only rebuild used `/p:BuildProjectReferences=false`, without
rebuilding or repacking product code. Independent discovery and fresh TRX
validation confirmed all nine guarded rows plus the console control executed and
passed on Windows (10/10, zero skips), including the Unix/empty-controller row.
Consumed SDK, core and WinUI package hashes match the preserved shipping packages.

The dedicated facade builds all four declared TFMs. Its binaries contain only
the `<Module>` type definition and respectively 5, 12, 6, and 6 forwarders for
netstandard2.0, net462, net10.0, and net11.0. Eight ordinary public API baseline
files were retained byte-for-byte, as were all 28 inert historical records from
the released parent. No UI definition or resource is compiled into the facade.

The 43-project actual-source solution filter passed the pinned Release repack,
including strict metadata and updated exact package-shape validation. The first
repack failed only because the old shapes still described the retired UI layout;
regenerating those intentional shapes and repacking passed without suppressions.
The four existing startup-guard tests were independently discovered and all four
executed and passed, with no skips. These commands exited zero:

```powershell
.\build.cmd -c Release -msbuildEngine vs -projects src\MSTest\TestFramework.Compatibility\TestFramework.Compatibility.csproj -bl
.\build.cmd -c Release -msbuildEngine vs -projects test\UnitTests\MSTestAdapter.PlatformServices.UnitTests\MSTestAdapter.PlatformServices.UnitTests.csproj -bl
.\eng\verify-nupkgs.ps1 -configuration Release -updateBaselines
.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer2-maintenance-pack.slnf -bl
.\build.cmd -c Release -msbuildEngine vs -projects artifacts\validation\layer2-maintenance-tests.slnf -bl
```

Final-head package consumption, execution counts, provenance, and remote CI will
be recorded once performed. The older evidence in the next section remains
historical; it does not prove the rebased facade layout.

The first committed maintenance head,
`a9406f988adb7cab787bddda02431ddfc232f948`, passed all 57 independently discovered
package/consumer/Playwright cases, including all six precompiled Playwright rows
and seven hashed ordinary/UI core consumers. Both packaged WinUI activation cases
also passed, as did real modern UWP net10/net11, unsupported deployment, and
unpackaged WinUI.

Real classic UWP exposed a distinct facade-deployment defect: all three unchanged
SDK/metapackage/direct-package cases executed and failed their layout assertion
because `MSTest.TestFramework.Extensions.dll` was missing. The classic test source
blob was `6dcd56b32e0b6b39715f13058ec669ce30878cc1`. Its binlog confirms that
`ResolvePackageAssets` supplied the netstandard facade as `RuntimeCopyLocalItems`,
but SDK-style UWP disables their default copying and the adapter's existing
explicit runtime allowlist omitted it. The initial hypothesis that the old layout
assertion should be renamed was rejected after this trace. The correction retains
the assertion and adds only the facade to that existing UWP copy list; no new
application-model or helper-copy policy is introduced.

The same three retained tests then executed and passed after the pinned Release
repack, with no skips and outer exit zero. The test blob stayed unchanged.
Production patch SHA-256:
`F68B4DAB401172220AEF0FDC59C2A328B83C5295CD15C3E365CEF26DA86B71D4`.
The pre-fix outer run exited 2 with exactly the three missing-facade layout
assertions; the fixed run exited 0 and also verified all six associated child
results in each classic app. Both variants used separate fresh marked consumer
caches and SDK `12.0.100-alpha.1.26562.108`.

Before publication, main advanced once through the Arcade update
[#12480](https://github.com/microsoft/testfx/pull/12480). The coordinator froze the
maintenance wave on main `c094c9cfc610ed31cdb4d22877ad7ce26cd881a5` and released
new shared-core parent `3af2ff923500f02dba67400a5a52f6d995d32097`. Its delta from
the first parent contains only the four Arcade metadata/script files and no
product/test source drift. The layer is replayed from the exact previous parent,
not from an inferred fork. The results above remain earlier-wave evidence;
final-new-pin pack and consumer results must be recorded separately.

Corrected-head Linux shipping/attachment CI passed. Azure build 1683240 then
exposed a separate sample-version regression: the three public UWP/WinUI samples
still pin published MSTest/SDK 4.5.3, where the extracted package IDs do not exist.
Unconditional new package references failed restore (`NU1101`), and the classic
sample's explicit new Uwp DLL path failed copy (`MSB3030`). The samples now retain
their original package references/UAP payload below major 5 and select the new
owners for 5+, including prereleases. The actual MSBuild version intrinsic was
checked with 4.5.3, 5.0.0-dev, and 5.0.0.

All three actual public samples then restored/built with their published 4.5.3
pins, warnings as errors, and binary logs (exit zero). Isolated short-path copies
of the same source also restored/built against locally packed MSTest/SDK
5.0.0-dev and aligned MTP 3.0.0-dev (exit zero). The VSTest host/object model was
aligned with the producer's `MicrosoftNETTestSdkVersion`, 18.12.0. An initial
attempt retaining the older public VSTest pin correctly failed restore with
`NU1605`; it was a mixed-version setup failure, not a product red phase.
The restored core/Uwp/WinUI/SDK package archives in the isolated cache match the
shipping producer SHA-256 values. The correction changes neither SDK
application-model policy nor package binaries.

The same Azure build's Debug incrementality step found five exact coverage-XML
assertions still referring to the retired project filename. The coverage emitter
had lost the old assembly-name mapping, so the fixture produced
`TestFramework.Extensions.dll` instead of `MSTest.TestFramework.Extensions.dll`.
The fixture now uses the real replacement `TestFramework.Compatibility.csproj`,
and its existing mapping names the facade assembly. No expected XML bytes, BOM,
line endings, regex semantics, or timestamp assertions were weakened.
With that retained migrated fixture (blob
`c070441e074aefb7aa6add325ffee8583b8f6c43`), the focused
`CoverageSettingsPreserveExactBytesAndTimestamp` case first executed and failed
on the missing assembly-name mapping. Adding the one production metadata mapping
then passed the existing complete harness: 12 executed, 12 passed, 0 failed,
0 skipped. The harness stages isolated snapshots and preserves the actual task
host/incrementality scenarios; it does not rebuild product assemblies.

CI subsequently composed the stack with main
`94711f3b4db336920e628039fdd54437458ae441` (#12490 and #12489). The coordinator
explicitly advanced the bottom to that revision rather than discarding its
restore/coverage and net462-timeout changes. The layer replay onto verified
`500f4f0c50401dd1e1945a846b3d183a379d5ddf` preserved the upstream deterministic
mapped-NuGet exclusion `^[\\/]_\d+[\\/].*` in both the production output and
exact expected XML, alongside the facade assembly-name correction. The actual
merged twelve-case incrementality harness passed all twelve cases with no
skips. All six-build-validated sample project blobs and the existing managed
product package hashes remained unchanged by that replay. These are explicit
revision/source-equivalence checks, not a claim that older CI runs tested a
later Git head.

## Historical validation and demonstration

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

The historical checkpoint below was followed by the maintenance fixes and
compatibility follow-through above. Its old results do not prove live CI execution,
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

### Maintenance alignment: 2026-11-16

The current-main binary-compatibility contract supersedes the earlier
Extensions-DLL absence checks. Verified package-layer commit
`a9406f988adb7cab787bddda02431ddfc232f948` contains the nonpackable
`TestFramework.Compatibility` project. It emits the
`MSTest.TestFramework.Extensions` facade for the ordinary core TFMs and references
core in one direction only. Core's package owns its same-TFM facade DLL/XML;
there is no separate facade package, dependency, UI implementation or UI type
forwarder. The old implementation/project/package owner remains retired.

Native parity must preserve that concrete contract rather than drop the facade
or retain Windows-flavored core/facade copies. The native core package carries
ordinary core plus its facade; modern UI packages depend on that core package.
Desktop/UWP/WinUI probes must resolve both shared assemblies from the ordinary
native producers. `TestContext` and `DeploymentItemAttribute` still resolve to
one actual core definition through the facade. UI attributes remain exclusively
in the selected UI assembly.

| Requirement | Focused evidence to rerun after the verified SDK parent arrives |
| --- | --- |
| Same-TFM facade payload owned only by core | `Assert-PackageLayout` and the real native package consumers, including restored/cached/loaded hashes for the facade. |
| Legacy non-UI names resolve to the one core definition | `NativePackageTests.PackagedTargetsProvideCoreAndParallelization` and `LibraryProbe.VerifyFrameworkApi`; reject public definitions or UI forwarding in the facade. |
| No flavored core/facade or other-UI leakage | `windows_targeting_test`, the five staged Windows consumers, and per-assembly/TFM hash comparisons. |
| First-use provenance and native net462 build | Missing/existing evidence-index regression; pinned net462 PlatformServices and adapter builds. |

Preparation reproduced the native net462 metadata-reference failure and repaired
its missing `System.Reflection.Metadata` dependency. The supplied package commit
also contains that dependency, so integration must preserve the exact net462
selection without adding a duplicate. The first-use provenance regression fails
with the old unguarded read and passes with the matching `Test-Path` guard.
These are preparation results, not final rebased-head or remote-CI evidence.
Rebase and publication remain gated on the coordinator's verified final SDK
parent. Launcher ownership, activation, cancellation and result/exit semantics
are unchanged; the facade checks are nonvisual.

The same two-case layout harness executed the actual `Assert-PackageLayout`
function before and after the adaptation. `CoreOwnsSameFrameworkFacade` fails
against the old source blob `42c5a5e6547a66ff36cc191bf083dfae9fcb46fe` because it
rejects core's facade as a retired payload; the adapted function accepts it.
`CoreRejectsMissingFacade` then verifies the precise missing same-TFM DLL error.
The old run exits 1 and the adapted run exits 0 with both cases passing. These
archives contain synthetic entry bytes: this proves the layout validator's
contract, not CLR type forwarding, real package provenance or final CI.

Interim execution at `ebc0578dab5bc5e97f8f0965fd53d718218a2cdf`, based on released
SDK parent `848e85672405fb84c0526048dd8d225bc883b4b3`, passed the maintained native
MSTest validator with ten real archives, fresh net10/net11 consumers, MSBuild
invocation and satellite removal/restoration. The same compiled
`PackagedTargetsProvideCoreAndParallelization` test also passed, failed its
explicit facade-presence assertion after removing only the DLL from an owned
publish copy, and passed after restoration: one executed case and exits 0/2/0.
Windows validation passed all five CLR probes, exact 26/5 compatibility
inventories and four additional unit suites (1,096 passed, 13 existing skips).
The full native graph and composed-product validator also passed on Windows.
These results do not claim final repaired-stack CI.

The native SDK validator exposed a stale expectation in this layer's four new
UI evaluation cases: packaged UWP/WinUI test applications correctly receive
`Microsoft.Testing.Extensions.PackagedApp.MSBuild` from the shipping SDK's
`Runner/Common.targets`. Preserve that helper and its aligned MTP version in
the expected package set rather than disabling the existing SDK behavior.
After that expectation-only correction, the native SDK/meta-package validator
passed all fourteen evaluations, five real consumer builds, default/minimal SDK
runner checks, `dotnet test`, and VSTest execution, including their expected
failure and empty-selection exits. UI evaluation remains policy coverage, not
native XAML application compilation or activation.

### Final native validation: 2026-11-16

Real package/SDK/controller validation was repeated at
`ad6ebd567cfe46e7f3f85de00ea3c8d760cecebb` on repaired SDK parent
`a84c52f49168d02ca50b6335f7c8eb2132aa2d77`, not borrowed from the interim
parent. Final implementation `abf2b526d70c2b1dd938cfd8c5b3876bb27e285a`
replays unchanged onto SDK `619c3129e23a478b480a6e401ab61fbfc662f7f2`.
The only difference between those tested and publication trees is the
unconsumed mutation catalog. Their `src` and `test` trees, native rules,
scripts, fixtures and documentation were verified identical before this
evidence-only addition. The inherited exact-identity, 26/5 inventory, net462
metadata dependency and lower OpenTelemetry environment repairs remain intact.

Bazel is `9.1.1`; its pinned SDK/runtime are
`11.0.100-rc.3.26522.148` / `11.0.0-rc.3.26522.148`. Consumer publishes run their
actual self-contained net10/net11 runtimes. Output/server and same-version
consumer feeds/caches are task-owned.

| Exact command | Observed result |
| --- | --- |
| `pwsh -NoLogo -NoProfile -File .\eng\bazel\validate-mstest-packages.ps1 -OutputRoot Q:\tfb\n12451-80f9f2\m-final -IncludeWindowsPlatformPackages -VerifySatelliteSensitivity` | Exit 0; ten real archives, fresh net10/net11 and MSBuild consumers, exact loaded/cache hashes, intended failure/empty exits and satellite removal/restoration. |
| `pwsh -NoLogo -NoProfile -File .\eng\bazel\validate-sdk-packages.ps1 -EvidenceRoot Q:\tfb\n12451-80f9f2\s-final -Jobs 2` | Exit 0; all fourteen package-policy evaluations, five consumer builds, real SDK/default/minimal and metapackage execution, native `dotnet test`, and expected negative exits. |
| `pwsh -NoLogo -NoProfile -File .\eng\bazel\windows\validate-app-model.ps1 -ArtifactsDirectory Q:\tfb\n12451-80f9f2\a-final` | Exit 0; real registration/activation, suppressed launcher, both net10/net11 controllers, discovery without handshake, failure/zero-result paths and sixteen append-only provenance records. The owned registration was removed. |
| `.\eng\bazel.ps1 test //:sdk_package_tests //:build_tools_tests //:windows_targeting_test //:windows_asset_selection_test --jobs=2 --nocache_test_results --test_output=errors --noshow_progress --show_result=0` | Exit 0; 15 SDK cases and 13 build-tool cases executed/passed with no skips, plus both executed analysis cases. |
| `python .github\scripts\test_mutation_testing.py` and `python .github\scripts\mutation_testing.py matrix --manifest eng\mutation-testing\modules.json --repository-root .` | Both exit 0; 37 catalog tests, 43 source projects = 32 enabled modules + 11 explicit exclusions. |
| `markdownlint-cli2 docs\decisions\explicit-mstest-packages.md` | Exit 0; the touched record passes the existing repository rules. |

The facade shipping-layout experiment was repeated against the final freshly
published net11 consumer, keeping its compiled test DLL unchanged:
`PackagedTargetsProvideCoreAndParallelization` executes once and passes with
the facade, fails its explicit facade-presence assertion after deleting only
that DLL from an owned copy (exit 2), and passes after restoration. The paired
TRX files and input hashes are retained. The first-use provenance guard also
passes both missing/existing-index cases; its old unguarded source fails the
missing-index case. These are controlled input/layout experiments, not claims
that a pristine parent or a particular CI thread interleaving was executed.

Final raw logs/binlogs/TRX, package and loaded-bit manifests are retained under
the task-owned output root. The app-model validator's existing null revision
field is supplemented by a source-state record and matching captured input
hashes. Evidence paths are local, not public artifact URLs. Native classic UAP,
MSTest XAML/UI-thread application activation, other architectures and release
branches remain unclaimed; the real activation above uses the native console
app-model host. No rollout/backport or merge action is performed.

### Native adapter package support boundary: 2026-11-16

Review on `204d4850de60b2b2fbc28a4a1934ad73f2b2c673` identified an accidental
Windows-only `net462` adapter package group. The native adapter's first-party
dependency packages expose only `net10.0` and `net11.0` runtime assets.
A real `net462` SDK-style consumer of native `MSTest.TestAdapter`
`5.0.0-preview.bazel` fails restore with exit 1 / `NU1202` at native
`Microsoft.Testing.Extensions.TrxReport.Abstractions`
`3.0.0-preview.bazel`; this is not a net10 proxy or a legacy runtime failure.
The physical nuspecs confirm the same incompatible boundary for native
Telemetry, Platform.MSBuild and Platform. Four resolved native cache archives
match the captured producer hashes; the consumer has a fresh owned cache,
explicit source mapping and no fallback package folders.

The chosen contract restores the existing portable-only adapter archive:
`net10.0` and `net11.0`, on every producer OS. Remove the misleading legacy
dependency group and payload rather than extending all native MTP packages to
.NET Framework. A full legacy runtime package closure was not designed or
validated here. Keep the existing native net462 compile labels and the
independently consumable ordinary core/facade package unchanged. This does not
change official MSBuild-produced packages or claim native legacy adapter support.

The maintained package validator requires exactly the two supported adapter
groups and rejects legacy adapter build payloads. The same assertion rejects
the original real archive and passes the freshly repacked archive. A separate
layout mutation removes only the original nuspec's net462 group while retaining
its real payload; the explicit legacy-payload assertion rejects that too.
Each layout probe executes once with zero skips, using the same validator bytes.

The post-fix legacy adapter restore has an important NuGet distinction:
build-only packages without a `lib` folder restore successfully when no framework
group matches, but contribute no assets. The actual net462 restore exits 0 and
selects only the adapter identity, with no dependencies, build, compile or runtime
assets. It does **not** enable or validate a native legacy adapter runtime.
The initial postprocessing expectation of a direct adapter `NU1202` was wrong;
the real assets were inspected rather than inventing that diagnostic. Adding
marker libraries or unsupported-framework guard assets solely to force it was
not necessary for the existing portable-only archive contract.

The executed reproduction uses the pinned native SDK
`11.0.100-rc.3.26522.148`, working directory
`Q:\tfb\n12451-80f9f2\net462-review-4587032669\red\consumer`:
`dotnet restore LegacyConsumer.csproj --configfile NuGet.config --packages
Q:\tfb\n12451-80f9f2\net462-review-4587032669\red\confirmed-packages
--disable-build-servers
-bl:Q:\tfb\n12451-80f9f2\net462-review-4587032669\red\restore-confirmed.binlog
--nologo -v:minimal` returned 1. Its source/layout identity, original package
hashes, exact command, NU1202 diagnostic and effective net462 assets/cache are
retained in `restore-evidence.json`, `producer-packages.json`, the copied
nuspecs and restore log beside that binlog.

The implementation was validated as base
`204d4850de60b2b2fbc28a4a1934ad73f2b2c673` plus the two-file native rules/validator
patch SHA-256
`86A8AA8C30892EF6C97777D863F8044C4E34F7BBBF9A9632F723C7E5B2BEAA0D`.
The original adapter archive is
`6129E376D454AC01B0EF15E1F6BCE854504AD9F558B6E8875990BA1B4991E50D`;
the corrected archive is
`9652B932323E13E15B4C44B622A07FF642679268D5F59505824E697299A2807D`.
The other nine archive hashes, including core and both UI packages, are unchanged.

| Requirement / exact check | Executed evidence |
| --- | --- |
| No misleading legacy adapter group / `NativeAdapterPortableLayout` | Actual maintained function fails on the original archive, exit 1, reporting three groups instead of net10/net11; the same function passes the repacked archive, exit 0. |
| No unadvertised legacy payload / `NativeAdapterRejectsLegacyPayloadWithoutLegacyGroup` | Owned original-archive mutation retains real legacy bytes but removes the legacy nuspec group. Exit 1 at the explicit legacy-payload assertion; no setup/compiler failure is counted. |
| Real supported package consumers | `pwsh -NoLogo -NoProfile -File .\eng\bazel\validate-mstest-packages.ps1 -OutputRoot Q:\tfb\n12451-80f9f2\net462-review-4587032669\modern -IncludeWindowsPlatformPackages -VerifySatelliteSensitivity`: exit 0; ten archives, real net10/net11 positive/failure/empty cases, MSBuild parent exits 0/1 and satellite removal/restoration, with new cache/loaded hashes. |
| SDK composition survives the archive boundary | `pwsh -NoLogo -NoProfile -File .\eng\bazel\validate-sdk-packages.ps1 -EvidenceRoot Q:\tfb\n12451-80f9f2\net462-review-4587032669\sdk -Jobs 2`: exit 0; fourteen evaluations, five builds, actual SDK/metapackage runs and native `dotnet test`. |
| Compile labels, Windows CLR ownership and 26/5 inventories remain intact | `pwsh -NoLogo -NoProfile -File .\eng\bazel\windows\validate.ps1 -ArtifactsDirectory Q:\tfb\n12451-80f9f2\net462-review-4587032669\windows`: exit 0; five real CLR probes and four additional unit suites. No registration or XAML activation is claimed. |
| Independently usable native net462 core/facade | Pinned native SDK builds `green\core-consumer\CoreConsumer.csproj` in Release, exit 0; its actual `.NETFramework,Version=v4.6.2` executable exits 0 on CLR `4.0.30319.42000`. Both forwarded identities and archive/cache/loaded core/facade hashes match. |
| Unsupported legacy adapter is not misreported as runtime coverage | `green\legacy-consumer\LegacyConsumer.csproj` restore exits 0 with only the package identity and no selected assets. This is recorded as NuGet's compatible-empty build-package behavior, not a passing legacy adapter scenario. |

These runs use distinct task-owned feeds/caches and retain commands, binlogs,
TRX, physical nuspecs, manifests and source identities under
`Q:\tfb\n12451-80f9f2\net462-review-4587032669`. The core-only executable and
unsupported-restore records are `green\core-evidence.json` and
`green\legacy-restore-evidence.json`; they include the exact native SDK path,
commands, effective target/cache and loaded paths. The old-archive and mutation
probes are layout evidence, not legacy test execution. No assembly scheduling,
production lifecycle, package versions, official package policy or lower-layer
source changed.

### UI package isolation guard: 2026-11-16

Review on `bb5012718fdeb0d82bdc9eb2cd29fe106bc4e9b7` identified a validator
defect, not a new archive-layout requirement. PowerShell evaluates `-and` and
`-or` at equal precedence, left to right. The combined predicate therefore
allowed a bundled core DLL in either UI archive and a foreign UWP DLL in the
WinUI archive. Parenthesize each flavor-specific conjunction so the existing
shared-core and cross-flavor requirements remain independent.

Six fixed-input probes use the real native UWP/WinUI archives: two valid controls,
core DLL insertion into each archive, and both cross-flavor DLL insertions.
The four mutations retain each original nuspec and add only one real DLL copied
from the native producer. On the original maintained function, all six execute;
three pass and three fail the expected-rejection assertion, exit 1, with no skips.
`CoreInUwp`, `CoreInWinUI` and `UwpInWinUI` are incorrectly accepted. The reverse
cross-flavor mutation is already rejected and remains a control.

With explicit conjunction parentheses, the same six archives, assertions and
selection execute and pass, exit 0 with zero skips. Each input archive hash is
identical across the pair. The validator source hashes are
`A5D6E4C6332A26F1DDFC9FE8C9EB7384173FF73EC9067D675299A3B84DE3CE1A` before and
`62334CD8DFF849B02AEE69C9C1C940FB324DD25B331361ED89F206E963E68ACA` after.
The one-file validator patch against the recorded base has SHA-256
`54674D28C4E71C7CAAA87E4FEB35362104718EDA2E522CAFC0204799D025C03F`.

`pwsh -NoLogo -NoProfile -File .\eng\bazel\validate-mstest-packages.ps1
-OutputRoot Q:\tfb\n12451-80f9f2\ui-isolation-review-4588012054\modern
-IncludeWindowsPlatformPackages -VerifySatelliteSensitivity` also returns 0:
ten real archives, fresh net10/net11 positive/failure/empty consumers, MSBuild
parent exits 0/1 and satellite removal/restoration. All ten archive hashes are
unchanged from the preceding adapter-boundary validation, so the existing SDK,
CLR and core-only net462 proofs remain product-equivalent rather than rerun claims.
No compiled product, package version, dependency closure, lifecycle or lower-layer
source changed.

The fixed-input harness is retained as `regression.ps1`, with `cases.json`,
`before.json`/`after.json`, logs and `evidence-summary.json`, under
`Q:\tfb\n12451-80f9f2\ui-isolation-review-4588012054`. It AST-loads the actual
maintained validator functions, not a copied predicate. These are executed
archive-rejection assertions; the normal package gate separately executes the
real native tests. The bounded mixed-operator check of the layer's other changed
PowerShell validators found only explicitly parenthesized conjunctions.

### Main integration: shared modern UI adapter: 2026-11-19

Merging main `d634b2fe38b8572045c7c648789f4fb1fe14f84e` into PR head
`db7e22ce6241abb7f7922a1792e06390c5535d02` (merge base
`28a041287150a48ad48134672c472a481fecf492`) brought two package-contract changes.
The #12515 ordinary adapter layout under `lib/{tfm}` auto-merged into the native
rule and validator; the only textual conflict was its satellite-directory comment
in `mstest-packages.bzl`. The #12516 shared modern UI adapter
([shared-modern-uwp-winui-adapter.md](shared-modern-uwp-winui-adapter.md)) changed
no Bazel input, so main's native graph still builds a separate 17763 UWP adapter.
Keeping that would make native `MSTest.Uwp` diverge from the MSBuild package.

Native modern UWP therefore follows MSBuild: `mstest_uwp` and its package target
`net10.0-windows10.0.18362.0`, and UWP reuses `adapter_winui` and
`platform_services_winui` instead of building a `WINDOWS_UWP` adapter that links
the classic-only `CommandLineOptionParser`. The targeting test owns both UI flavors'
adapter/services through the WinUI targets and rejects a 17763 modern UWP consumer
against the 18362 payload. The Windows probe and native package validators now also
require identical UWP and WinUI adapter/services bytes. The `windows_sdk_17763`
projection and its paket group remain declared but unused by MSTest libraries.
Removing them would require regenerating the shared lock, so they are left alone.
The earlier 17763 UWP archive layout and hash in this record are superseded.

| Requirement | Command / executed evidence |
| --- | --- |
| Native build | `eng\bazel.ps1 build //:mstest_uwp_package //:mstest_winui_package //:windows_libraries //:windows_probes --jobs=2 --noshow_progress`: exit **0**. |
| Targeting/assets | `eng\bazel.ps1 test //:windows_targeting_test //:windows_asset_selection_test --jobs=2 --noshow_progress`: exit **0**, 2 of 2 passed. |
| SDK/meta-package contracts | `eng\bazel.ps1 test //:sdk_package_tests --jobs=2 --noshow_progress --test_output=errors`: exit **0**. |
| Windows consumers | `eng\bazel\windows\validate.ps1 -ArtifactsDirectory %TEMP%\pr12451-windows-validation`: exit **0**; five probes, including 18362 UWP and the shared-adapter hash check. |
| Native feed | `eng\bazel\validate-mstest-packages.ps1 -IncludeWindowsPlatformPackages -OutputRoot %TEMP%\pr12451-package-validation`: exit **0**; ten archives, net10/net11 pass/failure/empty consumers (0/2/9) and MSBuild parent (0/1). |

UWP has 43 entries/26 satellites and WinUI 56 entries/39 satellites, all under
`net10.0-windows10.0.18362`. Archive SHA-256 values are
`0AA2CEC87DF5F360DB6D4FB83B50DBC0F31A2C32CEE938E2448E0A4E9B969260` (UWP) and
`74AF5E5A3CD501DC199B97E461C3F893DFC331F482931EBB5FBC62FEAED958D8` (WinUI). Both
ship `MSTest.TestAdapter.dll`
`E6E288D1F755D61612B36D1F4F7FD7FC7129299F2E07A7EA2556D435D041E5C2` and
`MSTestAdapter.PlatformServices.dll`
`9A6DE6F51D52F0BCA928ACE14CA9BC490EBF992C1BCC8309210CC75FFD4594D3`.

**Test-value evidence:** restoring only the UWP-owned adapter wiring in a temporary
`windows.bzl` copy made the unchanged `windows_targeting_test` execute and fail,
exit **3**, on exactly the four UWP probe compile/runtime ownership cases for
`MSTest.TestAdapter` and `MSTestAdapter.PlatformServices`. Restoring the file
byte-for-byte returned exit **0**. The shared-hash validator branches were not
mutation-tested. CI, classic UAP production, real UI activation and
release-branch validation remain unavailable.
