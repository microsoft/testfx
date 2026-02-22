# Microsoft.Testing.Extensions.Retry

Microsoft.Testing.Extensions.Retry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that provides a retry mechanism for failed tests, helping identify and manage flaky tests.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.Retry` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.Retry) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Retry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Automatic retry**: automatically re-runs failed tests up to a configurable number of times
- **Flaky test management**: helps distinguish between genuinely failing tests and intermittently flaky tests
- **CI resilience**: improves reliability of CI pipelines by reducing false failures caused by transient issues

Configure retry using `--retry-failed-tests <retries>`, and optionally limit retries with `--retry-failed-tests-max-percentage` or `--retry-failed-tests-max-tests`.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
