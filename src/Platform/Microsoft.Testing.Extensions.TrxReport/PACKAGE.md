# Microsoft.Testing.Extensions.TrxReport

Microsoft.Testing.Extensions.TrxReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that generates TRX (Visual Studio Test Results) report files.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.TrxReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.TrxReport) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.TrxReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **TRX report generation**: produces `.trx` report files compatible with Visual Studio and Azure DevOps
- **Standardized format**: TRX is a widely supported XML-based test results format
- **CI integration**: TRX files can be published to Azure DevOps, GitHub Actions and other CI systems for rich test result visualization

Enable TRX report generation via the `--report-trx` command line option.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport.Abstractions](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions): interfaces for extensions interoperating with TRX reports
- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): real-time test reporting for Azure DevOps Pipelines

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
