# .NET 10 and .NET 11 builds: research and decisions

## Problem and contract

Build the repository's modern .NET products and their unit tests for
`net10.0` and `net11.0`, in addition to the existing targets. Preserve .NET
Framework, .NET Standard, UWP, and WinUI minimum versions.

The producer is the repository-pinned .NET SDK and the MSTest/Microsoft.Testing.Platform
source projects on `main`. Consumers must receive matching framework-specific
assemblies and MSBuild assets from the packed NuGet packages, rather than silently
falling back to the `net9.0` adapter or framework extensions. Windows-specific
PackagedApp assets must remain preferable to plain assets for Windows consumers.

The existing test-host lifetime, discovery, filtering, exit codes, cancellation,
and protocol contracts are unchanged. Successful packed consumers must execute
their selected tests on the requested runtime; a passing build alone is not
runtime or package-selection proof.

## Research and experiments

- 2026-10-14: `Directory.Build.props` defined `SupportedNetFrameworks` as
  `net8.0;net9.0`, while `global.json` already selected .NET SDK
  `12.0.100-alpha.1.26509.101`. No SDK upgrade is needed.
- Several unit-test projects bypassed the shared framework list. The MSTest
  adapter and framework extensions also had explicit package-folder mappings
  and imports for only .NET 8 and .NET 9.
- `TargetFrameworks.Net` independently selected .NET 10 and .NET 8 acceptance
  consumers. The fast acceptance configuration intentionally selects only
  the newest framework.
- CI's Windows solution and Debug test-module commands select all built
  frameworks. Runtime provisioning comes from `global.json`, so the .NET 11
  runtime must be installed there, including the existing x86 and ASP.NET Core
  runtime sets.
- The [official .NET 11 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json)
  identifies `11.0.0-rc.2.26504.105` as the current runtime release.
- The pinned SDK's `Microsoft.NETCoreSdk.BundledVersions.props` maps `net11.0`
  to .NET 12 reference, runtime, and apphost packs. Merely adding the TFM would
  produce binaries requiring .NET 12. Comparing the installed official .NET 11
  RC2 SDK confirms that its corresponding pack versions are all
  `11.0.0-rc.2.26504.105`. A shared, narrowly guarded metadata correction must
  apply to both repository projects and generated acceptance consumers.
- A two-project `-projects` invocation failed during MSBuild argument parsing;
  it is not regression evidence. A single-project framework pack produced the
  baseline package, but the repository's aggregate verifier correctly rejected
  the incomplete package set and omitted UWP payload. A direct adapter pack
  using the already bootstrapped SDK succeeded and both baseline archives were
  retained separately for package-asset comparisons.
- Baseline `Microsoft.Testing.Platform` build at
  `5e6f70ef160e1d73c6c490866053ab5d51d3b382` succeeded for .NET 8, .NET 9,
  and .NET Standard with zero warnings or errors, using the pinned SDK.
- The first expanded build exposed .NET 10/11 WASI annotations on process and
  named-pipe APIs, plus .NET 11's newer text-writer overload selection. Preserve
  the existing desktop paths and make their already unsupported WASI paths
  explicit, including a localized invalid-option diagnostic. Keep the internal
  compiler-lowering polyfill only where the BCL lacks it; forwarding an internal
  compatibility attribute would unnecessarily introduce public exports.
- Metadata-conditioned updates must execute in a target, not in an
  evaluation-time item group. The effective-pack inspection confirmed that the
  correction changes only .NET 11, leaving .NET 8/9/10/12 metadata intact.
- The full Windows build exposed MSBuild.Sdk.Extras' first-digit framework
  inference (`net10.0`/`net11.0` were treated as .NET Framework). Explicit modern
  identifiers avoid changing the pinned SDK dependency. String replacement
  escapes Windows framework separators, while item transforms do not expand
  during property-first evaluation. Both approaches produced invalid restore
  input. Preserve the existing explicit Windows lists and append both new TFMs.
