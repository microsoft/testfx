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
- **CI auto-detection**: detects GitHub Actions environments through the `GITHUB_ACTIONS` variable

Log groups are enabled by default when running on GitHub Actions. Turn them off with `--report-gh-groups off`.

## Related packages

- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps reporting

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
