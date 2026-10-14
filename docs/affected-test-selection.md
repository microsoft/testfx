# Affected-test selection rollout

This repository is wired for the experimental affected-test workflow from
[dotnet/sdk#55574](https://github.com/dotnet/sdk/pull/55574). The workflow is Microsoft.Testing.Platform-only and
builds on the composable filter-provider support from
[testfx#10235](https://github.com/microsoft/testfx/pull/10235).

The repository consumes `Microsoft.Testing.Extensions.AffectedTests` from the `test-tools` feed together with the
provider-specific `Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps` package. Their shared version is
pinned separately from `Microsoft.Testing.Extensions.CodeCoverage`, allowing affected-test package updates without
requiring the shipping CodeCoverage version to change. Test applications that
register the affected-test provider override their direct CodeCoverage reference to that matching preview version;
the packages shipped by this repository retain the central CodeCoverage version. The provider publishes versioned
per-module mapping shards to the `TestFx_AffectedTestsMaps` artifact produced by pipeline definition 209. Its builder
hook is registered by the repository's hand-authored MTP entry points.

Package `18.13.0-preview.26508.3` is the minimum known-good build for this rollout: it includes the fixed JSON
discovery path and Azure DevOps upload request, and ships both `netstandard2.0` and `net8.0` assets for the base
extension, collector, CodeCoverage extension, and Azure DevOps provider. The currently consumed package versions
are recorded in [`eng/Versions.props`](../eng/Versions.props).

Affected-test execution is enabled with public SDK `12.0.100-alpha.1.26509.101`, built from VMR commit
`907535df2f7eb8e1845571867684244ad04b031b`. It includes both
[dotnet/sdk#56450](https://github.com/dotnet/sdk/pull/56450) and
[dotnet/sdk#56538](https://github.com/dotnet/sdk/pull/56538): successful collection can complete without an ordinary
reporting handshake, or after the matching `RefreshMappingsOrchestrator` handshake. An empty affected-test selection
can similarly complete after its `RunAffectedTestsOrchestrator` handshake without launching a test host.

The SDK accepts those exit-`0` completions only in the corresponding collection or selection mode. Nonzero exits,
controller-only or mixed partial handshakes, and protocol rejection remain failures. Protocol negotiation and the
orchestrator's server-control cancellation channel are preserved; the pipeline does not suppress handshakes or
normalize failure output. The rollout validator requires this SDK or newer, preventing rollback to intermediate
builds that contain only the no-handshake fix.

## CI layout

- `global.json` defines the repository-specific `test.affectedTests` change policy and selects Azure DevOps artifact
  storage for the public `microsoft.testfx` pipeline.
- The trusted main-branch Windows Release entry runs full tests and publishes coverage. A separate `AffectedTests`
  Windows matrix entry runs `--collect-test-map` without the normal report, retry, or coverage arguments, because
  collection owns its instrumentation and launches discovery children with `--list-tests`. A collection failure
  still fails its job; it is not normalized to success or allowed to consume the Release entry's time budget.
  Main-branch Windows entries each have a four-hour budget. PR jobs retain their 90-minute budget.
- The Windows Release PR test runs `--affected-tests`.
- Affected-test collection and selection cover all repository test TFMs, including .NET Framework through the
  package's `netstandard2.0` assets. The build restores the collector's required x64 native files beneath
  `runtimes/win-x64/native`, because NuGet otherwise flattens them for .NET Framework and `win-x64` RID-specific
  outputs, including the WinUI test-framework unit host.
  The native symbol reader (`msdia140.dll`) must accompany the profiler; otherwise collection can succeed with
  test identities but no source associations. The WinUI validation went from 1,598 tests with zero associations
  to 1,598 tests with 498 associations when the complete native layout was restored.
- The shared Windows test call site selects the mode from the source branch, supplies Azure DevOps build identity and
  the scoped system access token, and sets `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1` only for affected-test commands.
- The test infrastructure removes the ambient affected-test mode and per-module exit-code normalization from nested
  test applications launched by acceptance tests, while still allowing an individual test to opt in explicitly.
- `eng/validate-affected-tests.ps1` verifies the SDK gate, package reference, storage configuration, pipeline wiring,
  nested-process isolation, command names, and fallbacks. Its `-SelfTest` mode, run by `DetectChanges`, also rejects
  shared collection/validation jobs, bypassed rollout gates, ignored collection failures, and artifact collisions.

`DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` is an SDK-to-extension authorization marker. Repository scripts and pipeline
definitions must not set it.

## Collection budget isolation

[Run 20261013.16](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1633662&view=logs), from commit
`41b5f5343b23ab07df82f3339dc73a790ab5a066`, completed map collection in 3 hours 33 minutes. The normal full test run
then had less than 10 minutes before the shared four-hour Windows job timed out. Collection produced 45 shards,
but the required full test and coverage validation did not complete.

Collection therefore needs an independent job budget, not merely a separate step. A main-only `AffectedTests`
entry in the existing Windows matrix reuses the same Release build, packing, Windows prerequisites, and pinned
toolchain on its own agent. The ordinary Release entry runs full tests and publishes coverage without waiting for
collection. Each entry owns its workspace and diagnostic artifacts; only the collection entry publishes test maps.
Collection failures remain blocking and visible, but cannot cancel another matrix entry by consuming its timeout.

The root `EnableAffectedTests` parameter controls both the collection matrix entry and PR selection. The collection
entry is also compile-time gated by `SkipTests=false` and the main branch. Disabling affected tests retains the
ordinary Release and Debug entries without creating a redundant collection build. PR selection, conservative
fallbacks, SDK/package versions, and the collection command's exit semantics are unchanged.

Increasing the shared timeout again would not isolate required validation from slow or stuck collection. Running
ordinary tests first would still share cancellation and artifact-publication deadlines. Downloading another job's
outputs was not selected: an independently built matrix entry preserves the existing packing, native layout, and
acceptance-fixture setup without introducing an artifact-transfer contract. The cost is one additional Release
build on main. This repair is limited to the unofficial pipeline; it does not change shipping products or require
a release-branch product backport.

The collector's broken-pipe diagnostic is a separate upstream issue. A successful shard and mapped test identities
do not prove that every dependency event was received. This pipeline repair does not suppress collector errors or
claim that a package update fixes the disconnect.

The retained `CollectionUsesIndependentJob` guard detects the original shared-job layout on base
`c6a0b06f75b8bd02512976a2c2af4b0312eeecf2`: running the updated validator with the unchanged pipeline and template
in an isolated comparison exits 1 at that assertion. With the isolated collection layout, the same command exits
0 and all 12 isolation self-tests pass. The comparison retains only the validator patch, SHA-256
`E8D7BA937663A48656A9F3E258E623438F0E4195F7721A709FC5A85BE3D86700`. Reproduce the configuration checks with:

```powershell
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest
```

These are executable configuration assertions, not a simulated claim of running the Windows test suite. They
cover collection/validation exclusion, main/`SkipTests`/rollback gates, blocking collection failure, and separate
result/binlog/dump artifact names. Live main-branch collection and full-test execution remain CI validation.
No SDK, package, launcher, or test-assembly implementation changes are part of this repair.

## Headless collection compatibility

Collection launches discovery and execution batches in fresh, headless processes. On Windows, a headless process
can inherit a legacy console encoding rather than UTF-8. JSON discovery therefore escapes non-ASCII characters,
including surrogate pairs, so test UIDs and metadata remain lossless after decoding. Escaping only JSON control
characters is insufficient: a legacy encoding's best-fit fallback can convert Unicode into a raw control character
after serialization. The schema and decoded values are unchanged, and malformed discovery still fails collection.
Runner startup diagnostics and successful-run process statistics use stderr so they do not surround the JSON
document on stdout. Failed acceptance runs do not print process statistics: the collector prefers nonempty stderr
over stdout when reporting child failures, and a statistics-only stderr would otherwise hide the actual failure.

[Run 20261012.43](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1632174) exposed the flattened
WinUI profiler layout. [Run 20261012.44](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1632380)
confirmed the discovery and test-isolation fixes but hit the old 90-minute job limit during acceptance collection,
before the normal full test and coverage run. Its child failure messages contained only `Process started` statistics.
Restoring the failure output exposed concurrent collection batches contending for acceptance build binlogs.
Each process started the same counter in the shared testsuite directory, so unrelated builds could open the same
file and fail with `MSB4104`. Binlog names now include a fresh GUID for every invocation and retry.

`DotnetCli_BinlogsHaveGloballyUniqueNames` guards the GUID namespace, concurrent builds, and actual log creation.
With the same test retained and the original helper in isolated source state
`1b85d12ede74827f64215a505ddcde5562eeb9e9`, it executed and failed the GUID assertion (exit 2, one failed test).
With the corrected helper it passed, together with both MSBuild-server configuration cases (exit 0, three passed
tests, no skips). After a Release build/pack, reproduce the focused run with:

```powershell
.\.dotnet\dotnet.exe test --project (Resolve-Path test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj) -c Release --no-build -p:UsingDotNetTest=true -p:TestRunnerAdditionalArguments= '-bl:{}' --filter 'FullyQualifiedName~DotnetCli_BinlogsHaveGloballyUniqueNames|FullyQualifiedName~DotnetCli_MSBuildServer_DefaultsToDisabledAndPreservesExplicitSettings' --show-test-results all
```

Tests must also work independently of prior test execution and the launch directory. Desktop XML data-source tests
resolve their copied fixture from the application base directory. The AppContainer pipe test loads the framework
access-control and Windows-principal assemblies before impersonation, retaining the real restricted-token connection
and denial checks.

Regression coverage includes `Serialize_NonAsciiCharacters_ProducesLosslessAsciiJson` and the packed, headless
consumer test `ListTestsJson_HeadlessProcess_PreservesUnicode`.

## Storage design

The map uses the extension's Azure DevOps artifact provider:

- trusted main builds publish each module shard beneath the `TestFx_AffectedTestsMaps` artifact;
- PR builds query successful builds from definition 209 and select the newest artifact whose baseline commit is an
  ancestor of the PR checkout;
- fork PRs receive no access token, so unavailable storage safely selects all tests rather than exposing credentials;
- mapping identity includes the repository, module, target/runtime, test metadata, policy, and storage compatibility
  fields, so incompatible shards are rejected conservatively.

A missing, stale, incompatible, or inaccessible artifact is an expected state, not a partial selection: the extension
runs all tests. The pipeline's explicit full-test fallback remains for extension/process failures, while scheduled and
manual builds always keep full validation.

Selected-test runs and map collection do not publish partial coverage as the repository coverage report. The
independent normal Release entry and full fallback runs still publish complete coverage.

The one-switch rollback remains available: setting the root `EnableAffectedTests` pipeline parameter to `false`
keeps the package and storage configuration in place, omits the collection entry, and restores the ordinary
full-test command.
