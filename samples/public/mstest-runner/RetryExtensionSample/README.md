# Microsoft.Testing.Extensions.Retry Sample

This sample demonstrates how to use the `Microsoft.Testing.Extensions.Retry` extension to automatically retry failed tests. This is useful for handling flaky tests that might fail intermittently due to timing issues, network conditions, or other transient failures.

## What is Microsoft.Testing.Extensions.Retry?

The Retry extension is a testing platform-level feature that allows you to automatically retry tests that fail. When enabled, if a test fails, the entire test suite will be re-run up to a specified number of times until all tests pass or the maximum retry count is reached. This extension is compatible with any test framework that supports Microsoft.Testing.Platform, and is not specific to MSTest.

### Important: Difference from `[Retry]` Attribute

1. **`Microsoft.Testing.Extensions.Retry`** (this sample): A **platform-level** extension that retries the **entire test suite** when any test fails. Activated via `--retry-failed-tests` command-line option.
2. **`[Retry]` attribute**: A **framework-level** attribute that retries individual test methods. Applied directly to test methods like `[TestMethod]` and `[Retry(3)]`. This is specific to MSTest.

Use the platform-level retry extension when you want to handle environment-level failures that affect multiple tests. Use the `[Retry]` attribute when specific test methods are known to be flaky.

## Key Features

- Automatically retries failed tests
- Configurable retry count
- Failure threshold policies (max percentage, max tests count)
- Works at the test suite level

## How to Use

### 1. Add the Package Reference

Add the `Microsoft.Testing.Extensions.Retry` package to your project:

```xml
<PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$(MicrosoftTestingPlatformVersion)" />
```

### 2. Run Tests with Retry Enabled

Run your tests with the `--retry-failed-tests` command-line option:

```bash
dotnet test --project RetryExtensionSample.csproj --retry-failed-tests 3
```

This will retry failed tests up to 3 times.
