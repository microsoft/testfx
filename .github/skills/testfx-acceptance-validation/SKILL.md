---
name: testfx-acceptance-validation
description: Validate TestFx shipping paths and capable CI execution using packed consumers, package/cache provenance, exact exits and artifacts, and selected-versus-executed test evidence. Use for acceptance regressions, package/MSBuild/launcher/protocol changes, or skipped-only CI results; not for unrelated unit-only or documentation changes.
---

# TestFx acceptance validation

Produce evidence that the changed product ships correctly **and that the intended
regressions execute**. A successful build, discovered tests, published TRX, or green
job alone is not proof. Keep the work bounded to the changed contract.

## Runtime choice

The Python execution gate is a measured exception to the repository's conditional
PowerShell preference. A native PowerShell candidate did not meet the
no-performance-regression condition for the documented standalone CLI path.
On Windows with Python 3.12.10 and PowerShell 7.6.6, three warmups followed by
15 interleaved fresh-process samples per workload produced these medians,
including process startup:

| Workload | Python median, ms | PowerShell candidate median, ms |
| --- | ---: | ---: |
| Empty runtime startup | 76 | 528 |
| One passed case | 208 | 898 |
| 29 skipped cases | 200 | 819 |
| 1,000 passed cases | 222 | 1,330 |
| 10,000 passed cases | 222 | 3,882 |
| 10,000 failed cases | 305 | 3,983 |

Retain the existing gate and all 16 regression scenarios rather than accepting
that slowdown or weakening validation. This feasibility assessment does not
establish complete feature/security parity, compare a warmed persistent host,
or prove that every PowerShell implementation or other OS is slower.
A future port must preserve the same input-validation and failure contracts,
demonstrate feature/security parity, and avoid a performance regression on the
supported invocation paths, including startup where applicable.

## 1. Choose the smallest shipping contract

Before implementation, record the following in the maintained feature decision
record required by [feature-delivery](../feature-delivery/SKILL.md). For non-feature
fixes, use the session or existing issue/PR evidence rather than creating an
additional planning file:

| Decision | Required detail |
| --- | --- |
| Producer and consumer | Owning projects, exact NuGet IDs/versions, shipping binaries/sidecars/extensions, TFM/RID/architecture, SDK, and release branch that must work together. |
| Lifecycle | Who launches, connects, cancels, disposes, and cleans up; required handshake and intentionally absent handshake; unsupported/undeclared versus explicitly disabled capability. |
| Observable result | Exact parent and child exits, selected test identities/counts, expected artifacts/content, and fallback rules. An outer acceptance test can pass while asserting an intentionally failing child. |
| Scope | One representative real consumer per changed boundary; additional scenarios only where the change can affect them. Explain material exclusions. |
| Delivery owner | One owner through dependency flow, downstream rollout, release-branch proof/backports, and removal of temporary flags/denylists. |

An assertion-only change can need just its focused unit test. A package layout,
MSBuild launcher, process/protocol, SDK, or independently shipped component change
needs a packed consumer, not just mocks or in-repo project references. For an SDK
integration, a fake SDK is useful protocol coverage but cannot replace the real
shipping SDK invocation.

Before enabling a downstream feature, validate the branch/packages that will ship
and decide whether supported release branches need servicing/backports. A rollout
flag does not replace compatibility or composed-product proof.

Use this risk table to choose cases, not to impose a full matrix on every change:

| Changed boundary | Representative proof |
| --- | --- |
| Package/targets | Inspect actual `.nupkg` entries and restore a generated consumer; assert resolved props/targets/runtime assets and aligned dependencies. |
| CLI/launcher/routing | Real execution plus discovery (`--list-tests`); help/info if changed; a named launch profile with distinctive arguments/environment if profile routing is affected. Assert the marker/filter reaches the actual packaged host/sidecar, not merely the outer command. |
| Handshake/capability | Normal handshake, intentionally suppressed/no-handshake path, and one old/incompatible consumer when relevant. A successful no-work child must not become a false parent failure; a missing required handshake must not become false success. |
| Failure/fallback | Nonzero child exit, zero-result selection, partial/missing input, and missing/stale/incompatible data where fallback is supported. Assert which work actually runs, not only a message saying fallback ran. |
| Orchestration | Cancellation with bounded teardown and one mixed-success multi-module/TFM run. Verify every module's artifacts and the aggregate parent exit; do not let one passed module conceal a missing or failed module. |

## 2. Pack and establish provenance

