# Microsoft.Testing.Extensions.Retry Sample

This sample demonstrates how to use the `Microsoft.Testing.Extensions.Retry` extension to automatically retry failed tests. This is useful for handling flaky tests that might fail intermittently due to timing issues, network conditions, or other transient failures.

## What is Microsoft.Testing.Extensions.Retry?

The Retry extension is a testing platform-level feature that allows you to automatically retry tests that fail. When enabled, if a test fails, the entire test suite will be re-run up to a specified number of times until all tests pass or the maximum retry count is reached.

## Key Features

- Automatically retries failed tests
- Configurable retry count
- Failure threshold policies (max percentage, max tests count)
- Works at the test suite level

## How to Use

### 1. Add the Package Reference

Add the `Microsoft.Testing.Extensions.Retry` package to your project:

```xml
<PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$(TestingPlatformVersion)" />
```

### 2. Register the Retry Provider

In your `Program.cs`, add the retry provider:

```csharp
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => [typeof(Program).Assembly]);

// Add the retry extension
builder.AddRetryProvider();

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
```

### 3. Run Tests with Retry Enabled

Run your tests with the `--retry-failed-tests` command-line option:

```bash
dotnet run --project RetryExtensionSample.csproj -- --retry-failed-tests 3
```

This will retry failed tests up to 3 times.

## Sample Output

When you run the sample, you'll see output similar to:

```
Test execution count: 1
First execution - simulating failure
Tests suite failed, total failed tests: 1, exit code: 2, attempt: 1/4

Test execution count: 2
Test passed on retry!
Tests suite completed successfully in 2 attempts
```

## Command-Line Options

- `--retry-failed-tests <count>`: Sets the maximum number of retry attempts (default: 0, disabled)
- `--retry-failed-tests-max-percentage <percent>`: Sets the maximum percentage of tests that can fail before retries are disabled (default: 100)
- `--retry-failed-tests-max-tests <count>`: Sets the maximum number of tests that can fail before retries are disabled

## Example Commands

Retry up to 3 times:
```bash
dotnet run -- --retry-failed-tests 3
```

Retry up to 3 times, but only if less than 50% of tests fail:
```bash
dotnet run -- --retry-failed-tests 3 --retry-failed-tests-max-percentage 50
```

Retry up to 3 times, but only if less than 5 tests fail:
```bash
dotnet run -- --retry-failed-tests 3 --retry-failed-tests-max-tests 5
```

## When to Use

The retry extension is useful for:
- Handling intermittent network failures
- Dealing with timing-sensitive tests
- Managing tests that depend on external resources
- Working around environmental issues in CI/CD pipelines

## Important Notes

- The retry extension retries the **entire test suite**, not individual tests
- If you need per-test retry logic, consider using the `[Retry]` attribute from MSTest instead
- Set appropriate failure thresholds to avoid masking real issues
- Review retry patterns to identify and fix the root causes of flaky tests
