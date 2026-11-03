# Bazel build: research and decisions

## Problem and contract

Enable and validate a native Bazel alternative for TestFx on an experimental
branch, progressing from existing public examples to source libraries and tests.
Bazel must schedule compiler actions itself; a target that shells out to
`dotnet build`, `dotnet test`, or MSBuild is not a migrated build.

The existing Arcade/MSBuild build, package layout, CI, and release branches remain
unchanged until parity is demonstrated. This is a reversible development-build
experiment, not authorization to replace the shipping pipeline.

The initial producer/consumer contract is:

- Bazel owns dependency downloads, compilation, generated inputs, output
  directories, test launches, and action invalidation.
- `rules_dotnet` owns the compiler/toolchain and .NET runtime launchers.
  Tool versions and NuGet contents must be pinned and integrity checked.
- Public samples first consume their documented released package versions.
  Source-built samples must instead consume the Bazel-built TestFx graph, not
  prebuilt TestFx packages or assemblies from `artifacts/bin`.
- A passing test process returns zero. Test failures, an empty selection, and
  launcher failures must remain visible as nonzero exits. Bazel aggregates
  modules; one successful module must not hide a failing one.
- Existing MTP lifecycle, controller/child launch, discovery, filtering, report
  generation, and cancellation semantics are not intentionally changed.
- Bazel outputs are development artifacts, not officially signed or published
  replacement NuGets. Package/SDK/release parity requires separate shipping-layout
  validation.

## Research and experiments

### 2026-11-03: SDK/meta-package lane

This lane starts at `6bc33c48b9dae0e89486862f46af3c876fd7cf5d` and owns
`//:mstest_package` and `//:mstest_sdk_package`. The existing three-package
`//:packages` aggregate and validator remain unchanged. The coordinator owns
the combined guide, CI, dependency integration, and eventual rollout.

**Contract and sources.** `src/Package/MSTest.Sdk/MSTest.Sdk.csproj` explicitly
packs ten SDK props/targets, generates `Sdk.props` and `Runner.targets` from
their real templates, and suppresses both build output and package dependencies.
Its packaged-app project reference orders the shipping build; it does not embed
a controller or become an SDK NuGet dependency. The native package must retain
the actual conditional targets for classic execution, NativeAOT, VSTest, central
package management, test libraries, optional features, and Windows application
models without claiming that all those consumers have been validated.