Run from the worktree root. Use `.\build.cmd -pack -c Release -bl` on Windows
(`./build.sh -pack -c Release -bl` on Linux/macOS). Capture the exit immediately and
stop on failure. Repack after every source change. Do not pack/build concurrently
against the same outputs. Do not run the full integration matrix solely to validate
this skill or another documentation-only change.

Inspect these current sources before adapting commands:

| Source | What it establishes |
| --- | --- |
| `global.json`, `eng\Versions.props`, `Directory.Packages.props` | Pinned repository SDK/runtime and dependency versions. Do not substitute a globally installed SDK. |
| `test\Utilities\Microsoft.Testing.TestInfrastructure\Constants.cs` | Configuration-matched `artifacts\packages\<Configuration>\Shipping`, `NonShipping`, and `artifacts\tmp\<Configuration>\packages` feeds. |
| `test\Utilities\Microsoft.Testing.TestInfrastructure\TestAsset.cs` | Generated `NuGet.config`, local feeds, and public-feed/source-mapping behavior. A local feed in the config alone does not prove the resolved package came from it. |
| `test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Helpers\AcceptanceFixture.cs` | A fresh random `.packages` cache for generated consumers each assembly run. The fixture also links into MSTest acceptance. |
| `test\Utilities\Microsoft.Testing.TestInfrastructure\TestAssetFixtureBase.cs`, `AcceptanceSourceGen.cs`, `TestHost.cs` | Generated builds and separate reflection/source-generation outputs. Building a variant is not evidence that a test executed that variant. |
| `test\Directory.Build.targets` | MTP diagnostics/reporting and `UsingDotNetTest=true` wiring for TRX/results directories. |

Record commit/dirty source state, pack configuration/exit, exact package filenames
and SHA-256 hashes, consumer restore/build command, generated `obj\project.assets.json`
and `*.nuget.g.props`/`*.nuget.g.targets`, and loaded/executed binary paths. Check
package IDs/versions, restore `packageFolders`, chosen TFM/RID assets, and physical
package/binary content. Compare relevant cached package payloads with the current
pack; same version does not imply same bits. Keep MSTest and MTP version families
distinct while checking alignment within each contract.

The outer acceptance runner may reference source projects; the inner generated
consumer must consume the shipping package for the boundary under test.
`TestAsset.GenerateAssetAsync`, `DotnetCli.RunAsync`, `TestHost.LocateFrom`, and
`TestHost.ExecuteAsync` are existing building/launching helpers. Prefer them over
new ad-hoc runners, but assert the real shipping layout when apphost versus DLL,
published output, controller, or package identity matters.

Useful existing examples are `WindowsApplicationModelPackageTests` (physical
package entries), `PackagedAppIntegrationTests` (real packed consumer targets,
custom/suppressed launchers, incompatible payloads, and multi-target exits), and
`PackagedWinUITests` (real activation, package identity, native shipping SDK, and
TRX). The first two are under MSTest acceptance; the last is under MTP acceptance.
For CLI descriptions, update the corresponding `HelpInfoTests` and
`HelpInfoAllExtensionsTests` expectations rather than testing only option parsing.

For an independently constructed consumer, use a unique run-owned `NUGET_PACKAGES`
cache and explicit feeds; do not clear shared/global caches. The existing acceptance
fixture already isolates generated-consumer restores. CI's outer runner uses
`eng\pipelines\variables\test-env-vars.yml` (`DOTNET_ROOT=.dotnet`,
`NUGET_PACKAGES=.packages`); distinguish that cache from the inner fixture's cache.

When a consumer uses a different SDK, invoke the outer acceptance project through
`dotnet test`, not only its apphost. The CLI exports `MSBuildSDKsPath`,
`MSBuildExtensionsPath`, and `DOTNET_ROOT_X64` to the test host. A child `global.json`
and a successful `dotnet --version` do not prevent mixed-SDK targets/task loading.
Use `AcceptanceTestBase.ConfigureDotnetSdkEnvironment` for the isolated native
SDK, preserving shared cache/tooling settings. Verify actual build provenance
(`NETCoreSdkVersion`, `MSBuildToolsPath`, extensions and SDK paths) alongside the
real packaged-host results.

Keep CI's `DOTNET_CLI_CONTEXT_VERBOSE=1` enabled during native CLI validation.
SDK tracing can name the sidecar executable even when help/discovery correctly
comes from the activated host, and ANSI color/reset-only lines can interrupt an
otherwise deterministic block. Normalize presentation only, preserve the
diagnostics, and assert host usage or the delimited JSON payload rather than
requiring the entire mixed CLI output to be a payload.

## 3. Run a focused regression and verify execution

