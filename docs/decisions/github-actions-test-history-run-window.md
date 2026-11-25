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
