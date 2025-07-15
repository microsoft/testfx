# Custom Help Output Capability

The Microsoft Testing Platform now supports customizing the `--help` output through the `IHelpMessageOwnerCapability` interface, similar to how banner customization works through `IBannerMessageOwnerCapability`.

## Overview

This capability allows test frameworks to provide their own help content when users run `--help`, giving them complete control over the user experience and documentation.

## Usage

### 1. Implement the Help Capability

```csharp
using Microsoft.Testing.Platform.Capabilities.TestFramework;

public class MyCustomHelpCapability : IHelpMessageOwnerCapability
{
    public Task<string?> GetHelpMessageAsync()
    {
        return Task.FromResult<string?>("""
MyTestFramework v2.1.0

Usage: myapp.exe [options]

Options:
  --test-file <path>      Path to test file
  --config <file>         Configuration file
  --parallel              Run tests in parallel
  --help                  Show this help

Examples:
  myapp.exe --test-file tests.xml
  myapp.exe --config myconfig.json --parallel

For more information: https://mytestframework.com/docs
""");
    }
}
```

### 2. Register with Test Framework Capabilities

```csharp
public class MyTestFrameworkCapabilities : ITestFrameworkCapabilities
{
    private readonly IHelpMessageOwnerCapability _helpCapability = new MyCustomHelpCapability();

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => new[] { _helpCapability };

    public T? GetCapability<T>() where T : ITestFrameworkCapability
    {
        foreach (var capability in Capabilities)
        {
            if (capability is T match)
                return match;
        }
        return default;
    }
}
```

### 3. Register Test Framework

```csharp
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(
    _ => new MyTestFrameworkCapabilities(),
    (_, _) => new MyTestFramework());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
```

## Behavior

- When `--help` is invoked and a custom help capability is registered, the platform will call `GetHelpMessageAsync()`
- If the method returns a non-null, non-empty string, that content is displayed instead of the default help
- If the method returns `null` or an empty string, the platform falls back to its default help behavior
- If no `IHelpMessageOwnerCapability` is registered, the default help is always shown

## Integration with Existing Features

This capability works alongside other platform features:

- **Custom Banner**: You can have both custom banner (`IBannerMessageOwnerCapability`) and custom help
- **Custom Output Device**: Custom help respects the configured output device
- **Command Line Options**: Your custom help can document framework-specific command line options

## Example Scenarios

1. **Framework-specific help**: Show help content specific to your test framework's features and options
2. **Branded experience**: Provide a consistent branded help experience matching your framework's documentation
3. **Context-aware help**: Generate dynamic help content based on the current environment or configuration
4. **Simplified help**: Show a simplified help message for end users while keeping the full platform help available through other means

This capability provides test framework authors the flexibility to create a tailored user experience for their specific tooling while leveraging the Microsoft Testing Platform infrastructure.