This Windows example runs one existing packed-package regression. It is a package
layout check, **not** evidence of real UWP/WinUI activation. Adapt the project/filter
and expected names for the changed contract. Use a unique results directory, retain
logs/binlogs, and do not infer expectations from the report being verified.

```powershell
# After the successful configuration-matched pack above.
$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$case = 'PackedMSTestTestAdapter_ContainsRequiredWindowsApplicationModelAssets'
$results = Join-Path (Get-Location).Path ('artifacts\TestResults\Release\acceptance-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null

$packages = @(Get-ChildItem -LiteralPath artifacts\packages\Release\Shipping `
    -Filter 'MSTest.TestAdapter.*.nupkg' -File)
if ($packages.Count -ne 1) { throw "Expected one current adapter package, found $($packages.Count)." }
$packages | Get-FileHash -Algorithm SHA256 | ConvertTo-Json |
    Set-Content -LiteralPath "$results\package-provenance.json" -Encoding UTF8

& .\.dotnet\dotnet.exe build $project -c Release "-bl:$results\build.binlog"
if ($LASTEXITCODE -ne 0) { throw "Acceptance build failed: $LASTEXITCODE" }

$filter = "FullyQualifiedName~WindowsApplicationModelPackageTests.$case"
# Blank these global properties so discovery does not inherit execution-only reporters.
& .\.dotnet\dotnet.exe test --project $project -c Release --no-build `
    --list-tests --filter $filter -p:UsingDotNetTest=true `
    -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= `
    "-bl:$results\discovery.binlog" 2>&1 | Tee-Object -FilePath "$results\discovery.log"
$discoveryExit = $LASTEXITCODE
if ($discoveryExit -ne 0) { throw "Acceptance discovery failed: $discoveryExit" }
# Reconcile discovery.log with the one source-declared case before executing.

# The plan comes from source/data rows and independent discovery, not this run's TRX.
$expectedPath = Join-Path $results 'expected-tests.json'
ConvertTo-Json -InputObject @($case) | Set-Content -LiteralPath $expectedPath -Encoding UTF8
$started = [DateTimeOffset]::UtcNow.ToString('o')
& .\.dotnet\dotnet.exe test --project $project -c Release --no-build `
    --filter $filter `
    -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" `
    "-bl:$results\test.binlog" --show-test-results all `
    2>&1 | Tee-Object -FilePath "$results\execution.log"
$testExit = $LASTEXITCODE

$reports = @(Get-ChildItem -LiteralPath $results -Filter '*.trx' -File)
if ($reports.Count -ne 1) { throw "Expected one outer acceptance TRX, found $($reports.Count). Exit: $testExit" }
python .github\skills\testfx-acceptance-validation\scripts\verify_execution.py `
    --trx $reports[0].FullName --expected-tests $expectedPath `
    --started-after $started --process-exit-code $testExit
if ($LASTEXITCODE -ne 0) { throw 'Acceptance execution proof failed.' }
```

The verifier is standard-library Python and checks one **outer acceptance** TRX
per module/TFM. The JSON plan is a nonempty array of exact TRX `testName` strings,
including data-row display names and multiplicity. Establish them from source,
data expansion, and separate discovery; reconcile discovery with the plan before
execution. Neither `--list-tests` nor a list copied from a passing report proves
execution. Record discovery's own exit/output and use the same selector for the run.

The output distinguishes planned, reported-selected, executed, passed, failed, and
skipped counts. It requires all planned cases to be present and passed, consistent
counters, a fresh run start, unique execution IDs, and outer exit zero. Zero tests,
`NotExecuted`/inconclusive/skipped results, missing modules, stale artifacts, and a
nonzero exit fail the gate. Run it separately for each planned module with that
module's expected names; aggregate success only after every gate passes. Legitimate
unrelated skips belong in a separate selection/report, not a lowered proof bar.
For negative/discovery/help-only product commands, assert their contractual exits
and output inside passing acceptance cases; do not pass their child TRX to this gate.

### Regression sensitivity across production states

Apply [feature-delivery](../feature-delivery/SKILL.md)'s red/green evidence contract
to new behavioral regressions. Retain the same outer acceptance tests and their
assertions while removing the relevant production behavior in an isolated
comparison. Rebuild/repack **each** state and establish separate package/cache/
consumer provenance before execution. An outer test can pass while its child
fails intentionally; the red phase must fail the intended **outer assertion**,
not merely observe that expected child failure.