- Preserve WebAssembly acceptance on the bootstrap's .NET 10 workloads rather
  than implicitly retargeting it when the newest desktop acceptance TFM changes.
  New BCL nullability and async text-writer overloads need small compatibility
  adjustments, without weakening analyzer enforcement or diagnostic cleanup.
- Review follow-up: scope the identifier workaround to the four Windows
  projects that explicitly opt into MSBuild.Sdk.Extras before importing its
  props. Only unset, unknown, or mis-inferred identifiers are corrected, and an
  explicit framework version is preserved. Native SDK projects do not opt in.
- Review follow-up: metadata inspection of the compiled .NET 10 and .NET 11
  OpenTelemetry assemblies confirms both null-tag branches call
  `System.Array.Empty<T>()`; the empty collection expression does not allocate
  a new array. Retain the non-null fallback required by the newer BCL annotations.
- Follow-up validation: all four Extras projects and the native-SDK platform
  retain the expected .NET 10/11 identifiers and versions. The affected producer
  graph and repacked framework/adapter packages build with zero warnings/errors;
  both unchanged packed-consumer cases execute and pass again with no skips.

## Alternatives and decision

Keeping only the old binaries lets NuGet choose older assets for newer
consumers, but does not compile or test the requested framework-specific code.
Changing only the shared list would leave malformed or incomplete MSTest
packages and omit test projects with hard-coded lists.

Extend the shared list, align modern unit-test targets, and complete the existing
per-framework package layout. Reuse the .NET 9 framework-extensions API baseline
for newer plain .NET targets, as the core framework already does. Extend Windows
companion targets alongside the shared list so a new plain TFM cannot mask the
matching Windows implementation.

Keep the .NET 12 build SDK needed by existing repository infrastructure. Correct
only .NET 11 pack metadata that incorrectly names .NET 12 packs, sharing the same
correction with generated acceptance assets. A future SDK with correct .NET 11
metadata is left untouched; never patch the SDK installation itself.

Do not retarget source-only/analyzer shipping assemblies, fixed-framework
samples, or benchmark toolchains. Preserve the packaged-app controller's .NET 8
and .NET 9 deployment contract and existing UWP/WinUI minimums; introducing new
controller routing or changing runtime-feature behavior is outside this build
expansion.

## Validation and demonstration

Validation includes framework-specific builds, actual package entries, packed
consumer asset selection and execution, and focused existing tests on both new
runtimes. Binlogs, TRX/XML/CTRF reports, package hashes, consumer manifests and
runtime configurations are retained in task-owned session artifact storage.

The baseline command was:

```powershell
.\eng\build.ps1 -restore -build -projects src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -msbuildEngine dotnet -binaryLog /bl:<artifacts>\baseline-platform.binlog
```

Exit: `0`. This establishes existing build behavior, not regression sensitivity.
The expanded full Windows solution compiles; the first complete pack
identified two remaining file-count expectations. Actual package contents
confirm 87 files for `MSTest.Extensions.Hosting` and 12 for `MSTest`; update the
existing aggregate gate rather than disabling it.

### Executed evidence

All results below use Debug repository packages `MSTest.*` version
`4.6.0-dev` and `Microsoft.Testing.*` version `2.6.0-dev`, built with the pinned
SDK. The source state is the uncommitted change from base
`5e6f70ef160e1d73c6c490866053ab5d51d3b382`; the complete saved patch and its
SHA-256 are recorded with the session evidence rather than a fictitious head SHA.

The commands below use a portable repository-local evidence directory. The
original run retained the same artifact filenames in task-owned session storage.

