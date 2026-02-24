# Microsoft.Testing.Extensions.Retry

Microsoft.Testing.Extensions.Retry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that provides test resilience and transient-fault handling by rerunning failed tests.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.Retry` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.Retry) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Retry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Automatic retry**: automatically re-runs failed tests up to a configurable number of times
- **Retry guards**: can stop retries when failure thresholds are exceeded (`--retry-failed-tests-max-percentage`, `--retry-failed-tests-max-tests`)
- **Integration-test focus**: intended for scenarios where transient environment issues can cause intermittent failures

Configure retry using `--retry-failed-tests <retries>`, and optionally limit retries with `--retry-failed-tests-max-percentage` or `--retry-failed-tests-max-tests`.

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-policy#retry>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
