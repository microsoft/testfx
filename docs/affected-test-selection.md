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

The 2026-11-03 affected-test package update used `18.13.0-preview.26553.4`, built from commit
`eca1718b78e9adb640b700ede7b51ec97ed077fa`. This includes setup-aware collection batching on top of the existing
refill-on-completion scheduler. Validated, correlated child execution timing permits batches of up to 256 test UIDs,
amortizing repeated child initialization and collector setup. Unavailable or incompatible timing visibly retains
the legacy 4-32 batch-size policy; an explicitly incomplete terminal-test inventory rejects the whole module map.
Larger batches can conservatively associate more tests with a source, without removing tests or framework coverage.

The initial batch size of 16, minimum size of 4, two-child limit, cancellation, authoritative exits, and atomic map
publication are preserved. The SDK version, collection budgets, shipping CodeCoverage version, and rollout gates
are unchanged. Affected-test hosts resolve CodeCoverage `18.13.0-preview.26553.4` through the existing test-only
`VersionOverride` in [`test/Directory.Build.targets`](../test/Directory.Build.targets). The affected-test, matching
test-host CodeCoverage, and Azure DevOps storage packages at that version provide `netstandard2.0` and `net10.0`
assets; older .NET and .NET Framework test hosts use the `netstandard2.0` assets. The separate shipping CodeCoverage
pin was `18.13.0-preview.26530.1`; it was not the CodeCoverage version loaded by those affected-test hosts.

**Test-value evidence (2026-11-03):** this dependency update is based on
`d3ce600610baaebac245acdbd4162615f5dfcaae`; the dependency-metadata patch, excluding this document, has SHA-256
`B067E3E0586E17320669B7E8B0190606782313DED83E3EB9B927F4C49129D3B4`. An identical clean, isolated 512-case consumer
was built against affected-test, storage, and test-host CodeCoverage packages all at `18.13.0-preview.26530.1` for
the previous variant and all at `18.13.0-preview.26553.4` for the new variant, using separate caches. The retained
`project.assets.json` from the actual TestFx test-host build also resolves all three packages at
`18.13.0-preview.26553.4`, confirming the existing override rather than a mixed shipping/test-host pairing.
Both builds used SDK `12.0.100-alpha.1.26509.101`, Debug, Windows x64, published MTP/MSBuild
`2.6.0-preview.26552.13`, and MSTest `4.6.0-preview.26531.5`. The modern consumer targets `net11.0` and runs on the
SDK's bundled .NET 12 runtime; the second consumer targets `net481`.

Real SDK `--collect-test-map` commands exited 0 and retained all 512 exact discovered UIDs and their source
associations per module. The unchanged `TimingFeedbackGrowsBeyondLegacyBatchCeiling` assertion failed against the
old package's 32-UID maximum and passed against the new package's 128-UID maximum. Both new consumer TFMs reported
nine measured batches, zero fallback, and the unchanged two-child cap. Independent per-case execution markers
verified the work; the SDK's zero ordinary result count during collection is expected suppressed reporting, not
evidence of zero execution. These checks demonstrate optimization sensitivity, not a full-CI speedup measurement.

Unchanged two-module affected selection exited 0 without starting test work; a missing module map fell back to
all 512 passing cases; an intentional single-case failure retained SDK exit 2. The actual TestFx test-host graph
also restored and built with the new dependencies, and `TerminalResources_AreCompiledIntoThisAssembly` and
`SharedTerminalTypes_AreUsable` both executed and passed with fresh TRX verification. The repository build-wrapper
pack attempt was blocked by Windows PowerShell lookup during runtime bootstrap; direct pinned-SDK build and
consumer validation succeeded. Full CI collection performance, non-Windows native collection, live Azure artifact
publication, cancellation, and partial-feedback paths were not exercised by this dependency update.

