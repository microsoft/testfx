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

The Bazel experiment deliberately uses its own hermetic SDK 10.0.400 pin, not a
globally selected SDK and not a silent replacement of `global.json`. Initially
validate `net10.0`; only claim other TFMs after executing their build/test paths.
Windows app models, native AOT, shipping NuGet assets, .NET 11, release signing,
and Arcade services are parity gates, not implied by a green `bazel build`.

## Validation and demonstration

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
