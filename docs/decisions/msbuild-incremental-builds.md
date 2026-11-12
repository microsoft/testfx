# Repository build incrementality

## Problem and contract

Make unchanged repository builds avoid redundant coverage configuration writes
and WinUI manifest generation. Preserve coverage XML bytes, regex semantics,
WinRT registrations and self-contained payload identities and metadata.
Roslyn project-data tasks are outside this change.

These are repository build fixes, not changes to shipped MSTest/MTP APIs or
package targets. The WinUI workaround is limited to repository tests using the
pinned self-contained Windows App SDK. Real input changes, deleted outputs,
component additions/removals and single-file redirection must still regenerate
the manifests. Errors must remain build failures.

## Research and experiments

- 2026-11-12, base `50b513022be7c356cb342866a3b755a4b18bf97d`:
  two identical builds of `TestFramework.UnitTests`, targeting
  `net10.0-windows10.0.18362.0`, succeeded. The generated `app.manifest`,
  intermediate test DLL and `coverage.config` kept identical hashes but
  received new timestamps.
- The pinned Windows App SDK Base `1.8.250831001` puts component **directories**
  in `CreateWinRTRegistration.Inputs`. Native MSBuild diagnostics report one as
  a missing input file, forcing the target to run. Its final `Touch` invalidates
  `CoreCompile`; the unchanged manifest is explicitly reported newer than the
  intermediate DLL.
- `AddMicrosoftWindowsAppSDKPayloadFilesFromComponents` uses metadata-batched
  `CreateItem` calls for payload files. The preceding benchmark recorded 230
  calls per build. Item-group cloning can preserve their metadata without
  external task-host calls.
- The coverage writer in `test/Directory.Build.targets` is an unmarked inline
  task and writes every invocation. Values deliberately travel in metadata:
  passing the regex lines through `WriteLinesToFile` item identities normalizes
  path-like text on Unix.
- The first marked inline class failed under warnings-as-errors because the
  host also defines the marker (`CS0436`). Use the same attribute-local
  compatibility suppression as the shipped generation tasks, not a global
  warning suppression.
- Native `mt.exe` preserves an identical merged file's timestamp. The first
  override omitted the upstream `Touch`, leaving the cache/imports newer than
  `app.manifest` and repeatedly regenerating `WindowsAppSDK.manifest`.
  Retain `Touch` inside the incremental target only: consume an invalidation
  once, then skip the whole generator/merge target on the next unchanged build.

## Alternatives and decision

Do not remove the manifest from compiler inputs, restore old timestamps after a
write, disable coverage/WinUI or patch shared NuGet/SDK installations.

Keep the coverage writer inline and invocation-local, use the existing
name-recognized thread-safe-task marker, resolve its destination at the project
boundary, and compare the complete encoded bytes before writing. Retain the
cheap target invocation so all dynamically computed settings remain inputs.

Import a test-only WinUI override after the split-component package targets;
leave legacy aggregate-package builds on their original targets. Retain the
upstream fragment generator and native merge tool. Replace directory inputs with real
files and a content-preserving membership/scalar cache; include both generated
manifests as outputs. Track `project.assets.json` as well: modern
`MSBuildAllProjects` does not enumerate every transitive package import, so a
generator package upgrade must invalidate the cache too.
Use item groups instead of `CreateItem` without changing
payload identities, exclusion rules or copy/link metadata. The workaround must
be reevaluated when the pinned Windows App SDK changes.

## Validation

Use isolated fixtures and unchanged regression assertions for pre-fix and fixed
states. Check coverage byte/BOM/regex preservation, unchanged timestamps,
changed settings, missing output recovery and threaded project isolation.
Check WinUI generated registrations, payload metadata, actual file changes,
older additions/removals, single-file settings and missing outputs.

Then run the real repository build and compare matched repeated unchanged and
source-touch builds, with the same SDK, worker budget, reuse and logging policy.
Native binlogs must show the expected compiler skips and no external coverage
task hosts. These are nonvisual build changes; generated artifact identities,
content and build routing are the relevant surface.

The standalone regressions use copied target inputs and unique child-process
fixtures outside the repository. No test mutates the shared manifest tool,
package cache, original project/source timestamps, environment or scheduling.
The existing internal-framework host remains sequential; no test assembly
parallelization or resource declarations are changed.

Run the focused checks after preparing the pinned SDK and restoring the solution:

```powershell
$evidence = Join-Path ([IO.Path]::GetTempPath()) "testfx-incrementality-$([guid]::NewGuid().ToString('N'))"
.\eng\test-build-incrementality.ps1 -ArtifactsDirectory $evidence
.\eng\validate-code-coverage-settings.ps1 -Configuration Debug
```

Windows CI runs the regression script once in the Debug matrix, inside the
existing test/skip gate, using the restored `.packages` cache and owned agent
temporary storage. The existing Release coverage-content check is unchanged.
Hosted CI execution remains separate from the local evidence.

### Executed evidence, 2026-11-12

The final uncommitted production state is based on
`50b513022be7c356cb342866a3b755a4b18bf97d`. Tested file SHA-256 values:

