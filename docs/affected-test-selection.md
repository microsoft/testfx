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
- The Windows and gated non-Windows call sites select the mode from the source branch. Their shared
  `test-affected-tests.yml` template supplies Azure DevOps build identity and the scoped system access token, and sets
  `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1` only for affected-test commands.
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
Azure DevOps' [matrix contract](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema/jobs-job-strategy)
creates a separate copy of the job for each entry.

The root `EnableAffectedTests` parameter controls both the collection matrix entry and PR selection. The collection
entry is also compile-time gated by `SkipTests=false` and the main branch. Disabling affected tests retains the
ordinary Release and Debug entries without creating a redundant collection build. PR selection, conservative
fallbacks, SDK/package versions, and the collection command's exit semantics are unchanged.
The selected parameter value is also passed to the rollout validator, so its compatibility gates use the actual
run setting rather than the parameter's source-file default.

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
0 and all 13 isolation self-tests pass. The comparison retains only the validator patch, SHA-256
`4C8C117CAD3D9211DF7760CDD6FF3BA7D296F5E6DC2CE27D4AEAE2BFA03A2E54`. Reproduce the configuration checks with:

```powershell
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest
```

These are executable configuration assertions, not a simulated claim of running the Windows test suite. They
cover collection/validation exclusion, main/`SkipTests`/rollback gates, blocking collection failure, and separate
result/binlog/dump artifact names. Live main-branch collection and full-test execution remain CI validation.
No SDK, package, launcher, or test-assembly implementation changes are part of this repair.

Runtime rollback was also checked with a deliberately known-bad storage-version configuration in the isolated
comparison: `-EnableAffectedTests False` passes, while `True` retains the existing `Content-Range` rejection.
A targeted counterfactual that keeps the parameter but ignores its runtime value makes the disabled invocation
fail at that compatibility guard (exit 1); honoring it passes (exit 0). This is configuration-gate evidence, not
a claim that the deliberately incompatible package was restored or executed.

Azure DevOps' non-executing `previewRun=true` API successfully compiled the initial implementation commit
`1c6c1eec3fd177f26dd3aa953a6dd3e72b2ecf48` in four configurations:

| Configuration | Windows matrix entries | Per-entry budget | Expanded test behavior |
| --- | --- | --- | --- |
| Main, affected tests enabled | Release, Debug, AffectedTests | 240 minutes | Collection and full testing have opposite `_CollectAffectedTests` conditions. |
| PR, affected tests enabled | Release, Debug | 90 minutes | Selection, full fallback, and Debug unit tests; no collection. |
| Main, affected tests disabled | Release, Debug | 240 minutes | Ordinary full testing and Debug unit tests; no collection or selection. |
| Main, `SkipTests=true` | Release, Debug | 240 minutes | No Windows test steps or collection entry. |

The previews validate Azure template expansion and job ownership without queuing builds. They are not proof of
test execution or of the upstream collector's dependency completeness.

## Cross-platform rollout readiness

The requested next step is main-branch map collection on Windows, Linux, and macOS, with Linux PRs consuming their
own baseline. macOS remains excluded from PR validation; collecting there would exercise the collector rather
than reduce today's PR execution time.

Package inspection on 2026-10-14 found an upstream prerequisite, not merely missing pipeline wiring. Both the
repository-pinned `Microsoft.Testing.Extensions.AffectedTests` package `18.13.0-preview.26513.2` and the newest
available preview `18.13.0-preview.26514.4` explicitly reject non-Windows collection before test discovery, returning
exit 1. Their packaged README states that collection uses the Windows x64 CTS runtime; neither supplies a Unix CTS
runtime. These are inspections of the distributed packages, not Linux/macOS execution evidence.

The map's storage key includes the runtime description and runtime identifier. Windows shards therefore cannot
stand in for Linux or macOS baselines. The Azure DevOps provider uploads each shard at its distinct object path in
the build's shared container, tolerates an artifact-publication HTTP 409, then reads the artifact back and verifies
its container identity. Its file lock coordinates processes on one agent, not separate OS agents; real multi-agent
publication must still be validated when a compatible collector becomes available.

Collection is also not authoritative test validation: the inspected orchestrator accepts failed-test batch exit 2,
reports those failures, and can still save a map and return 0. Ordinary full testing must therefore retain its own
job and failure outcome, on every OS that collects maps.

The safe implementation is rollout-ready wiring behind a separate, off-by-default
`EnableNonWindowsAffectedTests` parameter, subordinate to the root `EnableAffectedTests` rollback. The validator
currently rejects every Unix activation; its support guard must be deliberately updated after a compatible package
has been verified, rather than assuming an untested newer version supports Unix. The existing full Linux/macOS runs remain
unchanged while that gate is off. Once support is established, main gets independent Release and collection
entries on those OSes, Linux PRs get selection with the existing conservative full fallback, and nightly/manual
validation remains authoritative. Collection entries own distinct diagnostic artifacts and do not enable live
test-result publishing.

Enabling the current collector, treating its non-Windows exit as success, reusing Windows maps, or substituting
collection for full validation were rejected because they respectively break CI, conceal missing baselines, violate
map compatibility, or lose test-failure enforcement. No dependency update or upstream implementation is included.
Unix activation remains blocked until collector support, actual OS-specific mappings and selection, concurrent
publication, cancellation, and failure behavior have been validated with the aligned shipping packages and SDK.
This is unofficial-main pipeline preparation only; it does not require a product servicing backport.

