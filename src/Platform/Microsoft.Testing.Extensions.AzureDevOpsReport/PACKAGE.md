# Microsoft.Testing.Extensions.AzureDevOpsReport

Microsoft.Testing.Extensions.AzureDevOpsReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that reports test failures and warnings in Azure DevOps CI builds, with file/line annotations when available.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.AzureDevOpsReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.AzureDevOpsReport) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.AzureDevOpsReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **Azure DevOps reporting**: emits CI errors/warnings for test failures via the Azure DevOps logging commands
- **Configurable severity**: supports `--report-azdo-severity` (`error` or `warning`)
- **CI auto-detection**: detects Azure DevOps environments through the `TF_BUILD` variable

Enable Azure DevOps reporting with the `--report-azdo` command line option.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport): TRX report generation for standardized test result files

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports#azure-devops-reports>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
