# GitHub Actions history run window: research and decisions

## Problem and contract

The GitHub Actions reporter currently reads one cumulative local JSON snapshot. The workflow restores and uploads that snapshot; the reporter keeps up to 1,000 samples per test and 10,000 overall, and applies an age window of 1–90 days. The request is to experiment with retrieving history from the last X workflow runs to improve flaky-test context.

Assumption for this experiment: the useful behavior is limiting historical context to the most recent comparable test-run attempts, not downloading X cumulative artifacts. The existing snapshot already contains run IDs and attempt numbers, so downloading several cumulative snapshots would duplicate earlier samples. The test process must remain independent of GitHub credentials and API access.

## Research and experiments

- 2026-11-24: Read [#4931](https://github.com/microsoft/testfx/issues/4931), related proposals [#5951](https://github.com/microsoft/testfx/issues/5951) and [#8761](https://github.com/microsoft/testfx/issues/8761), and merged [#10778](https://github.com/microsoft/testfx/pull/10778). The existing extension already persists bounded test-level outcomes and reads the snapshot before a run; workflow artifact transfer owns authentication.
- 2026-11-24: Read the GitHub Actions [artifacts API](https://docs.github.com/en/rest/actions/artifacts) and [workflow-runs API](https://docs.github.com/en/rest/actions/workflow-runs). These expose run/artifact metadata and archives, not test-level results unless a workflow retains structured test data.
- 2026-11-24: Ran the baseline command `.\build.cmd -test -projects "test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj"` at base `09c901458d491e05a8a2c9adc89ee6ddfea834cc`. The build and tests succeeded for net462, net472, net10.0, and net11.0. Individual test counts were not captured in the command output.
- Not attempted: retrieving and merging multiple GitHub artifacts. It is unnecessary for this experiment because the documented artifact is cumulative; fetching multiple snapshots risks duplicate weighting and adds API calls.

## Alternatives and decision

- Keep the current day-only window: no code or workflow changes, but users cannot ask for a fixed number of prior attempts.
- Retrieve the last X artifacts: introduces workflow API/download/merge logic and must deduplicate cumulative snapshots; rejected for this experiment.
- Add an optional run-attempt cap to the existing snapshot reader: chosen. Filter per test and existing assembly/TFM/architecture/OS scope, after the existing age cutoff. Count a GitHub Actions rerun attempt separately because it may produce a different outcome. Preserve the existing day-only behavior when the new option is omitted. Do not change test verdicts.

Reconsider artifact retrieval if users require run links or metadata that the current sample schema does not retain.

## Validation and demonstration

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Most recent attempts bound historical stats and durations | `AggregateStats_LimitsOutcomesAndDurationsToMostRecentRunAttempts`: counterfactual behavior-removal run failed at `FailCount` (expected 1, actual 2); the same test passed after restoring selection, 1/1 on net11.0. The full extension unit-test project also passed on net462, net472, net10.0, and net11.0. | Nonvisual annotation/summary text; unit tests assert the selected outcomes, count, and duration. |
| Existing behavior remains when the option is omitted | The full extension unit-test project passed on net462, net472, net10.0, and net11.0. The existing day-window behavior remains the default. | No visual change; packaged `--help` and `--info` cases passed. |
| Invalid values and dependent-option rules are rejected | CLI provider tests are included in the passing extension unit-test project. | Packed `Help_WithAllExtensionsRegistered_OutputFullHelpContent` and `Info_WithAllExtensionsRegistered_OutputFullInfoContent` each passed for net462, net10.0, and net11.0. |
| Workflow artifact transfer remains independent | No workflow or authentication code changed; the existing cumulative snapshot remains the only artifact read. | No live GitHub Actions workflow was run in this experiment. |

Validation details:

- `.\build.cmd -pack` completed successfully. The shipping artifact was `Microsoft.Testing.Extensions.GitHubActionsReport.3.0.0-dev.nupkg`.
- `.\build.cmd -test -projects "test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj"` passed for net462, net472, net10.0, and net11.0.
- The packaged acceptance executable was run with `--filter-uid Microsoft.Testing.Platform.Acceptance.IntegrationTests.HelpInfoAllExtensionsTests.Help_WithAllExtensionsRegistered_OutputFullHelpContent`; 3 passed, 0 failed, 0 skipped. The matching `Info_WithAllExtensionsRegistered_OutputFullInfoContent` selection also had 3 passed, 0 failed, 0 skipped.
- Counterfactual source state: detached worktree at `09c901458d491e05a8a2c9adc89ee6ddfea834cc`, with the implementation and test patch applied. Removed only the run-window selection before aggregation. Build succeeded with 0 warnings and 0 errors; the focused test executed 1 case and failed at `Assert.AreEqual(1, stats.FailCount)` (expected 1, actual 2; exit 2). Restoring the selection and rebuilding made the same test pass (1 passed, 0 failed, 0 skipped; exit 0).
- A full acceptance-project run was started after packing but stopped after roughly ten minutes without producing a test summary; it is not counted as passing evidence. The two targeted packaged checks above passed.
- Generated XLF catalogs were regenerated from the resource file. `git diff --check` passed, and all changed C# files retain their UTF-8 BOM.

## Open questions

- The initial maximum will align with the store's 1,000 samples per test. Whether a lower user-facing cap is preferable needs usage feedback.
- This experiment does not validate artifact retention policies, PR-fork permissions, or a live Actions workflow.

## MTP 3: passing tests with previous failures

### Problem and contract

The MTP 3 GitHub Actions extension should make a test visible when it passes
in the current run but failed in at least one comparable previous run attempt.
Treat this as historical context, not proof of an inherently flaky test: a
failure followed by a fix produces the same observations. Current test results,
exit codes, and the existing retry-recovered flaky count must not change.

History remains opt-in. The workflow restores and uploads the cumulative
snapshot; the test host reads it before execution and writes its current
observations after execution. History is scoped by assembly, target framework,
architecture, runner OS, and test identity, and is bounded by the day and
optional per-test run-attempt windows. The current GitHub run attempt is not
part of its own prior statistics. Missing history produces no historical
section, and canceled execution keeps the existing cancellation behavior.
History that was never captured cannot be reconstructed.

The intended shipping scope is MTP 3 on this branch, not a 2.5.x backport.
The producer is `Microsoft.Testing.Extensions.GitHubActionsReport` and its
packaged test host; the consumers are the GitHub step summary and, where the
SDK supports artifact post-processing, the aggregated summary artifact.
Older SDKs use the existing direct per-project summary path. No new handshake,
public API, or snapshot schema is required. The implementation owner remains
responsible for validating the eventual shipping branch and package flow.

### Research and alternatives

- The existing reporter enriches current failures but does not show previous
  failures for a currently passing test. Its `IsFlaky` flag records actual
  retry recovery and is persisted in the snapshot; using it for an inference
  from earlier runs would feed that inference back into later runs.
- The snapshot model in the released 2.5.3 source (`v4.5.3`) and the current
  model are identical, including schema version 1. A subsequent real-package
  experiment confirmed that the MTP 3 reporter reads a 2.5.3 snapshot.
- The released and current `TestNodeIdentity` implementations also match, and
  the released shared fragment model already declares the historical
  pass/fail fields. These source comparisons do not prove that the two shipped
  packages can exchange snapshots or fragments in a composed run.
- A 2.5.3 reporter does not populate those historical fields for passing
  tests. Consequently, its module will not gain this new section merely
  because it is aggregated with MTP 3 modules. Supporting that mixed-module
  case would require an aggregator to load history per module and preserve
  each module's scope and run-window settings. The explicitly permitted
  new-version-only behavior is preferable to inferring these missing values.
- The shared summary fragment already has historical pass/fail fields, and the
  Azure DevOps reporter uses them for a separate historical-results section.
  GitHub retry aggregation must preserve those fields without changing
  `IsFlaky`. Older fragments without those counts must remain readable and
  simply cannot display the new information.
- Doing nothing preserves current failure annotations but leaves passing tests
  with previous failures invisible. An informational workflow annotation is
  smaller, but its delivery depends on the SDK's display-message protocol and
  can generate an annotation per test. A bounded, per-module job-summary
  section works with both direct and aggregated summary paths and keeps this
  information distinct from retry-recovered tests. No additional GitHub API or
  artifact download is needed.

### Review: bounded history and summary labels

Review reproduced three ways the existing 10,000-sample limits can hide a
passing test whose previous failure is available: the test sorts after the
direct snapshot's first 10,000 identities, an earlier module fills the
aggregate limit, or earlier retry attempts fill the limit before the passing
attempt is merged. In the last case, the bounded merge can also retain the
old failure instead of the recovered result. A separate reproduction showed
that a very long historical label causes the direct writer to reject the
entire project summary, including its totals.

Removing the limits would increase memory and persisted snapshot sizes.
Adding a separate presentation channel would preserve the old sample selection
but introduce another fragment contract and retry-merge path. Instead, within
the existing bounds, select identities with previous failures first; for retry
fragments, retain all available attempts for a selected identity together.
Preserve the existing selection when everything fits and leave other providers'
aggregate selection unchanged. This changes which samples survive only when a
limit is exceeded: a previously failing identity may replace another current
sample. There can still be more relevant identities or retry observations than
the limit, so the report is bounded rather than exhaustive.

A further review found that giving historical identities priority can crowd out
the final attempt of an unrelated recovered test, incorrectly recording its
earlier failure. Within retry aggregation, select other identities as complete
groups too: after the historical priorities, retain other retry groups before
single-attempt tests, in encounter order. Discard a group that cannot fit
rather than persisting a partial outcome.

Clip summary test labels, preserving both the beginning and end and marking the
omission, without splitting Unicode surrogate pairs. A test with historical
context can also appear in the slowest or retry-flaky lists, so clipping only
the new section would still let that same label crowd out the entire summary.
An additional review found that two names differing only in the omitted middle
otherwise render identically. A stable, non-cryptographic fingerprint in the
omission marker makes such labels distinguishable without repeating an
unbounded name; as with any fixed-size fingerprint, collisions remain possible.
The unabridged identities remain in history.

### Validation and demonstration

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Passing test with prior failure | `WhenPassingTestPreviouslyFailed_SummaryUsesHistoryAndRunAttemptWindow` executed against packaged hosts for net462, net10.0 and net11.0. It checks failed/passed/passed child exits, both row identities, prior counts, persisted outcomes and retry flags. | The actual summary was rendered locally at 1280×720 and in a 360-pixel-wide content area, including an escaped long data-row label. Live GitHub rendering is unverified. |
| Defaults, bounds, and old snapshots | Focused tests cover disabled and missing history, the 20-entry bound, section selection and the run-attempt window. An actual 2.5.3 producer and MTP 3 consumer exchanged a schema-1 snapshot; see below. | No history predating the first snapshot can be shown; live workflow artifact transfer remains unverified. |
| Multiple modules and retries | `DotnetTest_HistoryRemainsAssociatedWithItsModuleAndDataRow` executed a real packaged SDK consumer with two modules and two rows per module. The snapshot and summary retained the correct association and exit codes. Unit coverage checks retry-fragment history preservation. | The local aggregate preview was expanded to inspect the module section. Mixed-version modules and a real older SDK without aggregation support were not executed. |
| Test sensitivity | With the earlier test source, a buildable counterfactual suppressing the new section made three of five focused tests fail on the intended missing output; a retry-field counterfactual failed its retry test. Final-source comparisons are recorded below. | These are targeted mutations, not pristine-base comparisons or packaged-acceptance red phases. |

The earlier feature-validation source and test changes were based on
`a3bab4d2a755bcf1e9ccb602e5c404d2fc2d9960`; their isolated Git patch has
SHA-256 `4452C88BC964F8A0D5CC0963BDBC45911E0896003442E2487B79472FF5ED5EA8`.
The original comparison patch, including the then-current decision record and
package documentation, has SHA-256
`D5FBD8646B80CF67780CC549512ABA2638F1B8E776B3888609A301E05F72BAA1`.
The counterfactual changed only the new section's `HistoricalFailCount > 0`
predicate to `< 0`; its mutation patch has SHA-256
`CD20468A8E9623B490942089F22AFD28C04D332FB1CDEB73C4D6EE30EC0D96DE`.
The separate retry counterfactual removed the three history-field assignments
from the retry merge; its patch has SHA-256
`809E745742B427797E7F129985B0C466D3C6AC8A4C09A78BAEBB45E647934FC7`.
The same test-source bytes were used in both builds.

The following commands ran from the worktree root on Windows, with the
repository-local `.dotnet` on `PATH`. `$results` stands for the task-owned
results directory in each invocation; the original machine-specific absolute
paths are intentionally omitted.

```powershell
.\build.cmd -pack -c Release '/bl:artifacts\log\Release\GitHubHistoryPackFixed.binlog'

$filter = 'FullyQualifiedName~SessionFinishing_PassingTestReportsPreviousFailures|FullyQualifiedName~SessionFinishing_DisabledHistoryDoesNotReportPreviousFailures|FullyQualifiedName~BuildAggregateMarkdown_PreviousFailures|FullyQualifiedName~SummaryPostProcessor_RetryWithoutDownstreamWritesMergedHistory'
& 'artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\net11.0\Microsoft.Testing.Extensions.UnitTests.exe' --filter $filter --report-trx --report-trx-filename 'history-unit-net11.trx' --results-directory $results

$filter = 'FullyQualifiedName~WhenPassingTestPreviouslyFailed_SummaryUsesHistoryAndRunAttemptWindow|FullyQualifiedName~DotnetTest_HistoryRemainsAssociatedWithItsModuleAndDataRow'
& 'artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Release\net12.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.exe' --filter $filter --report-trx --report-trx-filename 'history-acceptance.trx' --results-directory $results
```

The pack exited 0 with no warnings or errors. The five focused unit cases
executed and passed on net11.0, net10.0, net462 and net472, each with exit 0.
The acceptance selection executed four cases and passed with exit 0; its
generated consumers used isolated package caches. A broader net11.0 selection
of GitHub Actions summary, history and annotation tests exited 0 with 242
passed, 0 failed and 6 skipped. The skips are Windows-inapplicable FIFO and
symlink cases, not the new history tests.

After the compatibility note was added to the package README, the final
`.\build.cmd -pack -c Release '/bl:artifacts\log\Release\GitHubHistoryFinalPack.binlog'`
also exited 0 with no warnings or errors. The expanded six-case unit selection,
including `SummaryPostProcessor_ForRetryAttempts_ReturnsChainableFragmentAndWritesSummary`,
passed on each of net11.0, net10.0, net462 and net472. The same four acceptance
cases passed again against the final packages. Independent `--list-tests`
discovery selected those four identities, and the repository execution verifier
confirmed 4 planned, 4 selected, 4 executed, 4 passed and 0 skipped.

The isolated comparison built with
`.\build.cmd -c Release -projects 'test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj' '/bl:artifacts\log\Release\GitHubHistoryCounterfactual.binlog'`
and exited 0. Running the same five-case filter against its net11.0 executable
exited 2: three assertions detected the missing direct, module-associated and
bounded sections, and two cases passed. The unmodified implementation's same
selection passed, 5/5, before the comparison. The retry-field counterfactual
built with the same project and
`/bl:artifacts\log\Release\GitHubHistoryRetryCounterfactual.binlog`, then its
single retry test exited 2 at `HistoricalPassCount` (expected 2, actual 0;
1 failed, 0 skipped). After both mutations, the correct worktree's six-case
net11.0 selection passed, 6/6. The isolated comparison's production source
was then restored, rebuilt with
`/bl:artifacts\log\Release\GitHubHistoryCounterfactualRestore.binlog`, and
the same six-case selection passed, 6/6.

For cross-version validation, two independent net10.0 consumers with the same
assembly and test identities used separate package caches. The old consumer
ran the released 2.5.3 reporter with `--report-gh --report-gh-history $history`
and exited 2 after one intentional failed row. The MTP 3 consumer ran the same
options against that snapshot and exited 0 with both rows passing. Its summary
associated the first row with `0 passed, 1 failed`, did not mark it retry-flaky,
and the final schema-1 snapshot contained the two old and two new outcomes.
The released package SHA-256 was
`1493BF57D7C203DDCF268670B76418D3F51BBDF2C5014641C2DE290F5F88578A`.
Its executed reporter matched the package's `lib/net9.0` asset
(`FC273DE8A1DB950307A73464E633F303AEC74503E6BB7B8BF916240167E42EC2`);
the MTP 3 reporter matched its `lib/net10.0` asset
(`54470F8E8BAC124F189FBA2915E6711BC6DD60CC29DDB82E81311B7195BD1393`).
The restored dependency graphs identified reporter and platform versions 2.5.3
and 3.0.0-dev respectively. This proves sequential snapshot reuse, not
mixed-version module aggregation.

The shipping-package SHA-256 values for that earlier validation, before the
subsequent review fixes, were:

- `Microsoft.Testing.Extensions.GitHubActionsReport.3.0.0-dev.nupkg`:
  `0AEFA43C96A8CB492F1AE0EC3C6DF1985ACBF6B36E9480A1F693941D62A1662F`
- `Microsoft.Testing.Platform.3.0.0-dev.nupkg`:
  `A598A09F30B21EE3171E9CD4B4454B6CE4D56255ADA420CB006D99C8BAB9C2DB`
- `Microsoft.Testing.Platform.MSBuild.3.0.0-dev.nupkg`:
  `E2461E95507D5D85027F4A923BD188F6964CC863E746BB2EAD533281204115F6`

That reporter package includes the compatibility note, and its net10.0
reporter DLL is byte-identical to the MTP 3 binary used in the cross-version
experiment. The earlier package used for that experiment had SHA-256
`B96140914F6474F73803D03B2A06A3692638C13D3CD651D113C30A66AE722FF4`.
Both of those multi-module consumers resolved reporter and platform 3.0.0-dev;
their net11.0 reporter DLLs matched that package asset, SHA-256
`0E317F32C8C9A96BA4757DD3BFDFB877C3FC5E67E6C6461565E8578C24736BBC`.

The local preview used a Markdown renderer and local styling, including
wrapping for long list items; it is not GitHub's renderer. The long label was
readable in the constrained content area, and the existing table remained
horizontally scrollable. Live GitHub rendering, artifact transfer, and CI on
the eventual shipping revision remain unverified.

Both affected test assemblies schedule methods in parallel with the default
worker count. The unit fixtures use independent temporary directories and
injected environment values. The direct-host acceptance case uses a unique
snapshot and summary per test, while the SDK case creates a new two-project
asset and supplies environment values only to its child processes. Neither
case changes process-global environment variables or shares mutable test assets.

The Windows Release pipeline selects `TestFx.slnx` unless affected-test
selection is enabled; its fallback also runs that solution. Linux and macOS use
`NonWindowsTests.slnf`. These are selection paths, not evidence that CI has
executed the new cases. `SkipTests` and affected-test conditions still apply.

On 2026-11-25, `origin/main` was
`520819b353cad760c80c0d575a09e7ea43cc9758` and its `eng/Versions.props`
specified MTP 3.0.0. The working base
`a3bab4d2a755bcf1e9ccb602e5c404d2fc2d9960` was its ancestor, and the intervening
change did not touch the GitHub Actions reporter. `git ls-remote --heads origin
'release/*' 'main'` returned only `main`. This does not establish that the
change has landed on a shipping branch, or that a later release/backport
decision is unnecessary.

At final PR preparation, `origin/main` was
`394a1beb7ea17d8f51d1eed6c35a9c612f9f95d4`. Its intervening commits did not
modify any of the eleven paths in this change. This comparison does not prove
the feature has shipped or that a servicing decision is unnecessary.

The initial `.\build.cmd -pack -c Release -bl` reported IDE0028 in the new
test fixture; the collection initializer was corrected. Its shell was stopped
after the diagnostic, but the child MSBuild process continued and held the
default binary log. A second attempt therefore failed with MSB4104. After
stopping that specifically identified task-owned process, an attempt to use
`-bln` failed because the repository's outer build script forwards that switch
to MSBuild instead of recognizing it. The next pack passes a distinct
`/bl:...` switch directly to MSBuild. These attempts are not passing evidence.

### Final review and packaged-product evidence

The final reviewed production and test files are based on
`a3bab4d2a755bcf1e9ccb602e5c404d2fc2d9960`. Their Git patch, excluding this
decision record, has SHA-256
`7CD2795047B637B80FE8F4EEB562FA50840D652F7D8CEDC8BF309609ECBA7C35`;
the final unit-test source has SHA-256
`BF2D5B1ACAF9E9446C9CC5ED5CE3C84D397EC9763C4B0B41D82035645827FE78`.
These identify the actual tested files, including the review fixes; the earlier
patch and package hashes above describe earlier source states.

On Windows, with the repository-local SDK on `PATH`, the final Release build
and repack both exited 0 with no warnings or errors:

```powershell
.\build.cmd -c Release -projects 'test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj' '/bl:artifacts\log\Release\GitHubHistoryRetryPriorityFix-1b8d6602.binlog'
.\build.cmd -pack -c Release '/bl:artifacts\log\Release\GitHubHistoryFinalReviewPack-1b8d6602.binlog'
.\build.cmd -c Release -projects 'test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj' '/bl:artifacts\log\Release\GitHubHistoryFinalAcceptanceBuild-1b8d6602.binlog'
```

The last command also exited 0. The final shipping packages were:

- `Microsoft.Testing.Extensions.GitHubActionsReport.3.0.0-dev.nupkg`:
  `AB28E147269011769B62A1EC839FED637A164C5E470D2696277669B34A1F5DD4`
- `Microsoft.Testing.Platform.3.0.0-dev.nupkg`:
  `A598A09F30B21EE3171E9CD4B4454B6CE4D56255ADA420CB006D99C8BAB9C2DB`
- `Microsoft.Testing.Platform.MSBuild.3.0.0-dev.nupkg`:
  `E2461E95507D5D85027F4A923BD188F6964CC863E746BB2EAD533281204115F6`

Four generated consumer asset graphs resolved reporter and platform
`3.0.0-dev`. Their isolated restored reporter package matched the final
shipping package, and the eight consumed reporter DLLs matched assets inside
it. The net11.0 asset used by the multi-module run had SHA-256
`D2582387EC0540FA044F560A48A46E94063C0DC03F718499F0977F5CEC1E804D`.

The following focused selection ran against the final built unit host on
net11.0, net10.0, net462 and net472. Each run exited 0 with 9 selected,
executed and passed, including both Boolean data rows, and 0 skipped. The
repository execution verifier reconciled each TRX against the independent
expected-test list. The result directory is task-owned; `$root` denotes that
directory below.

```powershell
$selectors = @(
  'SessionFinishing_PreviousFailureBeyondHistorySampleLimitIsReported',
  'SessionFinishing_LongHistoricalLabelDoesNotDropSummary',
  'SessionFinishing_LongHistoricalLabelInSlowTestsKeepsSummaryAndIdentityEnds',
  'SummaryPostProcessor_PreviousFailureInLaterModuleSurvivesHistoryLimit',
  'SummaryPostProcessor_RetryHistoryLimitPreservesRecoveryAndPreviousFailures',
  'SummaryPostProcessor_RetryHistoryPriorityPreservesOtherRecoveredOutcomes',
  'SummaryPostProcessor_RetryHistoryLimitDoesNotRetainPartialHistoricalIdentity',
  'BuildAggregateMarkdown_LongHistoricalNamesDifferingInTheMiddleRemainDistinct'
)
$filter = ($selectors | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'
foreach ($tfm in @('net11.0', 'net10.0', 'net462', 'net472')) {
  $exe = "artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\$tfm\Microsoft.Testing.Extensions.UnitTests.exe"
  & $exe --filter $filter --report-trx --report-trx-filename "history-review-final2-$tfm.trx" --results-directory $root
}
$broad = 'FullyQualifiedName~GitHubActions|FullyQualifiedName~AzureDevOps'
& 'artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\net11.0\Microsoft.Testing.Extensions.UnitTests.exe' --filter $broad --report-trx --report-trx-filename 'history-review-final2-regression.trx' --results-directory $root
```

The broader selection exited 0: 1,115 passed, 0 failed and 6 skipped. The
skips are Windows-inapplicable FIFO or symbolic-link cases, not the new
history cases.

After the final repack, the acceptance host used a fresh task-owned
`TESTFX_COLLECTION_NUGET_CACHE` and retained generated assets for provenance:

```powershell
$filter = 'FullyQualifiedName~WhenPassingTestPreviouslyFailed_SummaryUsesHistoryAndRunAttemptWindow|FullyQualifiedName~DotnetTest_HistoryRemainsAssociatedWithItsModuleAndDataRow'
$exe = 'artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Release\net12.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.exe'
& $exe --list-tests --filter $filter
& $exe --filter $filter --report-trx --report-trx-filename 'acceptance.trx' --results-directory $root
```

Discovery and execution exited 0. The verifier confirmed 4 planned, selected,
executed and passed, 0 skipped: the direct-host
`WhenPassingTestPreviouslyFailed_SummaryUsesHistoryAndRunAttemptWindow`
case on net11.0, net10.0 and net462, and the real SDK
`DotnetTest_HistoryRemainsAssociatedWithItsModuleAndDataRow` case. The latter
checks two modules with two distinguishable rows each, their persisted outcomes,
module association and parent exits. Both affected test assemblies ran with
method-level scheduling and 32 workers. Their new tests use unique paths and
child-process environment values rather than process-global mutations.

The final test source was also retained in isolated, successfully built
counterfactuals:

| Contract and case | Counterfactual | Executed result without fix | Final implementation |
| --- | --- | --- | --- |
| Historical section and retry composition; six selected cases | Suppress the new section by changing `HistoricalFailCount > 0` to `< 0`; patch `5CB097079D26384AEACACB585B08A8A1ED4089829A629A41AADCA09FDBAC85BC` | All 6 executed: 4 failed assertions for the missing historical section, 2 passed, 0 skipped; test host exit 2. | The same final-source selection passed, 6/6, 0 skipped, exit 0. |
| Retry history fields; same six cases | Remove the retry merge's historical pass/fail assignments; patch `33102E3F05E951E6F2C4004CD9DE3CBF8D58CB53AAD6DB124E84266DB6F1F1DC` | All 6 executed: `SummaryPostProcessor_ForRetryAttempts_ReturnsChainableFragmentAndWritesSummary` failed because `HistoricalPassCount` was 0 instead of 2; 5 passed, 0 skipped, exit 2. | The same final-source selection passed, 6/6, 0 skipped, exit 0. |
| Bounded direct, aggregate, retry and long-label behavior; seven selected cases | Restore the pre-review production behavior; patch `5E0596E2F7B5B8E1F33A1CD1BE3D75AE6F8B049DD6235A740520E145DDDBE6DA` | All 7 executed and failed at the intended assertions, 0 skipped, exit 2. | All 7 passed within the nine-case selection above. |
| `SummaryPostProcessor_RetryHistoryPriorityPreservesOtherRecoveredOutcomes` and the two `SummaryPostProcessor_RetryHistoryLimitDoesNotRetainPartialHistoricalIdentity` rows | Restore the intermediate retry-priority selection; patch `57899508D9204151909BA80350D3E05F694C24894B0B6B8985C20CAF296FFBBB` | 3 executed, 2 failed and 1 passed, 0 skipped, exit 2. The unrelated recovered result was `failed` instead of `passed`, and the nonhistorical partial identity was retained. | All 3 passed within the nine-case selection. |
| `BuildAggregateMarkdown_LongHistoricalNamesDifferingInTheMiddleRemainDistinct` | Remove the omission-marker fingerprint; patch `940D771895CCE6B3E7EC5D18953DC46FDFECEF39D5B3284E8C9AFC1F18A5462C` | 1 executed and failed because the two shortened labels matched, 0 skipped, exit 2. | The same case passed. |

Each comparison built the same unit project in a task-owned checkout, then ran
the selected net11.0 executable with `--filter`, `--report-trx`,
`--report-trx-filename 'red.trx'`, and its isolated `--results-directory`.
The comparison binlogs were respectively
`GitHubHistoryFinal2TestsCounterfactual-1b8d6602.binlog`,
`GitHubHistoryRetryPriorityFinalCounterfactual-1b8d6602.binlog`, and
`GitHubHistoryLabelFinalCounterfactual-1b8d6602.binlog`.
These are executable behavior comparisons, not compilation failures or
packaged-acceptance red phases.

The final-source feature-disabled comparison built successfully in the
isolated checkout with
`.\build.cmd -c Release -projects 'test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj' '/bl:artifacts\log\Release\GitHubHistoryFeatureFinalDisabled-1b8d6602.binlog'`.
Its unit host and the final worktree host used the same six-test filter:

```powershell
$filter = 'FullyQualifiedName~SessionFinishing_PassingTestReportsPreviousFailures|FullyQualifiedName~SessionFinishing_DisabledHistoryDoesNotReportPreviousFailures|FullyQualifiedName~BuildAggregateMarkdown_PreviousFailures|FullyQualifiedName~SummaryPostProcessor_RetryWithoutDownstreamWritesMergedHistory|FullyQualifiedName~SummaryPostProcessor_ForRetryAttempts_ReturnsChainableFragmentAndWritesSummary'
# From the final worktree:
& 'artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\net11.0\Microsoft.Testing.Extensions.UnitTests.exe' --filter $filter --report-trx --report-trx-filename 'green.trx' --results-directory $root
# From the isolated comparison checkout:
& 'artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\net11.0\Microsoft.Testing.Extensions.UnitTests.exe' --filter $filter --report-trx --report-trx-filename 'feature-red.trx' --results-directory $root
# With retry-field assignments removed in the comparison checkout:
& 'artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\net11.0\Microsoft.Testing.Extensions.UnitTests.exe' --filter $filter --report-trx --report-trx-filename 'retry-fields-red.trx' --results-directory $root
# With final source restored and rebuilt in the comparison checkout:
& 'artifacts\bin\Microsoft.Testing.Extensions.UnitTests\Release\net11.0\Microsoft.Testing.Extensions.UnitTests.exe' --filter $filter --report-trx --report-trx-filename 'rebuilt-green.trx' --results-directory $root
```

Independent TRX verification matched all six test identities in both runs.
The four disabled-feature failures expected the previous-failure section in
the direct summary, the module/data-row summary, the bounded summary, and the
retry-composed summary. The other two cases passed. The wrapper's stricter
three-failure expectation rejected this valid four-failure result; the actual
test-host exit was 2 and its assertions, not a setup failure, provide the proof.
The retry-field comparison also built successfully with
`.\build.cmd -c Release -projects 'test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj' '/bl:artifacts\log\Release\GitHubHistoryRetryFinalCounterfactual.binlog'`
before the test-host exit 2.

An initial restoration copied the correct source with an older timestamp;
the incremental build reused the mutated reporter DLL and the retry test still
failed. That run is not green evidence. After updating the restored source's
timestamp in the isolated checkout, the following rebuild exited 0:

```powershell
(Get-Item 'src\MTP\Microsoft.Testing.Extensions.GitHubActionsReport\GitHubActionsSummaryArtifactPostProcessor.cs').LastWriteTimeUtc = [DateTime]::UtcNow
.\build.cmd -c Release -projects 'test\UnitTests\Microsoft.Testing.Extensions.UnitTests\Microsoft.Testing.Extensions.UnitTests.csproj' '/bl:artifacts\log\Release\GitHubHistoryFinalPairedRebuild-1b8d6602.binlog'
```

The restored source/test patch again matched
`7CD2795047B637B80FE8F4EEB562FA50840D652F7D8CEDC8BF309609ECBA7C35`.
The rebuilt comparison host then passed the same six identities, 0 skipped,
exit 0; its reporter DLL had SHA-256
`C1109FFD5FBC57ED8CCC1A89C7C5356D7CAA333939AAD35FFEA514EEDCBA23F6`.

The released 2.5.3 producer was then rerun against the **final** MTP 3
consumer. Both net10.0 programs used the same assembly and data-row identities:

```powershell
$env:GITHUB_RUN_ID = 'mixed-old'; $env:GH_TEST_MODE = 'history-fail'
& $oldHost --report-gh --report-gh-history $history
$env:GITHUB_RUN_ID = 'mixed-new'; $env:GH_TEST_MODE = 'history-pass'
& $newHost --report-gh --report-gh-history $history
```

The first exit was 2 for the intentional failed row; the second was 0. The
schema-1 snapshot contained exactly the two old and two new row outcomes,
all with `isFlaky=false`; the final summary associated `0 passed, 1 failed`
with the previously failed row. The old reporter DLL matched the released
package asset `FC273DE8A1DB950307A73464E633F303AEC74503E6BB7B8BF916240167E42EC2`.
The new consumer's restore graph selected the final isolated package and its
net10.0 DLL matched the final package asset
`7D9CAE3CB52B2DE32CA5FB9767FFE7302E70DC07BF8086CE5EA0A90FE1B9E783`.
This verifies sequential snapshot reuse; old-version modules still do not
provide the new historical fields for mixed-module aggregation.

For the final visual check, the actual multi-module summary was rendered
locally and its module disclosure expanded at a 1280×720 browser viewport.
A second packed net11.0 consumer produced two 512-character labels with
escaped markup and identical prefix/suffix but different omitted middles;
their fingerprints and prior-failure counts remained distinct. At a simulated
360-pixel content width the labels wrapped without body overflow and the
totals tables remained horizontally scrollable. This was local Markdown
rendering with local styling, **not** GitHub's hosted renderer. Live GitHub
rendering and artifact transfer, mixed-version module aggregation, a real
older SDK fallback, and CI on the eventual shipping branch remain unverified.

## Follow-up: matrix workflow guidance

- 2026-11-25: The published workflow example used one artifact name and a workflow-level concurrency group. Multiple matrix jobs using it could collide when uploading artifacts, and the shared group could replace a pending workflow run. Clarified that the example is single-writer and documented per-partition artifact naming and job-level concurrency. Each partition keeps separate history; suite-wide aggregation requires a deliberate single writer and merge. No live GitHub Actions run was performed.