```powershell
$evidence = Join-Path (Get-Location).Path 'artifacts\log\net10-net11-validation'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$env:NUGET_PACKAGES = Join-Path (Get-Location).Path '.packages'
$env:DOTNET_ROOT = Join-Path (Get-Location).Path '.dotnet'
.\eng\build.ps1 -restore -build -pack -binaryLog "/bl:$evidence\expanded-repository-verified.binlog"
.\eng\verify-nupkgs.ps1 -configuration Debug
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Debug --no-build --filter 'FullyQualifiedName~FrameworkTargetingTests' -p:UsingDotNetTest=true -p:EnableCodeCoverage=false "-p:ArtifactsTestResultsDir=$evidence\framework-consumer-results" "-bl:$evidence\framework-consumer-execution.binlog" --show-test-results all
```

Each command exited `0`. Discovery selected the two explicit framework data rows
before execution. The existing execution verifier reconciled their planned,
selected, executed and passed counts with a fresh outer TRX: `2/2/2/2`,
zero failures and zero skips.

`PackedPackages_UseMatchingFrameworkAssetsAndRuntime` verifies the imported
per-framework targets, compares five loaded assemblies byte-for-byte with the
matching entries in the current packed packages, and runs one child test that
asserts the requested runtime major. Each child exits `0` and reports exactly
one passing test. Consumer `project.assets.json` files select `lib/net10.0` and
`lib/net11.0` respectively and use the run-owned acceptance cache, not the
repository restore cache. Their manifests/imports/runtime configs and loaded
assembly hashes are retained separately.

| Focused selection | .NET 10 | .NET 11 |
| --- | --- | --- |
| `NonCooperativeParentProcessListenerTests`, `TestHostBuilderTests`, `IPCTests`, `DotnetTestHttpClientTests` | 55 passed | 55 passed |
| `OpenTelemetryPlatformServiceTests`, `PackagedAppNativeInformationalLaunchTests` | 41 passed | 41 passed |
| Client synchronization-context tests, `LaunchInProcessAsync_DrivesInitializeDiscoverRunAndExit`, `TestNodesUpdated_LargeMultibytePayload` | 34 passed | 34 passed |

Each selection used the same `dotnet test --project ... -c Debug -f <tfm>
--no-build --filter ... -p:UsingDotNetTest=true -p:EnableCodeCoverage=false`
shape, with unique binlog/result directories. Every command exited `0`; separate
discovery and the execution verifier confirmed all planned identities executed,
with no failures or skips. Existing method-level parallelization was preserved;
new consumers own separate generated assets and the client locks remain
instance-local.

`WasmBuild_GeneratesTestingPlatformEntryPoint`, `RawPlatform_BuildsForWasi`, and
`BrowserWasmBuild_GeneratesTestingPlatformEntryPoint` each executed and passed
against the .NET 10 workloads and the expanded packed products. The outer hosts
target .NET 11. These three cases prove builds, not WASI/browser execution.
Localization validation passed all 41 guard tests and all 364 catalogs.
Together, the ten fresh TRX reports contain 265 executed passing cases,
zero failures and zero skips; the test-results canvas shows the complete merged
selection. The successful full build/pack binlog records 81 projects and no
build errors.

### Test-value evidence

The unchanged package-content assertions fail against retained baseline
framework/adapter archives because all 26 .NET 10/11 assets are missing, then pass
against the expanded archives. This is an executed package-layout
counterfactual, separate from the passing packed-consumer tests.

An isolated `Net11RuntimeContract` executable retains the same assertion in both
states. With the shared correction it reports
`ExpectedRuntimeMajor=11; ActualRuntimeMajor=11` and exits `0`. Without that
correction it still builds, reports `ActualRuntimeMajor=12`, and exits `9` at the
runtime-contract assertion. Running the correct variant again exits `0`.
Separate intermediate/output directories and binlogs identify each variant.
An initial experiment accidentally included a sibling generated-object directory
and failed compilation; that attempt is not red-phase evidence.

Build and package transcripts are a more useful demonstration than screenshots
for this framework-support expansion. Live CI and release-branch validation are
not implied by local results. No artifacts have been uploaded or published.

## CI follow-up: reference and application-model contracts

Azure build `1635514` failed at head
`5e92781259824528ddb5fb22165eaf3c95a0c17c`. Three bounded regressions were
identified; no test, analyzer diagnostic, or CI gate will be disabled:

- The C# and Visual Basic analyzer verifiers compile against .NET 8 reference
  assemblies while referencing the source-built MSTest assembly from their
  executing target. The unchanged conditional-access code-fix test executed on
  .NET 11 and failed with unexpected `CS1705` diagnostics. Reference assemblies
  must be at least as new as the referenced source assembly, while preserving
  tests that explicitly require a newer BCL.
- The packaged-app targets recognize only `net9.*` and `net10.*` when selecting
  the existing .NET 9 controller. A real packed .NET 11 SDK consumer restored
  and evaluated successfully but selected the .NET 8 controller; both unchanged
  Windows routing assertions failed. Extend the compatibility condition without
  changing the controller payloads or the .NET 8/.NET 9 deployment contract.
- Modern UWP .NET 10 consumers correctly resolve the newly shipped ordinary
  `lib/net10.0/MSTest.TestFramework.dll`, alongside the existing specialized
  .NET 9 UWP adapter/extensions. The asset assertion incorrectly requires the
  ordinary .NET 9 framework for every consumer. Bind that expectation to the
  independently specified consumer TFM, retaining all specialized-asset checks.

The unchanged in-flight cancellation-observer test also executed on .NET 11 and
failed its exact diagnostic assertion. Its reflection walker does not inspect
private fields inherited by continuation tasks. Investigate that runtime-shape
boundary while preserving the diagnostic assertion, assembly scheduling,
process-wide debug-flag restoration, and bounded worker cleanup.

The local baseline runs used the pinned SDK/runtime and Debug outputs, with
fresh reports under the task-owned evidence directory: analyzer `1 failed`,
cancellation observer `1 failed`, and packed SDK routing `2 failed / 1 passed`,
all with zero skips and process exit `2`. These are executed assertion/harness
failures, not restore or compilation failures of the test hosts. Green evidence
follows below.

### Repair decisions and executed validation

The analyzer fixtures now share host-compatible .NET references; the .NET 11
reference-pack version is supplied by the project's effective build metadata,
not duplicated in source. Emitted legacy assemblies use matching references.
The intentionally mixed-version architecture case separately compiles a .NET 8
MSTest surface and a .NET 9 consumer, with an exact X64 diagnostic/code-fix
positive control to prove the analyzer remains active.

An expanded .NET 11 analyzer run executed 1,994 cases and found three additional
`Task.WaitAll` snippet failures: newer BCL overload resolution selected a
params-collection overload requiring a newer compiler language mode. Explicit
`Task[]` arguments preserve those snippets' legacy blocking-call contract and
their original diagnostic/scope assertions. No preview mode or suppression was
added. Both complete .NET 10 and .NET 11 analyzer modules subsequently passed
all 1,994 cases.

The packaged-app controller selection now uses TFM compatibility with .NET 9:
.NET 8 consumers retain the .NET 8 controller, while .NET 9/10/11 consumers
retain the .NET 9 controller. Five packed WinUI evaluation rows pin the four
Windows selections and the non-Windows no-controller path. The unchanged
Modern UWP SDK assertion that failed before the fix now passes.

UWP report expectations take the declared consumer TFM. Reports also record
that TFM explicitly; the ordinary framework must match it while all three
specialized adapter/extensions remain the .NET 9 UWP payload. Two focused
report tests reject both a mismatching ordinary framework and ordinary adapter
payloads. Real x86/ARM64 builds and .NET 9/.NET 10 activation passed using the
configuration-matched packed packages, including both plain and UI child tests.

The cancellation diagnostic probe walks inherited private fields, preserving
its exact description assertion. It now joins the original cancellation task
after releasing the registration and before restoring process-wide debug
tracking. Its existing `[DoNotParallelize]` exclusion remains unchanged.