`src/Package/MSTest/MSTest.csproj` packs empty `_._` files and dependencies, not
an assembly. For each portable framework its dependencies are the framework,
adapter, TRX reporter, Microsoft.NET.Test.Sdk, and CodeCoverage. The test SDK's
build assets must flow transitively (`PrivateAssets="none"`); other dependencies
retain `Build,Analyzers` exclusions. The
[4.5.2 published manifest](https://api.nuget.org/v3-flatcontainer/mstest/4.5.2/mstest.nuspec)
was inspected only as corroborating pack-contract evidence, not as a substitute
for native dependencies.

Version inputs remain `eng/Versions.props`, `Directory.Packages.props`, and
`Directory.Build.props`: MSTest `4.6.0-preview.bazel`, MTP
`2.6.0-preview.bazel`, and JUnit `1.0.0-preview.bazel` are separate families.
CodeCoverage, Fakes, Playwright, Aspire,
and Microsoft.NET.Test.Sdk keep their actual external pins. The producer uses
the explicitly selected Bazel SDK 11 RC3, not the unchanged repository SDK 12
pin. The portable meta-package scope is .NET 8/9/10/11; net462/UAP groups and
Windows/AOT execution remain unvalidated and are not enabled by writing an
archive.

**Alternatives and decision.** The pinned upstream `nuget_pack` rule requires
one compiled library or binary, which is inappropriate for these assembly-free
packages. Its
[public native packer](https://github.com/bazel-contrib/rules_dotnet/blob/add5c92b30888a70aa951de1744a770b8f8ef436/dotnet/private/tools/nuget_pack/BUILD.bazel)
accepts file-only JSON requests and already owns deterministic ZIP, OPC, nuspec,
and collision validation. Reuse that tool behind an area-owned rule and a small
template/metadata generator. Do not add a fake anchor assembly, implement a
second ZIP writer, invoke an MSBuild producer, or change shared BuildTools.cs.
The private request schema is pinned with the upstream commit and must be
rechecked on upgrades. Package descriptions, readmes, tags, license, and release
notes come from the repository; native artifacts must not acquire official
signing or servicing claims.

**Planned validation and ownership.** First build and inspect both archives and
exercise template/version/metadata rejection cases independently of other
lanes. Then integrate the coordinator's native framework/adapter/analyzer and
support-package commits. A fresh local feed/cache must supply every TestFx
package; repository feeds may supply external CodeCoverage/test-SDK dependencies
but must never conceal a missing native package. Real consumers will cover the
default SDK, explicit extension suppression, test-library and layered-SDK
evaluation, and the meta-package. Record actual restore/build/run exits,
dependency versions, loaded assembly hashes, discovery identities, passing and
intentionally failing tests, zero-result handling, and TRX content. Consumer
MSBuild is the product boundary under test, not a producer escape hatch.
No runtime lifecycle or scheduling changes are intended: MTP retains process
launch, handshake, cancellation, reporting, and teardown. Composed success is
pending the other lanes; archive/layout success alone does not satisfy it.

Test generation is focused on the new SDK package helper. Tests use per-case
temporary directories and child-process arguments, never process-global
environment mutation. Existing method-level scheduling is retained.

Implementation and initial experiments:

- `eng/bazel/sdk-packages.bzl` exposes both archives through `DefaultInfo` and
  `NuGetPackInfo`, without compiled assembly anchors. `SdkPackageTool.cs` reads
  the real project/property inputs, checks the declared asset/dependency
  contracts, generates the two templates outside the source tree, and feeds
  the pinned upstream packer. It fails on unknown template tokens, version
  ambiguity, feature-pin drift, and missing/changed package assets.
- The first cold toolchain build failed with disk exhaustion on the shared
  worktree drive before compilation. The lane-owned Bazel server was stopped;
  subsequent builds use a process-local `TESTFX_BAZEL_OUTPUT_ROOT` on a drive
  with space. Moving the cache across volumes failed on symbolic links;
  incomplete copied contents and the lane's rebuildable extracted cache were
  removed using ordinary file operations, without elevating privileges or
  touching another lane's cache. Source edits were preserved.
- A first package analysis rejected a glob for authored `.props` files because
  this SDK currently has only a generated `.props`. Empty glob alternatives
  are now allowed; the generator still enforces the full explicit project
  asset set, so missing required files cannot silently pass.
- `validate-sdk-packages.ps1 -LayoutOnly` is the independent gate: it inspects
  archive metadata and template/static bytes, then resolves the packaged SDK
  in an isolated cache and checks actual SDK evaluation. The default mode
  additionally requires the native framework/adapter/analyzer/telemetry/
  platform-MSBuild closure, builds real consumers, and executes them. It does
  not silently downgrade a failed default consumer to the `None` profile.
  The only public-feed exception to `Microsoft.Testing.*` source mapping is
  the exact external `Microsoft.Testing.Extensions.CodeCoverage` ID.
- A metadata-only read of the pinned CodeCoverage `18.13.0-preview.26527.4`
  [manifest on test-tools](https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/438ed41f-de8e-41cb-9183-cc6c25081f52/nuget/v3/flat2/microsoft.testing.extensions.codecoverage/18.13.0-preview.26527.4/microsoft.testing.extensions.codecoverage.nuspec)
  found a minimum MTP dependency of `2.6.0-preview.26526.14` in both its
  `net10.0` and `.NETStandard2.0` groups. `2.6.0-bazel` sorts below that
  minimum. The unchanged default SDK/meta-package therefore has a version-range
  blocker even after the native closure is available. This has been raised
  with the coordinator for a common experimental-version decision. Do not
  hide it with a released MTP, an older CodeCoverage pin, a downgrade-warning
  suppression, or a silent switch to the `None` profile.
- Native compilation was paused before completion at the coordinator's
  shared-host resource gate; the lane's own Bazel server was shut down.
  Static inspection confirmed ten explicit SDK assets and nine version
  substitutions, and the PowerShell parser and Git whitespace check passed.
  These are not archive, executed-test, or composed-consumer success claims.
- The coordinator chose the common `-preview.bazel` suffix after checking
  NuGet.Versioning ordering: `2.6.0-preview.bazel` is above the pinned
  CodeCoverage minimum but below stable `2.6.0`. The SDK/meta generator,
  focused expectations, and consumer validator now use that suffix without
  changing numeric product prefixes or external pins. The coordinator owns
  the matching shared BuildTools and MTP package change. This resolves the
  version-range design issue; actual binary/API compatibility still requires
  the native composed consumer run.
- Consumer SDK isolation removes named inherited MSBuild variables from each
  `ProcessStartInfo.Environment` dictionary rather than setting them to empty
  strings. This includes `MSBUILD_EXE_PATH`, as well as SDKs/extensions paths.
  The parent process environment is unchanged, and each child starts in its
  own consumer directory beside its pinned `global.json`.
- At `9e1604807cacd4f704b4e416137a8ad106513634`, the resumed command
  `.\eng\bazel.ps1 build //:mstest_sdk_package //:mstest_package --jobs=2 --noshow_progress --show_result=0`
  completed with exit 0 and eight actions. The isolated
  `.\eng\bazel\validate-sdk-packages.ps1 -LayoutOnly -Jobs 2` invocation
  (with an explicit run-owned `-ArtifactsRoot` on the available drive)
  also exited 0. It verified both complete payloads, dependency groups,
  unchanged static assets/readmes, all generated template text, and the SDK
  archive actually restored into the fresh cache.
- That run evaluated eight real SDK consumers: default profile, `None`,
  explicit extension disabling, test library, layered Web SDK, central package
  management, VSTest precedence over AOT, and opt-in source generation. All
  eight selected the expected references and the pinned SDK/MSBuild paths;
  the invalid-profile target returned its expected exit 1 and diagnostic.
  This is evaluation evidence, not AOT/Windows/source-generator execution.
  `validation.json` explicitly records `ConsumersPassed=false`.
  The retained run is `bazel-sdk-validation/20261103-141504-951f465d`.
  Native archive SHA-256 values are
  `1C2164B473C7E0128E7ABF3639485336E99FB4D6E4245A62A750874D2CC197AD`
  (SDK) and
  `BBC675E5100C12D7672A3C0BD1D696A2DA76A181163844BACEE1EE2848EA6195`
  (meta-package), both at `4.6.0-preview.bazel`.
- The final independent harness also checks AOT **reference selection only**
  and caller-supplied version overrides, bringing the evaluated configurations
  to ten. Each selected `PackageReference` or central `PackageVersion` is
  checked against the correct product/external version. `-EvidenceRoot` is
  aligned with the shared validators; `-ArtifactsRoot` remains an alias.
  Relative roots and `-Jobs 0` fail before execution. The retained successful
  run `bazel-sdk-validation/20261103-145104-041a6740` includes `actions.json`:
  two native input generators, two native packers, and no MSBuild executable
  in the producer graph. Both complete archives and all ten evaluations
  passed again, including the expected invalid-profile rejection.

#### Composed consumer evidence: SDK/meta-package lane

The native core and all six framework/adapter package checkpoints through
`ba725abc5cb28aaa36ec789ab50891f5e65b0b6e` are integrated at
`133c1785583d89399531b7bd7f4a1923f9c3d13d`, along with the shared version and
analyzer prerequisites. All ten archives build and pass the SDK harness's
layout, version and native-action checks. Consumer isolation now explicitly
uses its own NuGet.config and clears inherited fallback package folders.

The first full default-SDK build exposed a real remaining boundary, not a
restore-source issue. In
`bazel-sdk-validation/20261103-152228-1bdaf495/sdk-default-net8.0.binlog`,
the generated CodeCoverage registration fails with CS0012. The external,
unchanged CodeCoverage package requests `ITestApplicationBuilder` from
`Microsoft.Testing.Platform, Version=2.6.0.0, PublicKeyToken=b03f5f7f11d50a3a`.
Binlog reference provenance records the selected native
`2.6.0-preview.bazel` compile/runtime asset in the fresh cache, with
`PublicKeyToken=null`. The coordinator owns the shared native identity
correction. No released MTP, coverage downgrade, warning suppression, or
default-profile success is implied.

Separate checks progressed without concealing that failure:

- The explicit SDK `None` profile builds for net11.0, discovers both fixture
  methods, verifies loaded native framework/MTP hashes, and returns 0/2/9 for
  `NativePackagePass`, `NativePackageFailure`, and an empty selection. Its
  restored graph contains neither the CodeCoverage nor TRX report extension;
  all seven native archives in that graph match the freshly built feed.
- The default meta-package builds for net8.0/net9.0/net10.0/net11.0. Its
  actual net11.0 VSTest `dotnet test` consumer discovers both methods and
  executes one passing case (exit 0) and one intentional assertion failure
  (exit 1), with the corresponding single-result TRX files and zero skips.
  All nine native archives in its graph match the isolated feed.
- These successful scopes are deliberately separate from the default SDK.
  The run's `validation.json` remains failed; independent command logs,
  binlogs, `independent-consumer-provenance.json`, and meta-package TRX files
  preserve the narrower results.

Two harness assumptions were corrected against actual source/output, not by
changing product behavior: the framework assembly is named
`MSTest.TestFramework` in its authoritative project, and the platform's
unconditional `--coverage-threshold-*` options do not mean the optional
`--coverage` collector is registered. Help checks now match complete option
tokens instead of prefixes.

#### Final SDK/meta-package composed validation

The coordinator's public-signing checkpoint
`aff2cf0f702a07779cee99a8acf29cf515e858bb`, with alias prerequisite
`797eb6f29721389a9178d42df726bce6155b9e42`, is integrated as
`3dcde1f7db1b2e85db75dadd4a8e5b157685bbf0` and
`80b2380cc9d9065f98af2755b5d555d4561a9fca`. It supplies the repository's
public-only Microsoft identity and proper friend/satellite identities, not
private-key signing or official release provenance. The native net8.0 and
net11.0 MTP files now report the required
`PublicKeyToken=b03f5f7f11d50a3a`.

At the exact integrated head
`3dcde1f7db1b2e85db75dadd4a8e5b157685bbf0`, the original full command
`.\eng\bazel\validate-sdk-packages.ps1 -EvidenceRoot <run-owned absolute path> -Jobs 2`
completed with exit **0**, using the lane's isolated C: Bazel output root.
It rebuilt all ten native archives and used a new feed/cache; it did not reuse
the unsigned consumer cache or alter the default SDK's pinned CodeCoverage
reference. The full commands, binaries, package SHA-256 values, selected SDK,
native action graph, resolved assets and run outcomes are retained in
`bazel-sdk-validation/20261103-161231-02a3781e`.

| Contract | Executed result |
| --- | --- |
| Native packages and SDK evaluation | Ten packages, ten evaluated configurations, exact SDK/meta layouts and dependency versions; expected invalid-profile diagnostic. |
| Unmodified default MSTest.Sdk | Builds for net8.0/net9.0/net10.0/net11.0 with CodeCoverage and TRX enabled; net11.0 discovery and native loaded-hash checks pass. |
| Explicit SDK `None` profile | net11.0 build/discovery succeeds; collector/report options remain absent, without confusing core coverage-threshold options with the collector. |
| SDK runtime outcomes | Both default and `None` execute `NativePackagePass` / `NativePackageFailure` / empty selection with exits 0 / 2 / 9. Default-profile pass/failure TRX files have exactly one executed case and no skips. |
| Native SDK `dotnet test` | The real SDK 11 driver executes `NativePackagePass`, exits 0, and writes a single passing TRX result. |
| Default MSTest meta-package | Builds for all four TFMs; net11.0 VSTest executes one pass (parent 0) and one intentional failure (parent 1), with exact TRX outcomes and no skips. |
| Provenance and isolation | Every resolved native archive matches the newly staged feed; loaded framework/MTP hashes and Bazel metadata match; external coverage is the unchanged production pin. |

The final `validation.json` records `Passed=true`, `LayoutPassed=true`,
`ConsumersPassed=true`, **40** completed steps and **zero** unexpected exits.
The two failed TRX cases are intentional negative fixtures asserted by the
successful harness, not unhandled failures. This closes the observed default
SDK/CodeCoverage identity boundary; the earlier CS0012 is retained as integration
failure evidence, not counted as behavioral mutation proof.

This lane's runtime evidence is net11.0; its other three SDK/meta TFMs are
compiled-consumer evidence. Windows-qualified/UWP/.NET Framework execution,
NativeAOT execution, repository SDK 12 parity, official signing/publishing, CI,
release-branch certification and downstream servicing remain outside this
delivery. Conditional assets are preserved without advertising those gates as
passed.

#### Test-value evidence: SDK/meta-package lane

At `78b990d9986486f1c3755fc3a9f38ddaceab87c6` (the shared version-helper
prerequisite integrated, with only this documentation update pending),
`.\eng\bazel.ps1 test //:sdk_package_tests //:build_tools_tests --jobs=2 --noshow_progress --show_result=0 --test_output=errors`
exited 0. The actual TRX files reported 12/12 SDK helper cases and 13/13 existing
build-tool cases passed, with zero failures or skips. Existing method-level
scheduling remained enabled.

The same SDK test DLL then ran against an isolated, natively compiled mutation:
only the `MicrosoftTestingPlatformVersion` substitution read MSTest's
`VersionPrefix` instead of `TestingPlatformVersionPrefix`. The compiler and
arguments came from the real Bazel action; only the mutant source/output paths
changed. The original helper and test assembly hashes were unchanged afterward.
This is a compiling behavior-removal comparison, not a pristine-baseline run.

| Contract / exact test | Counterfactual | Without correct behavior | Correct implementation |
| --- | --- | --- | --- |
| SDK component versions / `Generate_SdkPreservesTemplatesAndComponentVersions` | Use the MSTest prefix for MTP | Executed: detected the wrong `7.8.9-preview.bazel` instead of `2.3.4-preview.bazel` in generated SDK XML | Passed before and after |
| Meta-package dependency versions / `Generate_MetaPackagePreservesDependencyAndAssetFlow` | Same substitution | Executed: detected the wrong TRX dependency version | Passed before and after |

Every paired run used
`--filter FullyQualifiedName~SdkPackageTests --minimum-expected-tests 12 --report-trx --report-trx-filename tests.trx`
with its own results directory. Counts were respectively 12 passed / exit 0,
10 passed + 2 intended assertion failures / exit 2, and 12 passed / exit 0;
all three executed 12 cases and skipped zero. The ten
`Generate_RejectsContractDriftBeforeWritingOutputs` cases remained green.
Mutation patch SHA-256:
`123F7CECD92BAC1A07052F9956F89CFB2F818D047C7B1CFED9CF352A05403EB5`.
The retained `sdk-counterfactual` artifacts contain the patch, original native
compiler arguments, discovery, all three TRX reports, and `comparison.json`.
An initial launcher probe used the wrong runfiles working directory, and a
repeat encountered a copied read-only sidecar; these setup failures are not
counted as regression evidence. The successful comparison used the real test
runfiles root and task-owned copies.

### 2026-11-03: experimental version compatibility and constrained storage

The repository's actual production CodeCoverage pin is
`18.13.0-preview.26527.4`. Its package requires
`Microsoft.Testing.Platform >= 2.6.0-preview.26526.14`; the original native
`2.6.0-bazel` identity sorts below that requirement. Downgrading the external
dependency, suppressing restore errors, or accepting a released platform from a
remote feed would not validate the native product.

Native package and assembly informational versions now use the common suffix
`-preview.bazel`, keeping numeric product/assembly versions unchanged. The real
NuGet.Versioning comparer verified that `2.6.0-preview.bazel` is above the
required numbered preview but below stable `2.6.0`. This avoids an official-looking
release identity or invented build number. Actual native helper compilation and
both product-version commands, informational metadata, and missing/duplicate
property rejection passed; integrated consumer API compatibility still needs its
real package handoffs.

Seven concurrent worktrees exhausted Q: during bootstrap. Only lane-owned caches
were stopped/removed; build outputs and large restore/evidence directories moved
to separate C: locations through process-local overrides. The shared validators
now accept positive `-Jobs` and an absolute `-EvidenceRoot`, preserving default
behavior and test-worker settings. Parameter binding was checked independently
without launching another heavy build. Compiler work is released in small waves
while source/layout work remains parallel.

### 2026-11-03: C# reference aliases for the parallel test lane

The remaining test projects require `global,PlatformMSBuild`,
`global,serverclient`, and named-only `ghactions` references. The pinned C# rules
have no alias attribute. Adding an extra compiler option while leaving the
ordinary global reference intact would not preserve named-only semantics.

The parent owns an additive `reference-aliases.patch`, layered after the existing
compatibility patch. The planned interface is a C# `reference_aliases` dictionary
from assembly simple name to alias list; the list replaces implicit `global`,
which must be included explicitly when both access paths are needed. Reference
providers and runtime identities remain unchanged, so aliases are local to the
consuming compilation. All implementation/reference-only passes must receive the
same aliases. Unknown assemblies and empty/duplicate alias declarations must fail
explicitly rather than being ignored.

Validation will compile and execute real assemblies with colliding type names,
including friend-only members, using global-plus-named and named-only references.
Unrelated C#/F# targets must retain the existing no-alias path. The test lane will
receive the validated commit/interface before wiring its real legacy tests.

Executed conformance now passes: `GlobalAndNamedAliasesReferToTheSameAssembly`,
`NamedOnlyReferenceDoesNotMakeTheGlobalTypeAmbiguous`, and
`AliasesPreserveFriendAssemblyAccess`, with all compiler passes enabled. The
unchanged-alias path also passed all 13 BuildTools cases with the new shared
package versions. Unknown, empty, and duplicate alias configurations were each
executed through Bazel and rejected with their exact intended diagnostics.
The first attempt used one comma-joined compiler alias argument, which Roslyn
rejected with CS1679; the corrected implementation emits one reference switch per
alias, matching the compiler's actual contract. No test assertions were weakened.
These are build/behavior conformance results, not a claim of a full mutation
campaign. Logs and TRX files are retained in the parent session's
`files/reference-aliases` evidence directory.

### Native MSTest.Analyzers package lane

Starting revision: `6bc33c48b9dae0e89486862f46af3c876fd7cf5d`.
The producer is the native `//:analyzers` and `//:codefixes` netstandard2.0
assemblies, their XLF-derived satellites, and `//:globalconfigs_generator`.
`//:analyzers_package` exposes one experimental `MSTest.Analyzers` NuGet,
using `VersionPrefix` from `eng/Versions.props` with `-preview.bazel`, plus
`NuGetPackInfo` for the framework/adapter and SDK package lanes.
The coordinator subsequently changed the final experimental suffix to
`-preview.bazel`: the original `-bazel` sorts below a required MTP dependency
minimum in the combined package graph. This lane consumes that shared version
helper correction rather than maintaining a different version policy.

The source package project is the layout contract: analyzers in both
`analyzers/dotnet/cs` and `analyzers/dotnet/vb`, code fixes in C# only, the
corresponding culture subdirectories, four generated globalconfigs, the
unmodified `buildTransitive` and `tools` assets, and `PACKAGE.md`. No assembly
belongs under `lib` or `ref`; neither Roslyn nor its dependencies may flow to
consumers. `developmentDependency` remains true. Compilation keeps the existing
Roslyn 3.11.0 API references rather than linking the SDK compiler's newer APIs.

The simplest upstream `nuget_pack(library=...)` is not the intended contract:
the existing native assemblies are ordinary libraries for unit-test consumption,
and automatic library layout/dependency traversal would ship the wrong assets.
Changing those assemblies to analyzer-only targets would change existing
consumers. The chosen lane-local rule reuses the pinned native JSON NuGet packer
and its provider with explicit entries and empty dependency groups. This avoids
an MSBuild wrapper, a second ZIP writer, and changes to shared `packages.bzl`,
`BuildTools.cs`, or the compatibility patch. This intentionally depends on the
pinned packer/provider contract and must be rechecked when upgrading rules.

The existing globalconfig generator expects an absolute `OUTPUT_PATH` and an
analyzer DLL beside its output. A Bazel-only initializer will normalize the
declared action-relative path; a declared symlink supplies the existing native
DLL. The generator's source, analysis-mode policy, and error propagation stay
unchanged.

Validation will use separate run-owned feeds/caches and real C# and VB compiler
consumers: default/recommended/all/none modes, the missing-test-class diagnostic,
successful corrected input, effective props/targets and analyzer selection,
exact package contents, compiler API references, satellite lookup, and a code
fix loaded by the Roslyn 3.11 host. A repacked isolated mutant that suppresses a
required diagnostic will test the same consumer assertion without altering the
tests. MSBuild is permitted only for external package consumers, never for the
producer. Process exit codes, diagnostics, package hashes, resolved asset paths,
commands, and source states will be retained.

The validation script owns its child processes, unique artifact paths and
process-local SDK/cache overrides, restoring overrides in `finally`. It does
not change test-assembly scheduling, machine settings, or shared caches. No
new handshake, cancellation protocol, test-result aggregation, or runtime
fallback is introduced by this compiler-plugin package. Publishing, signing,
release-branch validation, and servicing remain outside this experimental lane;
the migration coordinator owns combined integration and downstream rollout.

Executed research and infrastructure observations:

- Inspected the released `MSTest.Analyzers` 4.5.2 archive: the install and
  uninstall scripts belong under `tools/`, not at the package root. They and
  the build-transitive assets are copied without rewriting the signed scripts.
- The initial two-worker native compilation failed because Q: had no free
  space, not because an analyzer failed to compile. The lane-owned output base
  was removed with `.\eng\bazel.ps1 clean --expunge`; no shared cache or other
  checkout was removed. Subsequent commands use the process-local override
  `$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfba\6a8043a5c4a6'`. The validation script
  also puts its unique restore cache under that configured root when present.
- The new validation harness parsed successfully, C# helper BOMs were checked,
  and `git diff --check` passed. These are setup checks, not package-consumer
  or regression-sensitivity evidence.
- The native package built successfully in 142 actions. The initial
  `4.6.0-bazel` archive has 55 entries and SHA-256
  `E9E77ADA2F44098D2E783FE7DAE3017D82FB398D4EB03CA3858B6558C29D1FE1`.
  All 12 C#/VB compiler cases passed: the four analysis modes, corrected input,
  and intentionally disabled build analysis, with package/cache/SDK and selected
  analyzer/config provenance checked.
- Consumer setup caught two isolation defects in the new harness. SDK lookup
  uses the working directory, not merely `--project`, so dotnet runs from the
  generated consumer root. PowerShell's method binding turned a `$null`
  environment-variable argument into a present empty value; the restore binlog
  showed empty `MSBuildSDKsPath`/`MSBuildExtensionsPath`. Named `Env:` entries
  are now removed and restored exactly. A later assets inspection found the
  machine's Visual Studio fallback package folder; an explicit restore config
  and cleared fallback folders retain the single-cache assertion.
- The old-Roslyn host compiled, then its real package load failed the expected
  `.NETStandard,Version=v2.0` attribute assertion (actual `null`, process exit
  `-532462766`). The native compile configuration alone did not emit the SDK's
  assembly attribute. A lane-local linked `AnalyzerAssemblyInfo.cs` restores
  that metadata for the analyzer and code-fix DLLs without changing shared
  build helpers or their compiler API references. The unchanged host source
  has SHA-256
  `963B99DA9CE77E318BC0831B215437843E60BAE62AC1185288A59C0D7C4802C0`;
  that first probe subsequently caught one incorrect whitespace expectation in
  the harness's normalized source literal, which was corrected without changing
  the code fix.
- `.\eng\bazel\validate-analyzers-package.ps1` then passed all 15 cases on
  Windows x64 / SDK 11.0.100-rc.3.26522.148, before the common version correction.
  The archive SHA-256 is
  `C0094E399DB33DC32CBE4AC88E258C6534F34D70F5ACCA80BBC1AEE037DB7E01`.
  `roslyn-3.11-codefix-and-satellites` loaded 83 analyzer and 52 code-fix types,
  executed MSTEST0030, applied `AddTestClassFixer`, checked the exact resulting
  source and disappearance of the diagnostic, and verified French analyzer and
  code-fix resources against the XLF catalogs. The producer action graph contains
  native compilation, globalconfig generation and NuGet packing, not MSBuild.

#### Analyzer metadata test-value evidence

The final host source SHA-256 is
`1C6960A00A023EA3D382F44B44AD4966934D56C87A31F1974ECAD96C60199740`.
The same compiled host, input document and assertions ran against both retained,
native-built `4.6.0-bazel` package snapshots; only the loaded package directory
changed. This is a package-snapshot comparison, not a pristine-baseline Git run:
the Bazel packaging feature exists in both variants, and the defective variant
lacks the analyzer/code-fix target-framework metadata.

| Contract / case | Before attribute | Corrected package | Evidence |
| --- | --- | --- | --- |
| netstandard analyzer identity / `roslyn-3.11-codefix-and-satellites` host | Executed: detected; expected `.NETStandard,Version=v2.0`, actual `null`; child exit `-532462766`. | Executed: passed before and after comparison; child exit 0. | Package hashes above, unchanged host hash, `before-attribute.json` / `after-attribute.json` and logs. |

Evidence is retained under
`artifacts/bazel-analyzer-validation/20261103-141156-7f50da86`: `validation.json`
lists all 15 case outcomes, exact compiler/restore/host commands and exits,
package entries/hash, resolved package paths and old-Roslyn results. Every C#/VB
case also retains its SARIF, selected analyzers/globalconfig, SDK provenance and
binlog. The standalone host invocation is the recorded native `dotnet.exe`,
`consumer/legacy/bin/Release/net11.0/LegacyHost.dll`, the selected extracted package
directory and `MissingTestClass.cs`, in that order. A text transcript is more
useful than a staged IDE screenshot for this compiler/package boundary.

The shared `-preview.bazel` prerequisite
`2d79cf530332c91d54e62f95f37b85c784984209` was applied after this checkpoint,
preserving both lanes' decision-record sections. The final validator also checks
both loaded analyzer/code-fix informational versions against the package version,
and supports an absolute `-EvidenceRoot` like the shared validators.
Final-identity validation is recorded below. Combined shipping layouts, release signing, Linux,
IDE UI integration and downstream release/servicing remain coordinator-owned
gates; this evidence does not claim them.

#### Final analyzer package identity validation

The corrected package is `MSTest.Analyzers` **4.6.0-preview.bazel**. Both loaded
DLLs report the same informational version, retain their netstandard2.0 identity
and reference Roslyn 3.11.0.0. The final package has 55 entries and SHA-256
`F45295E95D041CF2256F906BEA63B9D5125DFD0019F64B6DD661E605BEE074AA`.

Executed from this lane's worktree:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfba\6a8043a5c4a6'
.\eng\bazel\validate-analyzers-package.ps1 -EvidenceRoot 'C:\tfba\6a8043a5c4a6\analyzer-evidence'
```

Exit **0**, all **15** checks passed. Evidence:
`C:\tfba\6a8043a5c4a6\analyzer-evidence\20261103-142351-14e2448a`.
`validation.json` records the package hash and loaded DLL paths/versions,
commands, child exits and case outcomes; SARIF and binlogs establish compiler
behavior and SDK selection. This run used the shared prerequisite applied as
`ef4ae3790425d5472d7f9e8bec4b977ff781ce24`, with the lane's final validator and
probe changes. Relative evidence roots and `-Jobs 0` were also rejected before
build/restore execution.

| Requirement | Evidence |
| --- | --- |
| Exact C#/VB package layout, resources, generated configs, assets and no dependency leakage | `package-layout-and-private-dependencies`: exact entries plus native/source payload hashes; fresh-cache restore checks. |
| Enabled, suppressed and corrected compiler behavior | `cs-default-missing-class`, `cs-recommended-missing-class`, `cs-all-missing-class`, `cs-none-missing-class`, `cs-default-fixed`, `cs-default-disabled`, and their six `vb-` counterparts: exact MSTEST0030 count, exit and selected assets. |
| Legacy compiler APIs, code-fix execution and localized resources | `roslyn-3.11-codefix-and-satellites`: Roslyn 3.11 host, 83 analyzer/52 fixer types loadable, actual diagnostic/fix, exact resulting source, French strings and package-aligned versions. |
| Native producer, not an MSBuild wrapper | `native-producer-action-graph`: compiler, generator and NuGet actions inspected; MSBuild remains exclusively in the external consumers. |

`bazel query 'labels(srcs, //:packages)'` still returned exactly
`//:platform_package`, `//:trx_abstractions_package`, and `//:trx_package`.
The new target's queried `DefaultInfo` contains one `.nupkg`; its
`NuGetPackInfo` exposes `package_id="MSTest.Analyzers"`, `version=None`,
`version_file=analyzers_package/version.txt`, `target_frameworks=["netstandard2.0"]`,
and the two bundled analyzer/code-fix assembly names. Framework/adapter and SDK
packages can consume that provider without inventing versions or dependencies.

#### Generated-config counterfactual

Revision `6f69c9d135c542f6c1f3bacf4e003da66506e9f3` was tested without source
changes. A separate native packer request copied the correct package inputs,
changing only `dotnet_diagnostic.MSTEST0030.severity = warning` to `none` in
`globalconfigs/mstest-default.globalconfig`. The original action inputs and
workspace files were not edited. The already-built upstream NuGet packer
executed successfully (exit 0); a byte comparison of all 55 archive entries
confirmed that only this one globalconfig differed.

Mutation package SHA-256:
`FEB7BDCACAD3308BBF54F05C1ECC7F272D843A532D289395B48E05629C87BDE2`.
Original/mutant config SHA-256:
`2DCA22D15DC71132E7D03709B2114A05B613B4A306D1F577F9B8278CBC68A9FC` /
`FC7FF585809806B9B95606D156BE4BF8E78FCB002F4CF567852F445720F2F931`.
The native request, exact packer path, input paths, mutation description and
exit are retained in `request.json` / `mutation.json` under
`C:\tfba\6a8043a5c4a6\analyzer-counterfactual\76facf33252d4f6f93cdc6eb80a32a72`.

The unchanged validation script was run against that package:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfba\6a8043a5c4a6'
.\eng\bazel\validate-analyzers-package.ps1 `
    -EvidenceRoot 'C:\tfba\6a8043a5c4a6\analyzer-counterfactual-evidence' `
    -PackagePath 'C:\tfba\6a8043a5c4a6\analyzer-counterfactual\76facf33252d4f6f93cdc6eb80a32a72\MSTest.Analyzers.4.6.0-preview.bazel.nupkg'
```

| Contract / case | Without required behavior | Correct package | Evidence |
| --- | --- | --- | --- |
| Packaged Default analysis / `cs-default-missing-class` | Executed: detected. Compiler completed successfully, exit 0, with zero MSTEST0030 diagnostics; the outer assertion expected exit 1 and failed. | Executed: passed before and after mutation, compiler exit 1 with exactly one MSTEST0030 error; outer validation exit 0. | Same script/fixture/assertions; fresh feeds/caches; package hashes and SARIF. |

This is a deliberate behavior-removal comparison, not a missing-tool, restore,
compile-setup, or missing-symbol failure. The mutant's structural check passed,
then the intended behavioral assertion failed: outer exit **1**, two checks
executed (one passed, one failed). Fail-fast left the other 13 checks unexecuted;
they are not counted as passing, failing, or paired mutation evidence.
Red evidence:
`C:\tfba\6a8043a5c4a6\analyzer-counterfactual-evidence\20261103-143726-14dd2d2b`.

The original command was then rerun with
`-EvidenceRoot 'C:\tfba\6a8043a5c4a6\analyzer-final-evidence'`, without
`-PackagePath`. Exit **0**, all **15** checks passed on the unchanged revision
and the original `F45295E9...` package.
Final clean-revision evidence:
`C:\tfba\6a8043a5c4a6\analyzer-final-evidence\20261103-143911-b8403951`.
Only documentation was added after this run.

#### Signed analyzer compiler-host refresh

The coordinator requested the same 15-case analyzer gate after the shared
public-signing checkpoint `aff2cf0f702a07779cee99a8acf29cf515e858bb`, alias/signing
fixture prerequisite `797eb6f29721389a9178d42df726bce6155b9e42`, and source-root
mapping patch `adeb20ba67eefec9464be5561eecfbe03d346963`.
Their lane-local cherry-picks end at
`05ba1d3e10c5e504a96f5f9c9bd907bee80037e3`. Conflict resolution retains the
shared public key, compiler options, friend identities and exact patch files;
the unrelated absent SDK, compatibility and expanded-test declarations remain
absent. Parent-owned decision sections are historical evidence, not claims
that those other lanes were built in this checkout.

The original `analyzer-packages.bzl`, `validate-analyzers-package.ps1` and
`AnalyzerPackageConsumer.cs` remain byte-for-byte unchanged from the unsigned
checkpoint. All package-layout, compiler-mode, selected-asset, Roslyn 3.11,
code-fix and resource assertions remain enabled. New native outputs will be
packed into a fresh feed and restored into a unique cache using the existing
validator. The preserved unsigned feed still matches SHA-256
`F45295E95D041CF2256F906BEA63B9D5125DFD0019F64B6DD661E605BEE074AA`.

The new producer contract is public-only development signing with the pinned
160-byte Microsoft public key, not official signing or publication. In addition
to the unchanged gate, inspect the actual packed main/satellite DLL identities
and strong-name signature regions to distinguish public signing from unsigned
outputs or private-key signature completion. Keep the package prerelease
identity and unchanged dependency suppression. Source-root mapping is a shared
compiler-input change, not a reason to alter consumer assertions.

The focused signed run passed on clean source
`66620597c6357e3e29d94546c0a1c27d2b6d116f`:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfba\6a8043a5c4a6'
.\eng\bazel\validate-analyzers-package.ps1 -Jobs 2 `
    -EvidenceRoot 'C:\tfba\6a8043a5c4a6\analyzer-signed-evidence'
```

Exit **0**, all **15** original checks executed/passed. Native packing rebuilt
128 actions (91 local actions; 14 cache hits) before the consumer run. The
fresh package remains `MSTest.Analyzers` **4.6.0-preview.bazel**, with 55 entries,
SHA-256
`B417D113D797FA56C38868D99C0726BBEBD478998BB3E76A4FB4CA4B8F54F712`.
The original gate, Roslyn host and package rule required no changes.

| Requirement | Signed refresh evidence |
| --- | --- |
| C#/VB layout and suppressed dependencies | `package-layout-and-private-dependencies` passed; source/native payload hashes and fresh-cache provenance remain enforced. |
| Compiler modes, corrected input and intentional suppression | All 12 unchanged `cs-` / `vb-` cases passed with the original exact diagnostic, exit, analyzer/config selection and SDK assertions. |
| Older compiler API, real code fix and satellites | `roslyn-3.11-codefix-and-satellites` passed on Roslyn 3.11.0.0; all 83 analyzer/52 fixer types loaded, MSTEST0030 was produced, `AddTestClassFixer` changed the exact expected source, and French analyzer/fixer strings matched the XLF catalogs. Both loaded DLLs report the package-aligned informational version. |
| Native producer and development identity | `native-producer-action-graph` passed. Supplemental read-only PE inspection verified all 42 main/satellite DLL entries carry public key token `B03F5F7F11D50A3A`, the exact pinned 160-byte key, the public-sign flag and zero/uncompleted 128-byte signature slots; no Authenticode or NuGet package signature was introduced. |

The signing and source-root patch blobs match the coordinator's source commits
exactly (`77ebdde668431f21d5e7a08d8c34098e1c36c6f5` and
`245d283dbdd6b54ada49a1b0948e4fbda7bb2f35`, respectively). A byte comparison of
the retained unsigned and new signed archives found exactly the 42 DLL entries
changed; all 13 other entries, including the four generated configs, package
metadata, readme, build hooks and signed installation scripts, are unchanged.

**Test-value evidence:** the 15-case gate is an executed green-before /
green-after preservation comparison, not proof that the old unsigned identity
was acceptable to every downstream component. The same supplemental signing
inspection rejects the retained unsigned code-fix DLL for its absent expected
public key, then passes all 42 signed entries. The shared signing fixture's
broader red/green evidence belongs to the coordinator; no new test scheduling,
diagnostic suppression or official signing was introduced here.

Evidence and the new package feed are retained at
`C:\tfba\6a8043a5c4a6\analyzer-signed-evidence\20261103-165840-50c02dd7`.
`validation.json` records the clean source revision, all case outcomes and
commands/exits; compiler SARIF, binlogs and selected asset files remain beside
it. `public-signing.json`, `verify-analyzer-public-signing.ps1`,
`unsigned-identity-rejected.log` and `signing-refresh-comparison.json` preserve
the supplemental probe and package comparison. The previous unsigned evidence
and caches remain separate. Only this decision record was edited after the
successful signed run.

#### Parent integration: shared framework metadata

The parent integrated the analyzer and netstandard core commits, then replaced
the analyzer-only attribute file with the shared
`CompatibilityFrameworkMetadata.cs` in exactly the two netstandard analyzer/codefix
declarations. No modern source target receives this guarded file.

The same analyzer validation command passed all 15 cases after consolidation,
including exact emitted framework metadata, C#/VB compiler behavior, the Roslyn
3.11 code fix and French resources. The integrated package has SHA-256
`117B33B04791A2DBB1A1344EC50EAE5496B7F27C10D4B0EAD99A117B37CE4229`;
the changed source/PDB provenance is not treated as byte identity with the lane
package. Evidence is under
`C:\tfb\parent-analyzer-evidence\20261103-151018-80b35180`.
The run started at parent revision `11ae3b9f2369f098d1ba352df7259b6e5b5b17fe`
with the three-file metadata consolidation; the only concurrent parent commit
changed the separate MTP package validator, not these producer/consumer inputs.

### 2026-11-03: native Windows library lane

This lane starts at `6bc33c48b9dae0e89486862f46af3c876fd7cf5d`.
It owns `eng/bazel/windows.bzl`, a separate Windows dependency group, and a
scoped `windows-targeting.patch`; the portable graph and three-package aggregate
must retain their existing behavior. Shared root wiring is additive. The
coordinating migration session owns combined documentation, CI, integration with
other package/SDK lanes, and any eventual release/servicing decision.

The first contract is a **native library and controller foundation**, not native
XAML application compilation:

- `PackagedApp` needs its four `net8.0` through `net11.0` Windows 10.0.19041.0
  variants, including `PACKAGEDAPP_WINRT` and actual WinRT projections.
- `mstest-appmodel-controller` retains its net8/net9 Windows 10.0.19041.0,
  win-x64, framework-dependent apphost layout and `LatestMajor` roll-forward.
  Its source-built MTP/PackagedApp/reporting assemblies must not be replaced by
  released first-party binaries through transitive NuGet resolution.
- `MSTest.Windows.UIAutomation` needs Windows-qualified .NET 8/9/10/11 TFMs
  and reference-only WPF targeting assets. Building this library does not
  require XAML generation or prove UI interaction.
- Framework extensions and adapter platform services need distinct WinUI
  (`net8.0-windows10.0.18362.0`, minimum 10.0.17763.0) and modern UWP
  (`net9.0-windows10.0.17763.0`) source/metadata variants. Classic UWP remains
  separately bounded by its reference/toolchain requirements.
- Package registration, activation, app identity, connect-back authorization,
  cancellation, exit propagation, and cleanup remain production-owned.
  An unpackaged/disabled launcher must not register an app; informational
  no-handshake launches and required-handshake executions remain distinct.
  Any real activation experiment uses only a task-owned identity and outputs.
  Win32 1346, missing capabilities, or denied registration remain failures,
  never a reason to alter ACLs, Developer Mode, global SDKs, or test assertions.

The no-change alternative leaves the Windows source branches uncompiled.
Manually compiling those branches against portable references would not preserve
TFM selection or runtime projections. The chosen bounded extension therefore
adds explicit native Windows targeting and asset selection first, then exercises
the resulting outputs. An MSBuild-driven XAML bridge is a possible separate
hybrid experiment, not a native producer or a replacement for this contract.

Planned evidence: native action graphs; target-framework, minimum-version and
reference/runtime asset checks; exact loaded assembly hashes; isolated
controller/library consumers with contractual exits; a buildable
counterfactual for new executable checks; and a bounded XAML toolchain
assessment. At this entry these experiments have **not yet run**. No shipped
package, application activation, native XAML pipeline, CI run, or backport is
claimed.

Research found two distinct CsWinRT 2.x profiles in the pinned SDK's
`Microsoft.NETCoreSdk.BundledVersions.props`: the net8/net9 Windows 19041 builds
use `Microsoft.Windows.SDK.NET.Ref` 10.0.19041.57 (`lib/net8.0`,
WinRT.Runtime 2.2.0.0), while net10/net11 use 10.0.19041.87 (`lib/net9.0`,
WinRT.Runtime 2.3.1.0). The packages' `data/FrameworkList.xml` separates
the ordinary `Windows` profile from `Microsoft.Windows.UI.Xaml.dll`'s `Xaml`
profile. The ordinary library targets must not accidentally include the latter.
Windows SDK revision `.1` requests the new CsWinRT 3 pipeline according to
`Microsoft.NET.Windows.targets`; it is not an alias for `.0` and stays unsupported
by this bounded patch.

The first native dependency-tool build passed on the unchanged baseline.
Q: then ran out of space. Only this lane's Bazel resources were relocated to C:;
an attempted link-preserving extracted-cache move failed because Robocopy could
not recreate symbolic links without a privilege the process does not have.
No privilege or machine policy was changed. Incomplete extracted caches were
discarded, and subsequent native work uses fresh C: repository extractions rather
than trusting moved cache records. The source worktree remains on Q:. Residual
owned cache cleanup is tracked separately and is not yet claimed complete.

The source draft preserves distinct modern UWP/WinUI extension, platform-service,
and adapter assemblies. WinUI's existing source-level supported-OS annotations
are retained (17763 for framework extensions, 18362 for platform services),
rather than emitting duplicate attributes or silently changing those contracts.
The desktop mode uses the WindowsDesktop reference/runtime pack without adding
XAML generation. Ordinary WinRT imports select only the `Windows` profile and
its matching generator/runtime. The Windows compatibility table is bounded to
the net8-net11 library matrix plus older net5-net7 package assets; it does not
claim untested future Windows framework or CsWinRT 3 support.

The coordinator temporarily paused new heavy builds/restores for the other
lanes' initial compiler wave. During that pause, the Windows draft gained a
19-assertion analysis test and seven isolated library-consumer probes. These are
not yet executed. Root wiring and the generated Windows lock will be enabled
together after dependency resolution; the existing portable graph is unchanged.
The controller's CodeCoverage pin is available on the repository's `test-tools`
feed, not nuget.org. Its native dependency closure belongs to this lane, while
the coordinating session owns the shared experimental package-version change.

#### Targeting-rule test-value evidence

The first bounded native check executes without C# compilation or NuGet restore:
`//:windows_targeting_test` is one Bazel analysis-test target with 22 assertions.
The tested production patch has SHA-256
`218BEF63B48ED1C921CA3D6553AE82FC90194D731F4B43B4A824F3E1220D5BFB`;
the test source has SHA-256
`4C8CE129AF297443DBB76E441477A1CE4591E81400916C9FBB709802FC928502`,
on the starting revision above. This validates selection, symbols, pack
mapping and runtime-configuration generation, not compiled Windows libraries.

The initial run exposed nonexistent WindowsDesktop arm64 runtime packs for
.NET Core 3.0/3.1. The patch now preserves their actual x64-only support rather
than declaring unsupported downloads. The final check covers that boundary too.

For the counterfactual, a task-owned copy of the patched rules retained the
Windows API/table shape but restored upstream's ordering-only
`get_highest_compatible_target_framework`. The same test then detected both
incorrect selections:

```text
NewerPortableCannotConsumeOlderWindows: expected None, actual "net8.0-windows10.0.19041.0"
RuntimeUpgradeCannotRaiseWindowsMinimum: expected None, actual "net8.0-windows10.0.19041.0"
```

| Production state | Bazel exit | Executed targets | Result |
| --- | --- | --- | --- |
| Correct, before | 0 | 1 | All 22 assertions passed |
| Ordering-only counterfactual | 3 | 1 | Test process exit 1; both assertions above failed |
| Correct, after | 0 | 1 | All 22 assertions passed |

The counterfactual patch SHA-256 is
`EA7FDED741B15412701381B23CEAE8118CE1CA4259A4142B70A38F97390529F1`.
JUnit reports were parsed for exactly one executed target, no skips, and the
expected outcomes. The isolated comparison materialized one existing internal
resource-test fixture symlink as a normal file; that fixture is not consumed by
this test. No TestFx source or test assertions were changed between runs.

Exact correct-state command, from the worktree root:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfbw\d683319bb48a'
.\eng\bazel.ps1 --batch --host_jvm_args=-Xmx2g test //:windows_targeting_test --jobs=2 --noshow_progress --show_result=0 --nocache_test_results --repository_cache=C:\tfb\d683319bb48a\cache\repos\v1\content_addressable
```

The counterfactual used the same command with output root
`C:\tfbw\d683319bb48a-red` and the additional argument
`--override_module=rules_dotnet=C:\tfbw\d683319bb48a\targeting-counterfactual\rules_dotnet`.
The three reports, command/test logs and counterfactual patch are retained under
`C:\tfbw\d683319bb48a\targeting-evidence`; the excerpts and outcomes above preserve
the decisive evidence in this versioned record. Batch mode left no test server
running. Windows library compilation, controller layout, real registration/
activation and native XAML remain separate, unproven gates at this stage.

#### Windows library execution and the controller identity boundary

The subsequent `net8.0-windows10.0.19041.0` PackagedApp build passed with
`--jobs=1`. Its copied, self-contained consumer ran on **.NET 8.0.33**, invoked
the real `PackageManager.RegisterPackageByUriAsync` API-presence query through
the WinRT projection, and verified the loaded native MTP, PackagedApp,
Microsoft.Windows.SDK.NET and WinRT.Runtime hashes against its producer.
The staged directory contained spaces and `!`, and `DOTNET_ROOT` pointed to a
missing directory. This is a library/runtime check, not app registration.
The desktop reference-only consumer and modern UWP library flavors also built.

The full graph exposed further native asset-selection requirements: Windows SDK
NuGet versions have four numeric components; `lib/native/*.lib` entries are C++
linker inputs, not unsupported managed TFMs; and a flat Bazel `select` cannot
choose between a newer portable TFM and an older Windows-qualified TFM because
their compatibility conditions form a partial order. The scoped patch preserves
the full versions, ignores C++ inputs for managed references, and uses ordered
single-condition aliases with the same nearest-framework precedence.

The exact CodeCoverage payload was independently hashed because the `test-tools`
feed's registration/catalog route did not expose its integrity. Its SHA-256 is
`D3BD12007741B5037080C4C97802FFD5C9DB97DBE4186F040A1D4B56EA8CAE4E`,
and the module fragment pins its SHA-512. Only that package's six managed
assemblies, 17 satellite cultures and 11 win-x64 native/config assets are
imported, with the source-built MTP dependency rather than the released one.

Controller compilation then failed at its real CodeCoverage hook: that package
requires `Microsoft.Testing.Platform, Version=2.6.0.0, PublicKeyToken=b03f5f7f11d50a3a`,
while the native platform has the same version but `PublicKeyToken=null`.
The coordinating session owns the shared signing/friend-identity correction;
this lane does not rewrite CodeCoverage, substitute released MTP, or use
reflection to hide the mismatch. At that point the controller was not yet a
built deliverable.

The complete **14-library Windows matrix** and seven isolated consumers
subsequently passed:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfbw\d683319bb48a'
.\eng\bazel\windows\validate.ps1
```

The command exited zero. WinRT consumers ran on .NET 8.0.33, 9.0.22, 10.0.14,
and the pinned .NET 11 RC runtime; the WPF and WinUI consumers ran on 8.0.33
and the modern UWP consumer on 9.0.22. Every loaded first-party/runtime DLL
matched its native producer hash and was loaded from the copied layout.
Windows target/minimum metadata, the WPF implementation-versus-reference
boundary, the ordinary WinRT profile's exclusion of the UWP XAML projection,
and the compiler action graph were checked. Evidence:
`windows-validation/20261103-152856-5ac73479` beneath the lane's output root.

The real packaged-host check also passed:

```powershell
.\eng\bazel\windows\validate-app-model.ps1 -HostOnly
```

This builds a native console host around the unchanged explorer framework/tests,
not a XAML application. It checks unpackaged pass/failure/empty-selection exits
**0/2/9**, launcher suppression without registration, then real registration,
AUMID activation, the selected passing test's TRX, exact installed package
identity/location, and loaded native hashes. The final registration readback
was an empty array after removal of the exact task-owned package. Evidence:
`windows-app-model/9f309006ea6d4343ad62a3a7704cca0e`. The identity was
`TestFx.Bazel.9f309006ea6d4343ad62a3a7704cca0e`.

Two harness attempts were corrected before that result: Windows PowerShell's
first-module-load progress CLIXML must not be appended to stdout JSON, and an
initial 52-character generated package name exceeded the Windows identity
limit, causing `0x800706F7` before registration. Neither was treated as a
production pass or a host-permission failure. No package remained registered
after any attempt, and no ACL, Developer Mode, SDK or VS installation changed.
`-HostOnly` explicitly excludes the still-blocked external controller checks;
the default script requires those checks too.

#### Bounded XAML boundary research

The pinned Windows App SDK WinUI package contains a standalone
`tools/net472/XamlCompiler.exe`. Invoking it with no inputs returned its documented
two-JSON-file usage and exit zero. The package's
`Microsoft.UI.Xaml.Markup.Compiler.interop.targets` supplies `RealBuildPass1` /
`RealBuildPass2` inputs, then consumes generated C#, XAML and XBF outputs.
Metadata inspection of `CompilerInputs`, `AssemblyItem` and `FileItem` established
the actual serialized input shape without invoking MSBuild.

A bounded direct-compiler experiment will test whether that private, versioned
JSON contract can generate a tiny XAML library with explicit native references.
That would be a compiler experiment only: it would not establish a complete
Bazel XAML rule, WinUI deployment, application activation, or supported tooling.
An MSBuild bridge remains hybrid and must not be described as native support.

The bounded experiment was executed with the native WinUI library's actual
compiler reference list and a tiny `MainPage.xaml`/partial C# class.
`XamlCompiler.exe <input JSON> <output JSON>` ran directly, not through MSBuild.
The first input returned exit 1 with an internal index-out-of-range error.
A second input supplied the core reference root and left `LocalAssembly`
absent; a third restored the vendor's omitted-field defaults, a trailing output
separator and the existing Windows SDK path. Both returned exit 1 without
generated output. Inputs and logs remain under `xaml-probe` in the owned output
root. No generated C# or XBF, native XAML build rule, or XAML app execution is
claimed. This establishes an unresolved private compiler-input/toolchain
boundary, not that native XAML compilation is impossible. Further work needs a
canonical input/output contract before a safe two-pass Bazel rule.

#### Signed controller completion and configured asset evidence

The shared portions of coordinator checkpoint
`aff2cf0f702a07779cee99a8acf29cf515e858bb` were integrated without importing its
absent SDK/alias-conformance lane files. The public-signing patch matches the
checkpoint's Git blob `77ebdde668431f21d5e7a08d8c34098e1c36c6f5`.
The public-only, hash-pinned MSFT key and `/publicsign+` preserve the source
assembly token `b03f5f7f11d50a3a`, qualified mock/Microsoft friend identities and
satellites. This is development **public signing**, not private-key release
signing, an MSIX certificate, or authorization to publish packages.

Both original `mstest-appmodel-controller` targets now build natively for their
net8/net9 Windows 19041, win-x64, framework-dependent apphost layouts. The
unchanged CodeCoverage extension binds to source-built MTP; none of its DLLs
were rewritten. Its 11 native/config files remain under
`runtimes/win-x64/native` in each controller publish layout.

The full `validate-app-model.ps1` run
`windows-app-model/2e60d7846fe94b6d8e9eb163f17c667a` exited zero. It exercised
both controllers, not `-HostOnly`:

| Path | Expected and observed exit |
| --- | --- |
| Each controller's malformed bootstrap | 5 |
| Each controller's selected passing test | 0, with one executed passing TRX result |
| Each controller's selected failing test | 2 |
| Each controller's empty selection | 9 |
| Each controller's discovery without execution handshake | 0, with the real host's test names |
| Native packaged host registration/activation and suppressed launcher | 0 |
| Exact package removal and empty registration readback | 0 |

The source MTP/PackagedApp public-key tokens and loaded native hashes were
checked in the activated host. Signed Windows-library validation also exited
zero for all seven consumers under
`windows-validation/20261103-162841-f7772f5c`; it additionally requires the
correct token on every source library and executes both analysis-test targets.
WinUI/UWP probes establish library API/metadata and runtime provenance, not
UI-thread execution or Windows App SDK initialization. The UIAutomation probe
loads real WPF types but does not interact with an application window.
Classic UWP, AppContainer activation, the native SDK's full packaged-test
routing and official packaging/release qualification remain distinct gates.

`windows_asset_selection_test` adds four configured checks around actual
`tfm_filegroup` aliases. A retained, buildable counterfactual reverses the alias
priority while preserving the same fixture/tests. It executes and fails:

```text
NewerRuntimeWinsWithoutAmbiguity: expected ["LICENSE"], actual [".bazelversion"]
```

The files are read-only markers for newer-runtime versus older-Windows assets.
The test passed before and after; Bazel exits were **0 / 3 / 0**, with one
executed test target per run and no skips. Counterfactual patch SHA-256:
`D1F37B396384FAC2354007DAEFCBA58C2B77F2E151B7034FBA67855723A15451`.
Test-source SHA-256:
`58960E7A1E1F703975549C5EE7241C8A3379D5B89D7F6B5AA908BEE1A74C6CEB`.
Windows-targeting patch SHA-256:
`2A63D276D7647CBACACCB3AA8AEBA0CD66CDDBB4A70BC6C42E948EBD8F55044F`.
The first attempted counterfactual restored the flat `select` and failed during
configuration before tests could execute; that is retained as an analysis
blocker, not counted as behavioral red evidence.

Exact selection command:

```powershell
.\eng\bazel.ps1 test //:windows_targeting_test //:windows_asset_selection_test --jobs=2 --noshow_progress --show_result=0 --nocache_test_results
```

The isolated red run selected only `//:windows_asset_selection_test`, used the
owned `-red` output root and the module override described above. Reports,
logs and patches are under `asset-selection-evidence`. The comparison checkout
and retired Bazel output/install subtrees were removed after their evidence was
preserved; active checksum caches and final validation evidence were retained.

After the shared signing integration, the unchanged portable baseline was
rerun with
`.\eng\bazel.ps1 test //:baseline_tests --jobs=2 --noshow_progress --show_result=0 --nocache_test_results`
and exited zero. Parsed reports account for **3,017 passing cases across seven
targets**: framework 1,634; adapter 152; platform services 1,215; build tools 13;
and one each for the source explorer, released explorer and Simple1. No cases
were skipped. The reports and counts are retained under
`portable-signed-evidence` in the owned output root.

This lane establishes native managed Windows compilation, self-contained
library consumers, two framework-dependent controller layouts, and real
registration/activation of a **loose MSIX layout**. It does not produce a signed
MSIX archive or establish native XAML, classic-UWP/AppContainer execution,
Windows NuGet integration, release-branch servicing, or CI completion.
The coordinating migration owner retains those rollout/integration decisions.

### 2026-11-02: extending the migration after upstream SDK discovery

The user authorized implementing as much of the remaining migration as possible.
The starting TestFx revision is
`22a3f9d008b53fdb97a569ee382307bf65bc5787`. The existing MSBuild shipping pipeline,
production source, test assertions, and `global.json` remain authoritative.

[rules_dotnet#593](https://github.com/bazel-contrib/rules_dotnet/pull/593),
merged as `add5c92b30888a70aa951de1744a770b8f8ef436`, supersedes the generated SDK
tables with release-metadata discovery and native Paket module extensions.
An unchanged released-MSTest consumer already passed against that exact upstream
revision on .NET 11 RC3, including its runtimeconfig and loaded assembly hash.
That bounded result is not validation of the TestFx source graph or upstream's
full suite; its Buildkite status was failed when inspected.

The implementation sequence is to pin this immutable upstream source, reassess
the nine old compatibility patches, restore the existing native baseline, expand
the portable framework/test graph, and then produce locally validated native
packages. Keeping 0.22.2 would require maintaining SDK discovery and dependency
machinery that upstream has replaced. Updating blindly to an unpinned branch
would make the experiment irreproducible.

The compiler remains an explicit investigation: TestFx pins an engineering
.NET 12 SDK, whereas upstream discovers published release metadata. No silent
edit of `global.json` or claim of compiler parity is authorized by this work.
Windows-qualified NuGet TFMs remain unsupported upstream, and a native XAML/UWP
toolchain is a separate requirement, not something an MSBuild wrapper can satisfy.

New packages must be produced solely from native Bazel outputs, staged in a
task-owned feed, and exercised by actual consumers. MSBuild is allowed in those
consumers because MSTest's SDK/build extensions are themselves products under
test, not because it is allowed to build the TestFx producer graph. Release
signing, publishing, and downstream rollout remain disabled until their own
parity gates pass.

Observed during this implementation:

- The pre-upgrade seven-target baseline passed again, before changing the
  compiler/rules. The same baseline then passed on Bazel 9.1.1 and SDK 11 RC3.
- The old minimal-context patches caused a Bazel patcher exception. They were
  rebased with full context against the immutable upstream tree, not applied to
  a mutable shared checkout. Upstream now resolves duplicate analyzers itself.
- Roslyn still crashes while running diagnostic analyzers in the extra
  reference-only compile. The implementation compile retains its diagnostics;
  the reference-only workaround remains. The netstandard code-fix build also
  still needs canonical framework facades instead of legacy System.* copies.
- Building net8/net9 exposed a separate reference-resolution defect: upstream
  discarded newer transitive `System.Text.Encodings.Web`, `System.IO.Pipelines`,
  and `System.Diagnostics.DiagnosticSource` references in favor of older
  framework copies. Version-aware transitive selection fixed the actual graph
  without changing TestFx source or weakening nullable diagnostics.
- `managed_all` now builds the four portable framework configurations. Existing
  protocol-contract (27), source-generation (197), and VSTestBridge (202) cases
  were added unchanged, preserving method scheduling and the generator suite's
  four-worker setting. The portable suite totals 3,443 cases in ten targets.
- Native NuGet packing hit a Bazel exception for a transition on a label-keyed
  file dictionary. Explicit package files retain their incoming configuration
  instead. All three MTP/TRX packages include four TFMs, resources, documentation,
  and their consumer build hooks.
- A package-consumer attempt failed with NETSDK1005 because overriding
  `TargetFramework` only on the build command did not change restore's project
  framework. The binary log identified that mismatch. The isolated copied
  consumer now declares each actual framework before its build; all four build.
- The package-consuming explorer executes on net11 with a new cache and
  first-party source mapping to the native feed. It verifies the loaded DLL
  hashes and native-build metadata, emits a real TRX, and returns 0/2/9 for
  pass/failure/empty selection. Four self-contained publishers also execute
  outside Bazel's tree, with nonexistent `DOTNET_ROOT`, and verify their actual
  .NET 8/9/10/11 runtime and staged CoreLib before the same exit checks.
- The full MTP suite was rerun: 3,378 passed, the same two Windows impersonation
  cases failed with Win32 1346, and none skipped. This is not included in the
  verified portable suite and its assertions remain unchanged.
- The exact engineering SDK archive on `ci.dot.net` now returns HTTP 200.
  The .NET 12 release-metadata URL and the equivalent `builds.dotnet` archive
  return 404. Availability of the archive does not mean the upstream metadata
  resolver can yet select it; compiler parity remains outstanding.

### Package-version test-value evidence

The new package-version command intentionally writes the selected product's
`-bazel` version, rather than an official-looking release version. The four
`PackageVersion_*` cases select both product versions and reject absent/duplicate
properties. An isolated counterfactual retained that command and the same tests,
but omitted `-bazel` from its output. Its native compiler invocation succeeded.

The same compiled test assembly and filter
`--filter 'FullyQualifiedName~PackageVersion' --minimum-expected-tests 4`
executed in all three runs:

| Implementation | Exit | Executed | Passed | Failed |
| --- | --- | --- | --- | --- |
| Correct, before | 0 | 4 | 4 | 0 |
| Counterfactual | 2 | 4 | 2 | 2 |
| Correct, after | 0 | 4 | 4 | 0 |

The two failures were exact equality assertions: `4.6.0-bazel` versus `4.6.0`,
and `2.6.0-bazel` versus `2.6.0`. The invalid-input cases still passed.
Counterfactual patch SHA-256:
`49CE9D3486B197EF1678088EB14C6403037606C13522070D43B376FCCEBADF24`.
Commands, compiler arguments, patch, and all three TRX files are retained under
`artifacts/bazel-expansion/package-version-counterfactual`. Earlier setup errors
while locating a direct test launcher are not counted as behavioral evidence.

### Remaining delivery boundaries after this stage

The new packages are the platform, TRX abstractions, and TRX reporter only.
MSTest framework/adapter packaging, analyzer packaging, SDK/meta-packages,
Windows-specific assets, netstandard/net462 parity, the other test projects and
examples, NativeAOT/browser execution, signing, and release rollout still require
work. Windows x64 was executed; the expanded Windows/Linux workflow is only
configuration until its jobs run. No TestFx PR or package publication is implied.

### 2026-10-29: baseline and upstream contracts

Baseline: `10847b7d879200796a35ea9888f36fdee7b9b593`, clean worktree.
No existing Bazel configuration was found.

Sources inspected:

- [rules_dotnet v0.22.2](https://github.com/bazel-contrib/rules_dotnet/releases/tag/v0.22.2),
  its `MODULE.bazel`, `dotnet/defs.bzl`, rule implementations, and examples.
  The module requires Bazel 8 or later, despite the release body's older Bazel 7
  instructions. Use Bzlmod and the actual pinned source contract.
- [Native C# test rule](https://github.com/bazel-contrib/rules_dotnet/blob/v0.22.2/dotnet/private/rules/csharp/test.bzl):
  `csharp_test` executes a C# program. MTP is already executable; NUnit wrapping
  and VSTest discovery are unnecessary.
- [Toolchain versions](https://github.com/bazel-contrib/rules_dotnet/blob/v0.22.2/dotnet/private/sdk/versions.bzl)
  and [TFM graph](https://github.com/bazel-contrib/rules_dotnet/blob/v0.22.2/dotnet/private/common.bzl):
  SDK 10.0.400 is supported with checksum-pinned downloads; modern TFMs stop at
  `net10.0`. TestFx's `global.json` pins SDK 12.0.100-alpha.1.26509.101 and its
  source build also includes `net11.0`. Full toolchain/TFM parity is therefore
  not available from the released rules.
- [Paket integration](https://github.com/bazel-contrib/rules_dotnet/tree/v0.22.2/tools/paket2bazel):
  use upstream dependency resolution/lock generation rather than inventing a
  second NuGet resolver.
- Local `Directory.Build.props`, `Directory.Build.targets`, product projects,
  `eng/Versions.props`, and sample defaults: implicit usings, linked sources,
  generated resource accessors, version templates, source generators, friend
  assemblies, and extension registration all need explicit Bazel equivalents.
  Merely globbing `*.cs` is insufficient.

The machine has no Bazel installation. A repository-scoped, checksum-pinned
bootstrap is needed; do not alter user-global SDK or tool settings.

### 2026-10-29: bootstrap experiments

- The checksum-verified Bazel 8.8.2 bootstrap executed successfully.
- Building upstream `paket2bazel` with the output root below this long worktree
  path compiled the F# tool but failed to launch `apphost_shimmer.dll.bat`:
  Windows `GetShortPathNameW` could not shorten the path sufficiently.
  The bootstrap now selects a short, worktree-specific output root;
  `TESTFX_BAZEL_OUTPUT_ROOT` allows an explicit supported cache location.
- The machine's temporary directory is `C:\Windows\TEMP`; Java could not resolve
  its extracted security configuration there (`AccessDeniedException`). Windows
  now defaults to `<checkout-drive>:\tfb\<worktree-hash>` instead of changing
  system permissions. Other systems use the normal temporary directory.
- PowerShell consumes an unquoted `--` passed to a script. Commands invoking
  `eng/bazel.ps1 run` must quote `'--'` to forward Bazel's separator.
- Native compilation of the existing platform explorer exposed an upstream
  response-file defect: source paths such as `In-process extensions/...cs` were
  split at spaces (`CS2001`). A one-line, version-scoped Bzlmod patch quotes C#
  source arguments. Do not rename sample directories or copy sources elsewhere
  to conceal the defect. This is build-contract evidence, not a runtime
  regression-test red phase.
- Paket's default maximum-version strategy selected Application Insights 3.x
  for a package that declares a 2.23 minimum. Use `strategy: min` and a committed
  lock to align transitive selection with NuGet's lowest-applicable behavior.
- The compiled explorer could not resolve its runtime dependencies with
  Windows manifest-only runfiles. Follow the upstream `.bazelrc` contract:
  `--enable_runfiles` and `--windows_enable_symlinks`. Windows therefore needs
  permission to create symbolic links (Developer Mode or an appropriately
  privileged shell); the bootstrap does not change machine policy.

### 2026-10-29: source-built platform

The initial `bazel test //:explorer` target compiled `Microsoft.Testing.Platform`, TRX abstractions,
and TRX reporter from this checkout and passed the same existing explorer test.
The real controller launched a child that reported `Microsoft.Testing.Platform
v2.6.0-bazel`, completed one test, and exited zero. The released-package baseline
reports `v2.5.2`. No MSBuild compile/restore outputs were used.
The runnable applications and filtered smoke tests were subsequently separated
into `//:explorer` and `//:explorer_test`, so application arguments do not inherit
a fixed smoke-test filter.

Native actions now generate RESX string accessors and binary resources, version
attributes, and existing version templates. A scoped rules patch preserves the
fully qualified manifest names of generated `.resources` files, including
hand-maintained cross-assembly resource accessors.

The first platform compile exposed another SDK difference: upstream emits `NET`
but not `NETCOREAPP` for `net10.0`. That accidentally enabled legacy polyfills and
the netstandard JSON parser. Explicitly restore the missing symbol in the TestFx
macro; do not delete conditional sources or suppress the resulting diagnostics.
Compilation then succeeded with warnings treated as errors.

The initial migration preserves SDK-style transitive compilation references via
`@rules_dotnet//dotnet/settings:strict_deps=false`. The first source-generator
compile with strict dependencies could not see `Microsoft.CodeAnalysis.Common`
through its declared CSharp package. This setting changes reference visibility,
not dependency ownership: the complete closure is still declared and cached by
Bazel. Tightening direct dependencies is a separate cleanup, not a prerequisite
for behavior-preserving build migration.

Windows executable/test labels live at the workspace root: deep labels doubled
inside runfiles exceed the batch-launch path limit even with a short output root.
Source files remain in their original locations.

### 2026-10-29: MSTest compiler boundary

The MSTest assertion generator remains a native `netstandard2.0` analyzer.
Supply the existing `EnforceExtendedAnalyzerRules` property through a global
analyzer config, and pass the repository `.editorconfig` to preserve its scoped
experimental-API diagnostics rather than changing product sources.

SDK 10.0.400's compiler crashed in the *extra reference-only* compilation used by
`rules_dotnet` for friend assemblies: `CSharpCompilerDiagnosticAnalyzer` reported
`Cannot enqueue data after PromiseNotToEnqueue`. Disabling compiler parallelism
was tried and still failed (AD0001); that experiment was removed. Public
builds.dotnet/ci.dot.net probes for the repository's SDK 12 pin returned 404, so
no SDK substitution was claimed.

A version-scoped patch passes `/skipanalyzers+` only in that extra `/refonly`
action. Source generators and analyzer configuration remain supplied there.
The implementation compilation still runs all configured analyzers with
warnings as errors; it is not disabled or replaced.

### 2026-10-29: localization, metadata, and existing-test sensitivity

The first full framework run executed 1,634 tests: 1,632 passed and two failed
because Korean satellite resources were missing. Native XLF compilation now
builds locale assemblies and propagates them through runfiles/dependency
manifests. The same 1,634 tests then passed, with no skipped cases or changed
assertions. Preserve logical names such as `FrameworkMessages.ko.resources`;
the shipped 4.5.2 package's satellite manifests were inspected for comparison.

Translation behavior was checked against `dotnet/xliff-tasks`' `XlfDocument`,
`XElementExtensions.GetTargetValue`, and `TranslatableDocument.Translate`:
apply targets regardless of state, and fall back to source when target is absent.
The existing localization guard passed all 364 catalogs. No RESX/XLF was edited.

Two existing platform-services tests exposed a second real difference: the
shipped `UTA_ExecuteThrewException` resource contains CRLF, while the initial
XDocument reader normalized it to LF. A non-normalizing XML reader, with DTDs
prohibited and external resolution disabled, preserves resource text. Both
tests and all 1,215 platform-services cases now pass. Added focused LF/CRLF,
XLF, invalid-input, BOM, and metadata tests; all nine execute and pass.

An existing adapter test also required the SDK's neutral-resource assembly
attribute. An MTP test required `Application.BuildTimeUTC`. Native metadata now
emits `NeutralResourcesLanguage("en")` and uses Bazel's volatile timestamp for
the platform's UTC date rather than inventing a date or consulting an undeclared
wall clock. All 152 adapter cases pass.

### 2026-10-29: expanded graph and dependency experiments

`//:managed` now compiles 35 of 41 source projects. The
[scope inventory](../bazel.md#targets-and-current-scope) identifies the six
packaging/Windows-workload projects outside this target.

NuGet package selection required care. `strategy: min` only affects transitive
dependencies; a lower-bound experiment selected unwanted newer direct versions.
`lowest_matching: true` plus broad lower bounds caused lengthy backtracking
around the composed AI/resilience/hosting graph. The final declaration pins
direct versions, retaining repository versions where possible and converging
shared abstractions to 10.0.13 as required by that graph. A resolver abandoned
during the experiment retained its process lock; its exact owned PID was
verified and stopped, and the final bounded resolution completed in 15 seconds.
No global cache was cleared.

Further executed failures established these scoped rules patches:

- Both SDK and explicit System.Text.Json generators ran, generating duplicate
  members. Prefer the explicit package generator when assembly names duplicate.
- Code-fix compilation received old System.* facades alongside NETStandard's
  canonical references. Give the selected framework references priority.
- Long DLL-based batch launcher names overflowed Windows' doubled runfiles
  paths. Use short launcher names and preserve the original source layout.
- Windows launcher argument rewriting corrupted quoted paths; forward arguments
  without rewriting them and disable delayed expansion while invoking dotnet.
  Actual report generation succeeds in a directory containing spaces and `!`.
- PDBs were not declared as runfiles. Carry them with the compiled assembly.

Analyzer release files and the existing MSTest diagnostic suppressor are native
inputs. The suppressor, not a new CS8618 allowlist, handles injected TestContext
properties. Ordinary analyzer and source-generator compilation still uses its
declared Roslyn API version.

### 2026-10-29: remaining host-specific MTP failures

Full native MTP execution ran 3,380 cases: 3,378 passed and two real Windows
impersonation tests failed with Win32 1346 (`ERROR_BAD_IMPERSONATION_LEVEL`).
They fail outside Bazel's test wrapper too. A separate plain .NET named-pipe
and anonymous-token probe, with no TestFx or Bazel code, reproduced HRESULT
`0x80070542`. No security settings or assertions were changed.

The explicit selection excluding those two ran and passed 3,378 cases; report
that separately from the failed full run. `baseline_tests` does not include the
full MTP target. Other suites with extern aliases, special child bootstraps,
shipping-package assumptions, or unsupported workloads remain migration work.

## Alternatives and decision

1. Keep MSBuild only: preserves all existing capabilities but does not answer the
   request.
2. Invoke MSBuild from Bazel: easy orchestration, but retains MSBuild's graph and
   ambient outputs. Rejected as a migration claim.
3. Native `rules_dotnet` targets alongside existing project files: chosen.
   Start with public examples, then grow the source graph and existing tests.
   Make generated inputs explicit and retain a documented supported matrix.
4. Extend the rules immediately for all TestFx workloads/TFMs: not attempted.
   Only demonstrated compatibility gaps in the supported graph received scoped
   patches; a complete toolchain/workload fork remains a separate decision.

The Bazel experiment deliberately uses its own hermetic SDK pin, currently
11.0.100-rc.3.26522.148, not a globally selected SDK or a silent replacement of
`global.json`. The portable matrix and three native packages have the bounded
evidence above. Windows app models, native AOT, the remaining shipping NuGet
assets, release signing, and Arcade services are separate parity gates.

## Validation and demonstration

### Initial 2026-10-29 baseline

Executed on Windows x64, Bazel 8.8.2, rules_dotnet 0.22.2 at
`7e7cff30826695e814bfbe05d555db6a780a7123`, SDK 10.0.400, runtime 10.0.11.
Source baseline is `10847b7d879200796a35ea9888f36fdee7b9b593` plus this branch's
changes. Logs and reports are local evidence, not published or CI-verified.

| Command | Result and scope |
| --- | --- |
| `.\eng\bazel.ps1 build //:managed --jobs=8` | Exit 0; 35 source projects. |
| Same build again | Exit 0; 315 action-cache hits, one internal action, 1.869 seconds in the recorded no-op run. Not a benchmark against MSBuild. |
| `.\eng\bazel\validate.ps1` | Exit 0; seven-target baseline, 3,013 passing cases, real reports, failure/zero-result/aggregation assertions, and action-graph inspection. |
| `.\eng\bazel.ps1 test //:build_tools_tests` | Exit 0; nine new cases, all passed and exported to TRX. |
| `.\eng\bazel.ps1 test //:platform_tests --nocache_test_results` | Bazel exit 3 / host exit 2; 3,380 executed, 3,378 passed, two host-specific failures, zero skipped. |
| `.\eng\bazel.ps1 run //:platform_tests_app '--' --filter 'FullyQualifiedName !~ CreateServerStream_AnUnauthorizedIdentityCannotConnect & FullyQualifiedName !~ CreateServerStream_AnAppContainerConnectsOnlyWhenItsOwnPackageIsAuthorized'` | Exit 0; 3,378 executed/passed, zero skipped; not a full-suite pass. |
| `python .github\scripts\check_localization.py` | Exit 0; 364 catalogs valid. |

The reproducible script writes `validation.json`, logs, TRX, four actual report
formats, and `actions.json` under `artifacts/bazel-validation/<run>`. The recorded
run `20261029-132646-a5e454f4` found 404 declared C# compiler actions and no MSBuild
execution. Product exits were 0 for success/discovery, 2 for the known failing
sample, 9 for zero work with a minimum of one, and Bazel 3 for mixed module results.
Console output plus structured reports are more useful than screenshots here;
no demonstration was fabricated or uploaded.

### Test-value evidence

The preserved localization red state has patch SHA-256
`B7CC4A0E2369ED1145384D8D9C92D137E35592EA46E934EDF09FAF4C09363FA1`;
the paired green state has
`7D4B0A9F33F01F1F043FEDB894DCFB3167D2572737275A406B0E92E4468D5309`.
Both apply to the baseline above. Their patches, logs, and TRX were retained in
the session's `bazel-evidence/localization-{red,green}` storage; these are
intermediate revision-bound comparisons, not a claim that every commit was
independently compared. Later baseline runs revalidated the final behavior.

| Requirement | Evidence |
| --- | --- |
| Localized resource behavior survives native compilation | **Executed: detected.** `GetDisplayNameArgumentsDisplayNameRefreshesLocalizedFormat` and `GetDisplayNameRefreshesLocalizedFormatWhenUICultureChanges` failed without satellites and passed with them; same assertions, 1,634 cases executed in both states. |
| Resource text and generated encoding are preserved | `Resx_PreservesTextAndEmitsBom` executes LF and CRLF cases; exact resource/source text and BOM assertions pass. Existing `ExecuteForTestThrowingExceptionShouldReturnTestResultWithFailedOutcome` and `RunTestMethodForTestThrowingExceptionShouldReturnTestResultWithFailedOutcome` failed before newline preservation and pass afterward. |
| XLF fallback and errors are explicit | `Xlf_AppliesTargetsAndPreservesMissingTargetFallback`, four `Xlf_RejectsInvalidCatalog` cases, and `Resx_RejectsNonStringDataBeforeWritingOutputs` pass. Invalid inputs produce nonzero exits without result artifacts. |
| Version and build-stamp outputs follow inputs | `Metadata_UsesVersionTemplateAndBuildStamp` passes exact source/metadata/BOM assertions. |
| Process/consumer integration is real | Existing sample tests run source-built MSTest/MTP, controller/child exits propagate, and `validate.ps1` asserts reports and mixed-result failure rather than accepting an outer success alone. |

Paired mutation evidence for every new helper branch, complete package/source API
compatibility, all platforms, and shipping/servicing parity remain unproven.

## Open questions

- How much of the existing source/test graph can the released rules build without
  weakening compiler diagnostics or changing product behavior?
- Which existing tests assume an MSBuild output or NuGet shipping layout?
- Which upstream changes are required for .NET 11 and Windows app-model parity?
- Release rollout/backports: none enabled. This branch owns the experiment;
  replacing any supported shipping build requires an explicit later decision and
  real package-consuming acceptance evidence.

## Framework compatibility lane (2026-11-03)

### Contract and inventory

This lane starts at `6bc33c48b9dae0e89486862f46af3c876fd7cf5d`.
It adds opt-in native compilation for the actual non-Windows-qualified
`netstandard2.0` and `net462` project variants, without changing the modern
`managed`/`managed_all` selection, production source, test assertions, or
scheduling. Compilation against a compatible lower TFM is valid for a dependency,
but is not evidence that the requested project output was built for that TFM.

The project inventory identifies these boundaries:

- MTP, its portable extensions, and the server-client validation assembly declare
  `netstandard2.0`. The portable `PackagedApp` project does not. The controller,
  packaged-app build package, SDK/meta-packages, and Windows-qualified variants
  belong to other lanes.
- `TestFramework` and `TestFramework.Extensions` declare both `netstandard2.0`
  and `net462`. PlatformServices, TestAdapter, and MSTest.Extensions.Hosting
  declare `net462`, but not `netstandard2.0`.
- The platform and server-client remove the System.Text.Json engine for
  non-.NETCoreApp builds. The platform additionally removes
  `JsonConfigurationFileParser.cs`. Keep the Jsonite sources and all
  conditionally compiled legacy code.
- The server-client has seven additional, explicitly curated compatibility
  polyfills; it must not acquire the complete polyfill glob. Other projects keep
  their existing polyfill exclusions and linked sources.
- The framework extensions and platform services require the framework
  `System.Configuration` reference on `net462`. Adapter/platform services need
  `System.Memory` 4.6.3 and `System.Threading.Tasks.Extensions` 4.5.4 there;
  Platform.MSBuild requires `Microsoft.Win32.Registry` 5.0.0 on netstandard.
- Preserve existing assembly names and friend grants. These remain unsigned
  development outputs; strong-name/package identity is not established by this
  lane. Fixed-framework analyzers and generators remain unchanged.

### Decision and planned experiments

Keep one source declaration per assembly, add its actual supported framework
choices, and select only the project-defined source/dependency differences.
An opt-in compatibility aggregate will verify the compiled TFM instead of
silently accepting the rules' highest-compatible fallback. Duplicating every
project macro would make source parity harder to maintain; globally changing the
default framework would disrupt other migration lanes.

Resolve the compatibility-only NuGet closure in its own Paket file/group.
The modern lock's `>= net8.0` restriction cannot describe dependencies needed only
by older package assets. Shared `MODULE.bazel` or rules patches require
coordination with the migration owner.

Validation uses an isolated Bazel output root and `--jobs=2` on the shared host:
compile every claimed project/TFM, inspect the selected framework/reference
inputs, run representative compatible consumers, and retain an unchanged modern
baseline. A failed compilation is build-contract evidence, not behavioral
red-phase proof. Exact results, unavailable variants, and runtime limitations
will be recorded as each experiment completes. Shipping packaging, SDK 12,
release-branch validation and rollout are separate gates.

### First independently usable slice

The first delivery is deliberately limited to three real `netstandard2.0`
outputs: `//:platform_netstandard2_0`,
`//:msbuild_extension_netstandard2_0`, and
`//:platform_msbuild_netstandard2_0`. Each wrapper forwards `DefaultInfo`,
`DotnetAssemblyCompileInfo`, and `DotnetAssemblyRuntimeInfo`, and rejects a
compiled framework different from the requested one. `//:managed_netstandard2_0`
also emits a JSON inventory of exact frameworks, output files, and runtime
dependencies. Its current membership is these three assemblies, not the whole
planned compatibility matrix.

The initial broad dependency experiment resolved, but combining the newer
AI/hosting closure with the adapter's exact Tasks.Extensions pin produced a
lock containing incompatible minimum-version requirements. It is not the
delivered lock. The first slice instead resolves only the MSBuild task's actual
three package references in an independent 14-package closure; broader areas
must keep independently compatible dependency groups.

Executed on Windows x64 with Bazel 9.1.1, rules_dotnet
`add5c92b30888a70aa951de1744a770b8f8ef436`, SDK
`11.0.100-rc.3.26522.148`, and `NETStandard.Library` 2.0.3 reference assemblies:

| Command | Result |
| --- | --- |
| Pinned Paket launcher, from `eng/bazel/compatibility`: `paket update --no-install` | Exit 0; 14 locked packages, with the existing modern lock unchanged. |
| `.\eng\bazel.ps1 build //:managed_netstandard2_0 --jobs=2 --noshow_progress` | Exit 0; all three implementation DLLs and the exact-TFM inventory built. |

The platform remains dependency-free. Its non-.NETCoreApp source selection
retains Jsonite, excludes only the project-declared System.Text.Json files, and
preserves the linked sources/polyfills. The task references the real
netstandard Microsoft.Build reference assemblies; their NuGets deliberately
provide no netstandard runtime DLL, because the executing MSBuild host supplies
them. Consumers must not stage these reference-only DLLs as runtime binaries.
Use the inventory/provider runtime closure for the remaining companion files.

The initial Q-drive baseline was interrupted by ENOSPC and is not a test pass
or a compatibility-source failure. Only the lane-owned failed output/cache was
removed, and subsequent heavy outputs and retained evidence moved to C: using
process-local overrides. Logs, compiler response files, and the inventory are
retained in this session's `files/bazel-compatibility` evidence. Runtime consumers
and the rest of the framework matrix remain subsequent validation; this first
slice is a compilation delivery, not shipping-layout or CI certification.

### Core assembly metadata correction

The source compilation in `a29a5ffc0b1005848e71a645abcdeddb818113ff` was
genuinely netstandard, but the three emitted assemblies had zero
`TargetFrameworkAttribute` instances. Compiler-provider information alone does
not establish emitted assembly metadata.

The reusable `//eng/bazel:CompatibilityFrameworkMetadata.cs` now supplies exact
`NETSTANDARD2_0` / `NET462` guarded attributes. The core declarations link it
only for their compatibility source selection; modern sources and fixed
analyzers are not changed by this correction. The analyzer lane can explicitly
reuse the file instead of its temporary local attribute without duplicate
attributes.

Recompiled the three core outputs with the same pinned toolchain and inspected
their actual `GetCustomAttributesData()` results. Each has exactly one
`TargetFrameworkAttribute` with `FrameworkName == ".NETStandard,Version=v2.0"`.
The focused command
`.\eng\bazel.ps1 build //:platform_netstandard2_0 //:platform_msbuild_netstandard2_0 //:msbuild_extension_netstandard2_0 --jobs=2 --noshow_progress`
completed with exit 0.
Assembly paths and SHA-256 hashes are in the retained
`core-metadata-verified.json`. The broader keep-going build exposed separate
net462 facade and native executable-pack gaps; those do not invalidate this
three-assembly metadata correction and are not reported as a matrix pass.

### Preserve no-op project removals in modern JSON selection

The initial compatibility source split copied all project `Compile Remove`
entries into the modern source selection. Two entries,
`JsonExtensions.cs` and `JsonPropertyBagDeserializer.cs`, no longer exist.
MSBuild correctly treats those removals as no-ops; explicitly adding the paths
back made the unchanged modern graph fail with missing inputs.

`modern_json_sources()` now globs the declared removal list, preserving every
actual JSON source while retaining no-op semantics for absent entries. This
correction does not remove conditional production code or change the
netstandard selection.

The focused command
`.\eng\bazel.ps1 build //:platform //:platform_msbuild //:msbuild_extension //:platform_netstandard2_0 //:platform_msbuild_netstandard2_0 //:msbuild_extension_netstandard2_0 --jobs=2 --noshow_progress`
completed with exit 0: three modern `net10.0` and three exact `netstandard2.0`
outputs. Retained evidence is `modern-input-fixed.log` and
`modern-input-fixed.events.json`. The earlier missing-input report and a
corrected Starlark syntax attempt are build failures, not behavioral red-phase
evidence.

## 2026-11-03: MSTest framework and adapter package lane

### Contract and research

This lane starts at `6bc33c48b9dae0e89486862f46af3c876fd7cf5d` and owns
`//:framework_package`, `//:adapter_package`, and their genuine portable support
packages. Existing `//:packages` and its MTP-only validator remain unchanged.
The consumer matrix is net8.0/net9.0/net10.0/net11.0; these are development
public-signed experimental packages, not a claim of Windows-app-model,
.NET Framework, or release-pipeline parity. The initial unsigned milestone is
preserved below. MSTest and MTP retain their separate version families
from `eng/Versions.props`, each with the explicit `-preview.bazel` suffix.

The authoritative layouts are `TestFramework.Extensions.csproj`,
`MSTest.TestAdapter.csproj`, `Microsoft.Testing.Platform.MSBuild.csproj`, and their
existing consumer targets:

- `MSTest.TestFramework` owns the core assembly under `lib/<tfm>` and the
  Extensions assembly under `buildTransitive/<tfm>`. The core's localized
  resources accompany the core assembly. Its dependency on `MSTest.Analyzers`
  must retain build/analyzer assets; no `NotPublishable` package may appear.
- `MSTest.TestAdapter` owns both adapter and PlatformServices binaries under
  `buildTransitive/<tfm>`, with shared adapter/PlatformServices satellites under
  `buildTransitive/_localization/<culture>`. The existing common hooks select
  binaries, copy requested cultures, generate parallelization attributes, and
  register MSTest's builder hook. PlatformServices is not a package.
- The portable adapter closure needs the existing TRX abstractions package,
  native Telemetry, and native Platform.MSBuild. Extensions.MSBuild is private
  payload of Platform.MSBuild, not a separately published package. The adapter's
  direct Retry/TRX-report references are for classic UWP source only; portable
  packaging must not invent dependencies from the unfiltered compiler graph.
- Platform.MSBuild's unmodified hooks load
  `_MSBuildTasks/netstandard2.0/Microsoft.Testing.Platform.MSBuild.dll` and the
  platform beside it. They require real netstandard2.0 native outputs, not net8
  binaries relabeled as netstandard. That source declaration is coordinated
  with the framework-parity lane.

The package rule must expose one nupkg in `DefaultInfo` and the pinned rules'
`NuGetPackInfo` so SDK/meta-package work can depend on the same native artifacts.
The analyzer package is owned by a separate lane; integration waits for its
actual ready commit, with no placeholder or released first-party substitution.

### Alternatives and implementation decision

The existing `nuget_pack` always puts ordinary assemblies under `lib`, which
does not express these shipping contracts. A fake library anchor or fabricated
NuGet dependencies would make restore succeed without proving the real layout.
Use explicit native output projections and the pinned rules' native
`nuget_pack` executable/request format instead. Preserve the repository's
consumer assets rather than rewriting them to fit an easier layout. Share a
small explicit-layout helper with the SDK lane if its contract is ready.

### Validation plan and isolation

Use `eng/bazel.ps1` with `--jobs=2`; the experimental producer toolchain remains
SDK 11 RC3. No TestFx producer is built or packed by MSBuild. External consumers
may use that SDK's MSBuild to test the packaged hooks and generated entrypoint.
Each validation run owns its feed, package cache, output, and report paths under
this worktree's `artifacts` directory. Child-only environment changes are
restored; existing test-worker settings and shared caches remain untouched.

Inspect complete archive entries and dependency groups, then restore a real
MSTest consumer with first-party source mapping to that feed. Assert native
package/cache hashes, loaded assembly paths and hashes, generated registration,
discovery identities, report contents, and pass/fail/empty-selection exits
0/2/9. Compare a relevant deliberately incomplete package with the correct
package while retaining the same consumer assertions. Missing compilation,
restore, or launch is a setup failure, not behavioral red-phase evidence.
The coordinator owns combined documentation/CI and later rollout decisions;
no shipping leg, servicing branch, or package publication is enabled here.

### Preparation results and current boundaries

- The existing platform-only baseline build was attempted with
  `.\eng\bazel.ps1 build //:platform_package --jobs=2 --noshow_progress --show_result=0`.
  It reached native C# compilation, then failed with CS2012/ENOSPC when the
  shared Q: drive filled. This was an infrastructure failure, not behavioral
  evidence. The interrupted patch was inspected; it had not changed the package
  helper. The complete intended patch was subsequently applied and its C# BOMs
  and PowerShell syntax were checked.
- The coordinator approved process-local
  `TESTFX_BAZEL_OUTPUT_ROOT=C:\tfb\d7ce736ef101` and run-owned C: consumer/cache
  directories. The owned Q: Bazel server was stopped. Copying the extracted
  repository cache was abandoned: Robocopy could not preserve 18 upstream
  fixture symlinks (Win32 1314). No security policy or privileges were changed,
  and the guarded command did not start a build from that incomplete copy.
  The native tool must recreate incomplete extracted entries. Shared caches
  and other lanes' outputs are not cleanup targets.
- The package rule exposes `DefaultInfo`, `NuGetPackInfo`, and `nuspec` /
  `package_request` output groups. Framework and adapter preserve private
  assembly placement instead of inferring package ownership from compilation
  dependencies. The adapter retains the shipping Windows-only framework
  dependency conditional. Development PDB/XML payloads are additional evidence,
  not a claim of official symbol/signing parity.
- The new support packages preserve the platform's
  `[current-bazel-version, next-major)` dependency bound. Telemetry deliberately
  uses the native graph's locked DiagnosticSource version (currently 10.0.13),
  rather than claiming equivalence with the Arcade graph's 6.0.0 minimum while
  compiling against newer assemblies.
- The coordinator's common version decision supersedes the initial `-bazel`
  spelling: all lane-local expectations now require `-preview.bazel`
  (4.6.0-preview.bazel for MSTest; 2.6.0-preview.bazel for MTP). The SDK lane's
  production CodeCoverage pin requires `>= 2.6.0-preview.26526.14`; the original
  `2.6.0-bazel` sorts below that minimum. This lane awaits the coordinator's
  shared version-generator commit rather than substituting released components
  or downgrading the dependency. Runtime compatibility still requires the real
  composed checks.
- The focused fixture is a separate generated consumer assembly, configured by
  the packaged `Parallelize.targets` for method-level execution with two workers.
  Its tests read immutable run-owned expectation files and explicit Korean
  resources; only the provenance test writes its own evidence file. They do not
  mutate process culture or environment. Existing repository test scheduling
  is unchanged.
- The compiled netstandard prerequisite
  `a29a5ffc0b1005848e71a645abcdeddb818113ff` was integrated locally as
  `81a3248d192619e7fbe6476f638a6af246226ae6`, preserving both lane records in the
  append-only document conflict. Its providers supply genuine netstandard
  implementations; reference-only Microsoft.Build assemblies are supplied by
  the executing MSBuild host, not copied into the task package.
- The consumer harness now removes the exact inherited `MSBuildSDKsPath`,
  `MSBuildExtensionsPath`, and `MSBUILD_EXE_PATH` environment entries rather
  than assigning null through PowerShell's .NET method binder. The analyzer lane
  found that the latter left present-but-empty entries in this host. A local
  process-only probe confirmed the `Env:` provider distinguishes absent and
  present-empty states; the harness records presence and restores it in
  `finally`. Consumer SDK commands run from their isolated `global.json`
  directories.
- The imported task-runtime inventory identifies the selected Registry,
  AccessControl, and Principal.Windows runtime assets on Windows. The package
  projects' `RuntimeTargetsCopyLocalItems` contract is reproduced from those
  declared native dependency files under the task folder; no reference-only
  MSBuild DLL or invented support-package ID is introduced.
- Consumer restore now uses its explicit `NuGet.config` and clears
  `fallbackPackageFolders`, preserving the single run-owned cache assertion
  instead of accepting a machine Visual Studio fallback directory. Discovery
  matches MSTest's source-declared short display names and the exact total,
  not fully qualified names that the native terminal reporter does not print.
- All source prerequisites are now integrated, including the analyzer's final
  identity follow-up `6f69c9d135c542f6c1f3bacf4e003da66506e9f3` and core
  netstandard metadata `b156a7381a6b627d504e2d2b06e4002c6d3d97e4`. The latter
  is compatibility-only; this lane does not claim portable assembly
  `TargetFrameworkAttribute` parity from it.
- The first full closure attempt found that root Bzlmod could not resolve the
  adapter's `@platforms//os:windows` selector. A direct dependency on
  `platforms` 1.0.0 exposes the already-selected module; `bazel mod graph` and
  the existing lock confirmed that version before the edit. This was an
  analysis/setup failure, not test-value evidence.
- The two-worker C: closure then completed 601 local actions but failed two
  missing modern platform inputs introduced by the compatibility declaration:
  `JsonExtensions.cs` and `JsonPropertyBagDeserializer.cs`. Neither is tracked;
  the original project removal list tolerated them, whereas `linked_srcs`
  requires real files. The framework-parity owner was given that exact failure
  for a narrow correction. This is an integration/build failure, not a failing
  consumer assertion.
- That attempt did produce the real framework package: its inspected archive
  has 89 entries, version `4.6.0-preview.bazel`, and the four expected analyzer
  dependency groups with only compile/runtime excluded. SHA-256 is
  `C589B3D8DE0DB7212AAB560621D6E037993A95E499F1F8BAF61B9E36EB01A27F`.
  This partial artifact is not a full closure or consumer pass.
- The canonical selector correction
  `46e50f3636cc8a7fcd3c5918b2c204a1282243ab` unblocked modern compilation.
  The next native pack exposed a lane-owned request mismatch: the upstream
  packer's `versionFile` accepts a version, not a dependency range. The small
  native lane helper now resolves the platform version into literal
  `dependency.version` ranges in the request; the pinned upstream packer still
  writes the actual archives. No package dependency or upper bound was dropped.
- `.\eng\bazel.ps1 build //:mstest_package_closure --jobs=2 --noshow_progress --show_result=0`
  then exited 0, producing all eight archives. Independent archive inspection
  confirmed the two corrected version families and entry counts: framework 89,
  adapter 75, Telemetry 78, Platform.MSBuild 112, platform 87, TRX abstractions
  17, TRX reporter 78, and analyzers 55. Package paths, SHA-256 hashes, and
  dependency metadata are recorded in
  `C:\tfb\d7ce736ef101\mstest-validation\built-packages.json`; the producer log
  is `closure-range-correction.log` beside it. This is native package production
  evidence; real consumer and sensitivity checks are the next gate.

### Package target contract

| Target | Package / result | Native payload contract |
| --- | --- | --- |
| `//:framework_package` | `MSTest.TestFramework`, MSTest version family | Core DLL/docs/PDBs/satellites in `lib/<tfm>`; Extensions DLL/docs/PDBs and existing hooks in `buildTransitive/<tfm>`. |
| `//:adapter_package` | `MSTest.TestAdapter`, MSTest version family | Adapter and PlatformServices in `buildTransitive/<tfm>`, shared localized satellites, and unchanged registration/resource/parallelization hooks. |
| `//:telemetry_package` | `Microsoft.Testing.Extensions.Telemetry`, MTP version family | Four portable assemblies/resources, existing build hooks, native platform bound and locked external dependencies. |
| `//:platform_msbuild_package` | `Microsoft.Testing.Platform.MSBuild`, MTP version family | Portable Extensions.MSBuild in `lib/<tfm>`; genuine netstandard task/platform binaries and selected runtime companions in `_MSBuildTasks/netstandard2.0`; unchanged root-relative build assets. |
| `//:mstest_packages` | Four packages above | Does not change the existing MTP-only `//:packages` aggregate. |
| `//:mstest_package_closure` | Eight-package isolated feed | Those four, the existing three MTP/TRX packages, and the independent native analyzer package. |

Each individual target exposes exactly one nupkg in `DefaultInfo`, the existing
`NuGetPackInfo` with a generated version file, and `nuspec` / `package_request`
output groups. Its public package groups are the four declared portable TFMs.

### Executed unsigned consumer gate (2026-11-03)

The complete focused command exited **0**, with all **31 expected-exit steps**
validated:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfb\d7ce736ef101'
.\eng\bazel\validate-mstest-packages.ps1 -OutputRoot 'C:\tfb\d7ce736ef101\mstest-validation' -VerifySatelliteSensitivity
```

Source state: `42dac12d6c5d7c79d1d6b1aed2fd20bc0683396a` plus the retained
`validated-harness.patch`, SHA-256
`FA281DC1151587A102AD3811642B4C8580B4C5BD89624AE2F39F34A8A0A3193B`.
The patch only prevents copying a read-only original adapter archive into the
counterfactual output slot; the native packer creates that variant afresh.
Full evidence is retained under
`C:\tfb\d7ce736ef101\mstest-validation\20261103-152848-a61a3ac9`.
It includes all eight original feed packages, isolated caches, generated
projects/assets, loaded-assembly hashes, command logs/binlogs, 13 TRX files, the
counterfactual request/package, the validated patch, and `validation.json`.

| Consumer TFM | Actual self-contained runtime | Discovery / pass / intentional failure / empty selection |
| --- | --- | --- |
| net8.0 | .NET 8.0.33 | Exits 0 / 0 / 2 / 9. |
| net9.0 | .NET 9.0.22 | Exits 0 / 0 / 2 / 9. |
| net10.0 | .NET 10.0.14 | Exits 0 / 0 / 2 / 9. |
| net11.0 | .NET 11.0.0-rc.3.26522.148 | Exits 0 / 0 / 2 / 9. |

Each discovery listed the four declared cases. Each positive run executed and
passed exactly three cases with no skips. Each negative run executed only
`IntentionalFailure`, returned 2, and emitted the expected assertion marker.
The published applications ran with `DOTNET_ROOT` pointing to a nonexistent
directory; their loaded CoreLib locations were verified inside their own
published layouts. The native netstandard MSBuild task also launched the net11
application through `InvokeTestingPlatform`: parent exits 0 and 1 corresponded
to three passing cases and the one intended failing case, with separate TRX
reports. Both paths retained result directories containing spaces and `!`.

| Requirement | Evidence |
| --- | --- |
| Native resolution and runtime provenance | `NativeAssembliesAreLoadedFromConsumer` checks all nine runtime assembly hashes, local loaded paths, and Bazel metadata. The outer harness checks the eight package/cache hashes, exact restore closure, and published CoreLib path. |
| Bundled localization | `KoreanSatellitesAreCopiedAndLoadable` checks three actual satellite files, hashes, load paths, and translated strings against the source catalogs. Archive checks require every declared framework/adapter source culture. |
| Build and registration hooks | `PackagedTargetsProvideExtensionsAndParallelization` checks the Extensions assembly, generated method-level/two-worker attribute, and generated option-default configuration. The outer harness also checks the generated MSTest builder registration. |
| Discovery, reports, and failure propagation | All four named cases are discovered; positive/negative TRX identities and counters are reconciled. `IntentionalFailure` supplies the failure marker; zero-result runs and real native MSBuild parent exits are checked separately. |

### Test-value evidence: package payload removal

The same three positive consumer cases and filter
`FullyQualifiedName!~IntentionalFailure` ran in all three states:

| State | Executed | Passed | Failed | Host exit |
| --- | --- | --- | --- | --- |
| Correct native package, before | 3 | 3 | 0 | 0 |
| Repacked adapter without its Korean satellite | 3 | 2 | 1 | 2 |
| Correct native package, fresh restored cache | 3 | 3 | 0 | 0 |

**Executed: detected.** `KoreanSatellitesAreCopiedAndLoadable` failed at
`Assert.IsTrue(File.Exists(satellitePath))` with
`Missing native Korean satellite: MSTest.TestAdapter.` Both package production
and consumer publication succeeded for the negative variant. It is not a
compile/restore/launch failure. Only
`buildTransitive/_localization/ko/MSTest.TestAdapter.resources.dll` was removed
from the payload; all remaining payload entries and the nuspec are byte-identical.
The native packer regenerated the expected OPC core-properties identifier and
relationship metadata.

Counterfactual request SHA-256:
`FB446EAB0440A7CF110F7F7A39BFBE949F1199A962D63EE48DBF637AFF25CE70`.
Original adapter archive SHA-256:
`5658CB1D5468AA681310AE9CB56C3722AD3094A7D10CB6D698033BC9A576DC5C`.
Counterfactual archive SHA-256:
`738061DC328BFA039FF6ACC56927D96B7F420C3FCF003F1F68EEE68B29CA9081`.
`counterfactual-package-diff.json` records the exact payload comparison.

The native producer action graph contains eight `NuGetPack` and 685
`CSharpCompile` actions, with no MSBuild executable or `dotnet build/pack/test`
invocation found in producer command arguments. The graph and its compact
summary are retained in the same evidence directory. MSBuild was used only for
external consumers.

Earlier harness failures were corrected rather than hidden: failed-test messages
must be read from TRX `ErrorInfo` rather than PowerShell's null
`XmlDocument.InnerText`; nested command-line quotes must use MSBuild `%22`
escaping; and a counterfactual writer must not overwrite a copied read-only
Bazel archive. The final complete command includes all affected checks.

This gate is **Windows x64 and unsigned-native-closure evidence**. It does not
establish Linux/macOS, net462/Windows-app-model, or official-release parity.
The separately owned default SDK consumer exposed the external CodeCoverage
strong-name requirement for MTP's Microsoft public key. The coordinator owns
carrying the existing public-signing contract into native outputs; these unsigned
packages/results are preserved for comparison, not substituted with released
MTP or presented as default-SDK/CodeCoverage success.

### Signed package consumer refresh (2026-11-03)

The shared identity correction
`aff2cf0f702a07779cee99a8acf29cf515e858bb` is integrated here as
`0ff1d234924f01d4b893cdd443a210ee3fee8f48`. The absent SDK/alias lane files and
alias-patch wiring remain absent; the native signed runfiles source helper is
retained. The necessary blank context line in the upstream signing patch is
preserved as patch syntax, not trimmed to satisfy a generic whitespace check.

The same complete command shown above exited **0** again: **31 expected-exit
steps**, all eight freshly public-signed archives, new isolated package caches,
and all four actual self-contained runtimes. In addition to the unchanged hash,
layout, configuration, discovery, report, and exit assertions,
`NativeAssembliesAreLoadedFromConsumer` now requires
`B03F5F7F11D50A3A` on all nine loaded product assemblies.
`KoreanSatellitesAreCopiedAndLoadable` requires the same token on the three
loaded satellites. Native MSBuild parent exits remain 0/1. These checks exercise
the packaged outputs, not a released MTP replacement.

Source state is the integrated commit above plus `validated-source.patch`,
SHA-256
`079F51DD4F1BA30E73FCB85D533349B1EE6B5DB952ED450762339A75BD55534A`.
Signed evidence is retained separately at
`C:\tfb\d7ce736ef101\mstest-validation\20261103-161417-ca2c12d5`,
including the new feeds, caches, loaded public-key/hash records, logs/binlogs,
and all 13 TRXs. The original unsigned run remains intact.

Signed framework archive SHA-256:
`6C37A03B2025A00E8120E1C5B5F70713DD5936B5C3ACB5E09C814A2E3E4FB35B`.
Signed adapter archive SHA-256:
`AA105CE6E5542E5AB4C9411BE7AF1FCBA5331A6185B63ECD299044CAFEAA5D80`.
`validation.json` records all eight package hashes and the exact invocations.

**Test-value evidence — executed: detected.** The signed refresh repeats the
same satellite-removal comparison without changing the three selected test
identities or assertions: green 3/3 (exit 0), red 2/3 with the intended
`KoreanSatellitesAreCopiedAndLoadable` missing-file assertion (exit 2), then green
3/3 from a new correct-package cache (exit 0). Counterfactual request SHA-256:
`1D6AD21519AF76843858F87DD5C77346FF1BE38B206944287BC17793246690EF`.
Publication succeeded for both consumer variants; no setup failure or skipped
test is counted as red evidence.

The signed payload comparison again confirms one omitted satellite and
byte-identical remaining payload/nuspec entries (apart from regenerated OPC
metadata). Signed counterfactual archive SHA-256:
`C275E9CBDC9D4A0DFB7AF6089BAFE8496BC4DEEA6C8BC05CA5396B6969D209E0`.
The refreshed signed action graph still has eight `NuGetPack` and 685
`CSharpCompile` actions and no MSBuild producer invocation. The run directory
contains both compact summaries and the complete graph.

This completes this lane's portable framework/adapter and support-package
contract. Default SDK/meta-package/CodeCoverage composition remains the
separately owned SDK lane's claim to validate. Full legacy/Windows-specific
package layouts, Linux/macOS execution, official signing/publishing, SDK12,
release-branch servicing, and replacement of the Arcade pipeline are not implied.

## 2026-11-03: native public signing and friend identities

The default SDK consumer and the Windows controller independently reached the
same real composition failure: the repository-pinned CodeCoverage extension
requires `Microsoft.Testing.Platform, Version=2.6.0.0,
PublicKeyToken=b03f5f7f11d50a3a`, but native compilation emitted an unsigned
platform. Fresh isolated-cache provenance ruled out a stale or released MTP
substitution. The NuGet preview suffix fixes package version ordering, not
assembly identity. The SDK None profile and the default meta-package have
separate executed successes; they do not establish default SDK compatibility.

The existing repository contract is already public signing:
`Directory.Build.props` selects `StrongNameKeyId=Microsoft`, and the pinned
[Arcade StrongName.targets](https://github.com/dotnet/arcade/blob/4163bf82769cbb7f0a6e1e02f85090d30b53e988/src/Microsoft.DotNet.Arcade.Sdk/tools/StrongName.targets)
selects `PublicSign=true` and its public-only `MSFT.snk`, including on Windows.
The 160-byte public blob was independently read and verified: SHA-256
`933598639CBAA1DE502F80D2FD1DB78F13C8D7BB64A5FDC1BC73AC0B5CE4F5CA`,
token `b03f5f7f11d50a3a`. No private key, signing service, machine registration,
verification bypass, or official-signing claim is involved.

Mirror that existing development-build contract in native source assemblies,
test hosts, and satellites. Preserve the distinct `MoqPublicKey` for
`DynamicProxyGenAssembly2`; other native friends use the Microsoft public key.
The upstream C# provider currently matches friend names literally, so qualified
friend attributes also require simple-name normalization for internal-reference
selection. The actual compiler attribute retains the full public key and still
enforces the friend identity. Keep this as a separate C# patch, independent of
package-reference selection and Windows targeting.

Leaving assemblies unsigned cannot satisfy the real consumer. Replacing native
MTP with released bits, disabling default coverage, or changing CodeCoverage is
not an acceptable solution. Signing only MTP would break its compiled friend
closure. Public signing is the smallest existing-contract correction; official
signature completion, publication, and replacement of Arcade remain out of scope.

Before implementation, extend the existing alias/friend conformance fixture
with emitted-public-key and satellite-identity assertions. Retain its three
existing cases and method-level, CPU-count scheduling. All new observations
read stable assembly metadata or load an explicitly named culture; no test
changes global culture, environment, files, or scheduling, so no new lock is
needed. Run the same cases before and after the identity correction, preserve
their reports and source states, then re-run the real SDK/controller consumers
from freshly packed native artifacts. Compilation failures alone are not
test-value proof. Results and remaining platform limitations will be recorded
after execution.

The first regression attempt then found a genuinely coupled compiler failure:
the two native build-helper test hosts referenced upstream's unsigned `runfiles`
assembly, which now triggers CS8002 under the unchanged warnings-as-errors
policy. No tests executed in that failed attempt. Rather than suppress the
warning, weaken signing, or modify the helper's behavior, export its unchanged
pinned source and compile it as `//eng/bazel:runfiles` with the same public key
and original netstandard2.0 target. Only native tooling tests consume this
helper; it is not added to any shipping package.

### Executed identity comparison

The comparison starts at `960366ed123ea6d75083afbcaa68d071f7983665`.
The retained-test unsigned patch is
`0097C523566740B27C1BF18020AA93329A28C7F9AC35AB48F97B252E4E9D94D2`;
the first public-signing implementation patch is
`FA6B86EED4ACC3991B2EA665708209F00CCFDC14942B77C0C24C8C7DF7DEC42C`.
With `TESTFX_BAZEL_OUTPUT_ROOT=C:\tfb\aliases-df07ee`, both states ran:

```powershell
.\eng\bazel.ps1 --host_jvm_args=-Xmx2g test //:reference_alias_tests --jobs=2 --nocache_test_results --test_output=errors --noshow_progress
```

| State | Executed result |
| --- | --- |
| Unsigned, regression tests retained | Bazel exit 3; 9 executed, the 3 existing alias/friend cases passed and 6 identity cases failed, zero skipped. Each failure expected `B03F5F7F11D50A3A` and observed an empty token. |
| Public-signed | Bazel exit 0; the same 9 display identities passed, zero failed/skipped. |

`NativeAssembliesHaveThePublishedPublicKey` separately checks the platform,
framework, test host, globally visible alias, and named-only alias assemblies.
`SatellitesPreserveTheParentAssemblyIdentity` loads the actual French framework
satellite and checks name, version, culture, and its published public-key token.
The unchanged alias cases exercise global/named identity and actual internal
member access. Both runs compile net8.0 and execute on the pinned SDK11 runtime.
Fresh TRXs were parsed and their exact display-name multisets reconciled.
Reports, logs, and reproducible patches are retained under this session's
`files/native-signing/red` and `files/native-signing/green`.

The final patch, including the unchanged-source signed runfiles helper, is
`DB1E09501F89D8EE4B2D9166E872F2F5561FCDA5CD4BBBF9E2068CC44C8FA78A`.
The command below exited 0; all four fresh reports were parsed: alias/identity
9/9, PlatformServices 1,215/1,215, build helper 13/13, and SDK package helper
12/12. Total: 1,249 passed, zero failed/skipped. The existing PlatformServices
suite exercises real mock/friend access, and both tooling suites execute their
real native helpers through the signed runfiles source.

```powershell
.\eng\bazel.ps1 --host_jvm_args=-Xmx2g test //:reference_alias_tests //:platform_services_tests //:build_tools_tests //:sdk_package_tests --jobs=2 --nocache_test_results --test_output=errors --noshow_progress
```

Evidence is under this session's `files/native-signing/final`, including
`results.json`, four TRXs, the build events/log, and the complete source patch.
Only documentation was updated after that source state. This is a validated
shared compilation/identity checkpoint, not a claim that the signed ten-package
SDK or Windows-controller composition has already run; their owners must repack
and execute those consumers against this checkpoint.
## 2026-11-03: completed compatibility validation

### Complete compatibility source matrix

The opt-in matrix now compiles **26 netstandard2.0 and five net462 source
variants**. Every implementation DLL was inspected and has exactly one
`TargetFrameworkAttribute` naming its requested framework. The generated
`managed_netstandard2_0.json` and `managed_net462.json` inventories include the
assembly names, output paths and runtime dependencies; retained
`verified-project-frameworks.json` additionally records the owning project,
actual framework metadata and SHA-256.

All labels below are workspace-root labels. Append `_netstandard2_0` or
`_net462` to the listed assembly label to select that exact output:

| Framework | Validated source labels |
| --- | --- |
| netstandard2.0 | `ai`, `azdo_report`, `azure_foundry`, `configuration`, `crashdump`, `ctrf`, `framework`, `framework_extensions`, `github_report`, `hangdump`, `hosting`, `hotreload`, `html`, `junit`, `logging`, `msbuild_extension`, `opentelemetry`, `platform`, `platform_msbuild`, `retry`, `server_client`, `telemetry`, `trx`, `trx_abstractions`, `video`, `vstest_bridge` |
| net462 | `adapter`, `framework`, `framework_extensions`, `mstest_hosting`, `platform_services` |

The existing modern `managed` / `managed_all` defaults remain unchanged.
Fixed-framework analyzers and generators are not duplicated in this aggregate.
The server-client validation assembly uses its project-defined seven additional
polyfills and Jsonite source selection, not the full repository polyfill glob.

#### Dependency and framework-reference decisions

Compatibility dependencies are independently locked by area rather than
overriding incompatible minimum-version requirements:

| Paket group | Contract |
| --- | --- |
| `compatibility` | The original 14-package MSBuild closure remains independent, including Immutable 8. |
| `compatibility_mstest` | Repository telemetry/ObjectModel dependencies; net462 Memory 4.6.3 and Tasks.Extensions 4.5.4. ObjectModel's net462 assets require Immutable 10. |
| `compatibility_hosting` | Repository Configuration/Hosting 8 and DI/Logging 9.0.4 rather than the unrelated AI closure. |
| `compatibility_extensions` | The existing native graph's newer AI/report convergence, allowing its required Tasks.Extensions 4.6.3 instead of forcing 4.5.4. |

Additional groups use Paket `=` constraints rather than `==` overrides.
Regenerate from `eng/bazel/compatibility` with the existing pinned Paket launcher
(`update --no-install`), then run `.\eng\bazel.ps1 mod deps --lockfile_mode=update`.
The original `eng/bazel/paket.dependencies` and `paket.lock` remain unchanged.

Net462 projects consuming netstandard assemblies need the SDK's genuine
`Microsoft/Microsoft.NET.Build.Extensions/net461/lib/netstandard.dll` facade
and `System.Runtime.InteropServices.RuntimeInformation.dll`. Native declarations
obtain these from the selected pinned toolchain, not an ambient SDK, a higher
framework's reference pack, or a copied modern implementation. They are added
only to the affected net462 adapter/hosting dependencies. Framework extensions
and platform services retain their own reference-pack `System.Configuration`
and polyfill behavior.

The separately maintained `compatibility-package-references.patch` addresses
two real reference-closure failures without changing the original shared patch:

- A newer NuGet package reference replaces an older reference of the same DLL
  basename only when package/file ownership and version are known. Source-built
  and unknown owners are preserved; a published package's numeric version must
  not evict a native assembly's placeholder provider version.
- A direct package reference selects the consumer's appropriate TFM asset.
  Its same-package transitive alternate-TFM reference must not be passed too.
  This matters for DI.Abstractions 9.0.4: its netstandard and net462 reference
  assemblies have different assembly versions and otherwise produce CS0433.

Actual compiler response files confirm that the independent MSBuild task still
uses only Immutable 8, while the net462 adapter uses only Immutable 10.

#### Executed validation and test-value evidence

Validation uses SDK 11 RC3 and isolated C-drive outputs on Windows x64.
The source baseline is `6bc33c48b9dae0e89486862f46af3c876fd7cf5d`, with the
independent core/correction commits above and this additive matrix change.

| Command / scenario | Result |
| --- | --- |
| `.\eng\bazel.ps1 test //:managed_compatibility //:compatibility_reference_tests //:compatibility_explorer_test //:framework_netstandard2_0_consumer_test --jobs=2 --noshow_progress --test_output=errors --nocache_test_results` | Exit 0; all 31 source outputs and six fresh checks: four reference-selection analysis tests and two real consumer probes. |
| `.\eng\bazel.ps1 test //:managed_all //:portable_tests --jobs=2 --noshow_progress --test_output=errors` | Exit 0; the four modern frameworks build, and all 3,443 existing cases execute/pass across ten targets, with zero failures/skips. Counts were reconciled with logs and available TRX files. |
| `.\eng\bazel.ps1 run //:compatibility_explorer --jobs=2 --noshow_progress '--' --filter-uid <UID> --minimum-expected-tests 1 --report-trx --report-trx-filename result.trx --results-directory <isolated-case-directory>` | Real netstandard MTP/TRX consumer: `SomeTests.TestMethod1` exits 0 with one pass; `SomeTests.TestMethod2` exits 2 with one failure; `Compatibility.NoMatchingTest` exits 9 with zero results. All three TRX files were parsed and checked. |
| Pinned SDK: `dotnet build LegacyConsumer.csproj -c Release --disable-build-servers -p:UseSharedCompilation=false -m:2 '-bl:legacy-consumer-{}.binlog'`, then `CompatibilityConsumer.Net462.exe '.NETFramework,Version=v4.6.2'` | Both exit 0. An isolated external consumer references the actual native net462 framework DLL, resolves reference assemblies 1.0.3, executes on Windows CLR/mscorlib, verifies framework metadata, and observes an intentionally mismatched MSTest assertion throwing. Its copied framework hash matches the native producer. |

The CLR consumer is validation only: MSBuild did not build the TestFx producer.
Upstream `csharp_binary` currently asks for unavailable apphost/runtime packs on
net462, so no native net462 executable target or modern-runtime fallback is
claimed. Runtime execution was on the installed compatible Windows CLR, not a
claim that the machine runs the original .NET Framework 4.6.2 release.

**Test-value evidence:**

| Contract / unchanged check | Without behavior | Correct implementation |
| --- | --- | --- |
| `compatibility_reference_newer_package_test` | Executed against isolated pre-fix rules: expected only the newer reference, but received older + newer; failed. | Passed. |
| `compatibility_reference_direct_tfm_test` | Same tests and isolated prior rules: expected direct reference only, but received the alternate TFM too; failed. | Passed. |
| `compatibility_reference_independent_test`, `compatibility_reference_native_test` | Both pass with prior rules, preserving the independent old closure and native placeholder-version reference. | Both pass; preservation is intentional. |
| `CompatibilityConsumer.Main` framework-metadata assertion | A buildable isolated producer counterfactual omitted only `CompatibilityFrameworkMetadata.cs`'s attributes. The unchanged CLR consumer exited 2 at the exact framework-name equality assertion: expected `.NETFramework,Version=v4.6.2`, actual `null`. | The identical consumer executable exited 0 before and after. Its SHA-256 is `4240E40D746FB9CA0B2827EAFB0F686B5C883EFF5BAC5D0D402E5C9412B61A39`. |

The reference-selection comparison executes the actual Starlark collector, not
a translated implementation. Four tests execute in both states: pre-fix exit 3
(two pass, two fail), then corrected exit 0 (four pass). The isolated prior
`common.bzl` SHA-256 is
`DBD5C9F95EF1F3BAF6E455CC128F48DDDF1823E6A9BBD351E97A31C60B10AF56`.
Unknown/native preservation is also an explicit positive assertion.

The metadata counterfactual uses the real native compiler arguments and source,
changing only output locations and the attribute input. Initial setup failures
from an inactive Bazel input symlink forest and a corrected diagnostic-text
matcher are not counted as red-phase evidence. Paired metadata execution covers
net462; netstandard attributes were separately inspected and exercised by the
standard consumer.

Retained evidence includes `final-verification.log`,
`verified-project-frameworks.json`, `verified-immutable-references.txt`,
`modern-test-counts.json`, `explorer-results.json`, paired analysis-test XML/logs,
the metadata counterfactual response file/results, and the external consumer's
binlog/assets. Console output and structured artifacts are the demonstration;
no media or GitHub publication is implied.

#### Remaining boundaries

No new package layout, signing, release/servicing rollout, Windows-qualified
TFM, WinRT/UWP/WPF workload, or SDK 12 claim is made here. Those remain owned by
their lanes and require their composed shipping-consumer validation. Net462
adapter/hosting compilation is not full CLR execution coverage for every
adapter feature. The source-only server client's package transformation is not
validated by its DLL compilation. Linux/macOS execution is unverified.

The two known full-MTP Windows impersonation failures are still present and
unchanged; they are not hidden by the portable-suite pass. No existing tests,
assertions, or scheduling settings were removed or weakened.

## Engineering SDK 12 registration investigation (2026-11-03)

This lane starts from `6bc33c48b9dae0e89486862f46af3c876fd7cf5d`.
Its requirement is the exact `global.json` compiler,
`12.0.100-alpha.1.26509.101`, under native Bazel, not an SDK renamed to
resemble that version. The shipping MSBuild/Arcade build and `global.json`
remain unchanged. Keep the working SDK 11 default until an explicit
engineering-SDK path has trustworthy inputs and composed-product validation.

### Contract and design gates

- The engineering archive owns the compiler and its execution runtime; its
  bundled metadata must identify both. An SDK version is not evidence that
  its runtime, reference-pack, or ASP.NET versions have the same number.
- Targeting packs, RID-specific runtime/apphost assets, and .NET 8/9/10/11
  consumer behavior remain independent compatibility requirements.
- `rules_dotnet`'s compiler-worker/apphost bootstrap SDK is independently
  pinned upstream. Changing the user compiler must not retarget those tools
  or silently change the runtime on which they execute.
- Downloads, outputs, consumer feeds, and caches are owned by this lane.
  Archive hashes must bind the real downloaded bytes; no invented release
  dates, module facts, or replacement SDK tables are acceptable.
- Native compilation, source-built consumers, and staged self-contained
  layouts must establish provenance, real test selection/results, report
  artifacts, and success/failure/zero-result exits. A successful download
  or compiler version display alone is not compiler parity.
- The coordinating Bazel migration session owns combined integration and
  rollout. No shipping-leg replacement, package publication, servicing, or
  backport is enabled by this experiment.

### Archive and compiler research

The pinned rules' `dotnet/extensions.bzl` resolves one user SDK and one
independent bootstrap SDK. `dotnet/private/sdk/releases.bzl` currently obtains
SDK archives, runtime/language versions, and release dates from Microsoft's
published release metadata, then remembers complete metadata as Bzlmod facts.
An engineering input must be explicit rather than pretending that discovery
produced a released SDK.

On 2026-11-03, the exact SDK's Windows x64, Linux x64, and macOS arm64 archives
on `https://ci.dot.net/public/Sdk/12.0.100-alpha.1.26509.101/` returned HTTP 200.
Both `sdk-productVersion.txt` and `productVersion.txt` returned the exact
engineering version. The same Windows archive on `builds.dotnet.microsoft.com`
and the published `release-metadata/12.0/releases.json` returned HTTP 404.
The conventional `.sha512`, `.sha512.txt`, `.sha256`, and version-level
checksum names probed there also returned 404. These are availability
observations, not archive verification or proof of installed contents.

The first baseline command used a task-owned repository cache inside the
checkout. Bazel 9 rejects a repository *contents* cache there. The retry uses
a task-specific short output/cache root outside the source tree; no shared
cache was modified. Logs remain in `artifacts/bazel-sdk12`.

The checksum search resolved the official daily alias once, only to discover
the separate `public-checksums` container. The implementation will not consume
that mutable alias. All six host archives have version-specific SHA-512 files
under `https://ci.dot.net/public-checksums/Sdk/12.0.100-alpha.1.26509.101/`.
The downloaded Windows x64, Linux x64, and macOS arm64 archives match those
published hashes. `productCommit-win-x64.txt`, the SDK's `.version`, and
the bundled runtime identify VMR commit
`907535df2f7eb8e1845571867684244ad04b031b`.

The verified Windows installation executes Roslyn
`5.13.0-1.26509.101 (907535df2f7eb8e1845571867684244ad04b031b)`,
whose default/max C# language version is 15.0, and F# compiler 15.2.200.0
for F# 11.2. Its host and three Windows shared frameworks are
`12.0.0-alpha.1.26509.101`. The compiler runtimeconfig requests that exact
runtime; its `tfm` string alone is not the runtime version.

The bundled SDK metadata is transitional: `NETCoreAppMaximumVersion` and
`BundledNETCoreAppTargetFramework` still name 11, but both `net11.0` and
`net12.0` use `12.0.0-alpha.1.26509.101` reference/runtime/apphost packs.
Linux and macOS archives agree. Earlier reference packs are 8.0.31, 9.0.20,
and 10.0.12. The engineering packs are absent on nuget.org and the configured
dotnet11/dotnet-public feeds, but exact package manifests are available on
the public dotnet12 feed. Azure Artifacts rejected HEAD with HTTP 405;
the publication check therefore used GET, not a false missing-package claim.

### Narrow, reversible registration decision

Use the pinned rules' existing `dotnet_sdk_repositories` and
`dotnet_toolchains_repo` helpers from a separate, explicit module extension.
It can describe the verified archive, compiler versions, and real runtime
without patching upstream release discovery or writing fabricated Bzlmod
release facts. Validate its pin against both `global.json` SDK fields.

Keep `dotnet.toolchain` at the published SDK 11 pin. It remains the default
compiler and the explicit source of the existing reference/runtime/apphost
pack graph. Select the engineering compiler with
`--extra_toolchains=@dotnet_engineering_toolchains//:all` during validation.
That selection uses the real SDK 12 compiler and .NET 12 execution host,
while self-contained .NET 8/9/10/11 applications retain their existing
target-specific runtime packs. The bootstrap compiler stays at upstream's
10.0.401; upstream deliberately runs its tools on the newer user runtime.

This is **compiler selection, not full bundled-pack parity**. Copying SDK 12's
`net11.0` pack choice would relabel a .NET 12 runtime as the existing .NET 11
publisher and invalidate its runtime-major assertion. Silently keeping those
packs while claiming SDK 12 bundled defaults would be equally misleading.
Replacing the default or changing the target-pack policy remains a separate
integration decision after the bounded opt-in path is proven.

Disk exhaustion interrupted the first native baseline and SDK extraction on
the shared Q: volume. The verified archive was copied and rehashed on C:;
only this lane's partial files and failed Bazel output base were cleaned.
New heavy downloads, SDK installation, native outputs, and caches use isolated
C: roots. Those infrastructure failures are not compiler regressions or
red-phase evidence.

The first opt-in Bazel analysis also exposed a repository-mapping requirement:
the root-owned extension's generated toolchains refer to `@platforms`.
Declare `platforms` 1.0.0 directly in the root module (the same already-resolved
version used by `rules_dotnet`); a transitive dependency is not visible through
that extension's mapping. No upstream compatibility patch is needed.

`verify-engineering-sdk.ps1` checks a native `aquery` capture against the
independent `global.json` pin, executes the selected compiler's version
command, verifies the SDK/VMR/runtime metadata, rejects mismatched compiler
hosts and bootstrap compiler changes, and checks the existing four reference
pack versions. It deliberately describes action counts as **declared**, not
executed: an action query is not a successful build. Its companion
`test-engineering-sdk-verifier.ps1` exercises ten explicitly synthetic
graph fixtures with real installed compiler metadata, plus two checks that
success and failure restore environment values and preserve absent versus
present-but-empty entries. Those fixture results
must not be presented as native graph or composed-product validation.

The source-build validation was paused at the coordinator's request to let
other prerequisites use the shared host's first compiler wave. Both owned
Bazel servers were stopped. The interrupted C: baseline had not executed
the tests and is not a passing baseline.

### SDK-specific verification procedure

Use one task-owned short `TESTFX_BAZEL_OUTPUT_ROOT` per concurrently active
worktree. Keep large artifacts/caches on a volume with sufficient free space.
The following are the intended native gates, not claims that the paused
builds completed:

```powershell
$toolchain = '--extra_toolchains=@dotnet_engineering_toolchains//:all'
.\eng\bazel.ps1 build //:managed_all $toolchain --jobs=2
.\eng\bazel.ps1 test //:portable_tests $toolchain --jobs=2 --nocache_test_results
$executionRoot = & .\eng\bazel.ps1 info execution_root
.\eng\bazel.ps1 aquery 'deps(//:managed_all)' $toolchain --include_param_files --output=jsonproto > "$evidence\actions.json"
.\eng\bazel\verify-engineering-sdk.ps1 -ActionGraph "$evidence\actions.json" -ExecutionRoot $executionRoot -OutputPath "$evidence\compiler.json"
```

Check every command's exit before continuing, and capture query stderr
separately from its JSON. Retain build/test logs beside the query: the verifier
explicitly does not turn an action description into build or runtime proof.
Native package validation must still run against source-built NuGets and the
published SDK 11 consumer, with the engineering override applied to the Bazel
producer only. Self-contained .NET 8/9/10/11 layouts must still pass their
independent runtime-major/CoreLib-path assertions.

The verifier's focused fixture command was executed on Windows with the
verified SDK 12 installation and the baseline's downloaded SDK 11:

```powershell
.\eng\bazel\test-engineering-sdk-verifier.ps1 -EngineeringSdkDirectory $sdk12 -PublishedSdkDirectory $sdk11 -EvidenceRoot $evidence
```

The latest run exited 0 with twelve checks passed: `NoSourceActions`, `MismatchedCompilerHost`,
`WrongNet11ReferencePack`, `MissingFrameworks`, `ChangedBootstrapCompiler`,
`OutsideExecutionRoot`, `PublishedCompilerRejected`,
`VerifiedEngineeringCompiler`, `EnvironmentRestoredAfterSuccess`, and
`EnvironmentRestoredAfterFailure`, plus `RequestedFrameworkSubset` and
`ExpandedCompilerArguments`. Ten deliberately use synthetic graphs; they test
the verifier, not the Bazel compiler graph.

An additional existing native `build_tools.dll` (`net10.0`, built during the
SDK 11 baseline attempt) successfully executed its `version` command on the
verified SDK 12 host with both roll-forward environment variables absent.
Host tracing selected
`shared/Microsoft.NETCore.App/12.0.0-alpha.1.26509.101`, and the command
returned 0 and wrote `2.6.0-bazel`. This rules out an assumed prerequisite
for an ambient prerelease-roll-forward setting on this host; no speculative
launcher patch was made. It is runtime compatibility evidence for that
already-built helper, not evidence that SDK 12 compiled it. An earlier call
used the wrong `packageversion` command name and reached the helper's argument
error; that was not a host-startup failure or regression evidence.

### Executed selection evidence

Implementation commit: `1bbefce63f8cc733831d62835deca9a187b6448e`.
The shared package-version/evidence-root update
`2d79cf530332c91d54e62f95f37b85c784984209` was applied locally as
`9df2a5f363b89c88bb5befa600c04a8b234e187b` for the forthcoming consumers.

At that source state, the cached
`cquery @rules_dotnet//dotnet:resolved_toolchain` with
`--extra_toolchains=@dotnet_engineering_toolchains//:all` exited 0.
It configured the actual toolchain and reported:

```text
sdk_version=12.0.100-alpha.1.26509.101
runtime_version=12.0.0-alpha.1.26509.101
runtime_tfm=net12.0
csharp_default_version=15.0
fsharp_default_version=11.2
```

Both `runtime_path` and `csharp_compiler_path` point into Bazel's
`+engineering_sdk+dotnet_sdk.12.0.100-alpha.1.26509.101_x86_64-pc-windows-msvc`
repository. The materialized `csc.dll` has SHA-256
`2A7EF338D000A8814BB1CAC533ED6694DF1AEC0FA8BE6716C69972F68A3CF513`,
matching the separate checksum-verified archive installation. The query ran
**zero build actions**; this is selection/provenance evidence only.

The opt-in does not enable `net12.0` source targets or claim the SDK's
transitional targeting defaults. Those remain bounded by the published
SDK 11 pack graph. Windows x64 SDK/compiler execution is established;
Linux x64 and macOS arm64 archive bytes and metadata were verified, not
executed. The other three host hashes were read from the authoritative
checksum feed, without claiming downloaded or executed archives.

### Bounded native compilation and consumer proof

On `9df2a5f363b89c88bb5befa600c04a8b234e187b` (plus documentation-only
working changes), this command completed with exit 0:

```powershell
.\eng\bazel.ps1 --host_jvm_args=-Xmx2g --host_jvm_args=-XX:ActiveProcessorCount=2 build //eng/bazel:build_tools //:explorer --extra_toolchains=@dotnet_engineering_toolchains//:all --jobs=2 --loading_phase_threads=2
```

The isolated C: output/cache configuration produced 157 actions, including
106 local processes; elapsed time was 193.922 seconds. This is an executed
native build, not an SDK download or a wrapper around MSBuild.

The subsequent
`aquery 'deps(set(//eng/bazel:build_tools //:explorer))' --include_param_files --output=jsonproto`
uses the same engineering selection. Its SHA-256 is
`B5C0F9AD748B5F7B08C1CBBEF5EE67163F4E0FA1EF9C1F250E0DBB3868144E4A`.
All 34 source-owned C# actions name the SDK 12 compiler. Two additional
*declared* upstream tool actions (`scoped_css` and `static_web_assets`) name
SDK 10.0.401 and reference pack 10.0.12. They are not counted as executed
bootstrap tools merely because the query traversed them. The source-owned
net10 compilation retains reference pack 10.0.14.

The compiled explorer's actual `--info` output reports platform version
`2.6.0-preview.bazel` and `win-x64 - .NET 12.0.0-alpha.1.26509.101`.
With the same `run //:explorer` toolchain selection:

| Application arguments | Observed result |
| --- | --- |
| `--list-tests` | Exit 0; exactly four named sample tests discovered. |
| `--filter-uid TestingPlatformExplorer.UnitTests.SomeTests.TestMethod1 --report-trx --report-trx-filename sdk12-pass.trx --results-directory <owned path with spaces!>` | Exit 0; TRX contains exactly one executed/passed `TestMethod1`, zero failures/skips, and the real controller observes child exit 0. |
| `--filter-uid TestingPlatformExplorer.UnitTests.SomeTests.TestMethod2` | Test executes and fails intentionally; child and Bazel-run exits are both 2. |
| `--filter-uid NoMatchingTest --minimum-expected-tests 1` | Zero-result diagnostic; child and Bazel-run exits are both 9. |

The outer bounded harness exits 0 after asserting those child outcomes.
Evidence is in the task-owned `bounded-consumers/dfc2058d51064e2c8681b67ea1322425`
directory, with the actual command arrays in `validation.json`, individual
logs, and the parsed TRX. Both roll-forward environment variables were removed
for this run. The built platform DLL SHA-256 is
`4731C9ACBEA68117200BE683113E19BCDBD9E7A0FDFB0C6141012BA41591070A`.

Initial harness attempts exposed three harness-only mistakes: a status message
was accidentally returned with log data, the ordinary test output was assumed
to include the full banner instead of querying `--info`, and the outer shell
inherited the intentionally nonzero final child exit. These were corrected
without changing product code or assertions about the intended test outcomes.
Only the completed exit-0 harness above is the bounded integration result.

This proof is for the helper and source-built MTP/TRX explorer on net10 using
the SDK 12 host. The .NET 8/9/10/11 package/publisher gate and broader combined
source/test graph are separate validation stages.

The actual Bazel 9 query expands response-file arguments directly into each
action's `arguments` array. The verifier handles that real format as well as
explicit `paramFiles` fixture input. `-TargetFrameworks net10.0` permits a
bounded audit without claiming the other three frameworks were checked;
omitting it still requires all four. That scoped audit passed against the
real bounded query above (34 source actions, SDK 12, net10 reference 10.0.14).
The extra fixture cases cover both the explicit subset and native expanded
argument shape.

### Independent consumer SDK correction

The first SDK 12 package build passed, but the existing package validator
looked for SDK 11 under `execution_root/external`. A distinct consumer SDK is
not necessarily a producer action input: the actual SDK 11 repository existed
under `output_base/external`, while its execution-root counterpart was absent.
This was a validator-location defect, not a missing SDK or package feed.

The shared correction is commit
`6d851a91cf44d479ebf549996b337c26849f4578`. `Get-ConsumerSdk` explicitly
queries the declared/default toolchain, locates its materialized repository,
retains the unique-match check, verifies the selected executable and archive
`.version`, and records consumer provenance. It never substitutes SDK 12 or a
machine SDK. Actual production-function checks passed with both SDK 11 and
SDK 12 producer selections, returning SDK 11 in both cases.

An isolated targeted counterfactual retained the function and changed only
`info output_base` back to `info execution_root`. With SDK 11 materialized
but not staged, it reproduced
`Expected one materialized consumer SDK for the execution host.`
Restoring the correct function passed. Counterfactual function SHA-256:
`7541D195B89080037DF3A7E358BB2D35DC7FC73EFF23B69B92FE932696524DC2`.
The local `locator-checks/f49dc4c2edbe413b9801baa38f46acec` evidence retains
the actual functions, command results, default/engineering contexts, and
restored outcome.

### Completed bounded package and runtime gate

Executed code revision:
`6d851a91cf44d479ebf549996b337c26849f4578`. A task-owned ignored
`.bazelrc.local` selected the engineering toolchain for Bazel producers and
the two-processor/2 GiB JVM cap; the delivered `MODULE.bazel` default stayed
unchanged. All output, SDK, download, and primary package-cache writes used
owned C: directories. The consumer process's MSBuild-path and roll-forward
overrides were removed before execution.

```powershell
.\eng\bazel\validate-packages.ps1 -Jobs 2 -EvidenceRoot C:\tfb\9d855da170b9-sdk12\package-validation
```

Exit **0**; all **22** recorded steps matched their expected exits. The run
`20261103-151148-505d36d4` retains `validation.json`, exact command arrays,
four consumer binlogs/assets files, package inventories/hashes, five passing
TRX reports, and failure/empty-selection logs:

| Boundary | Executed result |
| --- | --- |
| Native MTP/TRX producer | Three `2.6.0-preview.bazel` NuGets built with SDK 12 for all four portable TFMs. |
| Independent package consumer | SDK `11.0.100-rc.3.26522.148`, source commit `5254d8bcf59917edc1e1abc33bdf2442b77b1c70`; builds for net8/net9/net10/net11 all exited 0. |
| Loaded package provenance | All three loaded net11 DLL hashes matched the native feed and carried the native build metadata. Pass/failure/empty-selection exits were 0/2/9. |
| Isolated net8 publisher | Actual `.NET 8.0.33`, CoreLib in its copied layout, one passing test plus expected exits 2/9. |
| Isolated net9 publisher | Actual `.NET 9.0.22`, CoreLib in its copied layout, one passing test plus expected exits 2/9. |
| Isolated net10 publisher | Actual `.NET 10.0.14`, CoreLib in its copied layout, one passing test plus expected exits 2/9. |
| Isolated net11 publisher | Actual `.NET 11.0.0-rc.3.26522.148`, CoreLib in its copied layout, one passing test plus expected exits 2/9. |

Each passing report contains exactly one executed `TestMethod1`, with no
failures or skips. The deliberate `TestMethod2` failure and zero-result
selection are separate child-product outcomes, not failing outer validation.
Publishers ran outside the Bazel tree with `DOTNET_ROOT` pointing at a
nonexistent directory.

Native package SHA-256 values:

| Package | SHA-256 |
| --- | --- |
| `Microsoft.Testing.Platform` | `31EFFC2BDF11A0B8FD9761F076B59FFCF29CFD5F151B7F4EFB1E8BC899F63262` |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions` | `36CD8D67B72866ED8C243E0C2C52C11245B26DB52B4856AB7E75A9CFC303BCE7` |
| `Microsoft.Testing.Extensions.TrxReport` | `53395F8FC6042CFB175DA5713192D3F56647ED6721D98AF2A5F82C02FE029431` |

The complete package/publisher action query has SHA-256
`4FF508318AAD842906780A57EC746BDD4375F7A55565FAB178507FF166FA8BE1`.
The audit passed **129** declared source C# actions using SDK 12, all four
preserved reference-pack versions, and **four** SDK 10.0.401 bootstrap
declarations. The materialized `nuget_pack` and `apphost_shimmer` launchers
in their actual **exec configurations** select the SDK 12 runtime. Native
packing and apphost generation executed successfully with that separation;
unbuilt top-level configurations are not substituted as evidence.
The inspected producer graph contains 465 declared actions and zero MSBuild
invocations; MSBuild is used only by the external package consumers.

The net8 consumer binlog independently records SDK 11's `NETCoreSdkVersion`,
`MSBuildSDKsPath`, `MSBuildToolsPath`, and compiler task/executable, including
Roslyn `5.12.0-1.26522.148`. This differs deliberately from the producer's
Roslyn 5.13. RAR records the native MTP compile/runtime reference in the run's
primary cache. All **12** MTP/TRX DLL payloads across the four TFMs were checked
against the native `.nupkg` entries, with exact compile/runtime asset selection.
Visual Studio's read-only fallback folder remained configured in NuGet;
it was not erased or falsely reported absent. The checked first-party payloads
reside in the owned primary cache, and no global cache was modified.

### Test-value evidence for engineering selection

The same native `//eng/bazel:build_tools` source and verifier were retained on
revision `6d851a91cf44d479ebf549996b337c26849f4578`. Separate owned output
roots selected the engineering SDK or the existing published SDK. Each state
successfully **built** before querying its real compiler action and executing
the unchanged verifier with `-TargetFrameworks net10.0`.

| State | Native build | Action query | Identity audit |
| --- | --- | --- | --- |
| SDK 12, before | 0 | 0 | 0 |
| Published SDK 11 selected instead | 0 | 0 | 1: exact expected-SDK assertion |
| SDK 12 restored | 0 | 0 | 0 |

This is an **executed configuration-removal counterfactual**, not a
pristine-baseline or missing-symbol compilation failure. One source compiler
action was audited in each state. The wrong state executed the actual SDK 11
compiler and failed with:

```text
Expected global.json compiler SDK 12.0.100-alpha.1.26509.101,
selected 11.0.100-rc.3.26522.148
(5.12.0-1.26522.148 (5254d8bcf59917edc1e1abc33bdf2442b77b1c70)).
```

Verifier SHA-256:
`1BF79DFD84DCE5E161D9EF5D1307B6B76A5DEE87A8EDFEEB547CA02F4F69308A`.
Correct action-query SHA-256 before and after:
`8347FDB549845482D87348585AAC77F0E2B00B258B677AF353BC46B711FD9865`.
Wrong-selection query SHA-256:
`94ED142B360FC09E5BB08D355D6C6AA4E65C3C544EB2C8884D1C6971B4462835`.
The local `native-counterfactual/c4920a4c48c9436fbefd5c94308de06c` evidence
contains all commands, build/query logs, compiler identities, and audit results.

### Delivery boundary

The opt-in engineering compiler path and bounded MTP/TRX package/runtime
contract are established on Windows x64. The default remains the working
SDK 11 toolchain. Full SDK 12 bundled-pack parity, net12 source targets, the
combined source/test matrix from the other migration lanes, Linux/macOS
execution, additional host architectures, workloads/AOT, and shipping
signing/release/servicing parity are **not** claimed. The coordinating
migration session owns their integration and any later default/rollout decision.
No shipping pipeline, `global.json`, machine policy, or global setting changed.

The temporary `.bazelrc.local` was removed after verification. A final query
without an engineering override returned SDK `11.0.100-rc.3.26522.148` and
runtime `11.0.0-rc.3.26522.148`. Both owned Bazel servers were shut down.
Only the obsolete interrupted Q: cache and failed partial cache move were
cleaned; verified downloads, active task-owned C: caches, and reproducible
evidence remain available. Delivery is local/committed, not pushed, published,
or CI-verified.

## 2026-11-03: portable tests and samples lane

This additive lane starts at
`6bc33c48b9dae0e89486862f46af3c876fd7cf5d`. It migrates existing portable test
projects and examples, not replacement tests or the Windows application-model
examples. The verified `portable_tests` aggregate remains unchanged until the
coordinator composes independently passing additions. Package/SDK production,
shared toolchain changes, final aggregate counts, and CI remain separate lanes.

### Contracts and first inspection

| Existing project | Native wiring requirements |
| --- | --- |
| TerminalReporterContract | Compile the terminal manifest's linked sources and resource into the consumer, preserve the public-abstraction exclusions when MTP is referenced, and separately establish standalone compilation. |
| Platform.MSBuild | Preserve the `global,PlatformMSBuild` reference alias and the distinct source-linked IPC model copies. |
| ServerMode.Client.Sources | Preserve `global,serverclient`, assembly serializer initialization, and all three self-host child fixtures before normal runner argument augmentation. |
| Extensions | Preserve the `ghactions`-only alias, native extension references, portable PDBs, and the renamed conformance-vector runtime file. |
| MSTest.Analyzers | Use source-built MSTest, the existing Roslyn testing versions, release-tracking input files, and reference-pack resolution. |
| MSTest.SelfRealExamples | Preserve class-level scheduling, rather than the other suites' method-level scheduling. |

All six retain their source tests, data rows, exclusions, resource declarations,
and zero/unlimited worker settings. Existing generic native registration omits
affected-test/coverage collectors; those CI-only collectors must be recorded
separately from test behavior. Specialized child fixtures cannot be replaced
with the generic runner. No failing method will be excluded or assertion changed
to make a new aggregate green.

The producer is this checkout's native Bazel graph, with its explicit Bazel 9.1.1,
rules_dotnet, and .NET 11 RC3 pins, not Arcade outputs or released first-party
fallbacks. The consumers are actual existing test executables and samples.
Normal tests must produce reconciled case identities and TRX counts; failure,
zero-result, child exit, and cleanup behavior must remain observable. Tests
retain their scheduling while Bazel starts with `--jobs=2` on the shared host.
Outputs, generated consumers, and any additional dependency-resolution work use
lane-owned directories. This experimental branch does not enable release
rollout, servicing, signing, or publishing.

### Alternatives and staged validation

Reusing the generic entrypoint is appropriate only where it preserves the
suite's contract. Copying test bodies, adding released MSTest as a fallback,
serializing suites, or invoking MSBuild from a Bazel producer would hide missing
native wiring and is rejected. Target-specific bootstraps and explicit native
dependencies are preferred where the existing projects require them. Additional
third-party test dependencies will use a separately locked test-support graph
with coordinated module wiring.

The first validation is the existing native build-tools target before adding
targets. Subsequent additions are committed in coherent working stages with
exact commands, exits, TFMs, counts, and report locations recorded below.
Compilation/setup failures are not behavioral red-phase evidence; unchanged
tests passing under native wiring are preservation evidence, not a new-test
campaign. NativeAOT/browser feasibility will be assessed against actual upstream
build/publish paths after the portable unit targets.

### Executed setup findings

- The first baseline attempt ran zero tests: Q: filled during native compilation.
  The lane stopped its own Bazel server, removed only its failed generated
  `external`/`execroot` trees, and relocated builds to `C:\tfb\tests-05e68c`.
  Logs and large dependency caches also moved to C:. No shared cache, another
  worktree, machine policy, or test worker count was changed.
- A pure standalone compile of the terminal `.props` manifest exposed missing
  coverage-summary and retry-property dependencies. Existing MTP references
  supply those public types in the test application, but cannot prove standalone
  consumption. The extra standalone native target therefore explicitly includes
  the referenced coverage/retry model closure as well as the manifest sources,
  rather than excluding either reporter partial or adding a platform DLL.
  This is an explicit native closure extension, not proof that the current
  `.props` manifest itself is standalone.
- The pinned upstream NativeAOT publisher explicitly supports Linux/macOS, not
  Windows. Debian WSL is installed on this host; feasibility of a separate native
  Linux build remains to be established. Browser publishing uses upstream's
  WebAssembly bundler, not an MSBuild producer wrapper.
- Read-only package metadata confirmed the repository-pinned Roslyn testing
  preview is on `dotnet-tools`, not nuget.org. Its exact
  [ReferenceAssemblies implementation](https://github.com/dotnet/roslyn-sdk/blob/f98f564816af0ca213db2bcc32afed00a6d7c14f/src/Microsoft.CodeAnalysis.Testing/Microsoft.CodeAnalysis.Analyzer.Testing/ReferenceAssemblies.cs)
  also writes `Path.GetTempPath()/test-packages`, independently of
  `NUGET_PACKAGES`. The native analyzer bootstrap therefore redirects both
  temporary-directory conventions and NuGet package/HTTP caches to Bazel's
  per-test temporary directory before any runner or verifier initialization.

### Stage 1: isolated test dependencies and integrity

The additional dependencies use their own `eng/bazel/tests-support/paket.lock`
and the `testsupport.main`, `testsupport.analyzers`, and
`testsupport.benchmarks` repositories. Roslyn tests retain 4.10.0 and
1.1.3-beta1.26255.1; benchmarks retain BenchmarkDotNet 0.15.8. The complete
Hosting/Configuration implementation packages use 10.0.13, matching the source
graph's shared abstractions. No released MSTest or MTP package is introduced
into this support graph.

Upstream's hash lookup follows the preview feed's catalog URLs to unavailable
Azure `commitLog` endpoints. A successful `bazel mod deps` consequently left
all nine preview testing packages without persisted hashes. The separate
`testfx-tests-paket-integrity.patch` adds an optional explicit integrity file
to `paket.parse`, without changing existing tags. The updater downloads the
exact locked preview packages, records SHA-512 SRI values, and removes only
its uniquely named temporary downloads. Entries outside the lock or malformed
SHA-512 values are rejected. `package-integrity.json` is generated, not a
version-only or released-package fallback.

Executed on Windows x64 with the lane-owned C: output root:

| Command / check | Result |
| --- | --- |
| `.\eng\bazel\tests-support\update-dependencies.ps1 -Resolve` | Exit 0; pinned Paket 11.0.1 resolves the separate graph and records nine preview hashes. |
| `.\eng\bazel.ps1 mod deps --lockfile_mode=update --noshow_progress` | Exit 0; the explicit pins are persisted without the failing catalog requests. |
| `.\eng\bazel.ps1 build <nine @testsupport.analyzers//package:files labels> --jobs=2 --noshow_progress` | Exit 0; all nine actual preview archives are downloaded and verified against their SHA-512 pins; no TestFx compilation is involved. |
| Same digest gate over retained pre-pin and current module metadata | Before: exit 1, nine missing pins. After: exit 0, all nine exact values match the generated manifest. |

**Test-value evidence:** the metadata gate detects the actual missing-integrity
state rather than treating the resolver's exit 0 as success. Patch SHA-256:
`B2A7CBE4C97BABBBFE8A1CFCF31B3B1B0A52DC6E13EC9575993EF5673410D1ED`.
The retained `module-before-test-integrity.json`, `verify-package-digests.py`,
`integrity-{before,after}.log`, `integrity-validation.json`, and
`verify-preview-downloads.log` preserve the comparison and archive verification
under this session's `files/bazel-tests-lane` evidence. This stage validates
dependency inputs, not execution of the pending unit/sample targets.

### Stage 2: terminal contract and self-real suites

The first two existing suites are now native targets, without test-source or
assertion changes. Their source-bound inventory and independent discovery
reconcile with every executed TRX result, including duplicate display names for
the nested byte/short/long cases and the base/derived property cases.

| Target | Compiled / executed runtime | Scheduling | Discovered / executed / passed / failed / skipped |
| --- | --- | --- | --- |
| `terminal_contract_tests` | net10.0 / .NET 11 RC3 | Method level, workers 0 | 15 / 15 / 15 / 0 / 0 |
| `self_real_tests` | net10.0 / .NET 11 RC3 | Class level, workers 0 | 24 / 24 / 24 / 0 / 0 |

The terminal target also builds `terminal_contract_standalone` for net8.0 with
no platform reference, retaining all reporter partials and the explicit
coverage/retry model closure described above. The generic native bootstrap is
not substituted for the server-client child fixtures in the pending slice.

The composed compatibility graph initially failed before execution because of
two stale modern JSON source names. A temporary two-entry removal demonstrated
15/24 passes; it was then removed, and the canonical framework-lane correction
`46e50f3636cc8a7fcd3c5918b2c204a1282243ab` was integrated instead. Both suites
were rebuilt and rerun successfully on that canonical correction. No competing
selector fix is part of the test-target addition.

Discovery also exposed a metadata difference: without the SDK-generated
`TargetFrameworkAttribute`, the reporter displayed the newer runtime as the
target. Supplying only `FrameworkName` was insufficient because the existing
reporter reads `FrameworkDisplayName`. The native test metadata now supplies both
SDK-compatible fields, and the same discovery commands correctly identify
`net10.0` while execution continues on the pinned .NET 11 host.

Executed commands, with `TESTFX_BAZEL_OUTPUT_ROOT=C:\tfb\tests-05e68c`:

```powershell
.\eng\bazel.ps1 test //:terminal_contract_tests //:self_real_tests --jobs=2 --nocache_test_results --noshow_progress
.\eng\bazel.ps1 run //:terminal_contract_tests_app --jobs=2 --noshow_progress --show_result=0 '--' --list-tests
.\eng\bazel.ps1 run //:self_real_tests_app --jobs=2 --noshow_progress --show_result=0 '--' --list-tests
```

All three commands returned 0. The repository's existing `verify_execution.py`
gate then passed separately for 15 and 24 planned cases, using the independent
discovery name multisets, the captured pre-run UTC boundary, and actual process
exit 0. Retained evidence includes `stage2-final-tests.log`,
`stage2-final-run.json`, `stage2-test-inventory.json`, both discovery logs and
expected-name files, both `*-net10.trx` reports, and both execution-gate logs.
The new files preserve UTF-8 BOM encoding.

**Test-value evidence:** executed preservation checks cover all 39 existing
cases, including the real Windows STA and synchronization-context cases.
The discovery metadata guard detected the incorrect target label and passes
with both attribute fields. Earlier missing-input/compiler failures are not
behavioral red-phase evidence. An independent Arcade/MSBuild green-before run,
other runtime/TFM executions, CI collectors, and the remaining four suites are
not claimed by this slice.

### Stage 3: aliased MSBuild, server-client, and extension suites

The separately checkpointed slice exposes only three verified targets; the
analyzer target remains separate until its existing tests execute. All original
test sources, assertions, assembly initialization, resource locks, exclusions,
method-level scheduling, and CPU-count worker settings are retained.

| Target | Selected | Executed / passed | Original skips | Runtime |
| --- | --- | --- | --- | --- |
| `msbuild_tests` | 388 | 388 | 0 | SDK .NET 11, net10 compilation |
| `server_client_tests` | 313 | 305 | 8 Unix-only cases | Native self-contained .NET 10.0.14 |
| `extension_tests` | 2,870 | 2,863 | 7 OS/filesystem cases | SDK .NET 11, net10 compilation |

The exact `global,PlatformMSBuild`, `global,serverclient`, and named-only
`ghactions` semantics are preserved by the coordinated compiler patch. The
MSBuild fixture stages the native netstandard implementation at
`LegacyMSBuild/Microsoft.Testing.Platform.MSBuild.dll`, enabling all nine
existing legacy-configuration cases without a reference-assembly fallback.

The native launcher executes an application's physical DLL, not a runfiles
alias of its directory. The fixture wrapper therefore declares files beside
that actual DLL in the same TFM configuration and rejects a configuration/path
mismatch. Extension fixtures contain the original source files, including the
assertion implementation files whose real existence is part of the annotation
tests, and the correctly renamed appx conformance-vector JSON.

The server's original early child fixtures are preserved before any results
arguments are added: stdout flooding, stderr exit 17, and release-file exit 23.
Initially four tests executed but their children could not resolve the
runfiles-oriented dependency manifest. An upstream native self-contained
publish supplies a real executable/DLL/dependency layout. A separate published
test launcher retains every published file rather than renaming the apphost
away from its DLL, and declares the pinned muxer on PATH for the intentionally
missing-DLL fallback case. Neither production launch logic nor assertions were
changed to mask those failures.

After public signing and the source-root correction below, the full checkpoint
command returned 0:

```powershell
.\eng\bazel.ps1 test //:msbuild_tests //:server_client_tests //:extension_tests --jobs=2 --nocache_test_results --noshow_progress
```

The three reports reconcile to 3,571 selected, 3,556 passed, zero failed, and
the 15 original skips. The prior missing-child dependency and missing-source
failures are retained separately; launch/setup failures are not claimed as
production regression sensitivity. `stage3-checkpoint.log`,
`stage3-test-inventory.json`, `signed-test-inventory.json`, the original failure
TRXs, and final signed TRXs preserve the executed evidence. No existing
verified aggregate was enlarged by this checkpoint.

### Stage 4: all existing analyzer cases

The final prioritized suite runs all **2,102** existing analyzer cases, with
zero failures/skips, against the source-built public-signed analyzer/codefix
assemblies. The shared reference correction removes the conflicting Roslyn
3.11 transitive paths while retaining the explicitly requested 4.10 test
references. No test source, assertion, or test-version pin was changed.

The native producer's `OutputGroupInfo.globalconfigs` is narrowed to exactly
one file named `mstest-recommended.globalconfig`; no copied config text or
combination of incompatible modes is used. Inspection also corrected an
initially over-broad compiler-plugin dependency: unlike SelfReal, this project
declares analyzer/codefix projects as ordinary references and has
`UseMSTestFromSource=true`. The only inherited `OutputItemType=Analyzer`
reference is mutation-only. Removing the accidentally added native plugin
matches that project contract; individual diagnostics were not suppressed.

Actual execution exposed two additional host requirements:

- NuGet 6.3.4 consults Windows Program Files variables even with an explicit
  repository config. These are declared inherited inputs. Its temporary
  package area, package/HTTP caches, HOME/USERPROFILE, APPDATA and LOCALAPPDATA
  are isolated under `TEST_TMPDIR`; no shared user configuration is written.
- Eight tests use loaded DI assemblies as compiler references. Running net10
  test code on the SDK's .NET 11 host supplied DI 11 to net10 reference
  compilations, producing CS1705. A real upstream self-contained net10 publish,
  with declared source/release/config fixtures in its runfiles layout, aligns
  the loaded runtime and compilation references. All eight unchanged cases
  then pass, together with the other 2,094 cases.

The final full command returned 0:

```powershell
.\eng\bazel.ps1 test //:analyzer_tests --jobs=2 --nocache_test_results --noshow_progress
```

Independent discovery of the same native executable yielded 2,102 names. The
existing repository execution gate reconciled that name multiset, the captured
pre-run UTC boundary, and the actual TRX: 2,102 planned/selected/executed/passed,
zero failed/skipped, process exit 0. Retained files include
`analyzer-aligned-runtime.log`, `analyzer-aligned-run.json`,
`analyzer-discovery-native.log`, `analyzer-expected.json`,
`analyzer_tests-signed-net10.trx`, and `analyzer-execution-gate.log`.

**Test-value evidence:** this is execution of the original analyzer safety net,
including its real Roslyn compilations and diagnostic assertions. Initial
reference/compiler-policy and NuGet-profile failures are setup failures, not
behavioral red-phase proof. The runtime-mismatch failures are retained as
integration evidence; no source assertion was weakened to accommodate them.
Only the net10 runtime leg is claimed by this validation.

### Native source-root parity

Existing extension annotation tests require real source files beneath the
repository root and the existing Arcade `/_/` deterministic source-path
contract. Declaring the original source files beside the native fixture root
resolved eight of the nine initial failures. The remaining
`RealAssertFailure_AnnotationPointsAtTestMethodAndNotAtFrameworkFile` failure
observed `CallerFilePath == "./test/..."` while the real annotation normalized
it to `test/...`; its assertion already handles Arcade's `/_/` form.

The separately scoped `testfx-source-pathmap.patch` changes only the execution-
time pathmap destination from `.` to `/_/` in the Windows and POSIX wrappers,
the common worker subprocess path, and the F# worker in-process path. It does
not change signing, aliases, reference selection, test assertions, or test
scheduling. This is development source-path parity, not SourceLink parity.
The first generated multi-file patch lacked the file separators required by
Bazel's patcher; regenerating standard `diff --git` boundaries fixed that
setup error before compilation.

With the shared public-signing checkpoint integrated, this command returned 0:

```powershell
.\eng\bazel.ps1 test //:terminal_contract_tests //:self_real_tests //:msbuild_tests //:server_client_tests //:extension_tests --jobs=2 --keep_going --nocache_test_results --noshow_progress
```

The five real reports reconcile to 3,610 selected, 3,595 executed/passed, zero
failed, and 15 unchanged OS/filesystem skips: terminal 15/15, self-real 24/24,
MSBuild 388/388, server-client 305 passes plus eight Unix-only skips, and
extensions 2,863 passes plus seven existing skips. The extension test host's
actual token is `B03F5F7F11D50A3A`. The server-client run uses native self-contained
.NET 10.0.14; the other hosts use the pinned .NET 11 runtime.

**Test-value evidence:** the original extension source-location assertion
failed on the `.` mapping and now executes/passes on `/_/`, without edits or
exclusions. Those recorded before/after runs also cross the shared signing
checkpoint; they are not an isolated same-signing counterfactual. Evidence is
retained in `extension_tests-initial.trx`,
`stage4-reference-source-fixtures.log`, `signed-pathmap-suites.log`,
`signed-test-inventory.json`, and five `*-signed-net10.trx` files. Wrapper-based
Windows execution is verified; optional worker execution and POSIX/F# execution
remain separately scoped checks.

## SDK 12 public-signing refresh (2026-11-03)

Refresh the existing bounded SDK 12 MTP/TRX package/publisher gate against shared
checkpoint `aff2cf0f702a07779cee99a8acf29cf515e858bb`, applied in this lane as
`70e5cddb3045fbad3296e7cc38e58c23f4c1df0e`. Preserve the earlier unsigned
packages, copied layouts, reports, and compiler evidence separately; their
success did not prove compatibility with signed consumers such as CodeCoverage.

The integration keeps the SDK 11 default and SDK 12 opt-in unchanged. It takes
the pinned public-only Microsoft key, public-signing/friend provider patch,
signed product/satellite definitions, and the signed runfiles helper. It does
not introduce the absent SDK package tool, compatibility Paket group, or legacy
framework helpers from other lanes. Existing alias/friend fixtures remain
present and receive the checkpoint's identity assertions.

Validate the same three native MTP/TRX packages and four target/runtime versions
with fresh consumer caches and separately named signed evidence. Check the
actual public-key token, signed satellites and key-qualified friend metadata,
SDK 12 compiler actions, independently selected SDK 11 consumer, selected
package assets, loaded hashes, and exact 0/2/9 child outcomes. The old unsigned
payloads are retained as an identity counterfactual, not relabeled as signed
evidence. Public signing preserves development-build identity; it does not
complete an official signature, authorize publication, or change machine
verification policy. The coordinator retains ownership of the broader
CodeCoverage/SDK composition and combined source/test matrix.

### Refreshed signed gate results

The signed refresh executed on
`70e5cddb3045fbad3296e7cc38e58c23f4c1df0e`, with documentation-only working
changes and the same temporary engineering compiler override:

```powershell
.\eng\bazel\validate-packages.ps1 -Jobs 2 -EvidenceRoot C:\tfb\9d855da170b9-sdk12\signed-package-validation
```

Exit **0**; all **22** steps matched their expected exits. The independent
SDK 11 consumer built net8/net9/net10/net11, loaded the new native package
hashes, and returned 0/2/9 for pass/failure/empty selection. All four copied
self-contained layouts repeated those outcomes on the unchanged actual
8.0.33, 9.0.22, 10.0.14, and 11.0.0-rc.3.26522.148 runtimes. Each of the five
passing reports contains exactly one executed `TestMethod1`, zero failures
and zero skips. These are the bounded explorer cases, not the coordinator's
separate 1,249-case SDK 11 signing/mock/helper run or a new CodeCoverage run.

The signed run is retained under
`signed-package-validation/20261103-161316-124e480a`; the earlier unsigned
run `package-validation/20261103-151148-505d36d4` remains intact.
All packages still use `2.6.0-preview.bazel`, so matching versions are not
treated as proof of matching bits:

| Signed native package | SHA-256 |
| --- | --- |
| `Microsoft.Testing.Platform` | `B16EA5634803520CE303990D313139134591B3D11DC58FD91BDF72A987348A9B` |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions` | `7C83682E0CE66C7E90E10D9C8370DB7BAE04DE000E9D31365CE657CB5ADF68DE` |
| `Microsoft.Testing.Extensions.TrxReport` | `E7A15B0C863CBF2FBC241BF19F6EEBDEDD1C54D98FCF58FF7F100221A764994B` |

The actual loaded net11 platform DLL hash is
`9CC52C70A1B3989AC2848F27C4A200060F415B880B2BAB3AC96D61D01F2CB0FC`.
Loaded TRX abstraction/reporter hashes are
`BAF443AD6CD71C68C821597D8FA88563EC2204E9FFEE1EF7DEBDDEC91095F71E`
and `E168EFEF3D2850C04B6C1C18FFA9C2ECBECA2E85F415A333F4EA33408B8C6194`.
The live loader reported those hashes for both controller and child.

### Public identity and test-value evidence

The same PE/assembly-metadata audit was applied to the retained unsigned
packages and the new signed packages. It checked 116 implementation/satellite
assemblies, 128 friend attributes, 12 first-party assembly references, all
12 selected compile/runtime payloads in the fresh primary cache, and every
satellite's name/version/culture relationship with its parent.

| Input | Executed identity assertions |
| --- | --- |
| Retained SDK 12 unsigned packages | Audit exit 1: 116 published-key, 128 key-qualified-friend, and 12 signed-reference assertions failed. |
| Refreshed SDK 12 public-signed packages | Audit exit 0: all observations passed; every implementation/satellite has token `B03F5F7F11D50A3A` and the expected Microsoft public-key blob. |

This is a packed-output identity counterfactual, not a new test-execution count
or a claim that ordinary explorer tests rejected the old unsigned graph.
The earlier explorer tests passed with unsigned inputs; the identity oracle
distinguishes the defect. The audit source SHA-256 is
`D26B7157908D748DF80A9146FF3AF1582FA01F6F330AB1D5B9BD3992A7FEA2A4`;
its reports and extracted read-only inspection payloads are retained under
`signed-identity/unsigned-counterfactual` and `signed-identity/signed`.
An initial audit-harness attempt encountered PowerShell overload ambiguity
for an absent unsigned public-key blob; explicit byte-array handling fixed
that harness issue before either reported comparison run.

The actual signed package/publisher graph has SHA-256
`11FEE1B4DF6076AE6C9C69C31A1A0BC19E32BFFCA267268A11093FBBEE87C813`.
It retains 129 SDK 12 source compiler actions, all four original reference-pack
versions, and four SDK 10.0.401 bootstrap declarations. All 124 product,
satellite, and internal-reference compiler actions carry `/publicsign+` and
the one verified 160-byte public key. The remaining source actions are the
unchanged development helper and explorer executables. The complete producer
graph contains 465 declared actions and no MSBuild invocation.

The fresh net8 consumer binlog records SDK 11's compiler/MSBuild paths and RAR
resolution of `Microsoft.Testing.Platform, Version=2.6.0.0, Culture=neutral,
PublicKeyToken=b03f5f7f11d50a3a` from the fresh run-owned cache. Visual Studio's
read-only fallback folder remains present in effective restore configuration;
it is not reported absent. All 12 checked package payloads reside in the owned
primary cache and match the signed feed. No prior unsigned artifacts, global
cache, machine verification setting, or shipping/default SDK policy changed.

The audit source is retained as `signed-identity/audit.ps1` beside both reports.
The temporary signing-refresh `.bazelrc.local` was removed and the owned
Bazel server was stopped. A subsequent query without an engineering override
again returned SDK `11.0.100-rc.3.26522.148` and runtime
`11.0.0-rc.3.26522.148`. The original unsigned feed hashes were rechecked
unchanged. This refresh remains local Windows x64 evidence for the bounded
MTP/TRX contract, not official signature completion, a CI result, or validation
of additional SDK/CodeCoverage/Windows-controller consumers.

## Parent integration checkpoint (2026-11-03)

At exact source revision `a6c666d157e8ad8af694c1cfa049959b4da6164c`, the
combined public-signed modern and compatibility graph ran successfully:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfb\aliases-df07ee'
.\eng\bazel.ps1 --host_jvm_args=-Xmx2g test //:managed_all //:managed_compatibility //:portable_tests //:reference_alias_tests //:sdk_package_tests //:compatibility_reference_tests //:compatibility_explorer_test //:framework_netstandard2_0_consumer_test //:terminal_contract_tests //:self_real_tests --jobs=2 --nocache_test_results --test_output=errors --noshow_progress
```

Exit 0: all 20 test targets passed. Twelve fresh TRX reports contain **3,501**
executed/passed cases with no skips or failures. The other eight reports are
Bazel analysis/executable-wrapper checks and are recorded separately rather
than presented as additional MSTest cases. Their XML and the build-event target
summaries were also reconciled.

The build includes the four modern TFMs and all **26 netstandard2.0 plus five
net462** variants. Both compatibility inventories were retained, and all 31
physical implementation DLLs were read back, hashed, and verified to carry
`B03F5F7F11D50A3A`. This checks implementation outputs, not unrequested satellite
sidecars; runtime/package consumers must build and verify their own full layouts.

Evidence is retained in
`C:\tfb\parent-integration-evidence\a6c666d157`: source identity, complete build
events/log, all 20 reports, `test-results.json`, both framework inventories, and
`signed-compatibility-assemblies.json`. Only the user guide changed while the
run was active. The 718.286-second elapsed time is a combined integration run
with existing action-cache hits, not a cold-cache or full-cache performance
benchmark.

The subsequent source-path patch and newly integrated aliased suites require
their own final checks. This checkpoint does not include the full MTP suite's
two known host failures, the remaining analyzer suite, Windows-controller
integration, CI execution, or official release/servicing certification. The
separate lane records bind each package/SDK result to its actual source state.
## Signed compatibility validation (2026-11-03)

### Public-signing checkpoint validation

The next validation starts from compatibility commit
`870a6d86b6952e6ed2eb84411c34f92c3c7ee8ad` and selectively applies the shared
identity correction from `aff2cf0f702a07779cee99a8acf29cf515e858bb`.
The lane-specific shared integration is
`42b9e7f8d500cc6f3d4841136ecc67ecc9ae9cf9`.
SDK/alias conformance files absent from this lane stay absent. The independent
package-reference selection patch and compatibility dependency groups are
preserved.

The contract mirrors Arcade's development build: the pinned public-only
`MSFT.snk`, public signing for native libraries and satellites, and full
key-qualified friend identities (including DynamicProxy's distinct public key).
No private key, verification bypass, registry setting, or machine policy change
is allowed. Public-key identity is not official signature completion.

The prior 31 unsigned DLLs were copied to separate evidence storage after
checking every hash against the unsigned inventory; the unsigned CLR consumer
and previous reports are retained too. Signed consumers must verify the actual
`b03f5f7f11d50a3a` identity in addition to framework metadata and loaded paths.
The planned checks rebuild the native compatibility matrix, run the real
netstandard host and CLR consumers, inspect satellites/keyed friends, and report
any genuine CLR signing/load boundary rather than weakening verification.

Executed results for this checkpoint:

| Contract | Evidence |
| --- | --- |
| Public-only key | The downloaded key is exactly 160 bytes, contains `PUBLICKEYBLOB` / `RSA1`, matches SHA-256 `933598639cbaa1de502f80d2fd1db78f13c8d7bb64a5fdc1bc73ac0b5ce4f5ca`, and derives token `b03f5f7f11d50a3a`. |
| Signed compatibility outputs | All 31 primary DLLs have the expected token and original exact TFM metadata. All 107 friend grants have the Microsoft or DynamicProxy key as appropriate. |
| Satellites | After materializing runtime closures, all 325 satellites (260 netstandard, 65 net462) match their parent's version/key and declare a nonempty culture. |
| Source behavior and friend access | `.\eng\bazel.ps1 test //:managed_compatibility //:framework_tests //:compatibility_reference_tests //:compatibility_explorer_test //:framework_netstandard2_0_consumer_test --jobs=2 --noshow_progress --test_output=errors --nocache_test_results` exits 0: seven fresh targets, including all 1,634 existing framework cases and their localization checks. |
| Signed runfiles dependency | `.\eng\bazel.ps1 test //:build_tools_tests --jobs=2 --noshow_progress --test_output=errors --nocache_test_results` exits 0; all thirteen existing build-tool cases execute/pass with the shared signed runfiles assembly. |
| Netstandard host | The real explorer again returns 0/2/9 for pass/failure/empty selections, with parsed TRX executed counts 1/1/0. |
| Windows CLR | The separate signed consumer builds and exits 0 on `Framework64/v4.0.30319/mscorlib.dll`, verifies the actual native framework token, loads its Korean satellite, compares full public keys, and exercises the failing-assertion path. Staged hashes match the selected producer. |

#### Runtime-sidecar provenance correction

The first explicit CLR satellite probe failed with `0x80131040`: its staged
satellite was still the old unsigned file, although the primary DLL was newly
public-signed. A library-only build had not materialized that runtime sidecar.
The standard consumers had materialized their own satellites and passed.
An independent satellite build in a different incoming configuration was not
proof that the wrapper-selected runtime file had been refreshed.

Compatibility wrappers now expose the existing runfiles as an additive
`runtime_files` output group without changing their default files or assembly
providers. The exact command
`.\eng\bazel.ps1 build //:framework_net462 --output_groups=runtime_files --jobs=2 --noshow_progress`
materializes the selected framework and all thirteen satellites. The generated
inventory now names direct satellites too. After replacing the task-owned
read-only stale copy and verifying its token/hash before launch, the same CLR
consumer exits 0. No production source, key, machine policy, verification
setting, or binding bypass was changed to obtain that result.

The full runtime-closure build was also executed for all 31 exact wrapper labels
using `--output_groups=+runtime_files` and exited 0. This distinguishes a
materialized artifact from a path merely reported by analysis.

**Test-value evidence:** an isolated identity counterfactual retains the exact
signed consumer executable and correct satellite but replaces only the primary
framework DLL with its preserved unsigned predecessor. The CLR rejects that
wrong identity with `FileLoadException` / `0x80131040` and exit `-532462766`;
the correct payload exits 0 before and after. The consumer SHA-256 is identical
in all three runs:
`D87E07279F041D94F168918E77F6B61703F30667B25D3195D091713C04C29E10`.
This proves the identity/load boundary, not cryptographic signature completion.

Signed evidence is separate under `files/bazel-signing`: key verification,
primary/satellite/friend inventories, materialization logs, CLR build binlogs
and matched-runtime output, TRX reports, and `identity-counterfactual.json`.
The original unsigned evidence remains unchanged under
`files/bazel-compatibility` and the signed evidence's `unsigned` snapshot.
No genuine public-signing load failure was observed with correctly staged
artifacts on this installed CLR. Exact .NET Framework 4.6.2-runtime execution,
environments with different verification policy, SDK 12, official signing and
shipping-package certification remain separate validation boundaries.
