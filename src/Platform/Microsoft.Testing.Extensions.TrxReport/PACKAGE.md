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

On macOS, Windows, Linux, and other platforms that support a controller process, TRX uses one by default. The test host generates the report during normal execution; the controller can recover completed results if the host crashes, hangs, or is stopped by `--timeout`. On Android, iOS, Mac Catalyst, tvOS, WASI, and in browsers, TRX always runs in-process, without controller-backed recovery.

The controller adds another process and may increase startup time.

Use `--report-trx --trx-mode:in-process` to generate a report without starting a controller for TRX. This can be useful when a launcher runs tests in a separate app, for example on a device or simulator.

In-process mode cannot recover a report if the test host crashes, hangs, or is terminated. Other options, such as `--crashdump` or `--hangdump`, may still start a controller, but TRX recovery remains disabled.

Use `--trx-mode:out-of-process` to explicitly request controller-backed recovery. This mode is unavailable on Android, iOS, Mac Catalyst, tvOS, WASI, and in browsers. Both modes require `--report-trx`; leaving `--trx-mode` unspecified preserves the default behavior.

To make in-process mode the passive default, set `"commandLineOptionDefaults": { "trx-mode": "in-process" }` in `testconfig.json`. It takes effect only with `--report-trx`, and an explicit `--trx-mode` overrides it.

The controller passes its selected mode to the test host, so a custom launcher can use a child application with different configuration defaults. Configured run defaults remain passive when invoking `merge-trx` or `ms-trxcompare`.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport.Abstractions](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions): interfaces for extensions interoperating with TRX reports
- [Microsoft.Testing.Extensions.AzureDevOpsReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureDevOpsReport): Azure DevOps CI error/warning reporting for test failures

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports#visual-studio-test-reports-trx>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
