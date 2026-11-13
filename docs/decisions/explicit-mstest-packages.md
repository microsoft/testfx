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
