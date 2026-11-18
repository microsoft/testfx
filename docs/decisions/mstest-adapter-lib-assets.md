# MSTest adapter library assets: experiments and rewrite design

The November 12-13 sections preserve the earlier single-package experiments.
Their package-split exclusion and toolchain limitations describe those runs,
not the current proposal. The [November 18 design](#three-package-families-design-2026-11-18)
investigates separate component packages and compatibility metapackages, without
changing shipping defaults.

## Problem and contract

Investigate replacing `MSTest.TestAdapter`'s manually injected assemblies with
standard NuGet `lib/<tfm>` assets. The consumer must retain the correct adapter,
platform services, dependency resolution, localized resources, and test results.
This is a local prototype, not an approved release or package compatibility break.

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
regression guards; no shipping package migration has been applied.

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
followed by legacy VSTest/netfx and any supported special publish paths. These
follow-up findings revise the recommendation, not the shipping source.

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
| `.\build.cmd -pack -c Release -bl -projects src\Adapter\MSTest.TestAdapter\MSTest.TestAdapter.csproj /p:SkipUapTargetFramework=true` | Exit 1: compilation/pack produced the adapter, but the repository-wide verifier rejects missing unrelated packages and the omitted classic-UWP payload. |
| `.\.dotnet\dotnet.exe pack src\Adapter\MSTest.TestAdapter\MSTest.TestAdapter.csproj -c Release --no-build /p:SkipUapTargetFramework=true -bl:artifacts\log\Release\adapter-baseline-pack-only.binlog --verbosity quiet` | Exit 0; baseline package above. |
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

There is no shipping-product layout change. The samples' localized manifest
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
- Legacy consumers and downstream VSTest tests hard-code the existing package
  directories. No rollout, release-branch servicing, or downstream migration has
  been authorized.

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

## Three package families design: 2026-11-18

### Problem, scope and decision

Design baseline: `28a041287150a48ad48134672c472a481fecf492`.
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
[`eng/experimental/Test-PackageSplit.ps1`](../../eng/experimental/Test-PackageSplit.ps1).
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
