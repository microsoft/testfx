# Microsoft.Testing.Extensions.Retry

Microsoft.Testing.Extensions.Retry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that provides test resilience and transient-fault handling by rerunning failed tests.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.Retry` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Retry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Automatic retry**: automatically re-runs failed tests up to a configurable number of times
- **Retry guards**: can stop retries when failure thresholds are exceeded (`--retry-failed-tests-max-percentage`, `--retry-failed-tests-max-tests`)
- **Retry delay**: optionally wait between retry attempts (`--retry-failed-tests-delay`)
- **Integration-test focus**: intended for scenarios where transient environment issues can cause intermittent failures

## Usage

Configure retry using `--retry-failed-tests <retries>`, and optionally limit retries with `--retry-failed-tests-max-percentage` or `--retry-failed-tests-max-tests`, or add a delay between retries with `--retry-failed-tests-delay` (e.g. `1s`, `2.5m`, `1h`).

This extension restarts the test host for each retry. It is independent from MSTest's in-process `RetryAttribute` and custom `RetryBaseAttribute` implementations. When both mechanisms are enabled, their attempt counts multiply: a test using `[Retry(n)]` can run up to `(n + 1) * (--retry-failed-tests + 1)` times.

When `--report-trx` uses its default filename or a relative `.trx` filename, earlier attempts' TRX reports remain under `Retries/<id>/<attempt>/`, and the final attempt's TRX report is moved to the configured results directory. Collect these attempt reports with `**/*.trx` recursively from that directory to include every physical attempt exactly once. The top-level TRX represents only the final attempt, which may contain just the tests selected for retry; publishing only that file can omit tests that passed earlier. An absolute report filename keeps its explicitly configured location.

If the .NET SDK also performs artifact post-processing and writes an additional merged report, publish either that merged report or its input reports, not both.

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-retry>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
