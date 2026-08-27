# Microsoft.Testing.Extensions.TrxReport

Microsoft.Testing.Extensions.TrxReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that generates TRX (Visual Studio Test Results) report files.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.TrxReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.TrxReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **TRX report generation**: produces `.trx` report files compatible with Visual Studio and Azure DevOps
- **Standardized format**: TRX is a widely supported XML-based test results format
- **CI integration**: TRX files can be published to Azure DevOps, GitHub Actions and other CI systems for rich test result visualization

## Usage

Enable TRX report generation via the `--report-trx` command line option.

On platforms that can launch a test-host process, TRX uses controller-backed recovery by default. The test host still streams and generates the report during normal execution, while the surviving controller can recover completed results if the test host crashes, hangs, or is terminated by `--timeout`.

Use `--trx-mode in-process` to avoid the additional process when startup cost or application-model constraints take priority over crash recovery. Browser, WASI, iOS, and tvOS application models use in-process mode automatically because they cannot launch a test-host process. Explicitly requesting `--trx-mode out-of-process` on those platforms produces a command-line validation error.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport.Abstractions](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions): interfaces for extensions interoperating with TRX reports
- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps CI error/warning reporting for test failures

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports#visual-studio-test-reports-trx>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
