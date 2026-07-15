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
- **Failure annotations**: emits an `::error` workflow command for each failing test so failures appear in the workflow Annotations tab and, when the source location can be resolved, on the pull request's "Files changed" diff gutter. Skipped tests are surfaced as title-only `::warning` annotations so they are visible in the Annotations tab too. When the test session completes with a non-test-result failure — a `--minimum-expected-tests` violation, a run that discovered zero tests, a `--maximum-failed-tests` stop, or a test-adapter session failure — a single run-level `::error` is emitted describing the [Microsoft.Testing.Platform exit code](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes)
- **Job summary**: appends a markdown roll-up (totals, failures, slowest tests) to the file pointed to by `GITHUB_STEP_SUMMARY`, which GitHub renders on the workflow run summary page. When running `dotnet test` across multiple assemblies each assembly appends its own section (labelled with the assembly name and target framework), and a non-test-result failure exit code is called out so a failure is not hidden behind a green ✅

> [!NOTE]
> The exit-code callout and run-level annotation cover the outcomes the extension can observe once the test session has finished: `AtLeastOneTestFailed` (2, conveyed by the per-test failures rather than a callout), `TestSessionAborted` is _not_ covered because a hard abort/cancellation short-circuits end-of-session reporting, `ZeroTests` (8), `MinimumExpectedTestsPolicyViolation` (9), `TestAdapterTestSessionFailure` (10), and `TestExecutionStoppedForMaxFailedTests` (13). Codes raised before or after the in-process session — e.g. `InvalidCommandLine` (5) or `TestHostProcessExitedNonGracefully` (7) — occur outside the extension's reach and are not surfaced here.
- **Slow-test notices**: emits a `::notice` workflow command for any test still running past a threshold (default 60 seconds)

The extension activates when the test run is on GitHub Actions (`GITHUB_ACTIONS=true`) and the `--report-gh` switch is passed; it no-ops otherwise. When active, each feature is enabled by default and can be toggled individually:

| Option | Description | Default |
|---|---|---|
| `--report-gh` | Master switch that turns the extension on (required, in addition to running on GitHub Actions) | off |
| `--report-gh-groups on\|off` | Per-assembly log groups | on |
| `--report-gh-annotations on\|off` | Failure and skip annotations | on |
| `--report-gh-step-summary on\|off` | Markdown job summary | on |
| `--report-gh-slow-test-notices on\|off` | Slow-test notices | on |
| `--report-gh-slow-test-threshold <duration>` | Time before a slow-test notice is emitted; accepts a bare number of seconds or a unit suffix such as `90s`, `2m`, `1.5h` | 60s |

## Related packages

- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps reporting

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
