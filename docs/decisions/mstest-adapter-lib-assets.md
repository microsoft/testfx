# MSTest adapter library assets: experiment

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
