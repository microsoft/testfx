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

On macOS, TRX uses a controller process by default. The test host generates the report during normal execution; the controller can recover completed results if the host crashes, hangs, or is stopped by `--timeout`. On iOS and tvOS, TRX runs in-process by default, without controller-backed recovery.

The controller adds another process and may increase startup time.

Use `--report-trx --trx-mode:in-process` to generate a report without starting a controller for TRX. This can be useful for a launcher that runs tests in a separate application, such as on a device or simulator.

In-process mode cannot recover a report if the test host crashes, hangs, or is terminated. Other extensions or options, such as `--crashdump` or `--timeout`, may still start a controller, but TRX recovery remains disabled.

Use `--trx-mode:out-of-process` to explicitly request controller-backed recovery. This mode is unavailable on iOS and tvOS. Both modes require `--report-trx`; leaving `--trx-mode` unspecified preserves the default behavior.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport.Abstractions](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions): interfaces for extensions interoperating with TRX reports
- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps CI error/warning reporting for test failures

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports#visual-studio-test-reports-trx>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