Generated acceptance assets need the shipping CodeCoverage package in their local feed even when the affected-test
host uses a newer preview. The MSTest acceptance project downloads that shipping package separately and stages it
alongside the test-host version, without referencing the older assemblies or enabling extra consumer feeds.
[Build 1656046](https://dev.azure.com/dnceng-public/cbb18261-c48f-4abb-8651-8cdcb5474649/_build/results?buildId=1656046)
failed Windows Release, Linux Release, and Windows app-model acceptance with `NU1603` because only the newer
test-host package had been staged. The `AcceptanceAssetsStageShippingCodeCoverage` rollout-validator guard protects
this separate shipping/test dependency contract.

For the staging regression comparison, the shipping download and new guard were retained while the old copy target
remained unchanged. Restore and the real `net12.0` copy target succeeded, but `ShippingCodeCoveragePackageIsStaged`
failed while `AffectedTestCodeCoveragePackageIsStaged` passed. Adding the shipping item makes the same two package
identity/version assertions pass. An isolated consumer then restores the exact shipping package with warnings as
errors; its cached nupkg hash matches the staged file. This is package-staging proof, not a full acceptance-suite run.
Both validator invocations below pass the new guard, 19 Windows isolation cases, and 39 non-Windows rollout cases:

```powershell
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests true -EnableNonWindowsAffectedTests false
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests false -EnableNonWindowsAffectedTests true
```

Affected-test execution was initially enabled with public SDK `12.0.100-alpha.1.26509.101`, built from VMR commit
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

## SDK refresh (2026-11-09)

Both SDK pins in `global.json` advance to `12.0.100-alpha.1.26556.115`, the build selected by the
[public .NET 12 daily SDK link](https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-win-x64.zip).
Its stamped SDK commit is `6e6f978fd9ca19b62796a13ff5230b21af0c42fa`.
AffectedTests and its Azure DevOps storage provider are already at the newest available `test-tools` build,
`18.13.0-preview.26556.6`, so their shared pin remains unchanged. Test-host CodeCoverage resolves that same version.
Product target frameworks, bootstrap runtime pins, the minimum supported SDK gate, and rollout switches are unchanged;
this is a `main` toolchain refresh, not a release-branch feature activation.

Initial validation used Release packages freshly built from `0ed1f874bf8afd75aec83ee4b7743d3f15cea2a2` plus the SDK-pin
patch (SHA-256 `831069C0CD28D71FADB35F8C24B569A830328BAC146CF0E7F8D273C3314D58D7`).
The isolated package-only consumer uses MSTest `5.0.0-preview.local.net12.20261109` and MTP
`3.0.0-preview.local.net12.20261109`; all eight source-packed dependencies match its fresh cache byte-for-byte.
`PackedPlatformAssemblyIsLoaded` and `AlignedAffectedTestsAssemblyIsLoaded` compare the loaded DLLs with independent
hashes from the packed assets. The consumer targets `net12.0` and `net462` and compiles with C# 12 and warnings as errors.

| Real SDK command / state | Observed result |
| --- | --- |
| Discovery and ordinary execution | Three exact tests per TFM; six pass, exit 0, with fresh TRX verification. |
| `--collect-test-map` | Exit 0; two schema-v2 shards, each with three UIDs associated with both source files. Six independent execution markers confirm work despite suppressed child reporting. |
| `--affected-tests`, unchanged sources | Exit 0 and zero execution markers for both TFMs, even with the intentional-failure switch set. |
| Missing, partial, or unsupported-schema maps | Exit 0; full fallback executes all six tests, while one missing shard executes only that module's three tests. Original shards are restored byte-for-byte. |
| Intentional `TestFailureExitIsPreserved` assertion failures | SDK exit 2; two passed and one failed test per TFM, with the exact failing identities verified. |

**Test-value evidence:** all 27 unchanged protocol tests pass for each of `net10.0`, `net11.0`, and `net462`
(81 executed, no skips). The modern protocol hosts run on the new .NET 12 runtime via explicit roll-forward.
The old SDK was not rerun, so paired SDK preservation remains unverified; no new behavioral fix is claimed.
The CMD build wrapper and Arcade runtime bootstrap cannot resolve `powershell` on this host; the repository
PowerShell entry point installed the SDK, and direct pinned-SDK build/pack commands succeeded. Full CI,
live Azure artifact publication, non-Windows native collection, and cancellation were not exercised.

The review/CI follow-up also synchronizes the Bazel engineering SDK, six archive checksums, and executed
compiler/verifier metadata; see [the Bazel SDK refresh record](decisions/bazel-build.md#sdk-pin-refresh-2026-11-09).
Build 1670839 and the GitHub shipping-attachments job exposed new CA1001 diagnostics on four ownership declarations.
The follow-up keeps `TestContext`'s documented managed-cancellation lifetime and public API unchanged, gives the
message-bus test explicit event ownership, and removes implicit ownership from borrowed orchestrator/retry handles.
All three affected project graphs build with CI analysis and warnings as errors (zero warnings/errors);
19 orchestrator/message-bus cases and the retry cancellation/disposal case execute and pass with fresh TRX verification.

## PR cache consumption repair (2026-11-09)

The green main run [1669781](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1669781)
published 48 mapping shards, but the Windows Release checkout in
[PR run 1670899](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1670899)
fetched only its detached merge commit at depth one. The complete checkout and target-branch fetch in
`DetectChanges` belong to a different job. The published storage provider requires a merge-base with
`refs/heads/main` before searching successful builds; without it, an empty candidate list produces the
misleading missing-artifact fallback.

The consumer contract is to prepare history in the affected-test invocation's own checkout, without changing
HEAD, tracked files, credentials, the configured storage identity, or collection behavior. When enabled, PR
selection fetches the main remote-tracking ref and unshallows its checkout when necessary, then requires a merge-base.
An unavailable baseline must visibly choose the existing ordinary full-test fallback, not skip tests or mark
affected execution successful. Main collection, disabled selection, and the Debug-only test path retain their
existing behavior. The source branch is deliberately main because the rollout's map producer is main; this
does not enable selection on release branches.

An unconditional full checkout for every Windows matrix entry would also affect Debug and collection jobs
that do not need this repair. Preparing Git immediately before selection is narrower and protects the actual
consumer even when the hosted checkout defaults change. Relying on `DetectChanges` or merely adding a remote
ref without repairing shallow ancestry does not satisfy the storage contract.

There is a separate producer-owned blocker: the same main artifact downloads as a 565,133,476-byte ZIP
(538.95 MiB), exceeding the published provider's 536,870,912-byte whole-archive bound. The owning Azure DevOps
storage provider needs bounded individual-shard retrieval and an explicit missing-baseline diagnostic.
Raising the download bound alone would retain repeated whole-artifact transfers. TestFx must not pin an
unpublished local package or claim a Git-only repair restores end-to-end selection.

During the initial repair, the root `EnableAffectedTests` switch defaulted to off until the repaired provider was publicly
available, pinned, and validated with the shipping SDK. Ordinary full testing and coverage remain enabled;
the existing switch also suspends main collection while its consumers cannot safely use the artifacts.
Leaving selection enabled with only the Git repair would expose expensive whole-ZIP downloads in every
module instead of the previous early fallback. This staging gate does not replace the two fixes or their
composed-product evidence. The delivery owner must complete package flow, main re-enablement, and the first
real CI selection check; no release-branch activation or unapproved package publication is implied.

| Requirement / risk | Automated evidence | Consuming surface |
| --- | --- | --- |
| A shallow, detached PR merge checkout resolves main without altering user work. | Executed: `AffectedGitHistoryRepairsShallowMergeCheckout` and `AffectedGitHistoryPreservesCheckout`. | Actual shared affected-test PowerShell body, isolated local Git remote. |
| A complete checkout remains supported and repeated preparation is safe. | Executed: `AffectedGitHistoryHandlesFullCheckout` and `AffectedGitHistoryPreparationIsIdempotent`. | Same consumer invocation, not the separate change-detection job. |
| Fetch/baseline failures run ordinary full validation; test failures remain authoritative. | Executed: `AffectedGitHistoryMissingRemoteFallsBack`, `AffectedGitHistoryUnrelatedBaselineFallsBack`, and the three native-exit cases in `eng/validate-affected-test-git.ps1`. | Existing affected-test success flag and full-test fallback gate. |
| A real published cache can be read without downloading an oversized archive per module. | Final local provider `.2` passes both the native SDK matrix below and authenticated real Azure shard reads. | `Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps`, then TestFx's pinned SDK. |

This is a nonvisual Git/storage repair; a readable command transcript is more useful than screenshots.
Validation results, exact source identities, counterfactuals, and package-flow limitations are updated below
as the repair is exercised. The scope is main; release-branch activation and servicing are not implied.

**Test-value evidence:** the new regression harness executes the actual shared PowerShell body against
isolated local Git repositories, including a depth-one detached two-parent PR merge. Retaining those tests
against the original template from base `310b20be259256263a8619e303d01b4f060f9af5` fails
`AffectedGitHistoryRepairsShallowMergeCheckout` at the intended assertion: expected the main baseline, got an
empty remote ref. The corrected step passes all nine named cases. Fixture HEAD, tracked edits and untracked
files remain unchanged; each run owns its repositories and child processes. No test-assembly scheduling,
shared caches, credentials, or user Git configuration are changed.

Validation used base `310b20be259256263a8619e303d01b4f060f9af5` and implementation identity
`C9C05B4C4FF74B9D99956F85180E50B4AB77D629DD700586FAF4476EFF874EC4`: SHA-256 of ordered
`path + space + uppercase file SHA-256 + LF` records for `azure-pipelines.yml`,
`eng/pipelines/steps/test-affected-tests.yml`, `eng/validate-affected-test-git.ps1`, and
`eng/validate-affected-tests.ps1`, excluding this evolving evidence document.

The following existing CI-validator entry points both exit 0 with the new nine-case Git suite, the 19
collection-isolation checks, 39 non-Windows rollout checks, 13 CTS checks, and package-staging guard:

```powershell
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests true -EnableNonWindowsAffectedTests false
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests false -EnableNonWindowsAffectedTests true
```

The same retained Git regression can be run directly with
`pwsh -NoProfile -File .\eng\validate-affected-test-git.ps1`; `-TemplatePath` selects the isolated original
template for the counterfactual. Local execution uses task-owned `TEMP`/`TMP` directories because this host's
protected Windows system-temp directory does not permit normal recursive cleanup.

### Packaged-consumer experiments

The final local provider candidate, `18.13.0-local.azdo-shard.20261109.2`, uses the published
`18.13.0-preview.26556.6` affected-test extension rather than a replacement base DLL. Its nupkg SHA-256 is
`40503C669B55E8691D6EFFF167F3BBDF9CAB74DE320766AA2668919D99D8E80B`, stamped with owning-source commit
`f7de68c2738ff0bf7d14087b68282b1c7c2d300a`; the restored and executed net10.0 provider asset in both real
test modules matches archive payload `E6CAD5676587C929C729C5E0A4BF50613A5E7961E0181DD33292763A51911DF9`.
The immutable first candidate `.1` passed the same functional matrix; final inspection then bounded HTTP
error-body diagnostics, with an executed regression reading 16,384 bytes before the fix against a 2,048-byte
cap. The corrected error-prefix and UTF-16/status checks pass within the final 79-case provider suite.
The exact-package TRX confirms 79 passed, zero failed/skipped. Its central regressions include
`Read_ContainerArtifactOverArchiveLimit_DownloadsOnlyRequestedShard`,
`Read_DefaultBranchMissing_ExplainsWhyArtifactsWereNotSearched`,
`Read_DefaultBranchRefMissing_ExplainsRequiredGitBaseline`, and
`DownloadFile_LargeErrorResponse_ReadsOnlyBoundedDiagnosticPrefix`. The original three storage cases failed
on the original implementation and passed after the repair; the error-prefix case separately failed at
the intended byte-read limit before its fix. The provider retains the per-shard 128 MiB bound and bounded
legacy ZIP compatibility rather than increasing the whole-artifact limit.
The consumer uses the repository-pinned SDK `12.0.100-alpha.1.26556.115`, published MSTest
`5.0.0-preview.26556.8` and MTP `3.0.0-preview.26556.8`, Release/net12.0, and a fresh per-variant package cache.
The actual shared CI PowerShell body invokes the real SDK; a reporting-only wrapper adds TRX arguments.
The loopback Azure API requires authentication, the candidate build's project scope, and an octet-stream
read, and rejects whole-artifact ZIP requests.

| Named composed case | Observable result | SDK exit |
| --- | --- | ---: |
| `NativeCollectionRetainsAll120Identities` | Native map production retains 80 + 40 exact discovered UIDs and all 120 independent per-case execution markers. | 0 |
| `ZeroSelectionSkipsAllModules` | Neither module executes, even with both intentional-failure switches set. | 0 |
| `ChangedSourceExecutesExactMappedSubsetPlusConservativePeerFallback` | A production-library source edit executes the exact 16 mapped test UIDs plus the other module's conservative 40-test fallback; all 56 pass. | 0 |
| `SelectedTestFailureRetainsIdentityAndExit2` | The same selected set contains only the intended `FeatureA_Result (1)` failure, with each report's names and outcomes checked. | 2 |
| `MissingMapsExecuteAll120Identities` | Missing remote shards execute all 120 tests, rather than producing a false empty success. | 0 |
| `MissingModuleFallsBackWithoutExecutingUnchangedPeer` | With unchanged sources and one missing shard, its 80 tests run; the available-map peer executes zero despite its failure switch. | 0 |

These are functional work-selection observations, not a TestFx CI timing benchmark. Unknown source
associations deliberately retain conservative full-module execution; they are not ignored to manufacture
pruning. Positive TRX files are checked against independently planned names with the existing execution gate.
The same retained native consumer against the published original provider fails `ProviderMustReadSingleShard`
at its whole-ZIP request assertion after successful ordinary execution and native collection.

Experiments also corrected three fixture assumptions, none counted as product red-phase proof: collection
children need independent marker files even when each host disables test parallelism; editing a test assembly
invalidates its metadata, so the meaningful source edit belongs to a separately referenced production library;
and negative HTTP controls require a fresh, process-identified, noncached loopback endpoint. The missing-shard
control is independently verified to return HTTP 404 before the final matrix.

The final `.2` package also reads real build 1669781's 446,113-byte/27-UID and 104,935,457-byte/3,997-UID
shards without ZIP staging and reports a missing shard correctly, exit 0. Both schema-2 maps retain baseline
`54980bc56a22eb758c040a20b8f3571ee2d50417`. These are authenticated read-only checks through the explicit-token/Basic
path used by CI, using the supported current-build override; normal ancestry is independently covered by the
Git regressions and native SDK fixture. This host's `DefaultAzureCredential`/`AzureCliCredential` path is
unavailable and is not counted as passing. No interactive credential workaround, token persistence, remote
publication, or pipeline write is claimed.

Exact fixture sources, native commands/binlogs, planned names, TRX, execution markers, package hashes, and
per-command exits are retained in the session's `affected-test-fix` artifacts. They are local evidence, not
public reviewer download links. Full repository acceptance, a live PR pipeline using the repaired package,
and release-branch servicing remained unverified at that stage; the default-off rollout was intentional.
Before re-enabling, validate the actual aligned public AffectedTests/storage/test-host CodeCoverage package
tuple after dependency flow. The package-pin PR deliberately triggers full testing through `eng/**`;
confirm real pruning on a subsequent mapped product-only PR with a fresh main map, not by weakening the
force-all policy on the pin/configuration change itself.

### Published-package check before activation

The public `test-tools` feed was checked again on 2026-11-09 before finalizing this repair. Its newest
aligned AffectedTests, Azure DevOps storage, and CodeCoverage version is `18.13.0-preview.26559.2`, but all
three nupkgs identify source `3d593e008169bb3df357f337df901cea95a7c727`, before the repaired provider commits.
The published storage nupkg SHA-256 is
`C6F9A5FFF03F2A64DA7B7D953CE30FE7B6FD26FAE9F4C7A41125301DC052A4A1`.

Inspection of its actual net10.0 DLL confirms that `DownloadMappingAsync` still calls
`DownloadArtifactAsync`, stages the entire ZIP, and applies the 536,870,912-byte bound before selecting a
shard. It also retains the empty-candidate/missing-artifact behavior when the common Git commit is absent.
Git ancestry confirms that the package's stamped source does not contain
`f7de68c2738ff0bf7d14087b68282b1c7c2d300a`. This is static payload/provenance evidence, not an executed
claim that the newly published tuple passes the composed matrix.

Consequently this change deliberately keeps the existing `18.13.0-preview.26556.6` pin and leaves the
rollout off by default. A newer version number alone is not evidence that the repair shipped. The Git
preparation and regression guards are ready, but activation requires a published package containing the
verified provider repair; no unpublished local package is committed as a dependency.

## Public package activation (2026-11-10)

The public `test-tools` feed now contains the aligned `18.13.0-preview.26560.3` affected-test extension,
Azure DevOps storage provider, and test-host CodeCoverage packages, all stamped with source
`13fcbb6be5af9c988589f5474d595818085a7441`. Unlike the earlier `.26559.2` build, the actual published provider
has bounded individual-shard reads, explicit missing-Git-baseline diagnostics, and bounded error prefixes.
It retains both netstandard2.0 and net10.0 assets and requires MTP `3.0.0-preview.26559.7` or newer.

The shared affected-test version and its dependency provenance are updated together. The existing test-host
CodeCoverage override follows that version automatically; shipping CodeCoverage remains
`18.13.0-preview.26556.6`, avoiding an unrelated shipping dependency change. Package staging must continue
to include both versions where generated acceptance assets consume the shipping package.

The activation scope remains main/Windows Release, with the existing Git-history preparation, conservative
missing/stale/incompatible-map fallback, main full-test/coverage run, and separate map-collection job.
Non-Windows collection/selection and release-branch activation remain off. After the exact published tuple
passed the checks below, the root `EnableAffectedTests` default was restored to true. Setting it to false
still rolls both main collection and PR selection back to ordinary full validation.

Validation uses a fresh package cache, the pinned SDK `12.0.100-alpha.1.26556.115`, and the published tuple
rather than the earlier local-only provider. The nupkg SHA-256 values are:

| Published package, all at `18.13.0-preview.26560.3` | SHA-256 |
| --- | --- |
| `Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps` | `F96D4C9DE760B0917FA215CA6C69E4517A7CA6CBE242EF44AA3B791B00BD1ACE` |
| `Microsoft.Testing.Extensions.AffectedTests` | `5A5BB318085D1D049B4F88935AB1041D9515801400BEA95B9ED0D719FCE48E7E` |
| `Microsoft.Testing.Extensions.CodeCoverage` | `9AE65E788DF0E3DF64B17814714A4793B324960E396CF642DEB9B8377362EC4E` |

The same six native SDK scenarios pass with published MTP `3.0.0-preview.26559.7` and MSTest
`5.0.0-preview.26556.8`: all 120 collection identities, zero work on unchanged sources, the exact 16 mapped
tests plus 40 conservative peer-fallback tests, preserved named failure/exit 2, all-missing fallback of 120,
and one missing shard running 80 while its unchanged peer executes zero. Positive TRX identities/outcomes
and independent per-case execution markers are verified, not only console totals.

The published provider also passes actual authenticated Azure reads of build 1669781's 446,113-byte and
104,935,457-byte shards, with their independently recorded hashes, plus the absent-shard case and zero ZIP
staging. The loaded public provider/base hashes are
`1C4B84CF079FD28BF10BFFD1BAFBF795888181C49E716C9F5D1A25B6A9707CED` and
`62B9D12AC61B035EA75B2C8DAE6E77AE919D3362C6628D1BC33EC362487D4383`.
These read-only probes use the supported current-build override; normal ancestry is covered by the real
Git/shared-step regressions. Tokens are supplied only over stdin and are not persisted.

**Test-value evidence:** the retained original-provider comparison detects the whole-ZIP request at
`ProviderMustReadSingleShard`; the exact public replacement passes the same behavioral assertions. All 81
rollout checks pass with the enabled default, explicit activation, and rollback. The actual TestFx source
graph builds without warnings/errors, and `TerminalResources_AreCompiledIntoThisAssembly` and
`SharedTerminalTypes_AreUsable` execute and pass on each pinned .NET 10.0.14 and .NET 11.0.0-rc.3.26522.148
runtime: four passed, no skips. Source-host and independently restored consumer DLLs match all three
published nupkg payloads, with 12 physical file comparisons.

The source build/pack uses `OfficialBuildId=20261110.99`; the built MTP source version
`3.0.0-preview.26560.99` satisfies the dependency floor without changing unrelated published MSTest/MSBuild
pins. The source shipping package contains the netstandard2.0, net10.0, and net11.0 assets and has SHA-256
`E5E2A6428BDF249FAFE95DA5A9A0F7A8E03FA15351C43B35025314CAAEE2F450`.
Using `$project` for the terminal-contract unit-test project and `$evidence` for the owned evidence directory:

| Command | Result |
| --- | --- |
| `.\.dotnet\dotnet.exe build $project -c Release --disable-build-servers -p:ContinuousIntegrationBuild=true -p:OfficialBuildId=20261110.99 "-bl:$evidence\source-project-build.binlog" --verbosity minimal` | Exit 0; actual updated dependency graph, zero warnings/errors. |
| `.\.dotnet\dotnet.exe pack src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj -c Release --no-restore -p:OfficialBuildId=20261110.99 -p:ContinuousIntegrationBuild=true "-bl:$evidence\source-platform-pack-complete.binlog" --verbosity minimal` | Exit 0; source shipping package produced. |
| `.\.dotnet\dotnet.exe test --project $project -c Release --no-build -p:OfficialBuildId=20261110.99 -p:UsingDotNetTest=true -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= --filter "FullyQualifiedName~TerminalReporterContractTests" --report-trx --results-directory "$evidence\source-pinned-runtime-results" --no-ansi "-bl:$evidence\source-pinned-runtime-tests.binlog"` | Exit 0; four expected cases passed on the pinned runtimes, with both per-TFM TRX execution gates passing. |

The CMD wrapper/runtime bootstrap could not resolve Windows PowerShell on this host. After the repository
script bootstrapped its pinned toolchain, direct pinned-SDK build/pack succeeded; the two required x64
runtimes were installed into the worktree using `eng/common/dotnet-install.ps1 -DotnetPath`. An initial
`--no-build` pack lacked unbuilt netstandard resources; a normal focused pack built those assets. The first
partial-map fixture also selected an obsolete retained shard after the package changed object keys; binding
its expected map to the current producer commit fixed the fixture without weakening its assertions. These
setup failures are not product red-phase evidence.

Exact native commands, fresh-cache assets, payload comparisons, source binlogs/TRX, and live read results
remain in the owned session artifacts. Full repository acceptance and live CI using this activation were not
run locally. First fresh main-map publication and a subsequent product-only PR are still required to prove
actual CI pruning; the pin/configuration PR intentionally retains its force-all behavior. No release-branch
rollout, backport, push, or remote mutation is claimed.

## Main integration (2026-11-11)

The first hosted Azure Pipelines check on head `3e4fefaa5038f896d391d57d60b7139a26213024` was
`action_required` with title `Skipped due to merge conflicts` and summary `This branch has conflicts that
must be resolved.` No hosted test execution or cache-consumption result can be inferred from that check.

The branch integrates main commit `7d5d0508fd6984bf37d0eee47ba7dfc2443febbe`. The dependency conflicts retain
main's Arcade/templating `12.0.0-beta.26560.9`, published MSTest/MTP `5.0.0-preview.26559.7` /
`3.0.0-preview.26559.7`, and shipping CodeCoverage `18.13.0-preview.26560.2`. The affected-test/test-host
coverage tuple remains the deliberately validated `18.13.0-preview.26560.3`, with source
`13fcbb6be5af9c988589f5474d595818085a7441`; neither side's independent dependency updates are rolled back.
The pinned .NET SDK and runtime versions are unchanged. The merged default-enabled, explicit-enabled, and
rollback configurations each pass all 81 named rollout checks, including the nine real-Git regressions.
The updated source dependency graph builds with zero warnings/errors using the pinned SDK and
`OfficialBuildId=20261111.91`. `TerminalResources_AreCompiledIntoThisAssembly` and
`SharedTerminalTypes_AreUsable` pass on each pinned .NET 10/.NET 11 runtime, with both per-TFM TRX gates
verifying four executed/passed cases and no skips. This is green-before/green-after merge validation, not
new hosted cache-use or savings evidence.

## CI layout

- `global.json` defines the repository-specific `test.affectedTests` change policy and selects Azure DevOps artifact
  storage for the public `microsoft.testfx` pipeline.
- The trusted main-branch Windows Release entry runs full tests and publishes coverage. When affected tests are enabled,
  a separate `WindowsAffectedTests` job runs `--collect-test-map` without the normal report, retry, or coverage arguments, because
  collection owns its instrumentation and launches discovery children with `--list-tests`. A collection failure
  still fails its job; it is not normalized to success or allowed to consume the Release entry's time budget.
  Main-branch Windows validation entries retain a four-hour budget; the dedicated collection entry has six
  hours. PR jobs retain their two-hour budget.
- With the rollout enabled, the Windows Release PR test runs `--affected-tests`.
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
  The adapter trace-bridge test also removes collection-child and batch-feedback context from its isolated SDK
  invocation, preventing the nested host from overwriting the outer collection batch's final test inventory.
- `eng/validate-affected-tests.ps1` verifies the SDK gate, package reference, storage configuration, pipeline wiring,
  nested-process isolation, command names, and fallbacks. Its `-SelfTest` mode, run by `DetectChanges`, also rejects
  shared collection/validation jobs, regressed collection budgets, bypassed rollout gates, ignored collection
  failures, and artifact collisions.

### CTS comparison: research and decisions (2026-10-31)

The requested comparison is legacy CTS collection versus affected-test collection on the same unofficial-main
revision, Windows image, Release outputs, and test modules/frameworks. It must not replace full validation, alter
PR selection, publish CTS data into `TestFx_AffectedTestsMaps`, or change official/release-branch builds.

[Run 20261030.35](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1657355), at
`5fffd919d0f88837dce2e3799a8084d996e5fe2a`, spent 224.42 minutes in collection. Its 74 module/framework maps contain
70,695 discovered UIDs, not executed-test counts. Summing each module's start-to-upload interval gives 223.34
minutes, with no overlapping modules; discovery accounts for only 90.07 seconds. The ordinary Windows Release
test step took 62.03 minutes, independently of collection.

| Collection workload | Time, minutes |
| --- | ---: |
| MSTest acceptance | 80.44 |
| MTP acceptance | 75.76 |
| Analyzer unit tests, five frameworks | 25.43 |
| Other maps | 41.71 |

The two acceptance projects account for about 70% of collection time. Inspecting CTS confirms that its worker
pool can process different modules concurrently and that its batch-size strategy targets the fraction of a
batch spent executing tests, rather than just total elapsed time. Those are distinct from the refill-on-completion
repair already shipped by the affected-test extension. Candidate improvements are bounded module scheduling,
setup-cost-aware batch sizing, and avoiding repeated acceptance-fixture initialization. These remain proposals;
the logs do not expose per-batch setup/execution splits and do not establish a controlled CTS speedup.

The standalone tool is `cts` `2.13.0-alpha.26530.1`, independently versioned from the affected-test packages.
Its `collect testingplatform` command uses MTP's server protocol rather than `dotnet test --collect-test-map`.
CTS's default worker count is the logical processor count; the comparison must record this rather than imply
that it uses the affected collector's two-child limit. Its native maps are batch-based and are not compatible
with affected-test v2 shards. Compare test/module coverage, source associations, batch sizes, elapsed time,
failures, and missing modules, not literal map-file equality.

The configured comparison enables CTS's same-module batch parallelism and records the worker count explicitly.
It selects the affected-test-capable Release hosts from the existing build outputs, uses managed DLLs for
portable .NET hosts and EXEs for .NET Framework and Windows-targeted applications, and records every input
path/hash. Windows applications must retain the apphost's native activation manifest: a `dotnet` DLL launch
can fail WinRT activation and leave an incomplete trace when a native startup child exits. Other .NET hosts
still use DLLs because CTS deduplicates identical apphosts by hash and can silently drop a distinct
framework/flavor. `WindowsApplicationModulesKeepTheirApphost` protects the Windows exception. Successful
collection must produce one consistent TRX and committed native map per input; failed CTS exits remain
failures and retain diagnostics. Native-startup helper processes also clear inherited affected-test request
and batch-feedback variables so they cannot overwrite their parent batch's final UID/timing inventory.

Real package validation also exposed a separate timing contract gap. Both MTP JSON-RPC test-node serializers
emit `time.duration-ms`, but omit `time.start-utc` and `time.stop-utc`. CTS expects the latter fields and substitutes
minimum timestamps when they are absent. The paired .NET 10/.NET Framework run therefore reports nonzero test
durations in TRX but zero average test-time participation; its overhead-aware batcher cannot calibrate correctly.
The comparison warns and records timestamp availability rather than treating those metrics as trustworthy.
Fixing this producer/consumer contract is an additional prerequisite for evaluating CTS's adaptive policy,
not a protocol change made by this pipeline experiment.

**Activation prerequisite:** this CTS package is currently available only in the private VS-Platform feed.
Anonymous access is denied, it is absent from `test-tools`, and the current public pipeline has no external NuGet
feed connection. The producer's public-feed publishing allowlist also omits CTS. Prepare the comparison against
the existing public `test-tools` source, but keep it disabled until CTS is approved and published there and a
real composed-product smoke run succeeds. Do not substitute the affected-test extension, invent a service
connection, publish a private package, or commit credentials to make activation appear successful.

An isolated main-only job is preferable to adding CTS to Release validation or to the affected-test job:
both alternatives share deadlines and outputs, and could instrument the same applications concurrently.
The comparison should reuse the Windows build/prerequisites, own its timeout and artifact names, use local
filesystem map storage, retain CTS's exit status, and preserve failed-run diagnostics. Full-suite CI performance,
public-feed restore, and full map/result parity remain live validation requirements. No product servicing or
backport is required for this temporary unofficial-main experiment.

#### Local validation and remaining gates

The local implementation is based on `b150f7d75b285c55d49b553d6d143d969be761f0`. Its six executable/configuration
files, excluding this record, have combined SHA-256
`A502C0D4E7D62020CD395054A0D95D91DF0CF6D809725FABD6C2B95C4AADA549`.
The identity hashes sorted repository-relative paths plus a NUL, each file's raw bytes, and another NUL.
The unchanged ordinary Windows test dispatch remains in the shared template's `else` branch.

The actual CTS nupkg has SHA-256 `A003A1880135262D71C327B80E21884B5FED77F46D3A7825B832944F05D6D105`
and identifies producer commit `fe4721e980b711722351edfd491577a03b8d4421`, matching the affected-test package build.
It targets .NET 10; the smoke run used the repository SDK `12.0.100-alpha.1.26509.101` and runtime `10.0.13`.
Its generated consumer resolves MSTest `4.6.0-preview.26530.8`, MTP `2.6.0-preview.26530.8`, and affected-test
packages `18.13.0-preview.26530.1`. Both executed MTP DLLs match their resolved nupkg assets by SHA-256.
The consumer is a task-owned Git repository, not the main checkout.

| Contract / evidence | Executed result |
| --- | --- |
| Independent main-only comparison / `CTSComparisonUsesIndependentJob` | Retaining the new guard against the actual base pipeline rejects it at the intended missing-job assertion. The implemented pipeline passes all 12 CTS wiring cases, including main/skip gates, artifact isolation, native failures, and exclusive dispatch. |
| Framework input fidelity / `ManagedModulesAvoidApphostDeduplication` | An API-compatible counterfactual selecting apphosts instead of DLLs fails at the identical-module guard, exit 1. Restoring the implementation passes the same selection/artifact self-tests, exit 0. |
| Complete maps and reports / `CTSCompleteArtifactsAreVerified`, `CTSMissingReportIsRejected`, `CTSUncommittedMapIsRejected`, `CTSUnexpectedReportModuleIsRejected` | A successful run requires one TRX and committed map per input and the correct reported module paths. Each incomplete fixture is rejected. |
| Real packaged collection / `PositiveValueHasOneAdded`, `NegativeValueHasOneAdded` | Both tests execute on .NET 10 and .NET Framework 4.6.2: four passes, two TRX files and two committed maps, native/wrapper exit 0. Injecting one intentional failure per framework retains four executed tests, two failures, both reports, and native/wrapper exit 1. |
| Timing disclosure / `CTSMissingTimestampsAreReported` | Missing UTC test timing is recorded and warned about, not presented as valid adaptive-batching data. |

The following source-level commands exit 0. The collector has nine selection/artifact cases; each rollout
invocation retains 19 Windows and 39 Unix cases, adds 12 CTS cases, and passes the existing package-staging guard.
Changed YAML parses, the new script passes the repository's PowerShell analyzer settings, and the existing
validator retains its ten pre-existing analyzer findings without new findings.

```powershell
pwsh -NoProfile -File .\eng\collect-cts-comparison.ps1 -SelfTest
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests true -EnableNonWindowsAffectedTests false
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests false -EnableNonWindowsAffectedTests true
```

The packaged invocation is `pwsh -NoProfile -File .\eng\collect-cts-comparison.ps1 -Repository <owned-consumer>
-Configuration Release -DegreeOfParallelism 2`. Source, command arguments, input hashes, exits, console output,
TRX, and native maps are retained together. The canonical acceptance execution verifier was also attempted:
it rejects CTS's legacy TRX run timestamps because they omit a UTC offset. Reports and the verifier were not
rewritten or weakened. Direct test identities/counts and fresh run-owned artifacts establish the smoke execution,
but this does **not** claim that the canonical acceptance gate passed.

An initial `net12.0` smoke target was rejected by this SDK outside the repository's framework setup, and the
first tool launch lacked the pinned .NET 10 runtime. Neither setup failure counts as behavioral red evidence.
The final consumer uses supported `net10.0` and `net462` targets. Full-suite performance, internal TestContainer
and sample/module parity, public-feed restore, and Azure template expansion against published source remain
unverified. Enabling `EnableCTSCollection` is therefore not part of this local delivery; publishing/authorizing
CTS access and repairing the timing contract remain prerequisites.

### Collection acceleration implementation (2026-10-31)

The follow-up request authorizes implementation in TestFx and vs-code-coverage, not just a CTS comparison.
The shipped refill-on-completion improvement remains the baseline. No publication, new SDK-repository work,
private-package redistribution, test/framework exclusions, or changes to official validation are authorized.
The source identities in the earlier comparison record describe that completed investigation, not this new patch.

TestFx owns the MTP JSON-RPC timing producer, repository collection orchestration and acceptance-asset setup.
The isolated vs-code-coverage implementation owns execution feedback, adaptive batching, CTS report timestamps
and the changed collector packages. The coordinator owns aligned-package composed testing and the eventual
rollout decision. Both repositories must preserve test identities, conservative associations, failed-test versus
infrastructure-failure exits, cancellation/draining, atomic map publication and legacy/absent-feedback behavior.

The timing repair adds invariant, offset-bearing UTC start/stop fields when the corresponding real
`TimingInfo` values are available, retaining `time.duration-ms`. Unknown/default timestamps remain absent;
older consumers may ignore the additive fields. Both legacy and modern JSON serializers and the source-only
client package must stay consistent.

TestFx uses a bounded pool of independent SDK invocations against exact prebuilt
test modules. This avoids the SDK's forbidden in-invocation parallel-collection option without modifying the SDK.
The global instrumentation budget is at most four children, bounded by available logical CPUs,
usually two module slots with two child batches each. Each module owns logs/results and its lifecycle; artifact
publication still uses the provider's shared-container coordination. Scheduling must not silently multiply
per-module child limits, omit frameworks or turn partial/canceled collection into success.

The pool adds a project/TFM label to unscoped stdout and stderr. Affected-test diagnostics that already start
with `[affected-tests] [assembly | framework | ...]` retain that context without an additional pool label.
Assembly names may contain balanced bracketed text, but recognition cannot cross an unscoped tag such as
`[warning]` to treat a later `[a | b]` as the leading context. Suffix cleanup runs only after this scope check.
The trailing `[RID - runtime]` on a `Test-map collection child failed: MSTest v...` banner is also omitted when
both values exactly match the full leading context. Different child runtime/RID values, banners without that
context, other diagnostic text and additional failure details are preserved. All four fields must belong to
that same leading context; a short `[assembly | framework]` label followed by an embedded banner keeps the
banner's runtime/RID suffix. This keeps runtime/RID details once, including when the assembly name differs
from the project/output directory. Removing every pool label
would make interleaved unscoped messages ambiguous; stripping the provider's label would discard useful runtime
information. Only the console presentation changes:
per-module `collection.log` files retain the original output, and scheduling, collection failures, exit codes,
package versions and rollout gates are unchanged.

**Initial output regression evidence (2026-11-04, `f8c87eb536b50fe26199ec271c001f2ea3feb1df`):**
the source/test patch is based on
`8bae1c65b06293e2aea4481e4a7fd661279fd95b`; its SHA-256, excluding this document, is
`128E4D5005A75E2F9D8A4D14A4B77CF28D5CCDDC0DADA100CA622A1B69676D6F`.
`pwsh -NoLogo -NoProfile -File .\eng\validate-collection-module-pool.ps1` passes all 30 checks on Windows,
PowerShell 7.6.9 (exit 0). Two isolated comparisons retain the identical validator. The original reader from
the base revision exits 1 with 18 exact console-output assertion failures across nine scoped cases on stdout
and stderr, including `ModuleOutputDoesNotRepeatAffectedTestContext`. The prefix-only reader, SHA-256
`DB6E482A834B5147A3E32526B6E75CCDD8D99164D125FD50BBA8BF569F55C3B2`, exits 1 solely because
`ModuleOutputDoesNotRepeatRuntimeAndRid` still sees the duplicate suffix on both streams.
Different child runtimes/RIDs, missing full context, non-banner diagnostics and trailing failure details are
preserved in the prefix-only and final states. Raw-log preservation assertions pass in all three states.
Both red runs stop before the existing process-lifecycle checks; the complete green run includes them.
Before/after fixture replays of the actual reader were inspected at 80 columns by 24 rows for the initial
prefix cleanup and 85 columns by 58 rows for the runtime/RID suffix, including the full reported diagnostic.
This is output-reader and existing process-harness evidence, not a new SDK collection or hosted Azure DevOps
rendering run. No shipped .NET binaries or package versions change.

The initial PR's Ubuntu 22.04 `Validate bounded collection scheduling` step also reports
`Collection module-pool self-tests: 28 passed.` in
[build 1664226](https://dev.azure.com/dnceng-public/cbb18261-c48f-4abb-8651-8cdcb5474649/_build/results?buildId=1664226&view=logs&jobId=51849d02-8791-58b9-bb59-332a6babfdac),
log 8. This confirms the initial Linux count, not the later correction below; macOS execution is not claimed.

**Context-boundary regression evidence (2026-11-04):** the correction is based on
`f8c87eb536b50fe26199ec271c001f2ea3feb1df`; its source/test patch SHA-256, excluding this document, is
`23C9057C4552729C5BE4FEE343D37C48620393A7589C9B2AF880EBEF7A4BB24F`.
The same validator is retained with that revision's reader in an isolated comparison.
`ModuleOutputPrefixesDiagnosticsContainingBracketedText` and
`ModuleOutputPreservesEmbeddedBannerInUnscopedDiagnostic` fail their exact console-output assertions on
both streams (exit 1, four assertion failures). The unchanged command
`pwsh -NoLogo -NoProfile -File .\eng\validate-collection-module-pool.ps1` then passes all 32 Windows checks
with the correction (PowerShell 7.6.9, exit 0). Bracketed assembly names and raw logs remain preserved.
The red comparison stops before the existing process-lifecycle checks; the complete green run includes them.
Linux/macOS execution of this correction remains unverified.

**Short-context regression evidence (2026-11-05):** the correction is based on
`56f2bfb33922a592a51b4f2c1035423006a7018b`; its source/test patch SHA-256, excluding this document, is
`5B196AC8CE47A9126169426FC27144343CFF22162024C3D596FDE3DAEBF24B96`.
The same validator and inputs are retained with the pre-fix reader in an isolated comparison.
`ModuleOutputPreservesEmbeddedBannerAfterShortContext` fails its exact console-output assertion on stdout
and stderr (exit 1, two assertion failures); the fixed reader passes all 34 Windows checks (exit 0).
`ModuleOutputDoesNotRepeatRuntimeAndRidForBracketedAssembly` passes in both states, confirming that genuine
duplicate suffixes are still removed for bracketed assembly names. Raw-log preservation also passes.
Both runs use PowerShell 7.6.9 and `pwsh -NoLogo -NoProfile -File .\eng\validate-collection-module-pool.ps1`,
with process-scoped `TEMP` and `TMP` set to the same task-owned directory under `artifacts\tmp`.
An initial inherited-temp run failed during cleanup and is not counted as behavioral proof.
The red comparison stops before the existing process-lifecycle checks; the complete green run includes them.
Actual before/after reader output was inspected in an 80-column, 24-row terminal. SDK collection,
hosted Azure DevOps rendering and Linux/macOS execution of this correction remain unverified.

Execution feedback must identify the protocol version, module/run/batch and planned/executed identity set.
Atomic, task-owned feedback is preferable to parsing localized console text or coupling the extension to the
standalone CTS client's dependency closure. The schema and bounds below were agreed against the coverage implementation's current source;
absent, incompatible, incomplete or invalid timing is a distinct state with
observable conservative behavior. Increasing batch sizes without knowing setup versus execution cost was not
selected as an evidence-backed optimization.

The agreed feedback artifact uses `MTP_AFFECTED_TESTS_BATCH_FEEDBACK_PATH` and
`MTP_AFFECTED_TESTS_BATCH_FEEDBACK_ID`, with `schemaVersion: 1`, `batchId`, `exitCode` and
`tests[].{uid,state,timingStatus,startUtc,stopUtc}`. Timing is `measured`, `unavailable`, or `notExecuted`;
skipped entries have no timestamps. Superseded retry and nonterminal updates do not count as completed UIDs.
Only an exact correlated terminal UID set with coherent offset-bearing intervals inside the child lifetime
can enable timing-based adaptation. Supported feedback missing or duplicating required identities rejects
the map; absent/incompatible/unusable timing remains an observable legacy 4–32 fallback.

The coverage implementation selected a finite 256-UID timed ceiling, retaining initial 16/minimum 4,
at most 2x growth/50% shrink, and an execution target of `max(30s, 3 * observed setup)` capped at 120 seconds.
Its committed-source packaged measurement retains 1,024 UIDs and maps while reducing 33 published-baseline
startups to 11 at that ceiling. The aligned measurements below establish local composition,
not a full TestFx CI speedup. Larger batches preserve conservative associations
but may select more unaffected tests later.

Acceptance setup changes share only the traced immutable-package lifetime, not generated projects or outputs.
Shared caches may not conceal freshly packed producer changes or introduce cross-test/process races.
Required evidence is focused red/green sensitivity, actual changed shipping packages and representative
startup-heavy multi-module collection with identities, mappings, exits, cancellation/fallback and timings.
No full-suite speedup or downstream activation is claimed before that evidence exists.

The selected TestFx setup optimization shares only immutable NuGet package payloads among acceptance children
inside one parent-owned, marked collection-run/module directory (`TESTFX_COLLECTION_NUGET_CACHE`).
Ordinary acceptance still creates its fresh random cache. Mutable generated assets/build outputs remain private;
the assembly fixture never deletes the shared cache, nested test applications do not inherit the opt-in,
and the coordinator removes it only after confirming all owning SDK/collection children have terminated.
The cache is not part of published test-result artifacts and is not reused across packing/collection runs.

The SDK pool now prioritizes the two known long acceptance suites, then analyzer workloads, instead of leaving
the longest work behind short modules. It uses exact repository-relative module patterns and the repository
working directory; an absolute pattern is not accepted by SDK module globbing. An owned short results root
is supported for legacy .NET Framework deployment paths. With the unchanged published collector and an identical
196-UID startup-heavy two-framework fixture, the first interleaved measurement was 18.79 seconds with one module
slot and 11.91 seconds with two, both with ten startups and two complete 98-UID maps. This isolates module overlap,
not the new batching feedback or cache benefit, and is not a full-suite forecast.

#### Final local composition and performance evidence

The TestFx implementation is bound to base `b150f7d75b285c55d49b553d6d143d969be761f0` plus the
22-file executable/configuration/test manifest SHA-256
`771FB071D0C6C76CB5FE78008F4A3A4B6F507EACAC63765952C27167877F545A`.
This excludes this decision record and hashes sorted `relative-path|uppercase SHA256` lines,
joined with LF and encoded as UTF-8 without BOM. The retained `final-source-evidence.json`
contains every individual path/hash. Local commit/package handoff records preserve the resulting
commit identity; neither evidence nor local commits imply a push, public package or CI result.

The coverage implementation is clean at `8e1bd20fd881a2ca9d13e8081e39478fa1107598` on
`dev/amauryleve/affected-collection-acceleration`. The final local affected, coverage and storage packages
are `18.13.0-local.acceleration.20261031.2`; CTS is `2.13.0-local.acceleration.20261031.2`.
Nuspecs identify that exact source revision. Native payloads were preserved byte-for-byte from
`fe4721e980b711722351edfd491577a03b8d4421` (42 entries each in coverage and CTS).
This validates the available Windows x64 composition, not an ARM64/native rebuild or Unix execution.

The aligned MTP snapshot is `Microsoft.Testing.Platform.2.6.0-preview.26530.8.nupkg`,
SHA-256 `D4B104B10128CC749382D1063AFFFBFE7AE9114B412D8EA8F550BDA18A14E8C1`.
Its source-only companion has SHA-256
`C540970AAAF2CCBB062D4A57DF15262389217827F1D41E8FBAFB8F689052DD5F`.
These are local corrected snapshots, not the original published bytes despite the same version.
Fresh caches and consumer `project.assets.json` were checked; the actual .NET 10 DLL is
`5A735D0F6E9BD26A7D04ADDD802FA770AB32DA1FD8AFA9978F638D04B9A1776E`,
and the .NET Framework consumer's netstandard2.0 DLL is
`145A4381C8794518038447F8C1B69DBF6A071F2907D0D93CDBF35EF12DD0E085`.

The larger TestFx fixture contains 1,024 data rows plus two ordinary cases **per framework**,
2,052 identities across .NET 10 and .NET Framework 4.6.2. Each assembly startup waits 300 ms;
each data row waits 5 ms and asserts `Arithmetic.AddOne`. The unchanged source methods are
`StartupHeavyCollectionPreservesEveryCase`, `PositiveValueHasOneAdded` and `NegativeValueHasOneAdded`.
Published/final variants differ only in collector dependencies and their isolated configuration/cache.
They retain the same MTP producer snapshot. No builds ran concurrently with these five interleaved samples.

| Same 2,052-identity fixture | SDK slots | Maximum instrumentation children | Seconds | Assembly startups |
| --- | ---: | ---: | ---: | ---: |
| Published collector, serial modules | 1 | 2 | 85.787 | 68 |
| Published collector, overlapping modules | 2 | 4 | 41.237 | 68 |
| Published collector, warm overlap repeat | 2 | 4 | 40.999 | 68 |
| Final collector, overlapping modules | 2 | 4 | 32.759 | 22 |
| Final collector, overlap repeat | 2 | 4 | 24.496 | 22 |

Every sample exits 0 with two complete, nonduplicated 1,026-UID inventories and four expected sources
per module. Exact UID/source inventories agree between variants; every baseline source-to-UID association
is present in the final maps. Additional conservative associations are permitted, never missing ones.
Both final samples report 11 measured batches per framework and zero timing fallback. The range,
rather than just the fastest sample, is reported: combined improvement is approximately 2.6–3.5x here.
The smaller 196-identity fixture did not expose larger-batch benefits: final serial/parallel samples
were 22.941/11.171 seconds versus published 18.788/11.910. Do not infer a universal batching speedup.
These synthetic startup costs do not measure the cache benefit or predict the 70,695-UID CI workload.

The separate coverage-owned, unchanged 1,024-UID fixture builds a real startup project in each child.
Published collection took 82.495 seconds (warm repeat 81.870), 33 startups. Committed final packages took
31.412 seconds; with the exact MTP snapshot above, 30.661 seconds, 11 startups and zero fallback.
Those results are not interchangeable with the two-framework TestFx fixture.

Real final CTS also collected/executed/passed all 2,052 TestFx fixture identities, with two committed
native maps, native/wrapper exit 0, valid non-minimum UTC timestamps and complete module reports.
Its two-worker run took 35.439 seconds; that has **two**, not four, instrumentation workers.
The matched four-worker CTS run took 26.112 seconds, with the same two complete 1,026-case reports/maps,
consistent with the final affected collector's 24.496–32.759-second range at a four-child ceiling.
Independent `--list-tests json` plans contain 1,026 unique cases per framework. Both actual CTS TRX files
pass the unmodified canonical execution verifier against those plans and pre-execution discovery timestamps.
This closes the earlier legacy-TRX timestamp gate for the **final local CTS**, not the old pipeline pin.

The collection/cache composition invokes the actual built `AcceptanceFixture`, `DotnetCli.RunAsync`
and `TestHost.ExecuteAsync`, not replacement helpers. Eight real generated restores verify current MTP
package bytes and effective package folders. Four ordinary fixture children keep different fresh caches,
which assembly cleanup removes. Four marked children reuse their module's immutable package without
rewriting it; different modules retain different caches. Nested TestHost execution sees no collection-cache
opt-in, even with an invalid opt-in deliberately inherited by the coordinator. Parent cache removal occurs
only after both owning SDK processes return and descendant/output teardown is confirmed.
This establishes isolation and provenance, not a measured acceptance-suite cache speedup.

Assembly scheduling remains unchanged: acceptance tests still use method-level CPU-count parallelism.
The assembly fixture runs before test workers. Package payloads alone are shared through NuGet's own
package locking; mutable assets/builds and module caches remain separate. No additional test locks,
serialization, framework exclusions, disabled assertions or successful zero-test fallbacks were introduced.

#### Test-value evidence and reproducible gates

| Requirement / retained test | Without the relevant behavior | Final implementation |
| --- | --- | --- |
| UTC presence/culture/state / both `Serialize_TestNodeTiming_PreservesUtcPresenceDurationAndState` fixtures | Same assertions execute 169/125 cases on .NET 10/.NET Framework; 38/20 fail for missing UTC fields, exits 2. | 169/125 pass, exits 0; 33/17 source-only serialization cases and strict C# 12 packed consumers pass. |
| Safe cache reuse / `CollectionSharesOnlyExplicitMarkedRunCache` | An isolated, buildable mutation restores fresh-cache selection only for a valid marked path. All six cases execute; this equality assertion fails, exit 2. | The same six cases pass, exit 0; normal/invalid/unmarked controls remain intact. |
| Refill/budget / `ModulePoolRefillsWithoutWaveBarrier`, `ModulePoolNeverMultipliesChildBudget` | Rendezvous requires a new module to start before the held peer completes; budget cases cover CPUs 1/2/3/4/16, explicit one-child mode and rejected invalid/oversubscribed values. | All eleven pool cases pass. This is real child-process/configuration proof, not a full-suite performance claim. |
| Native failure despite logging faults / `ModulePoolPreservesNativeFailureDuringOutputError` | Removing the post-fault native-exit inspection replaces exit 37 with an output exception; the unchanged assertion detects it, exit 1. | Exit 37 remains authoritative and both additional output/cleanup errors are observable; metadata/cache-cleanup errors likewise cannot neutralize an existing native failure. |
| Cancellation/error ownership / `ModulePoolDrainsFailedPeers`, `ModulePoolDrainsEveryPeerAfterCleanupError` | A retained-test mutation aborting cleanup after its first error fails the named all-peer assertion, exit 1; the probe reaps its remaining owned child. | Native exit 37 stays authoritative; peers and a real grandchild terminate, all peers are attempted after an error, primary errors remain visible and unconfirmed caches are preserved. |
| CTS isolation/results / `CTSComparisonUsesIndependentJob`, `CTSComparisonSharedResultDirectoryIsRejected` | Missing jobs, rollout bypasses, shared results, ignored failures and wrong dispatch are rejected by executable guards. | 19 Windows + 39 Unix + 13 CTS wiring cases pass, along with nine selector/artifact cases and the package-staging guard. |
| Shipping composition / `StartupHeavyCollectionPreservesEveryCase` | Exact published/final fixture inventories are compared; setup/build failures do not count as behavioral red. | Both complete maps retain all baseline source associations; actual CTS independently executes both 1,026-case plans with no failures/skips. |

Repository-pinned SDK `12.0.100-alpha.1.26509.101` was used through `eng\common\dotnet.ps1`.
The current all-TFM unit-project build and actual Release acceptance-fixture build pass with zero warnings/errors.
C# BOMs, changed YAML parsing, PowerShell parsing/analyzer settings and whitespace gates pass.
The cache-comparison retained test SHA-256 is
`F75D045CEB750C261D99E94AF50EDEFEDB87DBB9FE53E0C974975F16C3A185B3`;
its behavior-removal helper is `AA3875D2772B9B373680B2306B4D5187FCB79FBE34CBA480CDC9977C3EF96EA5`.
The unchanged pool validator SHA-256 is
`8AA0DF8BA19AAE71EBD10D1C84F1E81915071CCF2B4C79090887D1B71C1C1179`;
its all-peer behavior-removal pool is `891020E309D5D4C9D38C8E9EBB4F14D470E3E350921B7DC24644F209454BB646`,
and its native-output behavior-removal pool is
`E4A8E6609F3DAAC69550213CD4905FC034DB3A3610588716E39EA7E2EDC34696`.
The final default-cache collection entry point also passes with both complete 1,026-UID maps,
zero timing fallback, confirmed teardown and no remaining owned module cache. That functional
check ran beside the fault probes and is not added to the controlled performance table.

```powershell
pwsh -NoProfile -File .\eng\validate-collection-module-pool.ps1
pwsh -NoProfile -File .\eng\collect-cts-comparison.ps1 -SelfTest
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests true -EnableNonWindowsAffectedTests false
.\eng\common\dotnet.ps1 build test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Debug -v:q "-bl:<owned-evidence>\unit-build.binlog"
.\eng\common\dotnet.ps1 build test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release -v:q "-bl:<owned-evidence>\acceptance-build.binlog"
pwsh -NoProfile -File .\eng\collect-affected-test-maps.ps1 -Repository <clean-packed-fixture> -Configuration Release -ChildBudget 4 -MaximumParallelModules 2 -ResultsDirectory <short-owned-results> -DisablePackageCacheReuse
pwsh -NoProfile -File .\eng\collect-cts-comparison.ps1 -Repository <clean-packed-fixture> -Configuration Release -DegreeOfParallelism 2 -ResultsDirectory <short-owned-results>
git --no-pager diff --check
```

All commands above completed with exit 0 for the corresponding final checks/samples.
The owned `Measure-LargeCollection.ps1` retains exact per-sample parameters, logs, package/consumer
provenance, complete inventories and source associations; `Verify-CacheFixture.ps1` retains cache/restore/
nested-host evidence. Red/green reports, native maps and full package metadata remain in session artifacts,
not source control. Text transcripts and these assertions communicate the CLI change better than screenshots.
Initial scratch failures from missing feeds/native layout, duplicate generated inputs and an artifact-root
lookup were repaired in owned layouts; none is counted as regression detection.

#### Committed-source package handoff

The implementation commit is `24644925e47e5c8d8f55bb8c71e37a8b9d1b9a80`.
Both MTP projects were repacked from that clean source using `VersionSuffix=preview.local.acceleration.20261101.1`,
with `SourceRevisionId` and `RepositoryCommit` set to the exact commit. Both resulting nuspecs were read back.
This unique local version avoids confusing the corrected package with the existing published preview.

| Final local package | SHA-256 |
| --- | --- |
| `Microsoft.Testing.Platform.2.6.0-preview.local.acceleration.20261101.1.nupkg` | `BD4DFB3AFF38D16A6DD8951C7102182C1B0215682913B7123C9483A598D14C16` |
| `Microsoft.Testing.Platform.ServerMode.Client.Sources.2.6.0-preview.local.acceleration.20261101.1.nupkg` | `220AEF9493A6A8A476B6F1CB8BFAE605807618E226B174A775C6AA8D57306158` |

The unchanged test sources were rebuilt from clean fixture commit `a3d06d3c82e43f3409555461e10501c1f2a3e446`
against these packages and the committed coverage packages, in a fresh dedicated cache.
`project.assets.json`, cached nupkg bytes and both executed DLLs match the selected feed/payloads:
.NET 10 DLL `AAE770725CBA12123608879C33F3162CCAC137F279A4C988A3ACFB0CC1A2E534`;
.NET Framework/netstandard2.0 DLL `DB81A2E23C28C0F2E3BB7AB5ABC707C7A227BDC9804ED66F0366B69369625FA5`.
The final default affected entry point took 27.060 seconds, returned 0, retained both exact 1,026-UID maps
and every baseline association, and reported 11 measured/zero fallback batches per framework.
Native CTS at the same four-child ceiling took 23.387 seconds, executed/passed all 2,052 planned identities,
returned 0, and passed both canonical TRX gates with UTC timestamps. These sequential final-composition checks
confirm the committed packages; the earlier interleaved samples remain the controlled performance comparison.
Exact final package metadata, loaded paths, commands and outcomes are retained in `final-package-evidence.json`.

#### Full real-solution collection validation

An isolated checkout of the committed implementation was built and packed with the final coverage packages,
the pinned SDK/runtimes, `FastAcceptanceTest=true` (as in the Windows pipeline), local storage and the short
local package suffix `preview.full1`. The validation-only configuration commit is
`f3a630003e981ec94d3759c714074cf18535d015`. The complete Windows solution builds/packs with
zero warnings/errors and produces the same 74 module/framework inputs as the original pipeline inventory.
No main checkout, global credentials, product pins or publishing configuration was changed.

The corrected real collection completes **74/74 modules in 2,366.615 seconds (39.444 minutes)**,
parent exit 0, with **828 measured batches, zero timing fallback and confirmed teardown**.
All module caches are removed after their owners finish. Independent JSON discovery agrees exactly with
all **70,935 unique module/UID identities** in the committed shards, and every UID has a source association.
This includes all .NET/.NET Framework targets, internal TestContainer hosts and the playground modules.
MTP acceptance takes 26.681 minutes and MSTest acceptance 23.546 minutes, overlapping in the bounded pool.
The original 224.42-minute CI run is not a controlled machine/revision comparison, so no full-suite
speedup factor is inferred from these different environments.

A preliminary run is **not performance evidence**: superseded packages from an earlier, longer local suffix
made acceptance-fixture version resolution ambiguous and caused class initialization failures.
The 32 specifically identified, task-owned superseded nupkgs were moved out of the active feeds; a real
package-consuming help fixture then passes both .NET 11/.NET Framework cases before the full rerun.
Discovery was refreshed for both acceptance modules after that repair. The NUnit playground intentionally
declares `[Values("one", "one")]`; its two byte-equivalent discovery records represent one UID.
The inventory verifier records that multiplicity, rejects conflicting records and still forbids duplicated
or missing UIDs in maps. None of these harness/setup failures is counted as behavioral red proof.

The earlier oversized inherited PATH prevented CMD from resolving PowerShell. A process-only bounded PATH
restored the normal Windows launcher; the missing declared runtimes were installed only into the task's
local SDK layout. Correcting the isolated clone's origin restored package URL provenance. The short suffix
keeps NuGet's long-path guard enabled rather than suppressing NU5123 or dropping source/native entries.
The final linear-time inventory checker and complete build/collection logs are retained with
`corrected-full-summary.json` and `verified-inventory.json` in the validation artifacts.
Collection still accepts failed-test batch exit 2 by contract; its successful map outcome is not a claim
that every acceptance test passed or a substitute for ordinary authoritative CI validation.

**Remaining delivery gates:** collection/selection on a capable live main CI agent, public package flow
and Azure multi-agent publication, release-branch
proof/servicing decisions, ARM64/native rebuild and non-Windows support remain unverified.
The full rebuilt 74-module local runtime/discovery/map inventory is now verified, not just statically predicted.
Public dependency pins are unchanged. `EnableCTSCollection` remains off until the corrected CTS tool is
approved/published and its pipeline pin updated. The coordinator retains dependency-flow and rollout
ownership; the local performance results do not authorize downstream activation or private publication.

#### Current-main merge validation (2026-11-03)

The original validated head `2a2240bfeaaaa044b0a41cd5ac4b49e86b341bce` is merged with
main `165333f32123111b70b3ead7f53e40e431c02055` without rewriting the existing evidence commits.
The sole conflict was adjacent, independent source links in the MTP unit project:
retain main's `DiagnosticAssertionObserver` and the collection-cache helper's existing Core-only condition.
The automatic merges also retain main's additional JSON tests and environment-name correction.
No collection, timing, cache or test-assertion behavior is changed to resolve the conflict.

Merged-source Debug validation uses the repository-local pinned SDK and the same timing/cache selection:

```powershell
$project = 'test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj'
$filter = 'FullyQualifiedName~FormatterUtilitiesTests|FullyQualifiedName~JsonTests|FullyQualifiedName~CollectionPackageCacheTests'
# $run is a separate task-owned result directory for each target framework.
& .\eng\common\dotnet.ps1 test --project $project --framework $tfm -c Debug --filter $filter -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$run" "-bl:$run\build-test.binlog" --show-test-results failed
```

Both commands exit 0: **179 passed on net10.0 and 125 passed on net462, zero failed/skipped**.
TRX definitions/results confirm all 48 modern/legacy UTC cases on Core, all 24 legacy UTC cases on
Framework and the marked-cache regression actually execute. The existing script gates also pass:
11 module-pool cases, nine CTS selector/artifact cases, 19 Windows + 39 Unix + 13 CTS wiring cases
and the package-staging guard. The final diff passes whitespace and required C# BOM checks.
An initial local invocation duplicated the repository-provided results-directory option and was
rejected before tests executed; that setup failure is not regression proof.

**Test-value evidence:** this is a behavior-preserving merge, not a new feature or a new red/green claim.
Retained UTC/cache assertions pass with current main; their earlier counterfactual evidence remains
bound to its recorded source states. Full packing, the 74-module collection benchmark and CI were
not repeated for this merge. Historical performance/package evidence is not relabeled as this new head.

#### Descendant-ownership review correction (2026-11-03)

Review exposed a gap in the teardown contract: normal SDK completion did not inspect descendants,
and cancellation inspected a process-tree snapshot only while the SDK remained alive.
Retained rendezvous tests reproduce the defect on head `531ac4ca16620bd9b779bd4886f219d4df3cb752`:
an independently launched grandchild remains after both its intermediate parent and the SDK exit,
yet the pool reports teardown confirmed. Both exit-0 and exit-37 ownership assertions fail;
the original eleven pool cases pass and all fixture workers are joined during failure cleanup.
An initial redirected-stream fixture instead inherited an extra pipe and hit output-drain timeout;
that setup failure is not counted as the intended red proof.

A later PID snapshot cannot reconstruct an intermediate parent that disappeared before observation.
The chosen correction is a per-module Windows job assigned atomically by the process-creation
`PROC_THREAD_ATTRIBUTE_JOB_LIST`, not a polling sampler or a new supervisor process.
Windows 10/Server 2016 and later support this attribute, matching the Windows SDK collection hosts;
see [UpdateProcThreadAttribute](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute).
Job membership includes descendants for the entire module lifetime. Teardown retains current job-member
handles, waits for their actual exit signals and observes zero active job processes before releasing
cache ownership. It verifies job membership before joining a PID and rejects new membership during
termination rather than claiming unobserved cleanup. A handle whitelist isolates redirected pipes
between concurrent modules. Job closure is a kill-on-close safety net, not termination proof;
query/wait failures remain explicit and retain the cache. Non-Windows collection stays disabled.

**Test-value evidence:** the same final test file
`6E14E8DEC71B6A86B6918A31240F347AB3CB8F49A50E08ABC633D7290F8C1021` runs against the archived
pre-fix pool and the corrected pool with
`pwsh -NoProfile -File .\eng\validate-collection-module-pool.ps1`.
All thirteen cases execute: pre-fix has eleven passes and the two named ownership assertion failures
(exit 1); corrected source has thirteen passes (exit 0). `ModulePoolDrainsOrphansAfterSdkCompletion`
and `ModulePoolDrainsOrphansAfterSdkFailure` confirm native exits 0/37, independently redirected
orphan termination before return, Unicode cache paths and argument quoting. Existing failure,
refill, grandchild cancellation and all-peer cleanup assertions remain unchanged.
The final CTS selector/artifact gates (nine cases), rollout wiring (71 cases) and package-staging guard
also pass. Logs and archived comparison sources are retained under `review-descendant-teardown`.

Real composed validation uses the existing packed fixture at
`a3d06d3c82e43f3409555461e10501c1f2a3e446`, its byte-verified MTP 2.6.0 local binary payloads and
coverage producer `8e1bd20fd881a2ca9d13e8081e39478fa1107598`; no product source/package changed
for this launcher correction. The current entry script collects both net10.0/net462 modules with two
SDK slots and four instrumentation-child capacity: exit 0, teardown confirmed, zero retained caches,
1,026 exact unique UIDs and four source paths per module, with every historical association retained.
This is mapping/ownership evidence, not 2,052 ordinary test-result passes or a new speedup claim.
Historical mapping storage is restored byte-for-byte after preserving the new maps.
A copied-PDB layout initially lost source associations, and a temporary tracked storage override
was rejected by the clean-repository guard; neither is counted as successful source-map validation.
The successful rerun keeps the original source/PDB layout and unmodified clean configuration.

#### CTS native-map inventory review correction (2026-11-03)

Marker counts alone did not bind each native map to a selected module. Two maps for module A
could hide a missing B while the separate reports still identified A and B. Validation now reconciles
the native `K1/<module-relative-path>/testfx-cts-comparison/<run>/committed` identity one-to-one
with the selected modules, rejects duplicate/unexpected identities and rechecks the input SHA-256
fingerprints after collection. Native bitmaps do not contain a separate SHA-256 field; the recheck
validates the original input snapshot rather than inventing native hash metadata.

**Test-value evidence:** retained `Invoke-CTSArtifactSelfTest` body
`41B1338C9E24B3F616A3D7F112671BF7666BE56129279A6340AC06F2CAD31CAA` executes with
`pwsh -NoProfile -File .\eng\collect-cts-comparison.ps1 -SelfTest`.
Before the fix, ten cases pass and three assertions fail (exit 1):
`CTSDuplicateMapCannotReplaceMissingModule`, `CTSUnexpectedMapCannotReplaceMissingModule`
and `CTSChangedInputHashIsRejected`. The unchanged tests then pass all thirteen cases (exit 0),
including a valid two-module inventory; all 71 rollout guards and package-staging validation also pass.
The corrected validator also accepts the retained real native CTS run
`5e0bff7df9514cb3bc571c090ae95b59`: two exact native map identities, both recorded input hashes
still matching and 1,026 executed report identities per module. Earlier two-test native archives
no longer match the rebuilt input binaries and are not relabeled as this evidence.
No native producer, package pin or disabled CTS activation gate changes.

#### Grouped review-maintenance validation (2026-11-03)

The requested nine-comment batch keeps the collection contract unchanged while compiling the native
helper and linked sources with explicit C# 12, naming the Win32 flags without changing their values,
and naming the PowerShell cache protocol values. Per-line disk flushing is removed; bounded
`StreamWriter` buffering and the existing completion/error disposal preserve captured logs.
`Write-Host` still streams live output. No wall-time speedup is inferred.

Identical strengthened pool tests pass before and after (14/14, exit 0), including exact completion,
stderr, native-failure, output-exception and cancellation logs. The combined changes pass the same
14-case command and all thirteen CTS cases; native interop probes also preserve exits 0/37,
Unicode I/O/environment, cancellation, launch errors and struct layouts. An actual synchronous
`CreatePipe` handle rejects `FileStream(..., isAsync: true)`, so overlapped-pipe redesign is declined.
Four isolated metadata-precedence probes confirm the existing entry script retains original
exceptions and native nonzero exits, rejects metadata failure after an exit-0 result, and writes
successful metadata only under the owned result path. No fallback location or metadata change is needed.
The review evidence is green/green preservation, not a new bug-fix red/green claim.
Combined real collection also completes both packed net10.0/net462 modules with all 2,052 exact UIDs
and historical source associations retained, exit 0 and confirmed teardown; historical maps are restored.

### Local acceptance profiling experiment (2026-11-03)

The follow-up request is to execute genuine acceptance tests locally and use OTel or equivalent tracing
to distinguish fixture, batching and scheduling costs. This authorizes a local profiling experiment,
not deployment, telemetry export to a remote service or an unmeasured production scheduling change.

Use the existing aligned validation checkout, its packed products and pinned SDK/runtimes.
The acceptance applications already register the MTP OpenTelemetry extension, but export nothing.
In the task-owned checkout only, attach a per-process JSON-lines exporter to that provider and add
`TestFx.AcceptanceProfiling` activities around fixture initialization/disposal, reflection/source-generated
asset builds, DotnetCli admission/execution and CommandLine admission/execution. Keep assembly
method-level CPU-count parallelization, package freshness and mutable-asset isolation unchanged.
Trace files belong to one run/process; output capture and remote exporters are disabled. The profiling
environment opt-in must not flow into nested generated test applications.

Compare ordinary acceptance execution with smaller real-test UID replays, then compare UID-oriented
and fixture-local batch partitions containing exactly the same tests. The replays are scheduling-cost
experiments, not a claim of executing the SDK's affected-map protocol. Require actual test outcomes,
source/package provenance and complete span/UID reconciliation. Measure an unexported/exported
control so diagnostic overhead is not mistaken for a scheduling gain. Quantify initialization count
per fixture, build time by metadata mode, nested process counts/overlap and queue waits before choosing
an optimization. These experiments are planned; measured results will be recorded after execution.

#### Profiling results

The local profiling checkout is committed at `5d439192c84c8dcd4bf5078bf606f71871314e42`;
the production worktree receives this decision/evidence record only. Both instrumented acceptance
applications build with zero warnings/errors. The measurement scope comprises 59 representative
fixture cases across MTP/MSTest acceptance plus two exporter-control cases, not the entire suite.
Across exporter controls, ordinary runs, repeated batch trials and compiler controls:
**42 valid runs, 319 passed executions, zero failures/skips and 3,423 exported spans** are verified.
Every passing TRX is checked against independent discovery and its recorded pre-execution timestamp.
For exported runs, the exact module/test UID multiset also agrees with the MTP test spans.

The genuine ordinary runs execute 31 MTP cases in 25.202 seconds and 28 MSTest cases in 24.843 seconds.
The exported two-test control shows 8.957–10.173 seconds in fixture initialization alone:
5.246–6.578 seconds reflection build plus 3.568–3.686 seconds source-generation build.
The test bodies are only about 0.4–0.6 seconds each. Per-test spans alone would miss most of that cost.
Unexported controls take 16.162–17.369 seconds end-to-end versus 15.156–16.033 exported;
this small sample shows no obvious material export penalty, not a statistically established overhead bound.

The batch comparison preserves the same exact UID set, four batches, maximum eight cases per batch,
two concurrent processes, metadata modes and one warmed, immutable per-suite package cache.
UID-oriented partitioning is compared with grouping by test class/fixture. These are native test UID
replays, not the SDK's affected-map protocol or a modification of the shipping collector.

| Scope | UID-oriented fixture starts / builds | Fixture-local starts / builds | UID-oriented wall seconds, repeats | Fixture-local wall seconds, repeats |
| --- | --- | --- | --- | --- |
| MTP, 31 cases / four classes | 12 / 24 | 4 / 8 | 50.123, 47.416 | 44.497, 98.769 |
| MSTest, 28 cases / four classes | 10 / 23 | 3 / 7 | 123.683, 70.861 | 201.941, 38.092 |

The work reduction is repeatable; the end-to-end speedup is **not**. Source-generation/compilation
latency varies substantially. An outlier build succeeds in 61.6 seconds, with a 55.744-second `Csc`
task and only 1.379 seconds in `RestoreTask`; another build's largest `Csc` task is 32.292 seconds.
These are actual recorded tasks, not an assumption that all setup time is NuGet restore or source generators.
Analyzer timing was not emitted, so no analyzer blame or precise compiler-server contention cause is claimed.
The outlier used shared compilation. Controlled private-compiler runs verify `UseSharedCompilation=false`
in the binlog but take 18.900/27.401 seconds versus 12.918/22.209 with sharing; disabling it is not selected.

DotnetCli/CommandLine semaphore waits stay below 0.1 milliseconds in the selected scopes. There is no
evidence here to raise those queue limits. Nested compiler work and unequal fixture cost still require
cost-aware balancing; grouping every whole class blindly can create stragglers and was slower in some trials.
One selected Runner class performs per-test project builds without a reusable `TestAssetFixtureBase`
initialization, so class affinity alone does not remove its build work.

**Decision:** the next bounded experiment should combine fixture affinity with measured setup/execution
cost and refill-on-completion, rather than impose unconditional whole-class grouping. Keep all required
reflection/source-generation/AOT checks, normal test semantics and private mutable assets. Reuse only
proven immutable preparation; treat persisted cost data as a scheduling hint with compatibility/invalidation
rules, never as selection authority. No production scheduling or fixture-cache policy was changed.

Exact script commands, UID partitions, environment overrides, TRX execution proofs, raw per-process JSONL,
`analysis.json`, CSV summaries and an offline interactive `report.html` are retained in the profiling artifacts.
The viewer uses no remote scripts/exporters and shows overlapping fixture/build/test/queue spans without
adding inclusive durations as wall time. Failed initial format, empty-environment and SDK-discovery harness
attempts are excluded from the passing totals and performance measurements.

### Windows timeout resolution (2026-10-26)

[Run 20261025.5](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1648684), at
`3c6614c5ae19ee4ddca55cbf9b3375bbaad3496b`, canceled collection at 08:06 UTC, four hours after its job started.
The collector was still producing maps, not hung: its last completed analyzer shard was uploaded at 08:04 UTC.
[Run 1648260](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1648260) hit the same four-hour limit.
The previous `iif` timeout expression depended on the `_CollectAffectedTests` matrix variable, but the service
resolved it to the ordinary 240-minute budget. The old configuration assertions only recognized the intended
expression; they did not establish its effective runtime value.

Collection now has its own compile-time-gated job with a literal 360-minute timeout. Release and Debug remain
in the existing Windows matrix with literal 240-minute main and 120-minute PR budgets. Both jobs reuse
`eng/pipelines/steps/build-test-windows.yml`, preserving prerequisites, build/pack commands, test selection,
failure propagation, and distinct diagnostic artifacts. The main, `SkipTests`, and `EnableAffectedTests` gates
are unchanged. This avoids another dependency on matrix expansion timing without extending ordinary validation,
dropping tests/frameworks, changing collector concurrency, or ignoring collection failures.

Validation must retain the real pipeline's former runtime-dependent timeout as a rejected configuration, not
only mutate numeric limits. A live main-branch run is still required to demonstrate completion of the full
collection within six hours; local configuration assertions are not that evidence. This repair changes only
the unofficial pipeline and its validation, not SDK/collector versions or release-branch product behavior.

The local repair is based on `92cbd59de751ca7ce5a44974073c30badeee93f5`. Its tracked executable patch
(excluding this document) has SHA-256 `9A1E867AC253E08D5058634C4C63227FBB705F35A3907033BB9B34C382A4D0F8`;
the new shared steps file has SHA-256 `62FD3794519E044E75E8EDE96CEBB3D12C9AB02D76B623BE83347CF85237E92A`.
The following commands both exit 0 with 19 Windows isolation cases and 36 non-Windows rollout cases:

```powershell
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests true -EnableNonWindowsAffectedTests false
pwsh -NoProfile -File .\eng\validate-affected-tests.ps1 -SelfTest -EnableAffectedTests false -EnableNonWindowsAffectedTests true
```

**Test-value evidence:** `MatrixDependentCollectionTimeoutIsRejected` restores the former runtime-dependent
expression and requires the `CollectionHasIndependentTimeBudget` assertion. Applying that same counterfactual
to an otherwise valid pipeline fixture exits 1 at the intended assertion; the correct configuration exits 0.
That fixture has SHA-256 `7578D89CD67EA20348F603FA75846B213398DDB3B2940F768D6F31CC18720A2C`.
The extracted Windows steps also compare exactly with the base job after indentation normalization.
This establishes configuration sensitivity and preservation, not a live Azure DevOps run.

The collection budget follows [run 1639792](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1639792):
its sequential collector spent about 70 minutes on unit-test maps and another 88 minutes on MTP acceptance maps
before starting MSTest acceptance. The four-hour job limit canceled that still-running collection, including its
artifact publication. The extra budget applies only to collection; it does not drop frameworks or acceptance
suites, change collector concurrency, suppress failures, or extend ordinary validation. Completing a full
collection within the new limit still requires a main-branch CI run.

`DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` is an SDK-to-extension authorization marker. Repository scripts and pipeline
definitions must not set it.

## Selection reporting

An unaffected module is **not selected**, not skipped: skipped is already a test-result state, and the selector
does not necessarily know how many tests an unlaunched module contains. Successful empty affected-test selections
need a module-level entry explaining that no affected tests were selected, without inventing passed or skipped
test results. The SDK owns this entry because the selection orchestrator can finish without launching a test host.
Collection, discovery, missing required handshakes, partial handshakes, cancellation and nonzero exits must retain
their existing contracts; merely enabling affected-test mode is not evidence that an empty run was expected.

The in-process MTP summary separately needs to honor an explicit zero-test opt-out. When exit code 8 is ignored
through `--ignore-exit-code` or `TESTINGPLATFORM_EXITCODE_IGNORE`, retain the honest `Zero tests ran` wording and
zero counters, but render that verdict neutrally rather than in red or as a green pass. The existing shared
exit-code policy, including environment-variable precedence, defines this opt-out. It must not hide failed tests,
minimum-expected-test violations, cancellation, process failures or handshake failures.

Treating every zero-test run as successful, reclassifying unselected tests as skipped, and parsing captured child
output were rejected: they respectively conceal broken discovery/filtering, corrupt test counts, and make the
contract dependent on localized presentation. Regression and composed-product validation are recorded with the
implementation; SDK source-vendoring and the independently shipped affected-test extension remain separate
delivery boundaries.

### Reporting validation (2026-10-15)

The TestFx implementation is based on `4bb976b571a50c0811671a87044435d6e9198ed7`. Its source, test and generated
localization patch, excluding this record, has SHA-256
`BD7837621A526387BD7129FCAC9B2A9E3F41E7355619208CF3E03AB7B7D95CE4`. The reporter-only subpatch, before the two
hosting-test output corrections below, is retained as
`9CAFC7BCE890619AE6E9C51F261BFE89EFE15A5424A768C6819792CBFBAAE03F`. Validation used the repository SDK
`12.0.100-alpha.1.26509.101`, Debug, Windows x64, and the existing method-parallel schedule with 32 workers.
The 236-case existing reporting baseline passed before implementation; the final focused suite passes 265 cases.
The unchanged `TestApplicationResultTests` selection also passes all 59 exit-policy cases. The standalone net8.0
and platform-linked net9.0 reporter contract hosts each pass both tests.

| Contract / retained cases | Counterfactual | Executed result without the behavior | Correct implementation |
| --- | --- | --- | --- |
| Explicit zero-test opt-out / `ActualSummary_ZeroTests_HonorsIgnorePolicy`, `ActualSummary_StrictAllSkipped_HonorsZeroTestsOptOut` | Restore the base summary and initialization behavior, retaining the final assertions. | Expected neutral ANSI 39, observed red ANSI 31. | Same assertions pass; ordinary unexpected zero-test runs stay red. |
| Failure preservation / `ActualSummary_IgnoredZeroTests_DoesNotHideOtherFailures`, `ActualSummary_IgnoredZeroTests_DoesNotHideMinimumAfterSkipping` | Base minimum-count behavior, which incorrectly includes skipped outcomes. | The skipped/minimum cases report a green pass or the wrong zero-test verdict instead of the red explicit-minimum violation. | Failed tests, cancellation and unmet minima remain red; skipped tests do not satisfy an explicit minimum, matching `TestApplicationResult`'s existing exit policy. |
| Module status / `AssemblyRunNotSelected_ReportsStatusWithoutInventingResults`, `AssemblyRunNotSelected_UsesNeutralColor`, `AssemblyRunNotSelected_MixedRun_PreservesActualTestCountsAndVerdict` | Retain the new API but replace its body with a no-op. | Six assertion failures among nine executed notification cases; the three informational/cancellation suppression cases still pass. | All nine pass, including unchanged real-test counts and the passing/failing mixed-run verdicts. |
| Supplied-log noise / `RunTestingPlatformAsync_ForwardsMtpLogsToHostLoggerFactory`, `RunTestingPlatformAsync_AfterRunCompletes_DisconnectsHostLifetimeBridge` | Omit their existing per-application `SuppressOutput` helper. | The 24-case hosting suite passes but emits two unrelated nested `Zero tests ran` summaries; the captured-output guard rejects it with exit 1. | The same 24 cases pass without either nested summary on net8.0 and net472; the guard exits 0. |

The summary comparison executes exactly the same 15 cases in both states: eight intended assertion failures
before the fix (process exit 2), then 15 passes (exit 0), with no skips. The notification comparison similarly
executes the same nine cases (exit 2 versus exit 0). Both changed test files were verified byte-identical in the
comparison and delivery worktrees. The API-retaining comparison patch has SHA-256
`DB2497E538CE1DBB420930D3B5B7ABA9ABBEAE0B60BEA15F9AE178EF49542104`; its unrelated no-op notification does not
change the base summary behavior. Its only configuration adjustment gives the identical local `.globalconfig`
values higher precedence, avoiding duplicate analyzer settings inherited from the enclosing worktree.

The minimum/skip interaction was also reproduced against the initial implementation: both policy variants failed
the intended assertion before the count correction. Compilation failures, the initial nested-configuration
problem, and an initial stale-import `UpdateXlf` failure are not counted as behavioral red evidence.

The `hosting-failure` diagnostics in the supplied example come from deliberately empty applications created
inside passing hosting tests, not from the affected selector excluding that outer module. Two tests had omitted
the same output-suppression helper already used by their neighbours. Applying that per-application helper removes
the misleading nested summaries while retaining the exit-8, logging and host-lifetime assertions. It neither
redirects process-global console streams nor weakens the real product's zero-test failure policy. The two-line
test-only patch has SHA-256 `D48995DE8B747B3A773360C8A38AAC7ACA88261DDD5704F1FBD853D45B7C554D`.

The exact runner selections are:

```powershell
$hostPath = '.\artifacts\bin\Microsoft.Testing.Platform.UnitTests\Debug\net8.0\Microsoft.Testing.Platform.UnitTests.dll'
$reporting = 'FullyQualifiedName~TerminalOutputDeviceTests|FullyQualifiedName~TerminalTestReporterTests|FullyQualifiedName~TestRunSummaryHelperTests'
$summary = 'FullyQualifiedName~TerminalOutputDeviceTests.ActualSummary_'
$notification = 'FullyQualifiedName~TerminalTestReporterTests.AssemblyRunNotSelected_'
# Set $evidence to a task-owned artifact directory.
.\.dotnet\dotnet.exe $hostPath --filter $reporting --report-trx --report-trx-filename final-reporting.trx --results-directory "$evidence\final" --show-test-results failed --progress off --ansi off
.\.dotnet\dotnet.exe $hostPath --filter $summary --report-trx --report-trx-filename final-paired-green.trx --results-directory "$evidence\final" --show-test-results failed --progress off --ansi off
.\.dotnet\dotnet.exe $hostPath --filter $notification --report-trx --report-trx-filename final-not-selected-green.trx --results-directory "$evidence\final" --show-test-results failed --progress off --ansi off
```

All three final commands exit 0, with 265, 15 and nine passes respectively. Red logs/TRX, binary logs, patches and
package provenance are retained in the task-owned artifact directory.
All 364 localization catalogs pass the localization guard, and its 41 regression cases pass.

The direct hosting-log checks also exit 0 with 24 passed, zero failed and zero skipped cases per target:

```powershell
.\.dotnet\dotnet.exe .\artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net8.0\Microsoft.Testing.Extensions.UnitTests.dll --filter 'FullyQualifiedName~MicrosoftExtensionsHostingExtensionsTests' --report-trx --report-trx-filename hosting-after.trx --results-directory "$evidence\hosting-after" --show-test-results failed --progress off --ansi off
.\artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Debug\net472\Microsoft.Testing.Extensions.UnitTests.exe --filter 'FullyQualifiedName~MicrosoftExtensionsHostingExtensionsTests' --report-trx --report-trx-filename hosting-net472.trx --results-directory "$evidence\hosting-net472" --show-test-results failed --progress off --ansi off
```

Shipping validation uses freshly packed MTP with a small package-only consumer, not the source-linked reporter
alone. The final package is `Microsoft.Testing.Platform.2.6.0-validation.20261015.2.nupkg`, SHA-256
`C91039A0545C2BAE329B1C3B5F9A33E0D1E91257BDA335CF140813B8FEB0D8BF`, containing net8.0, net9.0 and netstandard2.0
assets. The validation-only version satisfies the affected provider's preview minimum without suppressing
NuGet downgrade checks; it does not change the repository's dependency versions or represent a published release.
The base package has SHA-256 `2779D235AAAA6BADE5B4EE77618D374856FDEA26D23E131A4612862289E13656`.

The same 24 process probes run against separate base/fixed consumers and caches: 12 paths each on net8.0 and
net472, including CLI/environment opt-out and precedence, unexpected zero, strict/default skips, explicit minimum
violations with zero/skipped results, failed tests, passing tests, and an ignored failed-test exit that must still
render red. All 24 pass on the fixed package (harness exit 0); ten fail their intended summary assertions on the
base package (harness exit 1). Each child exit and count block is asserted independently, and the actual loaded
MTP binary hashes match the selected package asset. The generated projects' effective feeds, package folders and
resolved assets were inspected; equal package versions or a global cache listing were not treated as provenance.
The retained `validate-packed-reporting.ps1` harness and consumer sources reproduce these checks. A readable
transcript communicates the actual ANSI color/count differences better than a screenshot.

### SDK shipping-layout validation

The coordinated SDK implementation was built with its pinned `12.0.100-alpha.1.26480.102` toolchain and exercised
in an isolated copy of the published SDK layout using the aligned MTP validation package above and
`Microsoft.Testing.Extensions.AffectedTests` `18.13.0-preview.26514.5`. The real collector produced two mapping
shards, each with two test UIDs and three source paths. The focused SDK tests pass all 163 cases, with no skips.
Its source base/head is `54bdffb71caf1f2b6aec2417e105f361f1137cbd`, with persistent local changes on
`dev/amauryleve/affected-selection-reporting`; the final SDK patch has SHA-256
`9A9E735C7350B6B709C5D6ECD3B5BE1D6001E1A6AD1059D63E66E6354C15AAD0`. Neither repository change is published
to GitHub by this work.

Checking the actual loaded CLI matters: the initial published-layout copy still contained the old Native AOT
library. Its valid empty selections returned 0 but failed the retained assertion requiring module status entries.
The newly built managed CLI passed that assertion under `DOTNET_CLI_ENABLEAOT=false`, but that did not prove
the default muxer path. The updated native component was therefore published and deployed too; its SHA-256 is
`8152FCD44A6DC8409B5D28D6F495E99690553322F0A14DCB6A429D8E76DB7DCE`. The default-native muxer now passes:

| Real default-native case | Parent exit | Observable result |
| --- | --- | --- |
| Two confirmed-empty modules | 0 | Two exact neutral ANSI-39 `No affected tests selected` entries; total, failed, succeeded and skipped all remain 0; every recorded child exit is 0. |
| Real committed source change | 0 | Affected module selects two tests; the other correctly falls back to its two tests because the changed source is unmapped there. Four tests execute; this is not claimed as a mixed-empty selection. |
| One fallback module plus one confirmed-empty module | 0 | Two tests pass and exactly one neutral module entry appears; no skipped tests are invented. Only the first module's real map metadata hash is deliberately made stale to force its documented conservative fallback. |
| Earlier failed module plus later confirmed-empty module | 2 | One failed and one passed test, plus one neutral module entry; the later successful no-work completion does not erase the earlier failure. |
| Explicit minimum after two empty selections | 9 | The entries remain neutral, but the zero-executed minimum violation and aggregate summary remain red. |
| Missing required handshake | 1 | A child returning 0 without its required handshake is still a parent failure; no not-selected entry is emitted. |
| Ordinary zero-test defect | 8 | Genuine zero-test failure remains red with no not-selected entry. |
| Cancellation | 3 | Aborted, with no not-selected entries; child/parent cancellation exits remain nonzero. |
| JSON discovery and help | 0 | Discovery parses as version 1.0 with two test nodes; neither informational mode is polluted with selection status or run-summary output. |

The exact empty-selection command is `dotnet test --test-modules "*.Tests.dll" --root-directory <modules>
--affected-tests --no-progress`, executed by the isolated SDK's real muxer, not by a handler mock. Each case
retains `result.json`, `stdout.txt` and `sdk-trace.txt` beneath the SDK worktree's
`artifacts\selection-validation\cases` directory, including parent/child returns and the actual command.
The earlier wrong cancellation expectation and the source-change scenario that legitimately fell back are
retained as harness/contract corrections, not relabelled as successful mixed-empty proof.
All 11 canonical native cases pass. The generated map fault was restored byte-for-byte, and all owned build
commands completed or were stopped. The source-redist build's unrelated missing Blazor tool assets were not
worked around or presented as a completed full-source redist build; the actual managed/native CLI dependency
closure was overlaid into the isolated published-SDK baseline instead. General agent/build guidance changes
were excluded from the SDK delivery.

The SDK owns the successful orchestrator-only completion call to `AssemblyRunNotSelected`; this method creates no
test-host attempt, worker, result or aggregate-success override. Updating TestFx alone cannot change the SDK's
embedded reporter. The coordinated SDK change must ship before the pinned SDK can display the new module entry.
No rollout gate, global SDK version, release branch or servicing branch is changed by this implementation, and
local validation is not a claim that existing CI logs have changed or that SDK rollout is complete.

The task-owned comparison worktree was removed after archiving its source patch, base package, logs and TRX.
The final validation package remains in an isolated evidence feed. Ordinary `Shipping` output was restored to
one `Microsoft.Testing.Platform.2.6.0-dev.nupkg`, whose three MTP binary assets were verified byte-for-byte against
the validated package; validation-version siblings were removed to avoid ambiguous acceptance-package discovery.

### Main-branch integration (2026-10-15)

Merged `origin/main` at `8718afe9b99b11e5b36be7dbc8d9e9ca959b3982` after Azure Pipelines reported
`Skipped due to merge conflicts`, not a build or test failure. The only conflict was additive entries in
`InternalAPI.Unshipped.txt`; both the reporting entries and main's `ShutdownProgressReporter` constructor entry
were retained. The incoming .NET 11 runtime configuration was restored through the pinned repository build.
The merged net8.0 unit project builds without warnings/errors, and the combined reporting, exit-policy and
shutdown-reporter selection executes 334 cases with zero failures/skips (exit 0). The earlier packed-product
and SDK composition results above remain bound to their recorded revisions; they are not presented as reruns
or CI proof for this integration commit.

### Session-failure review correction (2026-10-15)

Review identified a separate zero-count failure: a framework can fail session creation or closure without
publishing any test outcomes. Ignoring exit 8 must not neutralize that session's exit-10 failure. The terminal
device now supplies a deferred reader of `ITestApplicationProcessExitCode.HasTestAdapterTestSessionFailure`;
the reporter checks it at summary time, after session lifecycle callbacks, and displays a red `Failed!` verdict.
The reader is deferred because the result service is registered after the output device. Existing zero-test
opt-outs, cancellation, test counts and process exit policies are unchanged.

The correction is based on `451b1bfb34575469b56c66aa71c2c28b4b7ac84c`; its source/test patch excluding this record
has SHA-256 `B0193AC322654E59E21ECD8BDBEC09B1F5A3EEE72BF40CF10C8651829F5CF748`. The unchanged
`ActualSummary_IgnoredZeroTests_FrameworkSessionFailureRemainsRed` regression executes real create/close
callbacks through `TestHostTestFrameworkInvoker` and the actual default output-device factory. Its four cases
cover both lifecycle phases and CLI/environment exit-8 opt-outs. Before the fix all four reached exit 10 but
failed the intended red-summary assertion (outer exit 2); after the fix all four pass (outer exit 0). The retained
test patch is SHA-256 `844CBC186F4E115AA3CBF1C23ECE81242D8377F1EA86525AD515F679DBCBAF01`.

The net8.0 focused reporting/exit/shutdown selection passes all 338 cases with no skips, and the standalone
reporter contract passes both cases. The corrected package
`Microsoft.Testing.Platform.2.6.0-validation.20261015.3.nupkg`, SHA-256
`73260538E631AED3AE26311B0097C2A8771DAC2F73409C9E4D2AAB8F1EF0D21B`, builds all five current shipping targets.
The package-only consumer passes 32 process checks on net8.0/net472, including all eight real create/close
failure variants: exit 10, red ANSI 31, and honest zero test counters. The final harness exits 0.
An earlier harness leaked its last intentionally nonzero child exit, and a retry ran out of C: space while
writing a binlog; neither is counted as behavioral red proof. Moving only task-owned logs/scratch to Q: and
returning an explicit harness success after all assertions produced the clean final result without changing CI
settings or clearing shared caches.

## Collection budget isolation

[Run 20261013.16](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1633662&view=logs), from commit
`41b5f5343b23ab07df82f3339dc73a790ab5a066`, completed map collection in 3 hours 33 minutes. The normal full test run
then had less than 10 minutes before the shared four-hour Windows job timed out. Collection produced 45 shards,
but the required full test and coverage validation did not complete.

Collection therefore needs an independent job budget, not merely a separate step. The initial isolation used a
main-only `AffectedTests` entry in the existing Windows matrix. The timeout correction above moves collection
into a dedicated job, still reusing the same Release build, packing, Windows prerequisites, and pinned
toolchain on its own agent. The ordinary Release entry runs full tests and publishes coverage without waiting for
collection. Each entry owns its workspace and diagnostic artifacts; only the collection entry publishes test maps.
Collection failures remain blocking and visible, but cannot cancel validation by consuming its timeout.

The root `EnableAffectedTests` parameter controls both the collection job and PR selection. The collection
job is also compile-time gated by `SkipTests=false` and the main branch. Disabling affected tests retains the
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

### Review correction: downstream preflight enforcement

Review on 2026-10-14 identified that the initial support check failed `DetectChanges`, but Linux/macOS still used
`succeededOrFailed()` and treated absent classification outputs conservatively. Consequently, rejecting Unix opt-in
did not actually prevent those jobs from executing unsupported collection. The earlier configuration checks and
previews established the validator's rejection and matrix wiring, not the downstream runtime gate.

Require `DetectChanges.result == Succeeded` whenever both affected-test parameters are enabled. Keep
`succeededOrFailed()` and missing-output full-validation behavior when either rollout parameter is disabled. An
unconditional `succeeded()` condition would also block ordinary full validation on classification uncertainty; a
new output variable is unnecessary because the existing dependency result already expresses successful preflight.
Apply the same gate to main collection and non-main selection, and retain existing change-scope and macOS PR gates.

Collection suppresses TRX fallback as well as live publishing. The configure and finalize helpers must therefore
not promise TRX fallback when publishing is simply disabled; the consuming job continues to own fallback policy.

The correction was compared with review base `8783751cb9ecd7fffb484dccf11b6a02bf45d89f`. The four-file implementation
patch (pipeline, validator, configure and finalize helpers, excluding this record) has SHA-256
`8F4F106ABA72F5D0BBF3A24C6CD8BC110BAF16D8B38E83088398DACCEBABB0DB`. The original head passed 43 configuration
cases; it did not detect this downstream bypass. With the correction, this command passes 49 cases, exit 0:

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\validate-affected-tests.ps1 -SelfTest
```

The same explicit root/Unix parameter commands documented above retain exit 0 for `True`/`False` and
`False`/`True`, and the intended `NonWindowsCollectorSupportRequired` rejection, exit 1, for `True`/`True`.

| Contract / retained case | Actual pre-fix source in the isolated fixture | Intended red result | Corrected result |
| --- | --- | --- | --- |
| Successful downstream preflight / `LinuxFailedPreflightBypassIsRejected`, `MacOSFailedPreflightBypassIsRejected` | Restore only `azure-pipelines.yml` from the review base; retain the corrected validator and all templates. | Validator exits 1 at `NonWindowsOptInRequiresSuccessfulPreflight` after the 13 Windows cases pass. | Same validator command passes 49 cases, exit 0, before and after the comparison. |
| Accurate configure output / `CollectionSuppressesPublishing` script probe | Restore only the original configure helper; retain the publishing probe assertions. | Probe exits 1 at the assertion that collection must not promise TRX fallback, after one preceding publishing case passes. | Publishing-only probe passes all eight cases, exit 0. |
| Accurate finalize output / `CollectionSuppressesPublishing-Finalize` script probe | Restore only the original finalize helper. | Probe exits 1 at the disabled-publishing fallback-output assertion, after two preceding publishing cases pass. | Same eight publishing cases pass, exit 0. |

The session-owned comparison commands were:

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File <session-artifacts>\verify-review-regressions.ps1
pwsh -NoLogo -NoProfile -NonInteractive -File <session-artifacts>\verify-affected-pipeline.ps1
pwsh -NoLogo -NoProfile -NonInteractive -File <session-artifacts>\preview-affected-pipeline.ps1
```

All three commands completed with exit 0. The comparison runner verified unchanged validator/probe hashes across
the source states and retained the expected red assertions, not setup failures. The full script probe executed 28
passing cases: the unchanged 20 native-command-fixture cases, five configure cases, and three disabled-publishing
finalize cases. It checked both collection helpers' actual output rather than only matching their source text.

Seven `previewRun=true` Azure previews passed, with both rollout parameters disabled added to the previous six
scenarios and the modified finalize template also inlined. For each server-expanded Linux/macOS job condition,
ten local eligibility cases cover successful, failed, partial-success, skipped and canceled preflight; missing
classification outputs; product/XML scope; and PR/manual triggers. All 140 local evaluations passed, including
rejection of failed preflight under Unix opt-in and preservation of ordinary full validation under either rollback.
These are local evaluations of the server-expanded expressions, not evidence of actual Azure worker scheduling.
No builds were queued, Unix was not enabled, and real cross-OS collector execution remains unverified.

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

## Full-suite collection performance investigation (2026-11-13)

The local comparison uses TestFx source `5b55ad287b4635ee4555abef0fd719450f6b7cf9`
with only local map storage configured in isolated fixture commit
`f52fed97d79a7958049490af46922a328c9aa905`. Its Release build and pack use
`FastAcceptanceTest=true`, matching the Windows collection job. Independent JSON
discovery covers the same 48 module/TFM paths as
[build 1676866](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1676866),
including both acceptance suites, and identifies 57,070 unique module/UID pairs.
The historical build has 57,066 pairs at its different source revision; these
are not identical-revision machine measurements.

Both collectors use the same built modules and four instrumentation-child slots.
AffectedTests, its Azure DevOps storage provider, and test-host CodeCoverage are
`18.13.0-preview.26560.3`, stamped with
`13fcbb6be5af9c988589f5474d595818085a7441`. Standalone CTS is
`2.13.0-alpha.26530.1`, stamped with
`fe4721e980b711722351edfd491577a03b8d4421`. All 144 affected-test companion
DLLs in the 48 test-host layouts match their selected package payloads.

| Complete local collection command | Elapsed | Exit and coverage |
| --- | --- | --- |
| AffectedTests repository wrapper | 5,851.629 seconds | Exit 0; all 48 exact maps and all 57,070 unique module/UID pairs. |
| CTS repository wrapper | 2,354.798 seconds | Exit 1; all 48 maps and unique inventories, but 45 failed and 184 skipped results. |

These are one sequential run per collector, not repeated medians or clean passing
performance proof. CTS reports 56,844 passed results and records one NUnit UID
four times versus two identical discovery records. The verifier retains that
excess multiplicity as a failure rather than weakening the inventory check.
AffectedTests reports 47 failed-test batches while preserving their maps; its
successful collection exit is not an authoritative test verdict. An initial CTS
launch rejected the local SDK junction before executing tests; that failed
5.6-second attempt is retained separately and is not a timing sample. The retry
excludes only that SDK junction entry, not additional tests or source modules.

The acceptance suites dominate affected-test collection. MTP takes 4,104.055
seconds with 186 measured batches of 3-16 tests; MSTest takes 5,382.700 seconds
with 218 batches of 4-19 tests. Both have zero timing fallbacks, and their module
lifetimes overlap. CTS's corresponding module logs contain 124 and 127 batch
starts, including discovery, with a different process ID for each start. The
evidence does not support assuming persistent acceptance-test processes in CTS.
Its module summaries are 33m 38.594s and 34m 49.203s. The current question is
which controller, setup, instrumentation, or feedback differences cause the
extra affected-test work, not whether the 40-second unit-only sample represented
the full repository.

### Optimization contract and planned comparisons

TestFx owns the unchanged consumer inventory, packaging, timing/report producers,
and bounded collection wrapper. The coverage repository owns affected-test
collection, native instrumentation, feedback interpretation, and batch policy.
The coordinator retains responsibility for testing the composed shipping layout
and subsequent package flow; local packages do not authorize a public-feed
update, pipeline enablement, or a release-branch rollout.

Preserve every discovered module/UID identity, conservative source associations,
the explicit distinction between failed tests and infrastructure failure,
legacy/absent feedback behavior, cancellation and descendant teardown, and
atomic map publication. No test/TFM exclusions, retry-based failure masking,
unbounded worker increase, or shared mutable acceptance output is an acceptable
way to match CTS's time. A protocol or capability addition must retain an
explicit unsupported/absent path for older hosts.

| Requirement or risk | Evidence required before accepting an optimization |
| --- | --- |
| Causal performance improvement | Compare the exact stamped implementations and retained per-module/batch evidence; change one supported cause before rerunning matched workloads. |
| Correct scheduling and lifecycle | Focused tests for the changed controller or lifecycle, including cancellation, nonzero exits, missing/invalid feedback, and multi-module completion where affected. |
| Real package consumption | Aligned local packages with source/version/hash provenance; verify resolved and physically loaded payloads in the real SDK collection path. |
| No lost work or associations | Independent discovery, all 48 module/TFM inventories, exact UID sets, and a deterministic source-association oracle or baseline preservation check. |
| Acceptance-heavy performance | Reuse both complete acceptance modules and the full inventory gate; report failed/skipped work separately and distinguish one run from repeated measurements. |

The existing/no-change path retains the measured baseline. Simply raising worker
counts would confound resource use with collector efficiency, so it is not the
first experiment. Replacing the affected-test entry point with standalone CTS
would change map/storage and SDK contracts and is not the selected approach.
The first experiment below isolates ordering before considering other changes.

### First source-backed cause: discovery-order locality

The coverage source investigation identifies an avoidable ordering difference:
`RefreshChildProcess.ParseDiscoveryResponseAsync` ordinal-sorts opaque test UIDs
before affected-test batching, while CTS preserves the producer's discovery
order. The sort is present at the affected package's stamped source and current
coverage main. For these MSTest acceptance modules, producer discovery is
class-contiguous, but UID order is effectively unrelated to class identity.
The same retained discovery documents independently reproduce the effect:

| Acceptance module | Classes | Discovery-order class transitions | UID-sorted class transitions | Class/batch touches, discovery order / sorted, at a fixed 16 UIDs |
| --- | ---: | ---: | ---: | ---: |
| MTP | 106 | 105 | 889 | 157 / 763 |
| MSTest | 140 | 139 | 1,145 | 206 / 996 |

These are replayed grouping counts, not measured speedup. The amplification has
a concrete consumer: `AcceptanceTestBase<TFixture>.ClassInitialize` creates a
fixture for each derived class in every new batch process, and
`TestAssetFixtureBase.InitializeAsync` builds its reflection and source-generation
assets. Splitting one class across many batches repeats that setup without adding
test coverage. Retained top-level acceptance build binlogs created during the
exact full-run intervals number 3,290 for AffectedTests and 1,326 for CTS; retained
log counts do not attribute every second of the difference to compilation.

The first selected experiment preserves first-seen discovery order while
ordinal-deduplicating UIDs. It retains the existing batch controller, two-child
module cap, four-child overall budget, feedback validation, and map format.
This is a performance-oriented scheduling change, not a pure structural
refactor: batch coarsening can change conservative false-positive associations.
Validation must preserve every actual source/test association with independent
oracles, rather than require every accidental old batch union to stay identical.
Focused order/dedup regressions must fail against the original sort and pass
against the corrected parser before packaged acceptance measurements are credited.
Persistent host reuse, source-data pooling, and a different execution-time target
remain separate, untried alternatives, not bundled explanations or improvements.

The existing MTP producer already enumerates its input sequence directly in
`DiscoveredTestsJsonSerializer.Serialize`; it does not sort UIDs. Its unchanged
16-case `DiscoveredTestsJsonSerializerTests` selection passes on each frozen
`net10.0` and `net462` host (32 executed/passed, zero failed/skipped), with exact
independently planned TRX identities verified by the acceptance execution gate.
This includes
`Serialize_MixedMetadataAndMalformedUnicode_PreservesNodeAndPropertyOrdering`.
These are producer-contract preservation checks, not the new consumer fix's
red/green evidence or a performance measurement. No MTP schema or public API
change is needed for the selected experiment.

### Packaged implementation and correctness proof

The coverage implementation is based on
`12d8abc4d08edd23d1123e2060d459c0b1edacd9` plus production patch SHA-256
`090977814B07ED8E2D5610360216CBF87F4ED46230083D6020ED39B3FDFAC489`.
The complete source/test/decision-record patch has SHA-256
`570247503C9A85913983730608EB69B46FBAB8858A57237B3CD861C44BD0CB59`.
The old committed `src` closure matches the published affected package's stamped
source. Only the discovery parser's first-seen planning changes production logic;
the existing test is strengthened for unsorted, repeated, and case-distinct UIDs,
and its coupled real-child probe UID follows the renamed test.

The locally versioned aligned tuples are
`18.13.0-local.discoverybaseline.20261113.1` and
`18.13.0-local.discoveryfixed.20261113.1`. Their 50 native entries match each
other and the published `.26560.3` inputs; all 88 CodeCoverage library/runtime
payloads are identical. Dependency groups match modulo the exact local tuple
version and retain minimum MTP `3.0.0-preview.26559.7`. The local native-input
overlay is build evidence, not a native-source optimization or a Unix/ARM64 run.

**Test-value evidence:** the unchanged final
`ParseDiscoveryResponseAsync_ValidResponse_PreservesDiscoveryOrderAndOrdinalUniqueness`
assertion executes against the actual baseline package DLL and fails the order
comparison (one case, raw exit 2), then passes against the fixed package DLL
(one case, exit 0). Both use identical test-source bytes, SHA-256
`C9390B86FF4ED506CF5DFB66D465972D69BB81A068FA2BA3A7926E7B350C0015`,
and identical test-assembly bytes, SHA-256
`449ED7D96ADF10B373B02E72E712E82F96D61683F01F6CC94551C212A547A053`.
The complete relevant collector suite executes 381 passing cases with zero
failures/skips, exit 0. Both packaged parser TFMs (`net10.0` and
`netstandard2.0`) retain all 48 original inventories and all 57,070 unique
module/UID identities. Restore/launcher failures during setup are retained
separately and do not count as behavioral red evidence.

The coordinator independently read back package hashes, manifests, payload
comparisons, the expected red assertion, and the original 381-pass TRX. The
full TestFx baseline has been rebuilt and packed with its own restored local
tuple/cache, all 144 physical host payloads verified, and the actual acceptance
`project.assets.json` and cached nupkgs checked. Timing begins only after the
coverage build/test processes have finished. No local tuple is committed as a
public TestFx dependency.

### Complete packaged before/after result

Both local tuples are restored into separate task-owned caches and used for
full Release build/pack commands with the same TestFx source, SDK, runtime pins,
`OfficialBuildId=20261112.90`, and `FastAcceptanceTest=true`. The only selected
production change is discovery ordering. Each variant rebuilds the consumer
against its own package inputs; 40 of the 48 test-assembly hashes differ, so this
is a source-matched packaged comparison, not a byte-identical-binary comparison.
All module/TFM paths, independent UID inventories, and frozen consumer setup and
collection-helper hashes remain unchanged.

| Complete affected-test collection | Unmodified local tuple | Discovery-order fix |
| --- | ---: | ---: |
| Parent elapsed, seconds | 4,879.184 | 2,778.635 |
| Parent elapsed, minutes | 81.320 | 46.311 |
| Exact module maps / independently expected module/UID identities | 48 / 57,070 | 48 / 57,070 |
| Measured batches | 817 | 640 |
| Conservative timing-fallback batches | 0 | 2 |
| Failed-test batch diagnostics, retained | 47 | 17 |
| Independently required source/UID associations verified | 13 / 13 | 13 / 13 |

Both parent commands return 0 and confirm descendant teardown. The observed
local elapsed reduction is **43.05%**, or **1.756x**, saving 35m 00.549s in this
one sequential pair. No tests or TFMs are excluded. Both maps contain every
independently discovered UID exactly once, every UID has a source association,
and no source association names an unknown UID. The 13 additional true
source/UID oracles cover eight source-declared methods across both acceptance
suites; a deliberately removed required association fails that gate.

The fixed MTP acceptance module takes 25.468 minutes and 79 measured batches,
versus 56.377 minutes and 163 batches before the change. MSTest acceptance takes
34.810 minutes and 107 measured batches, versus 66.088 minutes and 197 batches.
Their module lifetimes overlap. Retained top-level acceptance asset build logs
fall from 3,272 to 1,285 in the exact timed intervals, corroborating less repeated
class setup without claiming those counts are build CPU time.

The two fixed timing fallbacks explicitly report `NoExecutedTests`, one in each
acceptance suite. The collector visibly retains legacy 4-32 bounds for that
batch. The suppressed/interleaved output does not establish every case's
individual outcome in those batches, so they are not reported as proven
all-skipped batches. Missing/invalid timing is not reclassified as measured
feedback, and these are not incomplete or silently dropped inventories.
Failed-test batch diagnostics remain visible; successful map collection does
not mean every collected test passed.

The earlier CTS full run took 39.247 minutes, so the fixed whole-suite result
still trails that reference by 18.00%; it is not proven whole-suite CTS parity.
Its acceptance modules were 33.643 and 34.820 minutes, so the fixed collector
reaches that acceptance-heavy performance level. Replaying the fixed measured
module durations gives 91.888 minutes of total module lifetime and a 45.944-minute
two-slot work lower bound. Current-order replay is 45.994 minutes; longest-first
replay is 45.947 minutes. These are scheduling models, not executed additional
optimizations, and show that queue reordering alone cannot close the remaining
gap under those observed costs.

### Remaining structural differences, not bundled optimizations

The bounded source follow-up distinguishes three remaining mechanisms:

| Mechanism | AffectedTests versus CTS | Evidence limit |
| --- | --- | --- |
| Global cross-module refill | The repository wrapper has two SDK module slots with at most two instrumentation children each. CTS uses four global workers with `--parallelize-same-module-batches` and re-enqueues eligible modules. | Same four-child ceiling, different allocation/lifetimes; changing module queue order cannot replace global budget ownership. No concurrency/protocol redesign was made. |
| Source-collection lifetime | Affected collection creates/disposes a native source collection per batch; CTS resets and caches source collections for reuse. The fixed inventory has 642 batches; two leases per multi-batch module would require at most 90 successful initial collections. | This is an avoidable-work candidate, not a measured latency saving. The retained logs do not time creation/reset/disposal, so no claim is made that pooling closes the remaining gap. |
| Persistence | Affected maps expand source/UID pairs and serialize/write atomically before their module finishes; CTS stores source/batch indices and uses a separate bounded persistence channel. | The formats/contracts differ. Fixed JSON maps total 1,779.747 MiB and 25,125,994 source/UID pairs, but no persistence phase timestamps establish it as the latency root cause. |

The externally visible SDK process interval minus its final collection summary
is only 28.812 seconds summed across all 48 modules (median 0.530 seconds per
module, maximum 1.083 seconds). That boundary does not isolate work inside the
SDK summary, but outer process launch/teardown alone cannot account for seven
minutes. Two Framework PlatformServices maps are approximately 127.243 MiB,
near the existing 128-MiB map bound; neither the bound nor map schema is relaxed
to improve timing.

There is one full measured run per local variant on one shared Windows machine,
not repeated medians or an independent rerun. Earlier CTS and hosted-CI durations
are reference results, not members of this source-matched pair. No hosted cache
consumption, Azure upload, package flow, or actual CI savings is established.
The implementation remains local in the coverage worktree, with verified
source/test patches and immutable package tuples; TestFx's public dependency
pins, rollout gates, and official/release pipelines are unchanged.

Windows x64 is the measured surface; non-Windows execution, hosted Azure
publication/cache consumption, servicing, and actual CI savings remain unproven.
Retained commands, plans, raw maps, original TRX reports, package hashes, and
verification diagnostics are in this session's full-collection artifacts; no
remote publication is claimed.

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
