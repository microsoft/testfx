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
- **Failure annotations**: emits an `::error` workflow command for each failing test so failures appear in the workflow Annotations tab and, when the source location can be resolved, on the pull request's "Files changed" diff gutter
- **Job summary**: appends a markdown roll-up (totals, failures, slowest tests) to the file pointed to by `GITHUB_STEP_SUMMARY`, which GitHub renders on the workflow run summary page
- **Slow-test notices**: emits a `::notice` workflow command for any test still running past a threshold (default 60 seconds)
- **CI auto-detection**: detects GitHub Actions environments through the `GITHUB_ACTIONS` variable

All features are enabled by default when running on GitHub Actions (or when the `--report-gh` master switch is set), and no-op otherwise. Each feature can be toggled individually:

| Option | Description | Default |
|---|---|---|
| `--report-gh` | Master switch to enable the extension outside GitHub Actions | off |
| `--report-gh-groups on\|off` | Per-assembly log groups | on |
| `--report-gh-annotations on\|off` | Failure annotations | on |
| `--report-gh-step-summary on\|off` | Markdown job summary | on |
| `--report-gh-slow-test-notices on\|off` | Slow-test notices | on |
| `--report-gh-slow-test-threshold <seconds>` | Seconds before a slow-test notice is emitted | 60 |

## Related packages

- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps reporting

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
