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

On platforms that can launch a test-host process (all except Android, browser, iOS, tvOS, and WASI), TRX uses controller-backed recovery by default: the test host still streams results and generates the report during normal execution, but a surviving controller process can recover completed results into a TRX report if the test host crashes, hangs, or is stopped by `--timeout`. Android, browser, iOS, tvOS, and WASI cannot launch a test-host process, so TRX automatically falls back to its original in-process implementation there — no controller-backed recovery is attempted, and no configuration is required to get this fallback.

The extra process has a measurable startup cost, which varies by target framework: for a trivial single-test run, launching the controller added no statistically measurable overhead on .NET (differences were within normal process-launch noise) but added roughly 700-800ms on .NET Framework in local measurements. Weigh this against the reliability benefit for your scenario, especially on .NET Framework or in tight inner-loop test runs.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport.Abstractions](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions): interfaces for extensions interoperating with TRX reports
- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps CI error/warning reporting for test failures

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports#visual-studio-test-reports-trx>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
