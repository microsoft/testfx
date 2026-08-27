# Microsoft.Testing.Extensions.GitHubActionsReport

Microsoft.Testing.Extensions.GitHubActionsReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that emits GitHub Actions-native workflow commands so test runs on GitHub Actions produce a first-class experience.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.GitHubActionsReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.GitHubActionsReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **Per-assembly log groups**: emits `::group::` / `::endgroup::` workflow commands so each test assembly's output is collapsed by default in the runner UI
- **Failure annotations**: emits an `::error` workflow command for each failing test so failures appear in the workflow Annotations tab and, when the source location can be resolved, on the pull request's "Files changed" diff gutter. Skipped tests are surfaced as `::warning` annotations so they are visible in the Annotations tab too. When the test session completes with a non-test-result failure — a `--minimum-expected-tests` violation, a run that discovered zero tests, a `--maximum-failed-tests` stop, a deadline-triggered early stop, or a test-adapter session failure — a single run-level `::error` is emitted describing the [Microsoft.Testing.Platform exit code](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes)
- **Job summary**: writes one markdown roll-up (totals, failures, coverage, slowest tests) to the file pointed to by `GITHUB_STEP_SUMMARY`, which GitHub renders on the workflow run summary page. Use `--report-gh-step-summary-sections` to select `test-results`, `coverage`, `slow-tests`, or any combination; `all` keeps every currently supported section and is the default. With an SDK that supports required artifact post-processing, multi-module `dotnet test` runs produce one authoritative overall section using the SDK's outer duration and exit verdict, with deterministic per-assembly details underneath. Older SDKs preserve the per-assembly sections. A non-test-result failure exit code is called out so a failure is not hidden behind a green ✅
- **Slow-test notices**: emits a `::notice` workflow command for any test still running past a threshold (default 60 seconds)
- **Test history**: reads and updates a bounded JSON snapshot so failures can include prior pass/fail context. GitHub Actions artifact download/upload remains a workflow responsibility, keeping GitHub credentials out of the test process

> [!NOTE]
> The exit-code callout and run-level annotation only cover outcomes the extension can observe once the in-process test session has finished. Those are: `ZeroTests` (8), `MinimumExpectedTestsPolicyViolation` (9), `TestAdapterTestSessionFailure` (10), `TestExecutionStoppedForMaxFailedTests` (13), and `TestExecutionStoppedAtDeadline` (15). `AtLeastOneTestFailed` (2) is already conveyed by the per-test failures, so it gets no separate callout. A hard abort/cancellation (`TestSessionAborted`, 3) short-circuits end-of-session reporting, and codes raised before or after the session — e.g. `InvalidCommandLine` (5) or `TestHostProcessExitedNonGracefully` (7) — occur outside the extension's reach, so none of those are surfaced here.
>
> Cross-module aggregation is negotiated with `dotnet test`. If the SDK does not provide the authoritative run-summary context, the extension keeps its standalone behavior; a manually invoked post-processor labels totals as observed and leaves overall duration and exit verdict unavailable rather than reconstructing them.

### How the annotation source location is resolved

An annotation is pinned to `file:line` using, in order:

1. The first frame of the failure's exception stack trace that resolves to an existing file under the workspace (`GITHUB_WORKSPACE`, or the enclosing git repository when that variable is absent). This pinpoints the failing statement, and requires the test assembly to be built with debug symbols available at run time.
2. The location the test framework reported for the test itself (the platform's `TestFileLocationProperty`). This is used when no stack frame resolves — including for skipped tests, which never carry a stack trace — and pins the annotation to the test's declaration instead.

If neither is available, a title-only annotation is emitted; it still shows in the workflow Annotations tab, just not on the diff gutter. The second source is populated by the MSTest adapter and by the VSTest bridge (from `TestCase.CodeFilePath`), so xUnit, NUnit and other bridged frameworks are covered whenever they supply source information for their tests.

The extension activates when the test run is on GitHub Actions (`GITHUB_ACTIONS=true`) and the `--report-gh` switch is passed; it no-ops otherwise. When active, each feature is enabled by default and can be toggled individually:

| Option | Description | Default |
| --- | --- | --- |
| `--report-gh` | Master switch that turns the extension on (required, in addition to running on GitHub Actions) | off |
| `--report-gh-groups on\|off` | Per-assembly log groups | on |
| `--report-gh-annotations on\|off` | Failure and skip annotations | on |
| `--report-gh-step-summary on\|off\|on-failure` | Markdown job summary; `on-failure` writes it only when the test invocation fails | on |
| `--report-gh-step-summary-sections <section>...` | Job-summary content: `test-results`, `coverage`, `slow-tests`, or `all`; accepts repeated, space-separated, and comma-separated values | `all` |
| `--report-gh-failure-details on\|off` | Expand each failed test in the job summary into a collapsible section carrying its failure message, exception type, source location and stack trace | on |
| `--report-gh-history <path>` | Read and update the test history snapshot at the specified local path | off |
| `--report-gh-history-window <days>` | Retain and use 1–90 days of history | 30 |
| `--report-gh-slow-test-notices on\|off` | Slow-test notices | on |
| `--report-gh-slow-test-threshold <duration>` | Time before a slow-test notice is emitted; accepts a bare number of seconds or a unit suffix such as `90s`, `2m`, `1.5h` | 60s |

### Persist test history with workflow artifacts

The extension intentionally reads and writes only a local snapshot. The workflow owns GitHub authentication and artifact transfer. This keeps `GITHUB_TOKEN` out of the test process and allows pull request runs to consume history without granting them write access. Snapshots retain at most 1,000 samples per test and the latest 10,000 samples overall, in addition to the configured age window.

The following steps restore the newest default-branch snapshot, run tests, and publish the updated snapshot only from the default branch:

```yaml
permissions:
  actions: read
  contents: read

concurrency:
  group: mtp-test-history-${{ github.event_name == 'push' && github.ref == format('refs/heads/{0}', github.event.repository.default_branch) && 'writer' || github.run_id }}
  cancel-in-progress: false

steps:
  - id: test-history
    name: Find latest test history
    shell: bash
    env:
      GH_TOKEN: ${{ github.token }}
      DEFAULT_BRANCH: ${{ github.event.repository.default_branch }}
    run: |
      run_id="$(gh api \
        "repos/${GITHUB_REPOSITORY}/actions/artifacts?name=mtp-test-history&per_page=100" \
        --jq '[.artifacts[] | select(.expired == false and .workflow_run.head_branch == env.DEFAULT_BRANCH)] | first | .workflow_run.id // empty')"
      echo "run-id=${run_id}" >> "${GITHUB_OUTPUT}"

  - name: Restore test history
    if: steps.test-history.outputs.run-id != ''
    uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
    with:
      name: mtp-test-history
      path: .test-history
      run-id: ${{ steps.test-history.outputs.run-id }}
      github-token: ${{ github.token }}

  - name: Run tests
    run: dotnet test --report-gh --report-gh-history .test-history/history.json

  - name: Publish test history
    if: always() && github.event_name == 'push' && github.ref == format('refs/heads/{0}', github.event.repository.default_branch)
    uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
    with:
      name: mtp-test-history
      path: .test-history/history.json
      if-no-files-found: ignore
      retention-days: 90
```

For .NET SDK 8 or 9, add the `--` separator before the Microsoft.Testing.Platform options.

## Related packages

- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps reporting

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