| Validation | Executed/passed | Failed/skipped | Exit |
| --- | ---: | ---: | ---: |
| Full analyzer module, .NET 11 | 1,994 | 0/0 | 0 |
| Full analyzer module, .NET 10 | 1,994 | 0/0 | 0 |
| Focused analyzer preservation, .NET 8/.NET 9/.NET Framework | 56 | 0/0 | 0 |
| Full in-process client class, .NET 8/9/10/11 and .NET Framework | 245 | 0/0 | 0 |
| Packed SDK/package/framework selection | 11 | 0/0 | 0 |
| Real Modern UWP architecture and activation cases | 4 | 0/0 | 0 |

The combined validation matrix is 4,304 passing outer cases, excluding duplicate
focused runs and child test counts. The full Windows Debug build/pack and the
final acceptance-project build both exited `0`, with zero warnings/errors.
Acceptance discovery and the independent execution verifier reconciled
`11/11/11/11` and `4/4/4/4` planned/selected/executed/passed counts. All five
client TRX reports contain the repaired exact-diagnostic case once, 49 passing
results, and no skips. The full analyzer reports preserve the same 1,994-case
identity multiset as the expanded pre-fix run.

Representative reproduction commands, using the repository-pinned toolchain
and a task-owned evidence directory, are:

```powershell
.\eng\build.ps1 -restore -build -pack -binaryLog "/bl:$evidence\ci-fixes-full-debug-build-pack.binlog"
.\eng\build.ps1 -build -projects test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -msbuildEngine dotnet -binaryLog "/bl:$evidence\ci-fixes-acceptance-build.binlog" /p:SkipUapTargetFramework=true /p:EnableCodeCoverage=false
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Debug\net11.0\MSTest.Analyzers.UnitTests.dll --report-trx --results-directory $evidence
.\.dotnet\dotnet.exe artifacts\bin\Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests\Debug\net11.0\Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests.dll --filter "FullyQualifiedName~MtpServerClientInProcessTests" --report-trx --results-directory $evidence
$env:TESTFX_RUN_WINDOWS_APP_MODEL_TESTS = '1'
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Debug -f net11.0 --no-build --filter "FullyQualifiedName~ModernUwpTests" -p:UsingDotNetTest=true -p:EnableCodeCoverage=false "-p:ArtifactsTestResultsDir=$evidence" "-bl:$evidence\ci-modern-uwp.binlog" --show-test-results all
```

Actual runs used separate result/log directories and explicit report filenames.
Retained evidence includes the unchanged baseline failures, analyzer worker
commands and full-suite reports, five client reports, shipping package hashes,
consumer `project.assets.json`, resolved UWP assets, manifests, child TRX reports,
and desktop-MSBuild/outer binlogs. All four exact run-owned UWP registrations
were verified absent after cleanup. No machine policy or borrowed package
registration was changed. This is Windows Debug local evidence, not CI or
Release/Linux/macOS validation.

**Test-value evidence:** the unchanged Modern UWP routing assertion failed on a
successful packed evaluation with controller `net8.0` and passed with `net9.0`
after the target fix. Unchanged compiler-fixture expectations rejected
`CS1705`/`BC32207` before reference alignment; the three blocking-call expectations
rejected `CS8652` before the argument repair and passed afterward. These are
executed fixture corrections, not shipping-product mutation claims. The
unchanged cancellation diagnostic assertion failed before the reflection repair
and passed afterward on all five hosts. Strict UWP positive/negative report
cases and real activation protect the actual asset-selection contract.

## Open questions

This change is scoped to `main`; no downstream rollout or servicing/backport is
authorized. The .NET 11 runtime remains prerelease and must continue to flow
with future dependency updates.

Linux/macOS CI, Release validation and WASI runtime execution were not run
locally. `wasmtime` is unavailable; the selected WASI regressions do not skip
because they are build assertions. Windows Modern UWP activation was subsequently
validated during the CI follow-up above. The existing .NET 8/.NET 9 controller
deployment contract is preserved; no new scheduling opt-in, runtime
thread-detection behavior or public product API was introduced.
