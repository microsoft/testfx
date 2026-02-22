# Microsoft.Testing.Extensions.AzureDevOpsReport

Microsoft.Testing.Extensions.AzureDevOpsReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that provides real-time test result reporting for Azure DevOps Pipelines.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.AzureDevOpsReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.AzureDevOpsReport) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.AzureDevOpsReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **Real-time test reporting**: reports individual test results to Azure DevOps as they complete
- **Pipeline integration**: automatically detects Azure DevOps Pipelines environments
- **Test run summaries**: provides rich test result summaries visible in the Azure DevOps Pipeline UI

## Related packages

- [Microsoft.Testing.Extensions.TrxReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport): TRX report generation for standardized test result files

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
