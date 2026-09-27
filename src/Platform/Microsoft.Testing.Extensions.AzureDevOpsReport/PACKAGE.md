# Microsoft.Testing.Extensions.AzureDevOpsReport

Microsoft.Testing.Extensions.AzureDevOpsReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that reports individual test failures as errors or warnings in Azure DevOps CI builds, with file/line annotations when available.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.AzureDevOpsReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.AzureDevOpsReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **Azure DevOps reporting**: emits individual CI errors/warnings for each test failure via the Azure DevOps logging commands
- **Configurable severity**: supports `--report-azdo-severity` (`error` or `warning`)
- **CI auto-detection**: detects Azure DevOps environments through the `TF_BUILD` variable
- **Live publishing**: streams test results to the Azure DevOps Tests tab while the run is still in progress (`--publish-azdo-test-results`)
- **Automatic attachments**: for failed tests, attaches stdout/stderr and any `FileArtifactProperty` data (dumps, screenshots) to the matching result in the Tests tab; uploads `*.coverage`, `*.cobertura.xml`, and `*.opencover.xml` artifacts as run-level attachments
- **Extensions-tab summary**: `--report-azdo-summary` writes and uploads a compact Markdown dashboard with a friendly title, test totals, coverage, failures, and slowest tests. With an SDK that supports required artifact post-processing, a multi-module `dotnet test` invocation uploads one authoritative overall summary with a module overview. Older SDKs preserve per-assembly summaries. This does not change live publishing to the Azure DevOps Tests tab.

The Markdown summary is independent from the self-contained HTML report produced by `--report-html`. Both options can be enabled together: Azure DevOps renders the Markdown in the Extensions tab, while the HTML remains a report file that can be published as a build artifact or displayed by a separately installed Azure DevOps web extension.

## Usage

Enable Azure DevOps reporting with the `--report-azdo` command line option.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport): TRX report generation for standardized test result files

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports#azure-devops-reports>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
