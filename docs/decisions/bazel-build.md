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
- Bazel outputs are development artifacts, not signed or published replacement
  NuGets. Package/SDK/release parity requires separate shipping-layout validation.

## Research and experiments

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
`//:analyzers_package` will expose one experimental `MSTest.Analyzers` NuGet,
using `VersionPrefix` from `eng/Versions.props` with `-bazel`, plus
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

The shared `-preview.bazel` prerequisite and final-identity validation are still
pending at this checkpoint. Combined shipping layouts, release signing, Linux,
IDE UI integration and downstream release/servicing remain coordinator-owned
gates; this evidence does not claim them.

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