### Readiness validation and test-value evidence

The implementation was tested against base `1317c31eefe77c6a824e162a31119c4a60dca0aa`. Its six-file implementation
patch (YAML and validator, excluding this decision record) has SHA-256
`9030F4915D847C01CB2F3A84C84C79B66CE7DD6166C4659453BAAE40B0B66601`. Validation used PowerShell 7.6.7 and
`powershell-yaml` 0.4.12; no shipping SDK/package versions changed.

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\validate-affected-tests.ps1 -SelfTest
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests True -EnableNonWindowsAffectedTests False
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests False -EnableNonWindowsAffectedTests True
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests True -EnableNonWindowsAffectedTests True
git --no-pager diff --check
git --no-pager diff --cached --check
```

The first three validator configurations passed with exit 0: 13 existing Windows isolation cases and 30 Unix
readiness cases, no skips. The fourth was correctly rejected with exit 1 at `NonWindowsCollectorSupportRequired`,
before running self-tests. Both whitespace checks passed with exit 0. The original Windows-only baseline executed
and passed all 13 existing isolation cases before implementation.

| Contract / case | Retained-assertion counterfactual | Without behavior | Correct implementation |
| --- | --- | --- | --- |
| Independent Linux collection / `NonWindowsCollectionUsesIndependentJob` | Restore only the original Linux job from the base above in an owned configuration fixture; retain the final validator and templates. | Exit 1 at the intended Linux topology assertion after the 13 Windows cases pass. Unix cases beyond that assertion are not reached. | The same command passes all 43 cases, exit 0, both before and after the comparison. |
| Explicit support gate / `WindowsOnlyCollectorActivationIsRejected` | Change only `Assert-NonWindowsCollectorSupport`'s enabled condition to `if ($false)` in the fixture; retain its self-test body unchanged. | Exit 1 at the named self-test after 41 passing cases; the final unverified-version case is not reached. | Restore the guard: all 43 cases pass, exit 0. |
| Main/skip/rollback/solution/fallback/artifact contracts | Named mutation cases inside `Invoke-NonWindowsAffectedTestsSelfTest`, including `LinuxWrongModeIsRejected`, `MacOSCollectionJobRemovedIsRejected`, `UnixFullFallbackRemovedIsRejected`, and `UnixCollectionLivePublishingIsRejected`. | Each altered configuration is rejected at its required guard. | All 30 Unix readiness cases pass; this is configuration proof, not Unix test execution. |

The comparison command was the second validator command above in each fixture state. The final validator's
assertions were preserved between the green/red/green Linux comparison. For the support-guard mutation, the
`Invoke-NonWindowsAffectedTestsSelfTest` body was checked for exact equality before execution. Fixture source hashes,
exit codes and logs are retained in session artifacts; neither counterfactual changed the delivery worktree.

The session-owned script `verify-affected-pipeline.ps1` passed 25 script probes, exit 0. Twenty execute the actual
shared PowerShell step bodies for both solution paths with a native `dotnet.cmd` fixture returning exits 0, 1, 2, 3
and 8. They verify the native-bound arguments, collection exit propagation, selected-test failure preservation, and
explicit fallback messages without falsely setting `AffectedTestsSucceeded`. Five execute the live-publishing
configuration with present/absent synthetic tokens and ordinary/collection/unspecified ownership. These probes do
not execute the SDK, collector, or product tests.

The session-owned `preview-affected-pipeline.ps1` also passed six non-executing Azure DevOps previews, exit 0.
It inlines the four local step templates into a root YAML override, preserving ordered Azure discriminator keys,
and leaves unchanged repository templates resolved at the base revision. Main and non-main contexts use actual
repository refs; no system variable is overridden. The API requests set `previewRun=true`, so no builds are queued.

| Preview | Root affected tests | Unix gate | Skip tests | Windows matrix | Linux/macOS matrix | Result |
| --- | --- | --- | --- | --- | --- | --- |
| Main default | true | false | false | Release, Debug, AffectedTests | Release | Passed; Unix ordinary testing remains unchanged. |
| Non-main default | true | false | false | Release, Debug | Release | Passed; Windows selection/fallback and Unix ordinary testing. |
| Main Unix wiring only | true | true | false | Release, Debug, AffectedTests | Release, AffectedTests | Passed; separate collection/full-test conditions. Actual activation is rejected by the validator. |
| Non-main Unix wiring only | true | true | false | Release, Debug | Release | Passed; selection/full fallback present. macOS still excludes PRs at runtime. Actual activation is rejected by the validator. |
| Main root rollback | false | true | false | Release, Debug | Release | Passed; no affected-test steps or extra collection entries. |
| Main skip tests | true | false | true | Release, Debug | Release | Passed; no test steps or extra collection entries. |

Initial local harness attempts exposed PowerShell-function argument tokenization, unordered Azure mapping
serialization, and a prohibited system-variable override. The harness was corrected to use a native command
fixture, ordered mappings and actual source refs, then rerun successfully. Those harness failures are not counted
as regression red-phase proof.

No test-assembly parallelization or lifecycle code changed. Matrix entries retain separate agent workspaces,
caches, temporary directories and diagnostic artifact names. Real Linux/macOS collection, OS-specific associations
and selection, multi-agent artifact publication, and real worker completion remain **unverified and blocked by
upstream support**, not established by the configuration checks or previews. No .NET build/pack was run because no
shipping source or package layout changed. The Unix gate must remain off until composed-product proof is available.

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
