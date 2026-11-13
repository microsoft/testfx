# Shared-core UI contract: layer 1

## Problem and contract

Layer 1 of the replacement for [#12435](https://github.com/microsoft/testfx/pull/12435)
prepares the shared runtime contract requested by [#7225](https://github.com/microsoft/testfx/issues/7225).
It builds on the merged [#12412](https://github.com/microsoft/testfx/pull/12412) and
[#12428](https://github.com/microsoft/testfx/pull/12428) corrections.

- Core builds only netstandard2.0, net462, net10.0 and net11.0. It remains
  nonpackable (`NotPublishable`); Extensions remains the `MSTest.TestFramework`
  package owner with its seven Windows TFMs and existing package paths.
- UI Extensions reference the compatible ordinary core: netstandard2.0 for
  classic UWP and net10.0 for modern UWP/WinUI. Duplicate legacy package paths
  carry identical core bytes, not separately compiled UI cores.
- All six directory getters on the UI host's `TestContext` return null even
  with populated bag entries. Ordinary contexts retain their existing behavior.
- Modern UWP/WinUI tests with method/class `DeploymentItemAttribute`, including
  inherited attributes, return an ordinary failed-test result before any test
  body, through reflection, Rooting and ReflectionFree metadata providers.
  Classic UWP uses netstandard core without that attribute.
- No new handshake, launcher, cancellation or cleanup policy is introduced.
  Existing MTP result/exit aggregation handles the failure; unaffected tests
  still run normally. Package versions and assembly ownership remain aligned.

Non-goals: retiring Extensions, new Uwp/WinUI packages, moving `AppModel`,
dependency-props extraction, SDK/composition changes, analyzer/exception/apartment
prototypes, and Bazel package/tooling restructuring. No release-branch enablement
or servicing change is made by this prerequisite.

## Research and decision

2026-11-13: inspected the source delta
`8dd78e1347417e400c3ef8d786059a8bb346162f...00428d48c212cf3630dcf6fa31d131c5ae745c98`.
The base and live main matched the recorded base. The source PR remained open
and unmerged. Its final package-extraction design is not this layer's contract.

Keeping flavored cores would preserve the type-shape split that prevents shared
test libraries. Copying the final package design would reintroduce the oversized
review. Instead, use normal project-reference TFM negotiation and retain the
existing Extensions packaging/swapping temporarily. Preserve API history and
friend grants. Only the native probe's now-obsolete API-absence expectation changes.

## Validation and demonstration

Planned evidence uses this worktree's Release outputs, the repository-pinned
SDK and desktop MSBuild. The producer dependency cache is read-only task input;
generated package consumers use separate owned caches.

| Requirement | Automated evidence | Surface / limitation |
| --- | --- | --- |
| Ordinary and UI consumers use shared core at existing paths | `CoreFrameworkAssemblyTests.PackedConsumersUseMatchingCoreAndExtensions`; Windows package API cases | Packed/resolved/copied hashes and executable consumer assertions |
| Six UI directory getters ignore populated bags | `WinUITests.SimpleWinUITestCase`; existing classic/modern UWP consumers | Real adapter context; real packaged UWP activation requires its existing gated environment |
| Deployment fails before direct/inherited bodies in each metadata mode | `WinUITests.DeploymentItemsFailBeforeBodiesIncludingInheritedAttributes`; modern UWP negative case | Child exit, exact TRX identities/outcomes, diagnostic and absent body marker |
| Ordinary context semantics remain unchanged | `TestContextTests.MissingAndNullPropertiesHaveDefinedDefaults`, `DirectoryPropertiesUseIndependentKeys` | Existing TestContainer runner, no scheduling change |

Acceptance scheduling remains method-parallel (CPU-count workers). Per-invocation
assets/caches isolate new cases; the existing shared-fixture and real UWP
exclusions remain unchanged. No producer caches, machine-wide environment,
package registrations or unrelated outputs are cleaned.

2026-11-13 experiments: desktop builds passed for the four core and seven
Extensions TFMs. Modern UI copies had the exact net10.0 core hash; classic UWP
had the exact netstandard2.0 hash. The initial pack filter exposed the analyzer
package's existing `Exec` dependency on `MSTest.GlobalConfigsGenerator` (not a
project reference). Including that tool made the 28-project product pack and
its content/metadata verifier pass without production build changes.

Both focused test owners build on all their declared TFMs. The first 17-case
acceptance attempt failed in setup: the unchanged base fixture unconditionally
reads six additional extension package versions, starting with OpenTelemetry.
Those exact package owners are staged separately; this is not behavioral red
evidence. SDK 12 discovery requires explicit `--list-tests text`; the corrected
discovery selected the expected 17 cases. No test assertions or scheduling were
weakened to resolve setup.

The six fixture-only packages then packed successfully, without fixture changes.
The same selection executed **17 passed, 0 failed, 0 skipped**: six shared-core
consumers, seven package API cases and four WinUI runtime cases. Each generated
consumer used fresh owned restore state and the locally packed
`MSTest.TestFramework`/`MSTest.TestAdapter` `5.0.0-dev` family with MTP `3.0.0-dev`.
The shared-core cases assert the packed, cached, resolved and copied assembly
hashes and the loaded core/Extensions/resource paths.

All five `TestContextTests` executed on net48, net10.0, net11.0 and the WinUI
test host: **20 passed, 0 failed, 0 skipped**. Fresh TRX reports were checked
against the source-declared five-case plan. Localization regeneration and the
guard passed (41 guard tests; 364 catalogs).

Commands used from the worktree root (Release, SDK
`12.0.100-alpha.1.26556.115`, Visual Studio 18 desktop MSBuild):

```powershell
.\build.cmd -c Release -msbuildEngine vs -projects src\TestFramework\TestFramework.Extensions\TestFramework.Extensions.csproj -bl
.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer1-pack.slnf -bl
.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\layer1-acceptance-packages.slnf -bl
.\build.cmd -c Release -msbuildEngine vs -projects artifacts\validation\layer1-tests.slnf -bl
.\.dotnet\dotnet.exe msbuild src\Adapter\MSTestAdapter.PlatformServices\MSTestAdapter.PlatformServices.csproj /t:UpdateXlf -bl:artifacts\log\Release\layer1-update-xlf.binlog
.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~MSTest.Acceptance.IntegrationTests.CoreFrameworkAssemblyTests.|FullyQualifiedName~MSTest.Acceptance.IntegrationTests.WinUITests.|FullyQualifiedName~WindowsApplicationModelPackageTests.PackedMSTestTestFramework_DeploymentItemAvailability" -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\layer1-green-2 -bl:artifacts\log\Release\layer1-green-2.binlog --show-test-results all
```

The temporary filters contain actual solution projects, not source globs:
the eight verifier-required MSTest package owners and their project-reference
closure plus the pack-time generator; six unconditional fixture package owners;
and the two focused test owners, respectively. All four commands exited 0.
The context hosts used `--treenode-filter "/*/*/TestContextTests/*"` with TRX
export; every host exited 0. Logs/binlogs and TRX remain in task-owned storage.

At the initial checkpoint, behavior-removal sensitivity and real classic/modern UWP
activation are not yet established. Historical proof from the final source PR
is not credited. No layout or UI rendering implementation changes in this layer;
the executable API/result transcript is more useful than a staged screenshot.
Historical proof from #12435 is not proof of this intermediate source.

### Test-value evidence

Runtime/test revision: `e22e899febaeee44ea3af385403e1c65c18164bb`, based on
`8dd78e1347417e400c3ef8d786059a8bb346162f`. Later updates to this record do not
change those executable sources.

An isolated detached comparison retained the same API, resource declarations,
tests and scheduling. It removed the deployment guard and replaced five UI
null overrides with the base property-bag getters (the sixth getter remained
null). Counterfactual patch SHA-256:
`EE03335E10599D3CBBA26D905C342B7D67457FBBC08ED5A70EC7480430C7C6F5`.
The comparison lived outside the repository's ancestor analyzer configuration.
Its initial child-launch attempt lacked the fixture's required `.dotnet` path;
an owned SDK junction corrected setup, without source changes. That attempt is
not red proof.

Both variants built and packed successfully with the pinned toolchain. The same
17-case selector executed **13 passed / 4 failed / 0 skipped** without the two
behaviors (outer exit 2), then **17 passed / 0 failed / 0 skipped** after restoring
the exact original source bytes and rebuilding/repacking (outer exit 0).

| Contract / exact case | Counterfactual observation | Correct implementation |
| --- | --- | --- |
| Six UI directory getters / `SimpleWinUITestCase` | Executed: detected; child assertion reported `TestRunDirectory` was not null. | Executed: passed with all six bag entries populated. |
| Pre-body deployment rejection / `Deployment_Reflection`, `Deployment_Rooting`, `Deployment_ReflectionFree` | Executed: detected; diagnostic assertions exposed `DEPLOYMENT_BODY_EXECUTED` for direct/inherited method/class attributes. | Executed: passed; each child reports four associated failed results with the unsupported-deployment diagnostic and one passed metadata-provider check. |
| Shared-core identities/API / six core consumers and seven package API cases | These 13 unchanged controls passed; a separate core-flavor mutation was not executed. | Packed/cached/copied hash and loaded-path assertions passed. |

Loaded UI `MSTestAdapter.PlatformServices.dll` SHA-256 was
`A3EB4489A813F89DC5EF1FAD89DA7F058A632C5A08ED3C721CF9681EF0866D16`
in all three red consumers and
`8DDCD025DF63F4BC50F9E2CF594F4A6CB53A1FDDF71F5E142B7BEDF41F7C75B7`
in all three restored-green consumers. Primary green consumers matched the
primary packed UI runtime hash
`EDCEAAF4CD0548A9453CB391BA9A42F264C966E55AF95D56905CE17A11CF463B`.
The primary framework/adapter packages were `MSTest.TestFramework.5.0.0-dev.nupkg`
(SHA-256 `2C868DA86726A51A7B8965D9E9C2704FE8B8F84A10AA6AD93F7C649C3262FD0A`)
and `MSTest.TestAdapter.5.0.0-dev.nupkg`
(SHA-256 `C96BBEBD26D90ED384945B69E8F1DD35FFB6927B469662B12FF91EE83026613A`).
The overwritten red ZIP is not retained; the counterfactual patch, build logs,
resolved asset manifests, child binaries and TRX are retained.

The comparison used the same pack/fixture commands above, rebuilt the unchanged
acceptance project directly, and repacked the restored adapter with
`.\build.cmd -pack -c Release -msbuildEngine vs -projects src\Adapter\MSTest.TestAdapter\MSTest.TestAdapter.csproj -bl`
(exit 0). Its test commands used the same filter with separate `layer1-red-2`
and `layer1-restored-green` result directories and binary logs. Strict TRX
checks reconciled the planned identities, actual outcomes and fresh timestamps.

Real classic/modern UWP activation and a full native Bazel run were not executed
here. Modern UWP discovery is not activation proof; the package-extraction layer
owns the composed activation/release-branch evidence. No CI result is credited
for this head. The comparison source is restored and the primary worktree was
never mutated for the counterfactual.

## Delivery boundary

This layer targets main only. The series coordinator owns the subsequent package
extraction, downstream rollout, release-branch/backport decisions and retirement
of the temporary package layout. Those steps are not implemented or enabled here.
