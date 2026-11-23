# MSTest adapter library assets

> **Status (2026-11-20):** the ordinary `MSTest.TestAdapter` package (`net462`,
> `net10.0`, `net11.0`) ships standard `lib/<tfm>` assets. See
> [Shipping implementation: ordinary adapter lib assets](#shipping-implementation-ordinary-adapter-lib-assets).
> The unshipped `MSTest.Uwp` and `MSTest.WinUI` packages
> ([#12450](https://github.com/microsoft/testfx/pull/12450)) are removed again:
> the UI framework assemblies ship in `MSTest.TestFramework` and the UI adapter
> builds in `MSTest.TestAdapter`, both as `buildTransitive` payloads selected
> from the project's UWP or WinUI properties. See
> [core packages only](#core-packages-only-2026-11-20). The other sections
> record earlier experiments and designs and are kept as history.

The November 12-13 sections preserve the earlier single-package experiments.
Their package-split exclusion and toolchain limitations describe those runs,
not the current proposal. The [November 18 design](#three-package-families-design-2026-11-18)
investigates separate component packages and compatibility metapackages, without
changing shipping defaults.

## Problem and contract

Investigate replacing `MSTest.TestAdapter`'s manually injected assemblies with
standard NuGet `lib/<tfm>` assets. The consumer must retain the correct adapter,
platform services, dependency resolution, localized resources, and test results.
This started as a local prototype, not an approved release or package compatibility break.

The affected consumers are ordinary VSTest and Microsoft.Testing.Platform tests,
classic UWP (`uap10.0`), modern UWP (`UseUwpTools=true`), and WinUI. Modern UWP
and WinUI cannot be distinguished solely by their consumer's Windows TFM.
Existing .NET Framework adapter-adjacent framework copies address VSTest resolver
compatibility and must not be removed without equivalent consumer evidence.

The existing classic and modern UWP samples should be reusable for the
comparison. No public API, package split, machine-wide resource policy, or
application lifecycle change is intended. Package activation and cleanup must
remain owned by each sample run; shared registrations and caches are not ours.

## Research and experiments

### 2026-11-12: source and history

Source base: `29f8046dad20ec4d4cef871d63fb4d2000449c33`.

- [#7389](https://github.com/microsoft/testfx/pull/7389) made adapter assembly
  references unconditional, improving dependency resolution and binding redirects.
  Its rationale explicitly identifies a future `lib` layout.
- [#4537](https://github.com/microsoft/testfx/pull/4537) and
  [#6625](https://github.com/microsoft/testfx/pull/6625) explain the .NET Framework
  framework-sidecar copies required by VSTest's resolver.
- [#12134](https://github.com/microsoft/testfx/pull/12134) retains WinUI satellites
  while excluding the adapter's content items from PRI indexing.
- Windows SDK MSIX targets expose `FilterSatelliteAssembliesForMakePri`, but the
  inspected implementation filters the main-package/bundle PRI layout. It has not
  been established as a replacement for project-level per-item exclusion.
- `AppxGeneratePrisForPortableLibrariesEnabled=false` already exists in the
  classic UWP sample. This is an application-wide resource-conversion decision,
  not a safe default to impose on all consumers of the adapter.

### 2026-11-12: local execution preflight

The repository's application-model preflight exited 1 while handling an absent
UWP-capable Visual Studio instance (`installationPath` was missing). A direct
`vswhere` query requiring `Microsoft.VisualStudio.Workload.Universal` and
`Microsoft.VisualStudio.ComponentGroup.UWP.Support` returned an empty array.
The current process is interactive but not elevated. This does not establish
native UWP build, activation, or localization compatibility.

## Alternatives and decision

- Moving everything to `tools` would retain the same manual-reference model and
  is not the objective.
- Standard `lib` assets are the candidate for normal compile/runtime consumption.
  Windows application-model selection and the VSTest sidecar requirements must
  be addressed rather than hidden by a directory rename.
- Splitting packages or changing supported application models is outside this
  experiment's scope.

Do not present a candidate layout as release-ready without actual package and
consumer evidence. Preserve remaining compatibility questions explicitly if
the local toolchain prevents the comparison.

**Experiment outcome:** retain the shipping layout until native validation, but
do not reject `lib` because the first pure-TFM attempt failed. The follow-up
selection-only prototype below corrects model selection through build, publish
and dependency manifests while retaining NuGet asset filtering. Standard
resource assets also avoid the project-level `Content` classification that
caused the original PRI issue. The delivered source changes prepare samples and
regression guards; no shipping package migration had been applied at that point
(superseded by the shipping implementation below, once the UI payloads moved to
their own packages).

### 2026-11-12: isolated package-only prototype

The baseline adapter was compiled with the repository-pinned
`12.0.100-alpha.1.26556.115` SDK, Release, with
`/p:SkipUapTargetFramework=true`. Its package is `MSTest.TestAdapter.5.0.0-dev`;
aligned MTP dependencies are `3.0.0-dev`. Classic UWP is therefore absent from
both prototype package variants.

The trial was deliberately kept out of shipping source:

1. Copy the baseline package payload into a session-owned staging directory.
2. Move ordinary DLLs to `lib/<tfm>`, and the Windows variants to
   `lib/net10.0-windows10.0.17763.0` and
   `lib/net10.0-windows10.0.18362.0`.
3. Place the existing shared adapter satellites beside each framework's library
   assets. Remove the trial package's manual adapter references and satellite
   content-copy targets, retaining its registration and parallelization hooks.
4. Repack with the pinned SDK and restore identical consumers into separate
   baseline/candidate caches. Compiled assembly bytes remain unchanged.

| Artifact | SHA-256 |
| --- | --- |
| Baseline adapter package | `12901D25FB2695B95D2CA61C7D8E1F0966B7F7CB281AFA698EBB036D06FD51CD` |
| Trial adapter package | `8072F022105D25FA2B7BA444B5BFAC1A64A0AE46E802C148B66427030B1E9161` |
| Trial pack manifest | `C1392293C272A82FBD28B3D60BEF6CF7876F74A6F25C05A617C9CE8041C12D3C` |

The trial contains no DLLs under `buildTransitive`. For ordinary `net10.0`
consumers, restore records both adapter assemblies as `lib` compile assets,
whereas the baseline has no adapter compile assets and adds its references
through targets. Actual output DLL hashes match the respective package entries
in both variants.

**Observed compatibility problem:** resolving references for an otherwise
identical `net10.0-windows10.0.26100.0` consumer with `UseUwpTools=true` selects:

| Variant | Resolved adapter |
| --- | --- |
| Baseline | `buildTransitive/net10.0/uwp/MSTest.TestAdapter.dll` |
| Pure-TFM trial | `lib/net10.0-windows10.0.18362.0/MSTest.TestAdapter.dll` (WinUI) |

Both restores and reference-resolution commands succeeded. An independent
application-model assertion then failed for the trial. This is executed
asset-selection evidence, not native UWP activation evidence. The pure-TFM
layout is not safe to promote: NuGet selects by platform-version compatibility,
not the `UseUwpTools` application model.

### 2026-11-12: satellite and PRI boundaries

Under an identical French build UI language, baseline and trial consumers pass
the same three cases under native MTP and VSTest; MTP published output passes
too. These six runs execute 18 cases, all passed, none skipped.

The VSTest output nevertheless changes: the baseline contains one adapter
satellite culture (`fr`), whereas the trial contains all 13. MTP contains all 13
in both variants. An earlier English-build VSTest baseline run executed all
three cases but failed the French adapter-string assertion; the same
English-build trial passes. This confirms a real language-availability change,
not a claim that the current VSTest behavior is defective.

The real Windows SDK `WinAppSdkGeneratePriConfigurationFiles` task was also
executed on the two actual French adapter satellites, with
`FilterSatelliteAssembliesForMakePri=true` in both arms:

| Per-item metadata | Project PRI indexed inputs | Project PRI excluded inputs |
| --- | ---: | ---: |
| `ExcludeFromResourceIndex=false` | 2 | 0 |
| `ExcludeFromResourceIndex=true` | 0 | 2 |

The project-input helper does not use the bundle-filter property. Consequently
that property is not a drop-in replacement for the existing satellite `Content`
metadata. This does not prove that automatic `lib` resource assets require the
same metadata; their complete UWP packaging path remains untested.

The standalone bundle helper was not validated: its .NET host lacked
`System.Security.Permissions`, and the desktop-host attempt rejected the
incomplete standalone package-layout inputs. Those infrastructure/setup
failures are not product regressions or evidence that the property is broken.

### 2026-11-12: additional solutions and selection-only prototype

The distinction is not "all NuGet" versus "all hand-authored references".
Ordinary assembly/resource consumption can use standard `lib` assets while a
small build target handles the application-model distinction NuGet cannot
express.

A second isolated package retains the first trial's library layout and adds a
selection-only target. It replaces only the adapter package's existing
`ResolvedCompileFileDefinitions`, `RuntimeCopyLocalItems` and
`ResourceCopyLocalItems` with the corresponding ordinary/UWP/WinUI library paths.
It preserves their NuGet identity and asset metadata and updates `HintPath` and
`PathInPackage`. A publish hook also keeps freshly computed publish assets
aligned. It does not inject `Reference` or satellite `Content` items, change
global language/resource policy, or resurrect an asset excluded by the consumer.

The target is experimental: these SDK item/target names are implementation
coupling, not a new public NuGet selection facility.

| Follow-up artifact | SHA-256 |
| --- | --- |
| Model-aware adapter package | `8B594E80289F908F30D5844A3F4CAEAE47B5BEEF136AF03D3668D81BC2AF8290` |
| Model-aware pack manifest | `EBEC4C4F2CD9A45DD9673F55411544E8F9A1EE6F08A06568B18492D7E42E4163` |
| Packaged selection helper, verified against retained source | `D5B19E9064A67E8C18FCA7B7D6B9ADA95380C4B147DA2417AE228243A8591024` |

The actual restored package matches the feed hash and contains no DLLs under
`buildTransitive`. Seven managed smoke consumers each build and publish
successfully. Their selected paths, actual copied main/satellite bytes, and both
build/publish `.deps.json` asset paths are asserted against the intended model:

| Probe case | Expected adapter variant | Compile / runtime DLLs | Adapter resource DLLs |
| --- | --- | ---: | ---: |
| `ordinary-windows` | Ordinary `net10.0` despite a recent Windows TFM | 2 / 2 | 26 |
| `modern-uwp` | UWP `net10.0-windows10.0.17763.0` | 2 / 2 | 26 |
| `winui` | WinUI `net10.0-windows10.0.18362.0` | 2 / 2 | 26 |
| `modern-uwp-net11` | UWP specialization, matching existing selection policy | 2 / 2 | 26 |
| `uwp-exclude-compile` | UWP; `ExcludeAssets=compile` respected | 0 / 2 | 26 |
| `uwp-exclude-runtime` | UWP; `ExcludeAssets=runtime` respected | 2 / 0 | 0 |
| `uwp-french-only` | UWP; explicit `SatelliteResourceLanguages=fr` respected | 2 / 2 | 2 |

All seven have **zero adapter satellite `Content` items**. These are managed
build/publish and file/manifest checks, not native application execution or
seven executed MSTest cases.

The same probe script, assertions and consumer inputs were also run against the
pure-TFM package without the selector, in a separate cache. All seven builds and
publishes still succeeded, but six scenarios failed the intended model-path,
output-byte or dependency-manifest assertions; the WinUI scenario already
selected its intended variant and remained valid. This is executed
counterfactual evidence for the selector, not a restore/compile failure.

The initial helper's root placement produced NU5129. Putting it beside each
framework's actual entry target removed the warning without suppression.
Early probe-harness attempts also needed explicit handling of optional metadata
and empty asset arrays; the final seven-case run exited 0 with all assertions.

Exact final probe command, from the repository root, with `$solutions` pointing
at the session's `files/adapter-solutions`:

```powershell
& "$solutions\Probe-ModelSelection.ps1" -Repository (Get-Location).Path `
    -Experiment "$solutions\model-aware-v2" -RunName final
# Exit 0: seven probes, fourteen successful build/publish commands.

& "$solutions\Probe-ModelSelection.ps1" -Repository (Get-Location).Path `
    -Experiment "$solutions\pure-lib-control" -RunName control
# Exit 1: intended assertion failures, not infrastructure errors.
```

### Why the satellite workaround may disappear

The follow-up supplied the real selected runtime/resource metadata to the
Windows SDK bundle helper, using its desktop task host. With the actual
`NuGetSourceType`, `PathInPackage`, culture and target-path metadata, it succeeded:
two main DLLs remain in the indexed list and both French satellite DLLs go into
the excluded list. The earlier hand-constructed inputs were incomplete.

This complements, rather than contradicts, the earlier project-PRI result:

- The project PRI input collection examines content/custom output groups.
  The baseline manually adds satellite DLLs as `Content`, so that phase requires
  their per-item exclusion.
- Standard NuGet resources enter `ReferenceCopyLocalPaths`; the Windows SDK's
  `CopyLocalFilesOutputGroup` carries them as copy-local package files, not
  project content.
- The main-package/bundle helper can recognize and filter those package
  satellites when the normal NuGet metadata survives.

This is a much stronger basis for trying normal library satellites than merely
setting a global switch on the existing `Content` workaround. It is still not
proof of full UWP packaging, resource-pack splitting, native activation or
localized runtime lookup.

Modern UWP and classic UAP must be assessed separately. The Windows SDK also has
a managed-library resource-to-PRI conversion phase; the classic sample currently
disables it. Do not remove that setting, enable conversion everywhere, or disable
application PRI generation based only on the modern managed probes.

### Ranked alternatives

| Approach | Benefit | Cost / remaining contract | Assessment |
| --- | --- | --- | --- |
| Standard `lib` plus a narrow model-selection target | Removes universal manual references and satellite copying; correct compile/runtime/publish/deps selection; respects consumer asset exclusions. | SDK implementation coupling; native classic/modern UWP, WinUI and special publish modes still need real consumers. | Most credible near-term path; seven probes and the counterfactual executed above. |
| Separate ordinary/UWP/WinUI adapter packages | NuGet itself can select assets within the explicitly chosen model package, without rewriting resolved items. | New package IDs and direct-reference migration. MSTest.Sdk can select the package at restore evaluation, but a metapackage cannot change its dependency graph based on arbitrary consumer properties. Preserve a compatibility path for existing consumers. | Cleaner long-term design; not implemented or exercised here. |
| Unify the adapter/platform-services binaries and extract app-model behavior | Eliminates same-identity variant selection at its root. | Not a packaging-only refactor: source has different package/file loading, assembly resolution, deployment, telemetry and discovery behavior across these models. Requires explicit stable seams and runtime validation. | Larger architectural project, not the next workaround. |
| Retain only the legacy UAP/netfx exceptions during migration | Keeps modern consumers on normal NuGet assets without requiring every legacy behavior to be redesigned at once. | Does not fully remove the old layout while those exceptions remain; scope and retirement must be explicit. | Reasonable staged fallback if native validation finds a real legacy constraint. |
| Distinguish variants using RIDs | Would use NuGet's native runtime selection. | Modern UWP and WinUI both use `win-x64`; an app model is not a CPU/OS runtime identifier. Inventing an application-model RID would change the consumer contract. | Not a sound workaround. |
| Move the whole payload to `tools` or disable all application PRI | Minimal immediate authoring changes. | Retains the original bypass, or breaks unrelated application resources. | Rejected. |

The VSTest increase from one satellite culture to thirteen is a policy and
output-size change, not evidence that VSTest cannot use `lib`. A deliberate move
to standard NuGet language handling can use the existing
`SatelliteResourceLanguages` control rather than reproducing the build-machine
culture heuristic. That public control is consumer-owned and project-wide;
the adapter must not silently set it for unrelated packages.

Similarly, the new UWP fallback guards deliberately describe the baseline.
If native validation proves standard satellites work, a reviewed change can
replace those fallback expectations with positive translation assertions.
Preserving an obsolete workaround is not the ultimate requirement.

The measured development-package sizes are 2,471,525 bytes for the baseline,
3,235,058 for pure `lib`, and 3,239,404 for model-aware `lib`. Most of the increase
comes from placing satellites beside each framework instead of sharing one
nonstandard localization directory. No build-time/performance benefit is claimed.

The next decisive evidence is a real native resource/activation comparison,
followed by legacy VSTest/netfx and any supported special publish paths. At the
time, these follow-up findings revised the recommendation, not the shipping
source; the legacy VSTest/netfx and publish comparisons were later run for the
ordinary package in the shipping implementation section.

## Validation and demonstration

| Requirement | Automated evidence | Native execution gap |
| --- | --- | --- |
| Classic UWP uses current shipping packages and correct assemblies | Extended `ClassicUwp_ConsumesUapAssets_AndRunsPlainAndUiTestsThroughMtp` and `samples/public/ClassicUwpMtpApp` with four resource cases and strict six-result child TRX assertions. | UWP workload unavailable locally; prototype package omits classic UWP. |
| Modern UWP selects UWP rather than WinUI assets on a recent Windows TFM | Extended `ModernUwp_ConsumesUwpAssets_AndRunsPlainAndUiTestsThroughMtp` and `samples/public/UwpMtpApp`; the pure-TFM probe detects the wrong DLL, and the selection-only follow-up fixes managed build/publish/deps selection. | Native UWP build/activation still unavailable. |
| Localization remains present and PRI policy does not remove application resources | `BothAdapterFrenchTranslationsRemainLoadable` and `ApplicationFrenchResourcesRemainLoadable` execute under both runners; real project-PRI helper checks input inclusion/exclusion. | No full UWP package/layout/localized-runtime comparison. |
| Ordinary consumers retain discovery and test results | `AdapterAndPlatformServicesAreLoadedFromOutput` plus both resource cases: baseline and trial, MTP build/publish and VSTest, 18/18 passed. | This is `net10.0`, not a .NET Framework or Windows application-model run. |

### Commands and outcomes

Commands ran from the worktree root. `$scratch` below denotes the session-owned
`files/adapter-tests` artifact directory; these experiment helpers are not
shipping repository tools. Exact binlogs, generated projects, package manifests,
TRX and helper sources are retained with the session. The tables above preserve
the decisive results without requiring access to those local files.

| Command | Outcome |
| --- | --- |
| `.\build.cmd -pack -c Release -bl -projects src\MSTest\MSTest.TestAdapter\MSTest.TestAdapter.csproj /p:SkipUapTargetFramework=true` | Exit 1: compilation/pack produced the adapter, but the repository-wide verifier rejects missing unrelated packages and the omitted classic-UWP payload. |
| `.\.dotnet\dotnet.exe pack src\MSTest\MSTest.TestAdapter\MSTest.TestAdapter.csproj -c Release --no-build /p:SkipUapTargetFramework=true -bl:artifacts\log\Release\adapter-baseline-pack-only.binlog --verbosity quiet` | Exit 0; baseline package above. |
| `.\.dotnet\dotnet.exe pack "$scratch\lib-prototype\Prototype.csproj" -c Release -o "$scratch\lib-prototype\candidate-feed" "-bl:$scratch\lib-prototype\candidate-pack.binlog" --verbosity quiet` | Exit 0; isolated trial package above. |
| `& "$scratch\Invoke-LibConsumers.ps1" -Repository (Get-Location).Path -Experiment "$scratch\lib-prototype" -RunName french-ui -BuildLanguage fr` | Exit 0; four builds, four discoveries, six executions, 18/18 passed. |
| `& "$scratch\Invoke-LibConsumers.ps1" -Repository (Get-Location).Path -Experiment "$scratch\lib-prototype" -Variants candidate -Runners vstest -RunName english-ui -BuildLanguage en-US` | Exit 0; 3/3 passed, contrasted with the earlier English-build baseline's 2 passed/1 French-resource assertion failure. |
| `& "$scratch\Probe-WindowsAssets.ps1" -Repository (Get-Location).Path -Experiment "$scratch\lib-prototype"` | Exit 1 at the intended application-model assertion; both reference-resolution commands themselves exited 0. |
| `.\.dotnet\dotnet.exe build test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release /p:SkipUapTargetFramework=true -bl:artifacts\log\Release\uwp-sample-guards.binlog --verbosity quiet` | Exit 0; actual acceptance harness compiled, zero warnings/errors. Generated native sample bodies are not compiled by this command. |
| `.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --list-tests text --filter "<classic/modern runtime selector in samples/public/README.md>" /p:SkipUapTargetFramework=true /p:UsingDotNetTest=true /p:TestingPlatformCommandLineArguments= /p:TestRunnerAdditionalArguments= -bl:artifacts\log\Release\uwp-sample-guards-discovery-text.binlog` | Exit 0; five planned outer cases discovered, zero native launch attempts. The earlier attempt without explicit `text` exited 1 in the SDK's argument parser; the documented command was corrected. |

### Test-value evidence

These are package-only preservation and compatibility probes bound to the exact
package/manifest hashes above, not a shipping source migration. The same test
source and assertions were retained in both package variants. Each ordinary
execution discovered and ran the three explicitly named cases; exact TRX names,
outcomes and counters were checked. The Windows probe demonstrates that the
candidate is unsuitable, rather than weakening the assertion to make it pass.

No CI, real UWP activation, UI inspection, release-branch servicing or
cross-repository rollout has been performed or claimed.

The repository's `verify_execution.py` gate independently checked all six paired
TRX files and the additional English-build candidate report: each contains the
three source-declared names, three executed/passed results, zero failed/skipped,
and a fresh run timestamp. The UWP guards themselves have static sensitivity
predictions only: exposing previously suppressed adapter translations or losing
the application's French resource should fail their named assertions, but those
native counterfactuals have not run.

The initial experiment's nine consumer caches and the follow-up's two separate
variant caches were removed after recording exact cached-package/feed hashes.
Package feeds, transformed manifests, consumer/probe sources and outputs, assets
files, binlogs and TRX remain in session artifacts. No application was registered,
no shared cache was cleared, and no global resource/toolchain configuration was
changed.

The sample guards add real English/French manifest-description resources,
`AdapterFailureMessages_AreLocalized_English`,
`AdapterFailureMessages_UseNeutralFallbackForFrench`, and
`ApplicationResources_AreLocalized_English/French`. They preserve the existing
UWP adapter suppression rather than making an unsupported assumption that
French adapter translations are available. Application-resource lookup uses a
per-call `ResourceContext`; culture-changing adapter cases restore the previous
UI culture in `finally`. Existing scheduling and package-identity cleanup remain
unchanged. Compile references and copy-local inputs are recorded separately.

The actual acceptance harness builds with zero warnings/errors and independently
discovers all five outer runtime rows (three classic reference forms and two
modern TFMs). None of those native test bodies was executed. Public samples
retain their release pins; their generated acceptance counterparts are the
documented consumers for a fresh local package comparison.

The final 15 sample/acceptance deliverables (including four new `.resw` files,
excluding this decision record) have content identity
`37966e0420c58fa572a9e2e0d7e08d4da4f69c0c1693bade01ac353f3d5e995e`.
It is SHA-256 over UTF-8 `base=<full HEAD>\n` followed by ordinal-sorted
`repo-relative-path<TAB>lowercase-file-SHA256\n` entries. The baseline HEAD is
the full revision at the start of this record; this is not a commit identity.

At the time of this experiment there was no shipping-product layout change. The samples' localized manifest
surface and real activation remain uninspected because native tooling is absent;
build/discovery cannot demonstrate a localized user-visible result.

## Open questions

- The pinned toolchain can produce a focused ordinary-consumer closure. The
  complete classic-UWP baseline remains blocked by missing native tooling.
- Can standard resource/copy-local classification and the normal bundle
  satellite filter eliminate the current suppression in a real modern UWP
  package, while retaining the application's own PRI translations?
- Which classic-UAP resource-to-PRI constraints remain when normal library
  assets are used? The managed follow-up does not answer that legacy question.
- Can the selection-only prototype preserve design-time, transitive consumer,
  netfx/packages.config and supported Native AOT/single-file/runtime-store
  publish contracts? Those paths remain untested; separate model packages avoid
  much of that SDK-internal selection coupling.
  *Update (2026-11-18):* the separate-package route was taken (#12450), so the
  ordinary package needs no model selector. The netfx PackageReference,
  packages.config (fresh install and in-place upgrade), SDK build/publish and
  transitive `ProjectReference` paths for the ordinary package are answered in
  the shipping implementation section. Runtime-store publish is covered by the
  review follow-up below. Native AOT and single-file publish remain untested.
- Legacy consumers and downstream VSTest tests hard-code the existing package
  directories. No rollout, release-branch servicing, or downstream migration has
  been authorized.
  *Update (2026-11-18):* the in-repository consumers of the old ordinary
  `buildTransitive/<tfm>` DLL paths were moved to `lib/<tfm>`. Downstream
  consumers outside this repository have not been audited.

## PR review and CI follow-up: 2026-11-13

[Build 1677700](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1677700)
tested PR head `7fc7ac9b2bb816527a7dc3816460a4269eab5193`, with merge revision
`dca0010c890425f2043800d23b7169a0e355a6c7`.
Its native application-model test run recorded all three classic UWP variants
(`MSTest.Sdk`, `MSTest`, and explicit `MSTest.TestAdapter`) as passed. This is
native CI evidence for that earlier revision, not the current head.

The four modern UWP build/runtime rows and the public modern UWP sample failed
at `CheckForDuplicatePriItems` with `NETSDK1022`. The Windows SDK includes
`**/*.resw` after the project body. Therefore, the explicit `PRIResource` include
duplicated both English and French files; excluding the items already present
at that earlier evaluation point could not prevent the SDK's later inclusion.

The fix removes the explicit resource group from the modern generated consumer
and the equivalent two resource declarations from the public sample. Both
resources remain under SDK default-item ownership. Classic UWP's separate
resource wiring is unchanged because its actual native comparisons passed.
Application translations, strict result checks and resource lookups remain.

### Focused verification

The actual Windows SDK default-item import and `CheckForDuplicatePriItems` target
from `Microsoft.Windows.SDK.BuildTools.MSIX` `1.7.20250806.1` were run on isolated
copies of the resource groups extracted from the original/fixed projects:

| Source / mode | Original | Fixed |
| --- | --- | --- |
| Public sample, packaged | Exit 1, expected `NETSDK1022` duplicates | Exit 0, exactly one English and one French resource |
| Public sample, unpackaged | Exit 0, two resources | Exit 0, same two resources |
| Generated modern consumer, packaged | Exit 1, expected `NETSDK1022` duplicates | Exit 0, exactly one English and one French resource |

This is executed SDK item-validation evidence, not native activation or a
passing MSTest run. The first standalone harness attempt lacked task-assembly
properties and failed during evaluation; the corrected run used the actual
MSIX task assembly/namespace and produced the intended diagnostic comparison.
The source blocks, copied resources, commands and binlogs are retained in
session artifacts.

```powershell
.\.dotnet\dotnet.exe build test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-restore /p:SkipUapTargetFramework=true -bl:artifacts\log\Release\uwp-ready-build.binlog --verbosity quiet
```

Exit 0, zero warnings/errors. This builds the outer acceptance harness, not the
generated native UWP children.

The branch also merges existing upstream fixes
`8dd78e1347417e400c3ef8d786059a8bb346162f` (evaluated package versions for
acceptance assets) and `9cdf7ab8e417c0d15f4c10a1001e74c359a07c34` (async GitHub
client handling in offline action-contract tests), rather than duplicating
their unrelated changes. The pinned-action contract suite passed all 19 cases,
with `$pinnedActions` pointing to the session-owned `setup/js` files extracted
from action revision `221ba4f4319de6b8e809d1a15528b3ae6a818aa5`:

```powershell
python .github\scripts\test_gh_aw_runtime_contracts.py --actions-dir "$pinnedActions"
```

Exit 0. No workflow source or generated action pins were edited.

The remaining Retry recovery-delay and Playwright failures also occur on
`main` [build 1677886](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1677886).
They are not attributed to the UWP resource changes or changed in this PR.
Full modern UWP activation still requires the capable CI agent; a new pending
run must not be presented as passing evidence.

## Shipping implementation: ordinary adapter lib assets

*2026-11-18. Source base: `ba85050ce3075f6f8a5777e75fcede110baa1fa3`.*

### Decision

The ordinary `MSTest.TestAdapter` package (`net462`, `net10.0`, `net11.0`) ships
`MSTest.TestAdapter.dll` and `MSTestAdapter.PlatformServices.dll` as standard
NuGet `lib/<tfm>` assets, with their satellites in `lib/<tfm>/<culture>/`.
`buildTransitive` no longer contains any assembly. Explicit `<Reference>`
injection remains only for the UI payloads shipped by `MSTest.Uwp` and
`MSTest.WinUI` ([#12450](https://github.com/microsoft/testfx/pull/12450)).

With NuGet owning the assets, compile references, copy-local, `deps.json`,
publish, `ProjectReference` flow, and `IncludeAssets`/`ExcludeAssets` follow
standard NuGet semantics. They no longer rely on synthetic
`MSTest.TestAdapter.Reference/42.42.42.42`-style entries. #7389 already named
this layout as the follow-up.

The alternatives were:

- **Keep explicit references.** Rejected, because this is the mechanism the
  request asks to remove.
- **One package with a model selector.** This was the original experiment, made
  unnecessary once the UI payloads moved to separate packages.
- **`lib` assets without policy targets.** Rejected, because the consumer
  matrix showed three regressions:
  - copy-local behavior changed for `IsTestApplication=false`;
  - `EnableMSTestV2CopyResources` and VSTest culture filtering stopped applying;
  - packages.config projects upgraded in place lost the adapter (see the RED
    result below).

### Implementation

| Piece | Location | Purpose |
| --- | --- | --- |
| Pack layout | `_AddTestAdapterPackageFiles` in `MSTest.TestAdapter.csproj` | Packs DLLs and satellites into `lib/<tfm>`. |
| Sidecar exclusion | `_AddMSTestAdapterNuspecReferences`, after `Pack` | NuGet's MSBuild pack cannot emit `<references>`, so the packed nuspec is rewritten. Each group lists only the two adapter assemblies. The `net462` `MSTest.TestFramework.dll` sidecar needed by VSTest's resolver (#4537, #4426, #6625) stays in the package, but it is neither a compile nor a runtime reference. |
| SDK policy | `_MSTestApplyAdapterPackageAssetPolicy`, after `ResolvePackageAssets` | Removes adapter runtime items for `IsTestApplication=false`. Removes satellites for non-test applications, for `EnableMSTestV2CopyResources=false`, and under VSTest for cultures outside the build-machine UI hierarchy. Under MTP, all cultures permitted by `SatelliteResourceLanguages` are kept, as before. |
| SDK runtime-store publish policy | `_MSTestApplyAdapterPublishAssetPolicy`, after `_ResolveCopyLocalAssetsForPublish` | Runtime-store publish (`--manifest`, `PreserveStoreLayout`) re-resolves the publish assets from the assets file instead of reusing the filtered copy-local items. This target applies the same removals to `_ResolvedCopyLocalPublishAssets`. Otherwise it is a no-op. |
| Non-SDK policy | `_MSTestApplyAdapterNonSdkAssetPolicy`, after `ResolveAssemblyReferences` | Applies the same policy to non-SDK PackageReference and packages.config projects, and also applies `SatelliteResourceLanguages`, which their resolvers ignore. |
| packages.config fallback | `_MSTestAddPackagesConfigAdapterReferences` | Adds any missing `lib\net462` HintPath reference when a non-SDK project has no `PackageReference` items. This covers projects upgraded in place, which never received NuGet's install-time references. |
| Layout consumers | `eng/package-shapes`, `eng/bazel/*`, acceptance tests, `PACKAGE.md` | Move to the `lib/<tfm>` paths. The previous `NU5127`/`NU5128`/`NU5100` suppressions are removed; the pack has no warnings without them. |

### Package evidence

| Package | SHA-256 | Size | `lib/*` entries | `buildTransitive` DLLs | nuspec `<references>` |
| --- | --- | --- | --- | --- | --- |
| Baseline | `E8686D40F04BFA3DC77C23DBDD1AB84F92F99051F27753C06B94C9D6279BD228` | 1,871,000 | 0 | 33, including `_localization/<culture>` satellites | None |
| Candidate | `7DD4C5AD27D5A191BE47B2C0B1AD6C32DBB348D895F8355DBDF4AAC9ADACED2B` | 2,243,049 | 85 | 0 | 3 groups × 2 adapter assemblies |

### Consumer comparison

Session-owned harness scripts, which are not repository tools, built every
consumer against both packages. Each side's `NuGet.config` used that side's
`artifacts\packages\Debug\Shipping` folder as its local feed, with an isolated
global-packages folder. `bin`/`obj` were deleted before each build.
Output file sets were compared by relative path.

**Non-SDK consumers.** These were built with Visual Studio 18 `MSBuild.exe` and
run with `vstest.console`. All target .NET Framework 4.8:

- `legacy`: a non-SDK project using PackageReference.
- `pc`: packages.config with the references NuGet adds at install time.
  The candidate project includes the `lib\net462` adapter references.
- `pc-upgrade`: the candidate package with no adapter `<Reference>` in the
  project, which is the in-place upgrade case.

The variants were:

- `nontest`: `/p:IsTestApplication=false`.
- `fr`: `DOTNET_CLI_UI_LANGUAGE=fr`.
- `fr-nocopy`: `fr` plus `/p:EnableMSTestV2CopyResources=false`.

Output files (baseline = candidate):

| Consumer | default | nontest | fr | fr-nocopy |
| --- | --- | --- | --- | --- |
| `legacy` | 104 | 102 | 106 | 104 |
| `pc` | 120 | 118 | 122 | 120 |
| `pc-upgrade` (candidate only) | 120 | 118 | 122 | 120 |

All 12 candidate rows have the same file set as the baseline. The default,
`fr`, and `fr-nocopy` rows passed 2/2 tests, with French console output under
`fr`. The `nontest` rows report "No test is available" with exit 0 on both
sides. `legacy` reports the same single build warning on both sides.

**packages.config upgrade, RED→GREEN.** Run on the final candidate package with
`pc-upgrade`, default variant:

| Package targets | Build | Output files | `vstest.console` |
| --- | --- | --- | --- |
| `_MSTestAddPackagesConfigAdapterReferences` removed from the installed targets file | 0 | 118, with no adapter DLLs | exit 0, "No test is available" |
| Unmodified (file hash restored and verified) | 0 | 120 | exit 0, 2/2 passed |

The RED exit code is 0, so a CI run would not catch this regression. The
fallback is required.

**SDK consumers.** These used the repository-pinned `dotnet`:

- `sdk-vstest`: `net10.0;net48` with `Microsoft.NET.Test.Sdk`.
- `sdk-mtp`: `net10.0;net48` with `EnableMSTestRunner=true`.
- `mstest-sdk`: `MSTest.Sdk`, `net10.0`.

Besides the four variants above, they ran:

- `srl-de`: `SatelliteResourceLanguages=de`.
- `publish`.

Output files (baseline → candidate):

| Variant | `sdk-vstest` | `sdk-mtp` | `mstest-sdk` |
| --- | --- | --- | --- |
| default | 244 | 250 | 207 |
| nontest | 240 | **246 → 194** | 3 |
| fr | 248 | 250 | 207 |
| fr-nocopy | 244 | 198 | 181 |
| srl-de | 64 | 58 | 83 |
| publish | 211 | 121 | 206 |

- **The only file-set difference is intended.** In `sdk-mtp` nontest, the
  baseline copied 52 orphan satellites: 13 cultures ×
  `MSTest.TestAdapter.resources.dll`/`MSTestAdapter.PlatformServices.resources.dll`
  × `net10.0`/`net48`. Their assemblies were not copied, so the satellites were
  never usable.
- **Test runs.** Every test run in the default, `fr`, `fr-nocopy`, and `srl-de`
  rows passed 2/2 per target framework on both sides.
- **nontest outputs.** These are not runnable test applications on either side:
  - `sdk-vstest` reports "No test is available";
  - the `sdk-mtp` executable exits `-532462766` on both sides;
  - `mstest-sdk` produces three files.
- **`deps.json`.** It differs in every row, as intended: it lists the package's
  runtime and resource assets instead of synthetic reference entries.
- **Earlier `sdk-mtp` checks.** These ran against the candidate:
  - `deps.json` listed 2 adapter runtime assets and 26 adapter resource assets;
  - the published executable passed 2/2;
  - `dotnet clean` left no files.

**Asset flow.**

- **`ProjectReference`.** Adapter assets flow through `ProjectReference` as they
  did from `buildTransitive`.
- **`ExcludeAssets="runtime"` is a behavior change.** It now removes the
  adapter, following standard NuGet semantics. VSTest then reports "No test is
  available" with exit 0; no diagnostic was added. The baseline's
  `buildTransitive` injection did not honor this setting.
- **`ExcludeAssets` on the adapter is unchanged.** It also applies to the
  adapter's own dependencies, as in the baseline. Under MTP, `compile` produces
  `CS0234` and `runtime` crashes at startup.

### Repository validation

| Command | Result |
| --- | --- |
| `.\build.cmd -pack` | Exit 0, 0 warnings, 0 errors |
| `MSTest.Acceptance.IntegrationTests.exe --progress off --ansi off`, filtered to `AssemblyFixtureProviderDiscoveryTests`, `CleanupCancellationIsolationTests`, `CoreFrameworkAssemblyTests`, `CppCliVSTestTests`, `FrameworkTargetingTests`, `SourceGenerationNonAotTests`, and `WindowsApplicationModelPackageTests` | 133/133 passed |
| The same executable, filtered to `SdkTests` | 147 passed, 1 skipped (`MSTestSdk_ClassicUwpTargetFramework_InfersUwpComposition` requires the gated Windows application-model environment) |
| `.\eng\bazel\validate-mstest-packages.ps1 -VerifySatelliteSensitivity` | Exit 0 |

The Bazel validation steps:

- **Native consumers** (`net10.0`/`net11.0`): publish, discovery, and pass exit
  0. The intentional failure exits 2 and empty exits 9.
- **MSBuild:** pass exits 0 and failure exits 1.
- **Satellite counterfactual:** removing `lib/net11.0/ko/MSTest.TestAdapter.resources.dll`
  makes `KoreanSatellitesAreCopiedAndLoadable` fail with exit 2.
- **Restored package** (`net11.0` consumer): the same results as the native
  consumers.

The acceptance filters were reconstructed from the produced TRX class
composition; the literal filter arguments were not retained.

### Test-value evidence

`WindowsApplicationModelPackageTests.PackedMSTestTestAdapter_ContainsOnlyOrdinaryAssets`
asserts the `lib/<tfm>` entries and the exact nuspec `<references>`. It was run
with each package placed in `artifacts\packages\Debug\Shipping` in turn:

| Package | Exit | Failure |
| --- | --- | --- |
| Baseline (`E8686D40…D228`) | 2 | `Assert.IsEmpty(missing)`: required `lib` entries are missing |
| Candidate with `<references>` removed (`A8319FD27D0A83CB513BFEC2C412B9E8BE06DA42B1C0746B030CDC9F29080C78`) | 2 | `Assert.AreSequenceEqual`: expected 6 references, actual 0 |
| Candidate (`7DD4C5AD…ED2B`) | 0 | 1/1 passed |

The packages.config RED→GREEN result and the Bazel satellite counterfactual
above are consumer-level counterparts.

### Review follow-up: runtime-store publish and packages.config upgrade tests

The PR review raised two issues:

- **Runtime-store publish bypassed the policy.** `dotnet publish --manifest`
  reruns NuGet asset resolution for publish and ignores the copy-local items
  that `_MSTestApplyAdapterPackageAssetPolicy` filtered. Under VSTest, all 13
  adapter cultures were published instead of the build-machine UI culture.
  With `EnableMSTestV2CopyResources=false`, all 13 were still published. With
  `IsTestApplication=false`, both adapter DLLs were published. The baseline
  `buildTransitive` injection did not have this gap. The fix is
  `_MSTestApplyAdapterPublishAssetPolicy`.
- **The packages.config upgrade had no automated test.** It is now covered.

Acceptance tests:

- `SdkTests.RuntimeStorePublish_AppliesAdapterAssetPolicy` publishes against a
  store manifest with `DOTNET_CLI_UI_LANGUAGE=fr`. It asserts the published
  adapter assemblies and satellite cultures:
  - `VSTestDefault`: `fr`.
  - `VSTestWithoutCopyResources`: no cultures.
  - `MTPDefault`: all 13 cultures.
  - `NotTestApplication`: no adapter assemblies.
- `PackagesConfigUpgradeTests.UpgradedProjectWithoutAdapterReferences_CopiesAdapterAndRunsTests`
  is Windows-only. It needs Visual Studio `MSBuild.exe` and `vstest.console`,
  and is reported inconclusive without them. The test:
  - restores the package;
  - generates a packages.config project with no adapter `<Reference>` items;
  - builds it;
  - asserts both adapter DLLs are copied;
  - runs `vstest.console` without `/TestAdapterPath` and expects 2/2 passed.

Each test was run with both new targets removed from the packed
`MSTest.TestAdapter.targets`, then with the restored targets. The commands:

- `.\build.cmd -pack -projects <MSTest.TestAdapter.csproj>`.
- `MSTest.Acceptance.IntegrationTests.exe --progress off --ansi off --filter "FullyQualifiedName~RuntimeStorePublish|FullyQualifiedName~PackagesConfigUpgrade"`.

| Targets | Result |
| --- | --- |
| `_MSTestApplyAdapterPublishAssetPolicy` and `_MSTestAddPackagesConfigAdapterReferences` removed | 4 failed, 1 passed (exit code not retained). `MTPDefault`, the control row, passed. The other three runtime-store rows reported the unwanted cultures or adapter DLLs listed above. The packages.config test reported "MSTest.TestAdapter.dll was not copied". |
| Restored (packed file verified to contain both targets) | 5/5 passed, exit 0 |

The same executable, filtered with
`"FullyQualifiedName~SdkTests|FullyQualifiedName~SatelliteResourceLanguages|FullyQualifiedName~FrameworkTargeting|FullyQualifiedName~WindowsApplicationModelPackage|FullyQualifiedName~CppCliVSTest|FullyQualifiedName~CoreFrameworkAssembly|FullyQualifiedName~PackagesConfigUpgrade"`,
exited 0: 239 passed and 1 skipped (`MSTestSdk_ClassicUwpTargetFramework_InfersUwpComposition`, which needs
the gated Windows application-model environment).

### Remaining gaps

- No CI run has validated this revision.
- `MSTest.Uwp` and `MSTest.WinUI` are unchanged and were not re-run natively.
- Native AOT and single-file publishing are untested. Runtime-store publishing
  is covered only by the four scenarios listed in the review follow-up.
- Visual Studio design-time builds and Test Explorer were not exercised.
- Consumers outside this repository that hard-code the former
  `buildTransitive/<tfm>` DLL paths have not been audited.
- No servicing or backport decision has been made.

## Three package families design: 2026-11-18

Status: superseded by [core packages only](#core-packages-only-2026-11-20).
Kept as research history.

### Problem, scope and decision

Design baseline: `28a041287150a48ad48134672c472a481fecf492`.

Relationship to the shipped ordinary-adapter change: the
[shipping implementation](#shipping-implementation-ordinary-adapter-lib-assets)
([#12515](https://github.com/microsoft/testfx/pull/12515)) landed after this
design baseline, so the ordinary `MSTest.TestAdapter` is already `lib`-based on
`main`. The results recorded below remain bound to the stated baseline. The
experiment harness accepts both the baseline `buildTransitive` layout and the
shipped ordinary `lib/<tfm>` layout; UI adapter payloads are unchanged by
[#12515](https://github.com/microsoft/testfx/pull/12515) and still originate from `MSTest.Uwp` and `MSTest.WinUI`.

The current [explicit package design](explicit-mstest-packages.md) combines UI
framework APIs with an application-model-specific adapter, platform services
and runner integration. A UI test library cannot choose just the UI framework
through those package IDs. The [SDK composition contract](mstest-sdk-composition.md)
already distinguishes applications from helpers; the new package graph must
preserve that distinction rather than infer intent from `OutputType`.

The requested work is a rewrite design with executable feasibility experiments,
not an unconditional migration of shipping packages. The candidate separates
framework, adapter and convenience layers. Existing `MSTest.Uwp` and
`MSTest.WinUI` names remain combined entry points; new component names provide
the narrower choices. This favors an additive migration over requiring every
application to edit its package references.

The DLL directory alone is not the defect. `buildTransitive` can legitimately
own build integration. The problem is owning consumer DLL/reference/dependency
semantics there and coupling those semantics to a framework reference. Moving
DLLs without removing the reverse dependency does not make a framework clean.
Likewise, a plain package reference does not unconditionally change a library
to an executable: the SDK's application policy and runner opt-in are separate.

### Package graph and asset ownership

| Package | Owns | Dependency direction |
| --- | --- | --- |
| `MSTest.TestFramework` | Shared framework and existing compatibility facade | No adapter or UI dependency |
| `MSTest.TestFramework.Uwp` / `.WinUI` | Existing UI framework DLL and its framework-specific runtime dependencies | Exact aligned core; no adapter, MTP runner, composition marker or builder hook |
| `MSTest.TestAdapter` | Ordinary adapter/services implementations, resources and integration | Required framework and ordinary execution dependencies |
| `MSTest.TestAdapter.Uwp` / `.WinUI` | Corresponding adapter/services implementations, resources and integration | Matching framework component and required model-specific execution dependencies; no ordinary or other-model adapter |
| `MSTest` | Existing ordinary convenience dependency set | Ordinary framework/adapter and existing applicable tooling |
| `MSTest.Uwp` / `.WinUI` | Compatibility/convenience composition, not implementation DLLs | Matching framework and adapter components, preserving the existing convenience contract |

For example:

```text
MSTest.WinUI
  +-- MSTest.TestFramework.WinUI
  |     +-- MSTest.TestFramework
  +-- MSTest.TestAdapter.WinUI
        +-- MSTest.TestFramework.WinUI
        +-- model-specific execution dependencies
```

The repeated framework edge converges on one aligned package. This graph must
not include `MSTest -> MSTest.TestAdapter` underneath `MSTest.WinUI`.
Adapter variants contain alternate implementations of the same assembly
identities; they replace, rather than extend, the ordinary adapter. Extracting
a common runtime adapter core is a different project and is not required here.

Preserve `MSTest.Uwp.dll`, `MSTest.WinUI.dll`, `MSTest.TestAdapter.dll` and
`MSTestAdapter.PlatformServices.dll` assembly/type identities even though some
package IDs change. Moving a package owner is not a reason to rename a public
assembly, move a type again, or undo the existing core facade.

Component DLLs and satellites belong in supported `lib/<tfm>` groups. A genuine
`ref/<tfm>` assembly is optional if a separately justified compile/runtime split
needs it; a `ref` directory alone does not solve runtime selection. Classic UAP
and modern UWP can remain separate TFM groups within the UWP adapter package.
WinUI is a different package, so a newer Windows platform version cannot make
a UWP package resolve a WinUI implementation.

Retain only integration in `build`/`buildTransitive`: runner registration,
parallelization defaults, composition validation and required model-specific
deployment hooks. No adapter/services DLLs should remain there or in `tools`
in the final layout. Ordinary .NET Framework VSTest adjacent-framework lookup
and classic UAP explicit deployment still need an equivalent working solution;
neither is waived by the `lib` goal.

### Consumer and compatibility contract

| Consumer | Intended reference/selection | Required observable outcome |
| --- | --- | --- |
| Ordinary framework library | `MSTest.TestFramework` | Framework APIs without adapter/runner payload |
| UI framework library | `MSTest.TestFramework.Uwp` or `.WinUI` | Actual UI APIs compile; output stays a library; no adapter/services or MTP registration/dependency introduced by the framework |
| Explicit application | Matching framework and one adapter, or matching metapackage | Correct compile/runtime/resource identity and execution |
| Existing UI application | Existing `MSTest.Uwp` / `.WinUI` reference | Same assembly identities and application behavior with no required reference edit, where the compatibility probes establish this |
| SDK application | Existing application-model inference/override before restore | One compatible adapter for each inner TFM across ClassicEngine, VSTest and NativeAOT paths |
| Source-generated helper | Framework plus only required private compile-time runtime-hook APIs | `IsTestApplication=false`; no application registration or leaking adapter runtime dependency; final app owns the model-specific runtime |
| Conflicting compositions | Two different adapter models, including transitive paths | Explicit failure before compilation, not duplicate output DLLs or arbitrary last-copy wins |

Framework packages must not import the adapter's `PackageDependencies.props`
or retain a private dependency on it. Keep genuine UI framework dependencies;
"clean" does not mean discarding Windows API dependencies needed by the UI DLL.

Existing meta-package IDs are compatibility entry points, not proof of
compatibility. Preserve relevant props/targets entry points and marker semantics
for older SDKs without double-registering hooks. New adapter owners supply the
canonical registration and validation. Evaluate conflicts by adapter identity:
framework plus its adapter is valid, while ordinary and UI adapters remain
incompatible. Preserve current-inner-TFM and excluded-build-asset validation.

Consumers that hard-code physical DLL paths inside a package cannot be covered
by an XML forwarder. If they require the old `buildTransitive` DLL paths,
supporting them and eliminating every DLL there are conflicting requirements.
Identify those consumers and choose a documented migration/major-release
boundary rather than claiming both guarantees.

Standard NuGet assets change meaningful behavior:

- Honor `ExcludeAssets=compile` and `ExcludeAssets=runtime`; never re-inject
  excluded files through manual references.
- `PrivateAssets` controls propagation, not use by the declaring project.
  Check ProjectReference and packed-helper consumers independently.
- Check metapackage asset exclusions through the dependency graph; moving an
  asset into a dependency can change which exclusion applies.
- Retain satellite identity and honor explicit `SatelliteResourceLanguages`.
  The earlier move from build-machine-culture filtering to normal NuGet
  resource delivery was a visible output-size/language-policy difference.
- Preserve exact UI/core alignment, central version-management behavior and
  complete per-TFM dependency groups. Component versions must not silently mix.

Package selection belongs to SDK/project evaluation before restore. A restored
metapackage cannot reliably inject new flavor dependencies after restore.
`MSTestApplicationModel` remains the existing selection contract; introduce no
new competing model selector or invented app-model RID. Retain the distinction
between a framework-only helper and a generated helper that needs compile-time
adapter APIs.

No launcher handshake, app ownership/lifetime, cancellation, child/parent exit,
fallback, retry or multi-module aggregation change is intended. Validation must
compose aligned framework, adapter, SDK, MTP and packaged-app components instead
of claiming that local unit tests establish those unchanged runtime contracts.

### Alternatives and migration gates

| Choice | Decision |
| --- | --- |
| Keep the current combined layout | Lowest immediate upgrade risk, but framework-only consumption remains unavailable |
| One adapter package, `lib` plus post-restore model selector | Feasible historical managed experiment; retains SDK resolved-item coupling and does not by itself separate framework ownership |
| Separate component packages and retain old UI IDs as metapackages | Preferred design to test: explicit ownership and native model selection; additional package/version management and compatibility work |
| Make old UI IDs framework-only | Reject as the default transition: existing applications would lose their adapter |
| Unify runtime adapter implementations | Defer; much broader conditional-runtime behavior change |

Implementation should proceed behind evidence gates:

1. Construct isolated candidate packages from current built shipping DLLs.
   Compare real consumers with the baseline and a deliberately wrong
   composition using retained assertions. Do not edit shipping defaults.
2. If the experiment supports the design, add production component owners,
   dependency metadata, library assets and adapter-owned integration. Preserve
   assembly identities and compatibility entry points. Update exact package
   shape checks from actual packed output.
3. Update all SDK injection paths together, including generated helpers,
   central management, VSTest and NativeAOT. Re-run old-SDK consumers separately
   from the aligned new SDK; passing only the new pair is not compatibility.
4. Require native classic/modern UWP, packaged/unpackaged WinUI, ordinary
   net462 VSTest and supported publish-mode evidence before enabling the layout.
   A temporary legacy DLL exception must be labeled as incomplete migration.
5. Decide release targeting, downstream migration and servicing before shipping.
   No release branch or backport is selected by this experiment. Keep one
   rollout owner through dependency flow and any temporary-shim retirement.

The newly introduced UI IDs' release status has not been established here.
If they are unreleased, a pre-release correction may cost less than permanent
compatibility machinery. Do not infer this from a development version number.

### Experiment matrix and evidence policy

The current test work owns the reproducible package experiment; the historical
hashes/counts above are not evidence for the split. Each run must record the
source revision, script identity, input/candidate package hashes, SDK, consumer
cache, exact command, exit and named assertion outcomes.

| Requirement | Experiment | Full-product gate beyond managed probes |
| --- | --- | --- |
| Framework-only UI APIs | Compile actual UWP/WinUI attributes; inspect references, packages, hooks and library output | Real helper consumed by the corresponding native app |
| Native flavor selection | Same recent Windows TFM with separate adapter IDs; assert both DLL hashes, compile/runtime/publish/deps paths | Execute actual modern UWP and WinUI layouts |
| Existing convenience references | Baseline SDK plus old UI package IDs resolving candidate metapackages | Old supported SDK/package combinations and IDE discovery |
| Helper privacy | Generated helper plus model-specific application, including packed-helper dependency inspection | Native/AOT supported helper path |
| Asset semantics | Compile/runtime exclusions and French-only satellites | PRI, localized runtime lookup and application-owned resources |
| Ordinary execution | Actual MTP and VSTest named cases against aligned packages | .NET Framework resolver and other supported publish modes |
| Composition errors | Ordinary/UI and UWP/WinUI conflicts, transitive/excluded imports, valid mixed-TFM control | Non-SDK marker fallback and design-time integration |
| Regression sensitivity | Same assertions against baseline or a buildable wrong-flavor candidate | No restore/compiler/setup failure counted as a behavioral red result |

This is nonvisual packaging research. A transcript and exact resolved/output
identity checks are the appropriate evidence now; eventual IDE/discovery or
localized UI changes still require inspecting the real rendered surface.
Native preflight failures, unrun paths and expected counterfactual assertion
failures must remain distinct from passing cases.

### Current experiment results

The reproducible harness is
[`eng/experimental/Test-PackageSplit.ps1`](https://github.com/microsoft/testfx/blob/be6de2f432898fe8f5d624a2d364d65df0ef97cd/eng/experimental/Test-PackageSplit.ps1)
(removed with the UI package IDs; see
[core packages only](#core-packages-only-2026-11-20)).
It builds current producers, snapshots real packages, constructs separate
baseline/candidate feeds and caches, and preserves implementation DLL bytes.
Trial packages are explicitly labeled local experiments, not shipping artifacts.
The core and SDK archives are unchanged; candidate adapter integration removes
manual DLL/resource injection and validates the new adapter package identities.
No shipping package owner or SDK default is changed.

The final comparison is retained under `artifacts/package-split/a/f`.
It records 27 probes: 23 passed, two expected baseline cleanliness assertion
failures, two compatibility failures, and zero setup failures. Four managed
TRX reports contain eight executed/passed cases. Actual modern UWP launches
contain six baseline passes and five candidate passes plus one candidate
failure. Combined TRX totals are 20 executed, 19 passed, one failed, zero skipped.
Build-only probes are not counted as executed MSTest cases.

| Requirement | Evidence |
| --- | --- |
| Framework-only libraries compile real APIs without runner payload | `FrameworkOnlyOrdinary`, `FrameworkOnlyWinUI`, `FrameworkOnlyUwp` passed; UI cases compile `AppContainer.UITestMethod` and assert library output, no adapter/MTP packages/payload and no application hooks |
| Cleanliness assertion sensitivity | `BaselineFrameworkOnlyWinUI`, `BaselineFrameworkOnlyUwp` build successfully, then fail the same contamination assertion with the original combined packages |
| Native NuGet flavor selection at the same Windows TFM | `AdapterOrdinary`, `AdapterWinUI`, `AdapterUwp` passed: both implementation DLLs match original producer bytes in compile, build, publish and dependency-manifest paths |
| Asset exclusion and localization policy | `AdapterUwpExcludeCompile`, `AdapterUwpExcludeRuntime`, `AdapterUwpFrenchOnly` passed; exact reference/payload absence and two French satellite hashes/deps entries checked |
| Existing UI package IDs with the unmodified baseline SDK | `LegacyWinUIBaselineSdk`, `LegacyUwpBaselineSdk` passed build/publish with the correct flavor and no ordinary adapter; this is the current-source SDK, not historical released SDK coverage |
| Generated helpers | `baselineSdkHelperNoRuntime`, `candidateSdkHelperNoRuntime`, `SdkWinUIWithOrdinaryGeneratedHelper`, `SdkUwpWithOrdinaryGeneratedHelper` passed source generation, private compile-hook and no-runtime-leak checks |
| Real managed execution | `baselineManagedMtp`, `candidateManagedMtp`, `baselineManagedVstest`, `candidateManagedVstest` each execute `AdapterLoadsFromConsumerOutput` and `FrenchAdapterResourceIsAvailable`: eight passes |
| Classic UWP native build | `baselineNativeClassicUwpBuild` passed; `candidateNativeClassicUwpBuild` failed at the unchanged legacy DLL path |
| Modern UWP native build and activation | `baselineNativeModernUwpBuild`, `candidateNativeModernUwpBuild` both build and launch; only the candidate's `AdapterFailureMessages_UseNeutralFallbackForFrench` fails |
| WinUI native build | `baselineNativeWinUIBuild`, `candidateNativeWinUIBuild` passed and produced deployment recipes; neither app was launched |

#### Discovered incompatibilities and proposed treatment

**Classic UWP has a physical-path compatibility break.** The unchanged consumer
copies `mstest.uwp/5.0.0-dev/lib/uap10.0.16299/MSTest.Uwp.dll` directly.
That file no longer belongs to the candidate metapackage, producing `MSB3030`.
This is a demonstrated build-compatibility failure, not behavioral red-phase
proof and not evidence that native UAP cannot consume library assets.

For the production rewrite, migrate that consumer to the actual resolved UI
assembly instead of a versioned metapackage path, and ensure the adapter-owned
classic deployment logic copies it. Retain the unchanged legacy-path probe as
a documented compatibility boundary. Do not copy duplicate implementation DLLs
into metapackages merely to make the experiment green. If external callers
depend on those physical paths, they need an explicit migration/release decision.
Classic UWP runtime has not been validated after that migration.

**Modern UWP gains French adapter translations.** The identical
`AdapterFailureMessages_UseNeutralFallbackForFrench` case passes against the
baseline and fails against the candidate. It expects
`The parameter should not be null or empty.` but receives
`Le paramètre ne doit pas être null ou vide. (Parameter 'fileName')`.
The exception remains `ArgumentException` with parameter `fileName`; standard
library satellites change the message language. Application English/French
resource cases, the plain test, the UI-dispatcher test and the English adapter
message case all pass in both native apps.

The proposed rewrite policy is to adopt normal NuGet satellite delivery and
document this intentional localization change, rather than preserve a deployment
workaround by silently suppressing translations. This is not a claim of
behavioral compatibility. Keep the unchanged fallback assertion in the comparison;
when production policy is approved, add a positive localized-message assertion
while retaining application-resource and exception-identity checks.

These results support separate ownership and native asset selection but reject
the claim that converting the old UI IDs into metapackages is a completely
drop-in replacement.

#### Reproduction and provenance

The producer closure contains 30 real projects and 22 shipping packages at
`28a041287150a48ad48134672c472a481fecf492`. The pinned SDK is
`12.0.100-alpha.1.26562.108`; packages use MSTest `5.0.0-dev` and MTP `3.0.0-dev`.
Managed and modern native consumers use `net10.0-windows10.0.26100.0`; classic
uses `uap10.0.16299`; native builds are Release x64. These are local results,
not CI or release-branch validation.

Final executed commands, from the repository root:

```powershell
& .\eng\experimental\Test-PackageSplit.ps1 -Stage Pack `
    -RunRoot "$PWD\artifacts\package-split\a"
# Exit 0: real producer build/pack and immutable snapshot.

& .\eng\experimental\Test-PackageSplit.ps1 -Stage Probe `
    -RunRoot "$PWD\artifacts\package-split\a" -ProbeName f `
    -NativeBuild -ModernUwpLaunch
# Exit 1: the two compatibility failures above; neither is ignored.
```

The initial `r/f` run explicitly disabled NuGet auditing. That suppression was
unnecessary and removed from producer, managed and native invocations before
the final `a/f` repack/comparison. Effective restore records confirm auditing
enabled for all 30 producers and 29 consumers/helpers, with no `NU19xx`
diagnostics. The two compatibility findings and all case counts are unchanged.

Use fresh short `RunRoot` and `ProbeName` values for another run; existing evidence
is protected against overwrite and native loaders have path-length constraints.
Omit native switches for managed-only probes. `-NativeBuild` by itself does not
launch apps. `-Only` selects exact probe names and fails on unmatched names.
Native prerequisite checks are read-only and harness-local: `-NativeBuild`
requires only a UWP-capable Visual Studio with desktop MSBuild, while
`-ModernUwpLaunch` additionally requires Developer Mode to be enabled already
and an interactive desktop. The harness never changes machine settings; the
recorded runs above predate this split and used the CI agent preflight.

Each evidence directory contains `results.json`, `harness-provenance.json`,
`package-provenance.json`, exact `logs/*.command.json` invocations, binlogs,
consumer assets/output manifests and TRX. The producer root contains the
immutable package snapshot, `source.json` and `producer-packages.json`.
Consumer cache package hashes are checked against the selected feed; finding a
same-version package in a global cache is not accepted as provenance.

The pack-stage snapshot now selects each shipping producer's evaluated
`PackageId`, `PackageVersion` and `PackageOutputPath` after the successful pack,
rather than globbing the shared Shipping directory. This excludes older versions
and unrelated packages without deleting other builds' artifacts or relying on
timestamps (an up-to-date pack may retain its output timestamp). Queries and
binlogs are retained as `logs/producer-output-*`; a failed query or missing
expected archive fails the pack stage. The repository package-shape gate and
normal producer output locations are unchanged.

The final run's executed harness SHA-256 is
`D334EC0585BDE63A5EF41A03BD6433320324C699C112929EB169D69B5D75FA76`.
Candidate archive timestamps can change per-run package hashes; the manifest
and original-producer DLL hashes bind each actual comparison.

Selection guards were also executed: selecting `FrameworkOnlyOrdinary` alone
exits zero with exactly one result and no fabricated native results. Selecting
`candidateNativeClassicUwpBuild` without enabling native builds exits one with
`Requested probes were not run`, rather than treating an unrun case as passed.

#### Producer snapshot review correction

The shared-output glob could include an older version even after a successful
pack, causing an otherwise fresh probe to reject an ambiguous producer version.
An exploratory NuGet `_GetOutputItemsFromPack` query returned `Skipped` without
restore-style initialization; evaluated package properties provide the needed
identities without depending on that private target or changing package output
and validation infrastructure.

The correction was checked from parent
`250355b259b6bd27723b1e768b680076c641b99b`, with harness SHA-256
`11093F7B4371108345A3741902DEA23A7A896FD4A109A3D4A9825BADB19EA39B`:

```powershell
& .\eng\experimental\Test-PackageSplit.ps1 -Stage Pack -RunRoot "$PWD\artifacts\package-split\h"
# Exit 0: 30 producer queries, 22 shipping packages; package-shape validation retained.
& .\eng\experimental\Test-PackageSplit.ps1 -Stage Probe -RunRoot "$PWD\artifacts\package-split\h" -ProbeName p -Only FrameworkOnlyOrdinary,FrameworkOnlyWinUI,FrameworkOnlyUwp
# Exit 0: all three selected build/cleanliness probes passed.
```

**Test-value evidence:** a session-local `CurrentOnly` check executed the actual
snapshot assignment from both source states against isolated files and stubbed
MSBuild metadata. Its fixed expectation was exactly `Current.2.0.0.nupkg`, with
matching copied bytes and no changes to shared files. The parent failed that
assertion (exit 1), including the older version, an unrelated package and
non-packable/non-shipping outputs; the correction passed (exit 0), even with old
timestamps on every fixture. `MissingOutput` and `QueryFailure` also passed,
confirming explicit failures instead of substitution or a partial-success result.
These are focused harness checks, not native runtime evidence. The real pack
and three consumer probes above verify the metadata-query integration; the
full native/managed comparison matrix was not rerun.

#### Test-value evidence and remaining gates

The two framework cleanliness controls retain the same APIs/assertions across
successful baseline/candidate builds and detect the baseline's unwanted runner
dependencies. The native French-fallback assertion detects an actual changed
outcome after real packaged activation. Both are executed sensitivity evidence.
The classic-path build failure is separately classified; it is not counted as
an executed failing test.

Still unproved: classic UWP launch after consumer-path migration, WinUI launch,
native UI launch specifically through MSTest.Sdk, published older SDK versions,
other TFMs/RIDs, .NET Framework VSTest adjacent-framework resolution,
NativeAOT/special publish modes, incompatible-flavor co-installation and the full
non-SDK/CPM/multi-TFM matrix. Helper ProjectReference probes do not replace
packed-helper downstream dependency checks. No UI/IDE rendered inspection,
shipping metadata/signing validation, release enablement or publication is claimed.

Both owned modern UWP registrations were removed and their absence verified.
Task-owned feeds, caches, outputs, reports and logs remain under ignored
`artifacts/package-split` for reproduction; no shared/global cache was cleared.
Internal test research/plan/status files remain outside version control.

## UI package layout options review: 2026-11-20

Status: research and recommendation only. No package, build or shipping change
is made or authorized by this section. It re-reads the
[three package families design](#three-package-families-design-2026-11-18)
against three follow-up questions: whether `MSTest.WinUI` and `MSTest.Uwp`
should keep embedding the adapter next to the UI framework, whether a
metapackage over per-component packages is worth it, and whether NuGet
`TargetFramework` aliases can build one Windows target twice to replace the
split.

### Verified facts

- **The UI packages have not shipped.** `api.nuget.org` returns 404 for
  `MSTest.WinUI`, `MSTest.Uwp`, `MSTest.TestFramework.WinUI` and
  `MSTest.TestAdapter.WinUI`. Both UI projects' `PublicAPI.Shipped.txt` contain
  only `#nullable enable`. The compatibility cost of changing their shape is
  therefore limited to unreleased pre-release builds, and the
  "if unreleased, a pre-release correction may cost less" question left open by
  the design above is answered for the package IDs. Publication state of
  internal pre-release feeds was not checked.
- **Current shape.** Each UI package carries its UI framework DLL in `lib`, the
  adapter and platform services in `buildTransitive/<tfm>`, and
  `ProjectReference`s to `TestFramework` (a real, exact-version dependency) and
  `MSTest.TestAdapter` (`PrivateAssets=all`, embedded). The app model comes from
  the props/targets file name via `MSTest.Platform.props`.
- **Modern UWP and WinUI already share one adapter build** with identical DLL
  bytes ([shared adapter](shared-modern-uwp-winui-adapter.md)). Only classic
  UWP is a separate adapter build.
- **Why `lib` is not enough on its own.** NuGet selects by Windows platform
  compatibility, not by `UseUwpTools`, so a modern UWP consumer could resolve
  the WinUI adapter. Distinct package IDs discriminate the app model, which is
  what made native `lib` selection pass in the 2026-11-18 experiment.
- **TFM aliases** ([docs](https://learn.microsoft.com/nuget/reference/target-frameworks#targetframework-values-are-aliases),
  [NuGet/Home#5154](https://github.com/NuGet/Home/issues/5154)) let several
  aliases resolve to one effective framework. Building duplicate effective
  frameworks needs NuGet 7.6 / .NET SDK 10.0.300 or later. Packing them raises
  [NU5051](https://learn.microsoft.com/nuget/reference/errors-and-warnings/nu5051)
  unless every alias but one sets `IncludeBuildOutput=false` and
  `SuppressDependenciesWhenPacking=true`. A secondary alias can only be placed
  at a custom path through `TargetsForTfmSpecificContentInPackage`, for example
  under `runtimes/<name>/lib/<tfm>`, which NuGet does not use for ordinary
  framework selection. Aliases are consumed by SDK-style projects only and have
  no special Visual Studio package manager support.

### Evaluation of the three ideas

| Idea | Verdict | Reasoning |
| --- | --- | --- |
| Keep adapter DLLs copied from `buildTransitive`, not `lib` | Valid, lowest risk | It is the shipped behavior of the explicit `<Reference>` design and needs no consumer migration. Its cost is the confusion that motivated this review: the UI IDs bundle two components and cannot be consumed framework-only. |
| `MSTest.WinUI` / `MSTest.Uwp` as metapackages over `MSTest.TestFramework.*` + `MSTest.TestAdapter.*` | Worth doing only if framework-only UI libraries are required | Gains: framework-only UI libraries, native `lib` and satellite delivery in place of custom `<Reference>` logic, clear ownership. Costs: four more package IDs with exact-version alignment, explicit conflict failures, updates to every SDK injection path, and classic UWP consumer migration (physical-path copy of `lib/uap10.0.16299`). Cheapest before first release; every later change carries compatibility machinery. |
| TFM aliases to build `net10.0-windowsXX` twice | Not recommended | Aliases change how a project builds, not what a package ships or how a consumer selects an asset. Modern UWP and WinUI adapters are already byte-identical and are separate projects, so there is nothing to build twice. Packing would add NU5051 handling and a 10.0.300+ SDK requirement without removing the need for distinct package IDs. Reconsider only if the modern adapters diverge. |

### Recommendation

1. Do not use TFM aliases for this problem.
2. Treat the choice as status quo versus the three-family split, decided by one
   requirement: whether UI test libraries must reference the UI framework
   without a runner payload. If not, keep the `buildTransitive` adapter and
   improve the UI package descriptions and documentation to state that each
   package bundles the framework and the matching adapter.
3. If that requirement exists, do the split before the first UI package release,
   under the existing gates: native classic and modern UWP, packaged and
   unpackaged WinUI, `net462` VSTest resolution, SDK injection paths,
   older-SDK consumers, conflict co-installation, and a release/servicing
   decision. Those gates are still unproved; this review adds no new executed
   evidence for them.

### Requirement confirmed: shared UI test libraries

The maintainer confirmed the deciding requirement: referencing `MSTest.WinUI`
(or `MSTest.Uwp`) must not turn a shared UI test library into a test
application. The current package cannot meet it, because its
`buildTransitive` props and targets always run for the consumer. They add
`<Reference>` items for `MSTest.TestAdapter` and `MSTestAdapter.PlatformServices`,
set `IsMSTestTestAdapterReferenced=true`, and import the adapter's runner and
resource targets. A conditional switch inside the combined package would
recreate application-intent inference, which the
[SDK composition contract](mstest-sdk-composition.md) rejects. The status-quo
option above is therefore withdrawn.

Proposed shape, mirroring the ordinary `MSTest` / `MSTest.TestFramework` /
`MSTest.TestAdapter` family:

| Package | Owns | Consumer |
| --- | --- | --- |
| `MSTest.TestFramework.WinUI` / `.Uwp` | UI framework DLL in `lib`; exact `MSTest.TestFramework` dependency; no adapter, no runner targets | Shared UI test libraries |
| `MSTest.TestAdapter.WinUI` / `.Uwp` | App-model adapter and platform services, plus their build integration; the package ID selects the app model | Test applications |
| `MSTest.WinUI` / `.Uwp` | Metapackage over the two packages above | Test applications wanting one reference |

`MSTest.Sdk` would inject only the framework package for helper libraries and
the metapackage (or both components) for applications. A smaller alternative
makes `MSTest.WinUI` framework-only and adds only `MSTest.TestAdapter.WinUI`.
It has fewer IDs, but it changes the meaning of the existing name and
diverges from the ordinary family, so it is not preferred.

Implementation is not approved by this record. It needs the package-ID
decision, then changes across about 55 files: the two UI projects,
`PlatformPackage`, `MSTest.Sdk` injection, package-shape baselines, Bazel
package validation, samples, documentation, and the acceptance tests in
`SdkTests*`, `PackagedAppIntegrationTests` and
`WindowsApplicationModelPackageTests`. It must also pass the
[existing evidence gates](#test-value-evidence-and-remaining-gates). A shared
library that references `MSTest.TestFramework.WinUI` and compiles UI APIs
without adapter references is the regression test. In
`eng/experimental/Test-PackageSplit.ps1`, `BaselineFrameworkOnlyWinUI` and
`BaselineFrameworkOnlyUwp` already fail on the current combined package
("framework-only runtime is contaminated"). `FrameworkOnlyWinUI` and
`FrameworkOnlyUwp` assert a clean library against the proposed framework IDs.

### Test-value evidence

Documentation-only change. No behavior changed, so no regression test applies.
The facts above come from a nuget.org lookup, repository files at `be6de2f43`
and the linked NuGet documentation; none was produced by building or packing
the alias scenario. The NU5051 behavior and the NuGet 7.6 requirement are taken
from the documentation and were not reproduced locally.

### Implemented design: framework-only UI packages

Status: superseded and discarded before commit by
[core packages only](#core-packages-only-2026-11-20). The results below are
kept as experiment history.

Implemented on top of `be6de2f43`. It uses two packages per app model, not the
three-family proposal above:

| Package | Owns | Consumer |
| --- | --- | --- |
| `MSTest.TestFramework.WinUI` / `.Uwp` | UI framework DLL (plus xml, pri and WinUI resources) in `lib`; exact `MSTest.TestFramework` dependency; no `build*` assets | Shared UI test libraries |
| `MSTest.WinUI` / `.Uwp` | `buildTransitive` adapter, platform services, props and targets; `lib/<tfm>/_._`; exact dependency on the matching framework package plus the MTP packages | Test applications |

Why two packages rather than three: a separate `MSTest.TestAdapter.WinUI`
would have no consumer except the metapackage. The existing name keeps its
"test application" meaning, so this is not the rejected smaller alternative
(which made `MSTest.WinUI` framework-only). The pair mirrors
`MSTest` versus `MSTest.TestFramework`. Neither UI package has shipped, so no
compatibility shim is needed. Reconsider if an adapter-only consumer appears.

`MSTest.Sdk` helper libraries (`IsTestApplication!='true'`) receive
`MSTest.TestFramework.<UI>` with `PrivateAssets=all` and
`ExcludeAssets=runtime`. When source generation applies (non-`netstandard`
and either `EnableMSTestSourceGeneration=true` or `UseVSTest=false` with
`PublishAot=true`), they receive the application package instead, because the
generator ships with the adapter. Those checks are item conditions, not a
helper property: a property would evaluate before a later
`Directory.Build.targets` opt-in, which the first test run caught.
Classic UWP now resolves the UI DLL from `mstest.testframework.uwp`.

#### Validation

All from a local `.\build.cmd -pack` (Debug, MSTest `5.0.0-dev`, MTP
`3.0.0-dev`); pack exited 0 including NuGet package content validation.

Consumer probes with a fresh `NUGET_PACKAGES` cache; every build exited 0:

| Probe | Imported MSTest targets | Adapter in output | UI DLL runtime asset |
| --- | --- | --- | --- |
| Library referencing `MSTest.TestFramework.WinUI` / `.Uwp` | Analyzers, TestFramework | No | Present |
| `MSTest.Sdk` helper, WinUI / UWP | Analyzers, TestFramework | No | `_._` (runtime excluded) |
| Application referencing `MSTest.WinUI` | MTP plus `MSTest.WinUI` `buildTransitive` | `MSTest.TestAdapter.dll`, `MSTestAdapter.PlatformServices.dll` | Present |

Focused acceptance tests, 61 executed, 61 passed, exit 0:

```powershell
.\.dotnet\dotnet.exe run --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests --no-build -- --filter "FullyQualifiedName~WindowsApplicationModelPackageTests|FullyQualifiedName~MSTestSdk_Helper_KeepsOnlyRequiredCompileTimeComposition|FullyQualifiedName~CoreFrameworkAssemblyTests"
```

Behavior-removal check (mutation, not a pristine baseline): the helper
`PackageReference`/`PackageVersion` in `Sdk.targets` was switched back to the
application package, repacked, and the helper filter rerun. 8 executed,
6 passed, 2 failed, exit 2. The WinUI and UWP non-source-generation rows
expected `[MSTest.TestFramework.<UI>, MSTest.TestFramework]` and got an extra
`MSTest.<UI>/5.0.0-dev`. Restoring the change, repacking and rerunning the
full filter gave 61/61.

Not run: Bazel packaging and `Bazel.SdkPackages.UnitTests` (Bazel unavailable);
native classic/modern UWP and packaged/unpackaged WinUI activation; gated
app-model tests; `eng/experimental/Test-PackageSplit.ps1`; a framework-only row
in `PackagedAppIntegrationTests`. A release/servicing decision is still needed
before the first UI package release.

## Core packages only: 2026-11-20

Status: implemented on top of `be6de2f43`; supersedes the UI package IDs from
[#12450](https://github.com/microsoft/testfx/pull/12450) and the
[framework-only UI packages](#implemented-design-framework-only-ui-packages)
experiment.

### Problem and contract

The maintainer rejected both UI package designs and asked to go back to
`MSTest.TestFramework` and `MSTest.TestAdapter` only. Confirmed requirements:

- No `MSTest.WinUI`, `MSTest.Uwp` or other UI package IDs. Consumers reference
  the same two packages (or the `MSTest` metapackage, or `MSTest.Sdk`) for every
  application model.
- A shared UI test library references only `MSTest.TestFramework`, compiles
  `[UITestMethod]`, and does not become a test application.
- A WinUI or UWP test application gets the UI adapter build that dispatches
  `[UITestMethod]` to the UI thread.

Assumptions, not confirmed by the maintainer:

- The application model is read from MSBuild properties when targets execute,
  not when package targets are evaluated: `UseWinUI=true` selects WinUI;
  `UseUwpTools=true`, or `UseUwp=true` with `UseUwpTools` unset, selects modern
  UWP; a classic `uap10.0` target selects classic UWP. WinUI wins if a non-SDK
  consumer sets both. No opt-out property and no minimum target platform
  version gate are added.
- Classic UWP projects honor `buildTransitive`; `build/uap10.0` shims cover
  older PackageReference clients that only read `build`.
- Retiring `eng/experimental/Test-PackageSplit.ps1` with the split experiment is
  acceptable.

### Design

| Package | Ordinary assets | UI payload (not in `lib`) | Selected by |
| --- | --- | --- | --- |
| `MSTest.TestFramework` | `lib/{net462,netstandard2.0,net10.0,net11.0,uap10.0.16299}` | `buildTransitive/net10.0/{uwp,winui}/` (`MSTest.Uwp.dll`, `MSTest.WinUI.dll` with xml, pri and WinUI satellites) | Modern UI projects get compile and runtime assets according to the resolved core framework assets. For classic UWP, NuGet selects `lib/uap10.0.16299` directly. |
| `MSTest.TestAdapter` | `lib/{net462,net10.0,net11.0}` | `buildTransitive/net10.0/ui/` (UI adapter and platform services); `buildTransitive/uap10.0/` (classic UWP adapter, platform services and `_localization` satellites) | `_MSTestComputeUIAdapterSelection` decides; `_MSTestSelectUIAdapterAssets` then swaps the resolved `lib/net10.0` adapter DLLs for the `ui/` build in place after `ResolvePackageAssets` (and the publish equivalent); classic UWP references the `uap10.0` payload directly |

Classic UWP has a distinct TFM. Its `lib` group includes `MSTest.Uwp` alongside
the same core framework, type forwarders and satellites as `lib/netstandard2.0`:
NuGet selects one group rather than merging both. The minimum matches the
extension's build (`uap10.0.16299`); there is no classic framework reference target.

On Windows, `PackedMSTestTestFramework_ShipsUIFrameworksWithoutAdapter` failed
on the previous Debug pack (six required UAP entries missing) and passed after
`build.cmd -pack -bl`. All 17 files shared by the UAP and .NET Standard
groups were byte-identical. The SDK composition case resolved the UAP assemblies,
and `ClassicUwp_ConsumesUapAssets_AndRunsPlainAndUiTestsThroughMtp` launched
all three classic AppX consumers (SDK, metapackage, and explicit packages).
Each reported its plain test, UI-dispatcher test and four localization cases as passed.

Why not `lib` for modern UI: `MSTest.Uwp` and `MSTest.WinUI` both define
`UITestMethodAttribute` (CS0433 if both are referenced), and modern UWP and
WinUI share `net10.0-windows10.0.xxxxx` TFMs, so NuGet cannot pick one by
application model. The framework UI DLLs depend only on `MSTest.TestFramework`,
so a library that references `MSTest.TestFramework` alone gets them without
adapter assets or runner targets. The adapter swap keeps NuGet metadata, so the
copy-local policy, `deps.json` and publish still treat the files as
`MSTest.TestAdapter` assets; the satellites resolved from `lib/net10.0` are kept
because the UI build compiles the same resources.

Why execution time: `Microsoft.Common.targets` imports `nuget.g.targets`
(package `buildTransitive` targets) before `Directory.Build.targets`, so a
property group or item condition evaluated in a package targets file does not
see `UseWinUI`, `UseUwpTools` or `UseUwp` set in `Directory.Build.targets`. The
first implementation used evaluation-time conditions and silently gave such
projects the ordinary adapter and no UI framework; the `LateImport*` acceptance
rows reproduce that. Selection therefore happens in targets. Computed
conditions sit on inner item groups, not on the target, because a false target
condition also skips its `DependsOnTargets`.

Other consequences:

- `MSTest.Sdk` detects the application model from the same properties and
  injects the ordinary packages; the remaining SDK error is for selecting both
  UWP and WinUI. That error keeps `main`'s raw-property condition inside a
  target, so it also catches properties set in `Directory.Build.targets`. The
  unshipped `MSTestApplicationModel` property is removed. UWP and WinUI helper
  libraries get only `MSTest.TestFramework`, like other helpers; the private
  adapter reference is kept only for source generation.
- `MSTest.Sdk`'s `OutputType` and packaged-application defaults
  (`Sdk/Runner/Common.targets`) are still evaluation-time. A project using
  `MSTest.Sdk` must set the model properties in the project file or
  `Directory.Build.props`, or set `OutputType` and
  `EnableMicrosoftTestingExtensionsPackagedApp` itself; this is unchanged from
  `main`.
- The `MSTest` metapackage references `MSTest.TestAdapter` for every target,
  including `uap10.0.16299`.
- The classic UWP adapter dependency group adds the Retry and TrxReport packages
  that `MSTest.Uwp` previously carried, so classic UWP keeps its bootstrap.
- `src/MSTest/PlatformPackage`, the UI `PACKAGE.md` files and package shapes, and
  `eng/experimental/Test-PackageSplit.ps1` are deleted. The `MSTest.Uwp` and
  `MSTest.WinUI` projects remain as non-packable build inputs.

### Alternatives

| Option | Outcome |
| --- | --- |
| Combined `MSTest.WinUI` / `.Uwp` (framework plus adapter) | Rejected: every shared library becomes a runner. |
| Framework-only UI packages plus application packages | Implemented, validated, then discarded at the maintainer's request: four extra IDs. |
| TFM aliases | Rejected in the [options review](#ui-package-layout-options-review-2026-11-20). |
| UI DLLs in `lib` of the core packages | Not possible: CS0433 and one `lib` set per TFM. |

Reconsider if NuGet gains asset selection by MSBuild property, or if a consumer
needs a UI payload outside an SDK-style or classic UWP project.

### Validation

`PackedUiFrameworkHonorsAssetExclusions` publishes framework-only WinUI and modern UWP consumers.
With the unconditional hint-path reference (`8fc37d971`), all four direct and transitive
exclusion cases failed: compilation still saw the runtime-only UI DLL, or the compile-only
DLL appeared in the output. `_MSTestSelectUIFrameworkAssets` follows the core framework's
resolved compile and runtime items separately, including exclusions inherited via a project reference.
After `.\build.cmd -pack -bl` (exit 0), the Debug `net12.0` MSTest acceptance host ran
`--filter 'FullyQualifiedName~CoreFrameworkAssemblyTests' --report-trx`: 19 executed and
passed, including the four exclusion cases and `StoreLayoutWinUiRuntimeOnly`, which checks
the published DLL, PRI and culture satellite under the preserved package layout.

All runs are local on Windows with the repository SDK. They use the Debug
`5.0.0-dev` packs in `artifacts/packages/Debug/Shipping` and the acceptance host
`artifacts/bin/MSTest.Acceptance.IntegrationTests/Debug/net12.0`, with
`TESTFX_RUN_WINDOWS_APP_MODEL_TESTS=1`. The TRX files are kept in task-owned
session storage, not in the repository.

| Check | Source state | Result |
| --- | --- | --- |
| `.\eng\verify-nupkgs.ps1 -configuration Debug` | final packs | exit 0, "Successfully validated content and metadata of NuGet packages"; no baseline change (only targets content changed) |
| Red: the 14 rows of `CoreFrameworkAssemblyTests.PackedConsumersUseSharedCoreAndSelectedUiPayload` and `SdkTests.MSTestSdk_WinUIWithUwp_RejectsConflictingApplicationModels` | tests retained; packages packed from the first (evaluation-time) implementation | 14 executed, 11 passed, 3 failed at the intended assertions (see below) |
| Green: same 14 rows, same assertions | final packs | 14 executed, 14 passed |
| Broad: `WindowsApplicationModelPackageTests`, `CoreFrameworkAssemblyTests` and every `SdkTests` test | final packs | first run 179 executed, 167 passed, 12 failed; after the helper fix below, 179 executed, 179 passed |
| `.\eng\bazel.ps1 test //:sdk_package_tests` | final sources | exit 0, `PASSED` (cache hit on the current inputs; 14/14 cases when it last executed) |

Red failures, each in the targeted scenario:

- `WinUI conflicts with late UWP tooling`: `Assert.AreNotEqual(0, result.ExitCode)`
  at `SdkTests.cs:1621`. With the old evaluation-time condition, a WinUI project
  that sets `UseUwpTools` in `Directory.Build.targets` built successfully.
- `LateImportWinUI` and `LateImportModernUwp`: `Assert.IsTrue(File.Exists(...))`
  at `CoreFrameworkAssemblyTests.cs:152`, with the messages
  "buildTransitive/net10.0/ui/winui/MSTest.WinUI.dll was not copied to the build
  output" and the `ui/uwp/MSTest.Uwp.dll` equivalent.

The 12 failures in the first broad run were in a test helper introduced by
this change, not in the product. `AssertResolvedMSTestReferencesAsync` expected
ordinary adapter references directly under `lib/`, but they resolve from
`lib/<tfm>/` (for example `lib/net11.0/`). The helper now drops the TFM folder
for ordinary `lib` assets only; UI payload paths are still matched exactly.

Earlier evidence, from before selection moved to execution time:

- Six packed-consumer probes resolved the expected assets.
- Framework behavior-removal check: removing the hint-path `<Reference>` makes
  consumers fail with CS0234/CS0246.
- Adapter behavior-removal check: disabling the swap still builds; only the
  acceptance asset-path assertions catch it.
- When mutating restored package files, shut down the build server, disable
  node reuse (`-nodeReuse:false`, `MSBUILDDISABLENODEREUSE=1`,
  `DOTNET_CLI_USE_MSBUILD_SERVER=0`) and clean `bin`/`obj`. Reused nodes kept
  stale targets and once produced a false result.

These were not repeated after the move; the repacked acceptance runs above
cover the current design.

### Known gaps

- Bazel's native `MSTest.TestFramework` and `MSTest.TestAdapter` packages do
  not carry the UI payloads (see [Bazel](../bazel.md)). The adapter's common
  targets still select the `ui` build for WinUI and modern UWP projects, so such
  a project using the Bazel packages would reference files that are not in the
  package. This is inferred from the targets and was not executed.
- `project.assets.json` still lists the `lib/net10.0` adapter for UI projects;
  tools that read it instead of MSBuild items see the ordinary adapter.
- Linux packs omit the UI payloads because the UI projects build only on
  Windows.
- Classic UWP AppX `pri` merging through the hint-path reference, and native
  classic/modern UWP and packaged/unpackaged WinUI activation, are not executed
  here; the gated `WindowsApplicationModel` tests cover them in CI.

## PR #12578 CI repair: 2026-11-23

### Confirmed failures and decision

[Azure build 1693680](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1693680)
tested PR head `4d586f71f6e3242063648f4510eef91e18afd408` through merge
`b177b8983476232239c5e719a1fa36fcbcff7bfa`. Its two failing jobs are
PR-caused, not a transient restore/runner failure:

| Check | Cause | Action |
| --- | --- | --- |
| `microsoft.testfx` | Aggregate of the two failures below. | Fix both in this PR; a retry of the unchanged head is insufficient. |
| Windows Release | Log 167: both `PackedPackages_UseMatchingFrameworkAssetsAndRuntime` rows expected the common import, but Windows packages correctly select the `net10.0` wrapper. | Expect that exact wrapper on Windows and the common import for portable packs. Retain the exact adapter import, packed-binary equality, and requested-runtime assertions; omit the generated explicit MSTest using to exercise the wrapper's implicit-using forwarding. |
| Windows application-model acceptance | Log 102: `ModernUwp_Net10`, `ModernUwp_Net11`, and `ModernUwp_UnsupportedDeployment` failed after actual activation. Adapter satellites copied from `lib` replaced the neutral fallback under `fr-FR`. The unsupported-deployment TRX identifies the unexpected failed French-fallback case; application-owned English/French resources and UI dispatch passed. | Restore the previous adapter-only default policy in both copy-local and re-resolved publish assets. Leave native fallback assertions unchanged. |

The merge base `be6de2f432898fe8f5d624a2d364d65df0ef97cd` included MTP
adapter satellites only when `UseUwpTools != true` or
`SatelliteResourceLanguages` was nonempty. The earlier proposal to change
native UWP to French-by-default was not approved; package consolidation does
not authorize that behavior change. The shared policy now retains
`EnableMSTestV2CopyResources`, but suppresses adapter satellites for native
modern UWP MTP only when the language list is empty. Explicit selection,
ordinary MTP/VSTest, WinUI, framework resources, and application PRI resources
keep their existing contracts. No resource strings, CI configuration, or
public API changes are needed.

### Focused validation contract

The producer is the local Windows `MSTest.TestAdapter` / `MSTest.TestFramework`
pack, composed with the matching MTP and packaged-app controller packages on
this PR's `main` delivery line. This repairs an unmerged PR regression; no
servicing/backport is proposed. The PR owner retains rollout ownership.
No launcher handshake, cancellation, or multi-module protocol is changed.

| Requirement | Automated evidence |
| --- | --- |
| Default native fallback and unrelated app resources | Unchanged `ModernUwp_Net10`, `ModernUwp_Net11`, and `ModernUwp_UnsupportedDeployment`: real x64 AppX activation, exact result identities/outcomes, English fallback under French UI culture, and application English/French PRI lookup. Normal children pass all six tests with MSBuild exit 0; the negative child has exactly two deployment failures and MSBuild exit 1. |
| Explicit UWP language selection and publish bypass | Added `SatelliteResourceLanguages_ModernUwpDefault/French` and `RuntimeStorePublish_ModernUwpDefault/French` rows exercise the same policy with real packed managed consumers, checking both adapter satellite filenames and exact culture sets. These portable policy probes are not native UWP activation proof. |
| Ordinary consumers remain unchanged | Existing MTP/VSTest satellite-selection and runtime-store publish rows. |
| Correct framework import still forwards implicit usings | Both `PackedPackages_UseMatchingFrameworkAssetsAndRuntime` rows, retaining exact payload bytes and runtime-major assertions. |

Parallel-safety preflight: the acceptance assembly keeps method-level
parallelization (CPU-count workers). Each new row owns its generated asset
directory; the fixture provides a fresh per-run package cache. Build UI culture
is a child-process override, not a test-process mutation. The existing native
class exclusion and unique AppX identity/cleanup remain unchanged. No scheduling
or lock changes are required.

The interrupted attempt's `artifacts/TestResults/appmodel-ci-22ee5623`
artifacts were inspected: Debug pack succeeded with zero warnings/errors;
the outer TRX contains 27 executed/passed, zero failed/skipped, including all
three native rows. This is prior local evidence, not CI proof or execution
of the four newly added rows. Final paired validation is recorded below.

The first Release run executed 31 cases: 30 passed, and the new build-only
French policy probe found no satellites. Its generated `MSTest.Sdk` project
had `OutputType=Library` / `CopyLocalLockFileAssemblies=false`: UWP projects
own their output type, unlike ordinary SDK test applications. The two
portable build probes now explicitly use `OutputType=Exe` to exercise
application copy-local assets, as the publish probes already did. No
expectation or product behavior was weakened. All three native cases and
the explicit-French publish case already passed in that run.

### Executed paired validation

The corrected Release pack completed with exit 0, zero warnings and zero
errors. The isolated comparison at `artifacts\ci12578r` used production
from `4d586f71f6e3242063648f4510eef91e18afd408` with the final tests retained.
Its first pack encountered duplicate inherited `.globalconfig` inputs;
this was experiment setup, not behavioral red evidence. The two files
were byte-identical (SHA-256
`98BC48C6E58340572B44DEC62CC92DB5D650EB945C83372B3F408D58B40EAE50`).
Removing only the comparison's duplicate left the same ancestor analyzer
configuration effective. No diagnostic was disabled and no build/CI
configuration change is included in the repair. Repacking the comparison
then succeeded with exit 0, zero warnings and zero errors.

Revision binding:

- Corrected source: the same head plus `final-source.patch`, SHA-256
  `D0D563199B47CFF5B7A5C44079820381732827A52ED50DE4952CB1495EBF67E9`
  (production targets, package readme and both changed test files).
- Retained-test patch: SHA-256
  `F1901E739009E2168B53D27561D41F4E92F149B25649356DC9120D2FF4E47312`.
- Complete comparison patch, including duplicate-config removal: SHA-256
  `FA1101A8840D29817E1B87D1380AA42F2C570D375201E30B7286D41037B180BD`.
- `FrameworkTargetingTests.cs`, `SdkTests.cs`, and unchanged
  `ModernUwpTests.cs` were byte-identical between the paired runs. Protected
  source hashes were rechecked after the comparison.

Both runs used SDK `12.0.100-alpha.1.26562.108`, Release Windows packages,
the `net12.0` outer acceptance host, and distinct fresh NuGet caches.
Native activation used x64 `net10.0-windows10.0.26100.0` and
`net11.0-windows10.0.26100.0`; managed SDK policy probes used `net11.0`.
MSTest packages were `5.0.0-dev`, composed with MTP `3.0.0-dev`.

The exact common commands, run from each corresponding checkout, were:

```powershell
.\build.cmd -pack -c Release -bl

$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$filter = 'FullyQualifiedName~ModernUwpTests.ModernUwp_ConsumesUwpAssets|FullyQualifiedName~PackedPackages_UseMatchingFrameworkAssetsAndRuntime|FullyQualifiedName~SdkTests.RuntimeStorePublish_AppliesAdapterAssetPolicy|FullyQualifiedName~SdkTests.SatelliteResourceLanguages_FiltersAdapterResources'

.\.dotnet\dotnet.exe test --project $project -c Release --no-build `
  --list-tests text --filter $filter -p:UsingDotNetTest=true `
  -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= `
  "-bl:$results\discovery.binlog"

.\.dotnet\dotnet.exe test --project $project -c Release --no-build `
  --filter $filter -p:UsingDotNetTest=true `
  "-p:ArtifactsTestResultsDir=$results" "-bl:$results\test.binlog" `
  --show-test-results all
```

`$results` was the absolute `red-final` or `green-final` directory under
`artifacts\TestResults\ci-fix-12578-20261123` in the primary workspace.
Process-local settings were `DOTNET_ROOT=<primary-workspace>\.dotnet`
(also prepended to `PATH`), `TESTFX_RUN_WINDOWS_APP_MODEL_TESTS=1`,
`DOTNET_CLI_CONTEXT_VERBOSE=1`, and
`Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup=0`.
`TEMP`/`TMP` used the task-owned `artifacts\ci12578` directory;
`TESTFX_COLLECTION_NUGET_CACHE` selected separate marked `red-packages`
and `green-final-packages` directories beneath it. No shared cache was
cleared. Discovery used explicit `--list-tests text`; an earlier bare
`--list-tests` invocation failed argument parsing and is not execution evidence.

### Test-value evidence

| Contract / cases | Pre-fix production, final tests | Corrected production, same tests | Interpretation |
| --- | --- | --- | --- |
| Native UWP neutral fallback: three unchanged cases | All three failed. Child TRX showed French adapter messages instead of the expected English prefix; normal children had 5 passed/1 failed, and the negative child had 5 passed/3 failed. | All three passed. Normal children had 6 passed/0 failed; the negative child had 6 passed and exactly the two intended deployment failures. | **Executed: detected.** Actual AppX activation, not a launch/build failure; application French/English resources and UI dispatch passed in both states. |
| Default UWP copy-local and runtime-store publish: two new rows | Both failed exact culture-set assertions: expected no adapter satellites, actual all 13 cultures. | Both passed, with neither adapter nor platform-services satellites copied. | **Executed: detected.** The publish row exercises the SDK's re-resolved asset path. |
| Explicit French and ordinary MTP/VSTest controls: 24 rows | All passed. | All passed. | Preservation evidence, including exact `fr` selection for both adapter resource DLLs. |
| Framework targeting: two rows | Corrected tests passed against the original Windows packs. CI log 167 separately records the original incorrect common-import expectation. | Both passed, including implicit-using forwarding, exact packed assembly bytes and requested runtime major. | Test-repair/preservation evidence, not a production behavior-removal experiment. |

Both discoveries exited 0 and matched the independently prepared 31-case
plan. The comparison executed 31: **26 passed, 5 failed, 0 skipped**, exit 2.
The final run executed 31: **31 passed, 0 failed, 0 skipped**, exit 0.
The repository's `verify_execution.py` green gate independently confirmed
planned/selected/executed/passed counts of 31, fresh timestamps, unique
execution identities, and exit 0. Red failures were checked separately
against the five intended identities and their actual assertion messages.
No setup failure, skipped case, or expected negative child was counted as
regression detection.

For each variant, all 31 generated `project.assets.json` files resolved
through its own cache. Twelve consumed locally packed archives matched
the current `.nupkg` files byte-for-byte, and their NuGet metadata named
the corresponding Release Shipping feed. Each native layout's five MSTest
assemblies matched its resolved package inputs; framework French satellites
remained present while only adapter satellites changed.

| `MSTest.TestAdapter.5.0.0-dev.nupkg` | SHA-256 |
| --- | --- |
| Pre-fix comparison | `A3793D8D4F19FD1CBD45223746E7F44AE9030C59AB96D7116E54E60F1C0FEE1C` |
| Corrected | `47171484ACFEB7231B0D5B7D399B56E4BAE512B6D442E341F410DA520A7E60C5` |

Local evidence is retained under
`artifacts\TestResults\ci-fix-12578-20261123`: pack logs/binlogs and
source patches; per-run discovery, outer TRX/JUnit/CTRF, child outcome
records, package hashes, and `provenance.json`; and `consumer-evidence.zip`
archives containing generated projects, effective restore/import files,
native manifests, resolved-asset reports, child TRX and binlogs.
The first 30/31 setup experiment is retained separately under `green`;
only `green-final` represents the final passing run.
Cleanup verified all nine run-owned AppX identities unregistered, stopped
only the comparison's verified MSBuild-node PIDs, and removed the 93
generated consumers, comparison worktree, and owned caches/scratch.
The primary SDK and protected source files remained intact.

The final diff preserves method-level scheduling, the existing native-class
exclusion, per-row generated directories, and child-only build UI culture.
No additional serialization is needed. This is a package/resource-selection
repair, not a UI-layout change; the real child result/exception evidence is
the relevant demonstration. Explicit French was tested through packed
build/publish probes, not an additional native activation case. Linux/Bazel
packs and unrelated application-model cases were not rerun. These local
results do not replace fresh blocking CI checks on the published head.
