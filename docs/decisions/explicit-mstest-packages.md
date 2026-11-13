# Explicit MSTest packages: layer 2

## Problem and contract

This is the package-extraction layer replacing
[#12435](https://github.com/microsoft/testfx/pull/12435), following
[#7225](https://github.com/microsoft/testfx/issues/7225),
[#12412](https://github.com/microsoft/testfx/pull/12412) and
[#12428](https://github.com/microsoft/testfx/pull/12428). The
[shared-core prerequisite](shared-core-ui-contract.md) owns the runtime changes;
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

Validation uses this worktree's Release outputs, the pinned repository SDK and
desktop MSBuild. The populated producer cache is input, never cleared or copied;
same-version consumers use fresh task-owned caches. Pack precedes consumers.
Record exact commands/exits, source and package hashes, effective asset paths,
TRX identities/counts and any unavailable checks below as experiments complete.
Historical results from the unsplit source are not evidence for this layer.

The acceptance assemblies retain method-level, CPU-count parallelization.
Generated consumers use per-invocation assets and isolated cache state; existing
class exclusions for shared mutable app assets remain unchanged. The internal
TestContainer engine is sequential. No scheduling declarations change.

Package ownership is nonvisual; an archive/consumer transcript is more useful
than a staged screenshot. Actual UI activation remains a separate validation
contract, not something a correct package-content test establishes.

## Open questions

The first pinned-script invocation did not start because the bounded PATH omitted
Windows PowerShell; correcting that command-local path fixed setup. The clean
baseline build then passed all four declared framework-test TFMs, and
`WinUIExtensionTests.UnpackagedProcessHasNoPackageIdentityOrPath` executed
**1 passed, 0 failed, 0 skipped** before relocation.

The 36-project actual-source pack filter contains the ten MSTest package owners,
their reference closure, the pack-time global-config generator, and packages
unconditionally required by the existing acceptance fixture. The command
`.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer2-pack.slnf -bl`
exited **0**, with **0 warnings and 0 errors**, including the package content and
metadata verifier. Core built four TFMs, each adapter/service still built all six
Windows variants, and both classic/modern UWP plus WinUI package owners built.

The native adaptation is separate from the substantive package extraction:
16 existing source/layout/probe files and one matching unit-test fixture lose or
replace obsolete owner references. Existing labels, target matrices, rule
machinery, Hosting behavior and append-only app-model artifacts are retained.
No native UI package production, range machinery or expanded validation is added.

Consumer execution, regression sensitivity and CI evidence are pending. The known native
net462 PlatformServices System.Reflection.Metadata baseline failure is outside
this layer and must not be repaired here.

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
