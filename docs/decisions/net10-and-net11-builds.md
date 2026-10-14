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

```powershell
$evidence = 'C:\Users\amauryleve\.copilot\session-state\f35576e5-2153-4b60-ab0b-5cb2684a46ab\files'
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

## Open questions

This change is scoped to `main`; no downstream rollout or servicing/backport is
authorized. The .NET 11 runtime remains prerelease and must continue to flow
with future dependency updates.

Linux/macOS CI, Windows application activation and WASI runtime execution were
not run locally. `wasmtime` is unavailable; the selected WASI regressions do not
skip because they are build assertions. No new scheduling opt-in, controller
routing, runtime thread-detection behavior or public product API was introduced.