| File | SHA-256 |
| --- | --- |
| `test/Directory.Build.targets` | `0213787D77109374A7342BBCBD66CA3602F2B7A6A2152657694A8BCF4A4C2EC9` |
| `eng/WinUI.Incremental.targets` | `9254C99615AF3992DCE958D612F34F8AFE6D2C5EB1A56D25BD117275362D0F9E` |
| `eng/test-build-incrementality.ps1` | `B9EF839B26BDF983DFB4DB7F8E8573A518ECD4D102777D32480C263ACD1B1895` |

The command above passed all **11 cases**, exit **0**, with no skips.
The same selector containing `CoverageSettingsPreserveExactBytesAndTimestamp`
and `WinUIManifestsPreserveRegistrationsAndTimestamp` passed **2/2** against
the fixed targets (exit **0**). Against the pristine coverage target and an
empty WinUI override, both failed their unchanged-timestamp assertions
(exit **1**). This is a targeted pre-fix comparison retaining the same harness,
not a checkout-wide baseline claim. All **47 inner MSBuild invocations**
across the final suite and paired runs exited **0**; compilation/setup failures
were not counted as behavioral red evidence.

| Requirement | Evidence |
| --- | --- |
| Coverage byte/BOM/regex preservation and recovery | `CoverageSettingsPreserveExactBytesAndTimestamp`, `CoverageSettingsRefreshInputsAndRecoverMissingOutput`, `CoverageWriterPreservesValueMetadataAndProjectRelativePath` |
| Threaded project isolation and in-process execution | `CoverageParallelProjectsStayInProcess`, `CoverageParallelProjectsStayInProcessStrict` |
| Payload identity and copy metadata | `WinUIPayloadItemsPreserveIdentityAndMetadata` |
| Manifest preservation, membership and scalar changes | `WinUIManifestsPreserveRegistrationsAndTimestamp`, `WinUIFragmentAndComponentMembershipRefreshRegistrations`, `WinUISingleFileAndDllMembershipRefreshRegistrations` |
| Missing outputs, user manifests, imports and dependency upgrades | `WinUIMissingManifestsAndUserManifestChangesRecover`, `WinUIImportedInputsInvalidateRegistration` |

The canonical full-solution command passed after the final change:

```powershell
.\eng\build.ps1 -build -msbuildEngine dotnet -projects .\TestFx.slnx -nodeReuse:$false -verbosity quiet /m:8 /mt:true /p:SkipUapTargetFramework=true /p:SkipRuntimesInstall=true /p:UseSharedCompilation=false /tl:false
```

Exit **0**, zero errors and six existing NuGet warnings. Each executed MSBuild
command also captured its own binlog. All **67** generated Debug coverage
settings passed the existing validator. The normal .NET 10 and self-contained
WinUI hosts each executed
`AssertTests.AreNotEqualShouldFailWhenNotEqualInt`: **1 passed, 0 failed,
0 skipped** per host, with parsed TRX reports. The focused pre/post build
comparison produced byte-identical manifests, test DLL and coverage settings.
Two identical final canonical builds also preserved all relevant output/cache
hashes and timestamps, with zero compiler, manifest-generator or `CreateItem`
calls and 67 in-process coverage-writer calls.

### Performance

On the pinned SDK `12.0.100-alpha.1.26556.115` / MSBuild `18.14.0.55715`,
the final paired comparison used the full Debug solution, eight workers,
`-mt:true`, node/server reuse, identical binary logging, no restore and no
source edits/touches. Each arm received an untimed setup; arm order alternated.
A common import router preserved the nearest original `Directory.Build.targets`
for every project, substituting only the old versus fixed test build targets.
Both arms used the same checkout, output paths and warm shared caches.

| Unchanged full build | Samples | Median seconds | Observed range |
| --- | --- | --- | --- |
| Pre-fix behavior | 3 | 33.396 | 22.792-42.201 |
| Fixed | 3 | 14.318 | 11.558-17.495 |

That is **57.1% less elapsed time** in this small local experiment, not a hosted
CI prediction. Every pair favored the fix. Native replay confirmed **0 instead
of 1 Csc call**, **0 instead of 230 CreateItem calls**, and the same **67**
coverage-writer calls, now taking **22-31 ms aggregate** rather than task-host
round trips. The unrelated SDK project-data/resource work remained present.

An earlier pilot also favored the fix (23.095 versus 14.629 seconds median),
before the final dependency-assets invalidation input was added. A subsequent
attempt stopped in the **pre-fix** arm on the existing shared
`ReproNugetPackageLayout` copy race (`MSB3026`). That failed build was excluded;
the complete final 12-build comparison was rerun successfully without changing
settings or suppressing diagnostics.

The source-bound JSON results, exact commands, snapshots, generated-output
hashes and original binlogs are retained as local session artifacts. Repeated
no-change builds skip both `CreateWinRTRegistration` and `CoreCompile`; both
manifest outputs, the input-membership cache and coverage settings retain their
timestamps. The coverage target intentionally still computes its dynamic
settings every build, so target-level timestamp skipping is not applicable.

### Limits

These are local Windows results on one shared host, one SDK and worker budget,
with three timing samples per arm; no independent hosted rerun is claimed.
Linux/macOS, desktop MSBuild, other architectures, restore-inclusive or cold
performance were not measured. No shipping API/package changes or servicing
rollout are involved, and no package publication was performed. Reevaluate the
test-only override when upgrading Windows App SDK Base from `1.8.250831001`.