The execution verifier above is a **green-phase** gate: it requires all planned
outer cases to pass and exit zero. Do not use it as a red-phase validator or
weaken it to accept failures. Preserve red-phase reports/logs separately and
verify the planned tests actually executed and failed at the intended assertions.
Compilation, restore/launch failures, skips, zero tests, and stale packages are
not regression proof. Record labeled behavior-removal/mutation comparisons for
new APIs, or an explicit sensitivity gap when no safe comparison is feasible.

## 4. Prove the capable CI job will run the cases

Inspect `azure-pipelines.yml` and the actual referenced templates on the branch
that will ship. Trace change detection/job conditions, `SkipTests`, configuration,
project/module selection, filter/category, conditional attributes, and environment
all the way to each consuming test step. A category missing from a new test means
the dedicated filter will never select it.

For real Windows app-model changes:

- The blocking `WindowsAppModel` job uses
  `eng\pipelines\steps\test-windows-app-model.yml`, Release packs/builds, and
  `eng\pipelines\test-windows-app-model-preflight.ps1`. Preflight requires an
  interactive/elevated AppX-capable Windows session, appropriate VS/UWP tooling,
  and installed SDKs. A Windows image label alone is insufficient.
- Real activation classes use `[TestCategory("WindowsApplicationModel")]`,
  `TESTFX_RUN_WINDOWS_APP_MODEL_TESTS=1`, and relevant OS/member conditions.
  `WindowsApplicationModelPackageTests` is instead selected by its own FQN filter;
  package-content checks do not substitute for activation.
- Native .NET 10 tests need `TESTFX_DOTNET_10_PATH` pointing to the isolated
  installation from that template, including the required .NET 8 runtime.
  Verify the variable on **every** consuming MSTest/MTP step, not just where the
  installation happens. Copy current pinned versions from the template, not this
  skill. The repository SDK and the isolated shipping SDK have different roles.
- WinApp interoperability additionally requires
  `TESTFX_RUN_WINAPP_CLI_INTEROP_TESTS=1`, `TESTFX_WINAPP_CLI_PATH`, and the pinned,
  hash-verified WinApp CLI. Do not enable it for unrelated changes.

Compare local planned identities/counts with CI's actual selection and executed
outcomes. Read TRX/TestResults and the test-step log, not merely the job badge or
successful `PublishTestResults`. Reconcile retries per attempt/module; do not sum
duplicate retry reports. If the capable job is gated out or prerequisites are
missing, report **CI execution unproven/blocked** even if a different job is green.
Record build/job/attempt identity and diagnostic artifact location. Never fix that
by weakening conditions or treating skipped regressions as validated.

Historical failure modes motivating these gates:
[missing category in #11614](https://github.com/microsoft/testfx/pull/11614#discussion_r4130429859),
[29 NotExecuted cases and missing consuming SDK path in #11805](https://github.com/microsoft/testfx/pull/11805#discussion_r4216105096),
and [packed-host launch-profile/discovery routing in #11804](https://github.com/microsoft/testfx/pull/11804#discussion_r4216242074).
These are examples, not instructions to modify those PRs.

## 5. Preserve evidence and clean up only owned state

Retain exact commands and component versions, working directory/environment
overrides (no secrets), exits, expected/observed identities and counts, pack hashes,
resolved/executed paths, and meaningful output/artifact assertions. Explain which
contract cases and release branches were exercised and which remain blocked.
Reuse existing PR evidence when publication is requested; do not publish by default.
Read back and verify any published body.

Generated assets normally live under
`artifacts\tmp\<Configuration>\testsuite\<random-id>`. Setting
`Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup=0` retains asset
directories for diagnostics, but `AcceptanceFixture.AssemblyCleanup` still removes
its cache. Capture provenance before fixture cleanup if the cache is needed.
Prefer `finally` cleanup and existing cancellation tokens/bounded command helpers.
Terminate only owned PIDs, unregister only exact run-owned package identities,
and restore modified environment/state. Never remove broad shared caches,
registrations, or workspace roots.

The app-model pipeline runs `-Mode LeakCheck` and publishes binlogs/TRX/layout/
manifest/resolved-assets/log diagnostics even on failure. On a shared local machine,
do **not** use `-RemoveStaleTestPackages` without verifying ownership: that pipeline
switch removes reserved-prefix packages for the current user. Preserve failures
and evidence first; successful cleanup must not overwrite the test failure.

## Maintaining this skill

For changes confined to this directory, validate the executable gate with:

```powershell
python -m unittest discover -s .github\skills\testfx-acceptance-validation\scripts -p "test_*.py"
```

Check referenced repository paths, command flags, and CI/helper behavior against
the current branch. These self-tests validate the evidence gate, not the product.
Do not claim a composed-product or CI run from documentation/script checks.
