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
