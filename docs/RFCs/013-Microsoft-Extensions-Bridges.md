# RFC 013 - Microsoft.Extensions.* Bridges for Microsoft.Testing.Platform

- [x] Approved in principle
- [ ] Under discussion
- [x] Implementation (Logging bridge)
- [ ] Shipped

## Summary

Microsoft.Testing.Platform (MTP) intentionally ships zero-dependency abstractions for logging, configuration, dependency injection, and hosting. This RFC defines an architectural pattern and the first concrete deliverable for **opt-in side-package bridges** that let users interoperate with the `Microsoft.Extensions.*` ecosystem (`Microsoft.Extensions.Logging`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`).

The first bridge delivered with this RFC is `Microsoft.Testing.Extensions.Logging`, which forwards MTP's diagnostic logs to any `Microsoft.Extensions.Logging` provider (Console, Debug, Serilog, Application Insights, OpenTelemetry, etc.).

## Motivation

MTP today ships its own slim implementations of:

| Concern | MTP namespace | Equivalent in BCL ecosystem |
| --- | --- | --- |
| Logging | `Microsoft.Testing.Platform.Logging` | `Microsoft.Extensions.Logging` |
| Configuration | `Microsoft.Testing.Platform.Configurations` | `Microsoft.Extensions.Configuration` |
| Service location | `Microsoft.Testing.Platform.Services.ServiceProvider` | `Microsoft.Extensions.DependencyInjection` |
| Host lifecycle | `Microsoft.Testing.Platform.Hosts` | `Microsoft.Extensions.Hosting` |

This is deliberate: MTP must remain trim/AOT-friendly, must target `netstandard2.0`, and must not impose a particular DI/logging stack on test authors or extension authors. A test framework or test app must be able to use Microsoft.Testing.Platform without pulling any `Microsoft.Extensions.*` package into the closure of its dependencies.

However, real-world test apps frequently want to:

- Stream MTP's diagnostic output through an existing logging pipeline (Serilog/Seq, Application Insights, OTLP, the in-IDE Debug window, a custom in-memory sink in a test).
- Reuse the same `IConfiguration` they already build for the system under test.
- Plug an `IHostedService` into the test session lifecycle (similar to `WebApplicationFactory`).
- Consume `Microsoft.Extensions.Logging.ILogger<T>` from inside their fixtures.

The current "homegrown core + opt-in bridge extensions" pattern satisfies both constraints.

## Design principles

1. **Core stays dep-free.** `Microsoft.Testing.Platform` and the existing extensions (`Microsoft.Testing.Extensions.TrxReport`, `…CrashDump`, `…HangDump`, `…Telemetry`, `…HotReload`, `…Retry`, `…OpenTelemetry`, etc.) do not take a `PackageReference` on any `Microsoft.Extensions.*` package as a side effect of a bridge existing.
2. **Bridges are additive.** A bridge package never replaces a homegrown subsystem. Example: a future Configuration bridge does not replace MTP's vendored `JsonConfigurationFileParser`; it provides an additional `IConfigurationSource` on top of it.
3. **Bridges are end-user surface.** They are referenced only by application code (the test host `Program.cs` or a test framework that explicitly chooses the dependency). They are never transitive prerequisites of any existing MTP package.
4. **Bridges follow the established extension shape.** Same project layout, `BannedSymbols.txt`, `PublicAPI/*.txt`, `PACKAGE.md`, and `[TPEXP]` annotation as `Microsoft.Testing.Extensions.OpenTelemetry`.
5. **Honest about gaps.** Where the BCL contract is richer than the MTP contract (e.g. `EventId`, `BeginScope`, async logging), the bridge documents the mapping rather than inventing capability.

## Phasing

| Phase | Package | Status |
| --- | --- | --- |
| 1 | `Microsoft.Testing.Extensions.Logging` | This RFC |
| 2 | `Microsoft.Testing.Extensions.Configuration` | Future |
| 3 | `Microsoft.Testing.Extensions.DependencyInjection` | Future |
| 4 | `Microsoft.Testing.Extensions.Hosting` | Future |

## Detailed design — `Microsoft.Testing.Extensions.Logging`

### Goal

Let a user write something like:

```csharp
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

builder.AddMicrosoftExtensionsLogging(logging =>
{
    logging.AddConsole();          // any Microsoft.Extensions.Logging provider
    logging.AddDebug();
    // logging.AddSerilog(...);
    // logging.AddApplicationInsights(...);
});
```

…and have every diagnostic message that MTP and its extensions write flow through those providers, in addition to (not in place of) MTP's own `--diagnostic` file logger.

### API

The extension surface is a single `[TPEXP]` static class with two extension methods on `ITestApplicationBuilder`:

```csharp
namespace Microsoft.Testing.Extensions;

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class MicrosoftExtensionsLoggingBuilderExtensions
{
    // Builds and owns a new MEL LoggerFactory.
    public static ITestApplicationBuilder AddMicrosoftExtensionsLogging(
        this ITestApplicationBuilder builder,
        Action<Microsoft.Extensions.Logging.ILoggingBuilder> configure);

    // Forwards to a caller-owned LoggerFactory.
    public static ITestApplicationBuilder AddMicrosoftExtensionsLogging(
        this ITestApplicationBuilder builder,
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory);
}
```

### Adapter pipeline

Internally:

```text
MTP ILoggingManager.AddProvider(factoryFunc)
        │
        ▼
MicrosoftExtensionsLoggingProvider : MTP.ILoggerProvider, IDisposable
        │     (wraps a MEL.ILoggerFactory)
        ▼
MicrosoftExtensionsLoggerAdapter : MTP.ILogger
        │     (wraps a MEL.ILogger)
        ▼
Microsoft.Extensions.Logging.LoggerFactory → user's MEL providers
```

### Semantic mapping

| MTP concept | Mapped MEL concept | Notes |
| --- | --- | --- |
| `LogLevel.Trace…Critical/None` (0..6) | `Microsoft.Extensions.Logging.LogLevel.Trace…Critical/None` | Same numeric values; mapped via explicit `switch` for safety/AOT |
| `ILogger.Log<TState>(level, state, ex, formatter)` | `ILogger.Log<TState>(level, EventId.None, state, ex, formatter)` | `EventId` defaults to `None` |
| `ILogger.LogAsync<TState>(...)` | Synchronous `Log` + `Task.CompletedTask` | MEL has no async API |
| `ILogger.IsEnabled(level)` | `ILogger.IsEnabled(level)` | Forwards; MEL's per-category filter still applies |
| Category name | Category name | Pass-through string |
| Provider disposal | `LoggerFactory.Dispose()` | Owned factories disposed; caller-owned ones not |

### Filtering interaction

MTP's `Logger.Log` already calls `logger.IsEnabled(level)` per child provider in `Logger.cs:18` before forwarding. The MEL adapter forwards `IsEnabled` to the underlying MEL logger, so MEL's per-category filter rules continue to apply on top of MTP's coarse global level. There is **no double-filter bug**.

### What is intentionally not done

- **No new CLI options.** Configuration is purely programmatic.
- **No replacement of the built-in file logger.** `--diagnostic` continues to produce its `.diag` file when the user enables it; the bridge adds *additional* sinks.
- **No MTP-side `ILogger` exposure as a MEL `ILogger`.** Direction B (test code consuming `Microsoft.Extensions.Logging.ILogger<T>` and having it write through MTP) is omitted from the v1 shipment. It will be added once we have a concrete consumer scenario, to avoid speculative public API.
- **No MSBuild auto-registration hook.** The user must opt in with a `configure` delegate, so the MSBuild-based `TestingPlatformBuilderHook` pattern (used by `…HotReload`) does not apply.

### Package metadata

- **TFMs**: `netstandard2.0;net8.0;net9.0` (matches `$(SupportedNetFrameworks)`).
- **Version**: `1.0.0-alpha.*` (mirrors `Microsoft.Testing.Extensions.OpenTelemetry`).
- **Dependencies**: `Microsoft.Extensions.Logging` (drags `Microsoft.Extensions.Logging.Abstractions` transitively).
- **Trim/AOT**: `IsTrimmable=true`, `IsAotCompatible=true` (inherited).

### Risks

1. The bridge depends on the still-`[TPEXP]` `ILoggingManager` API. Acceptable: the bridge is itself `[TPEXP]`.
2. `EventId` is dropped. Users who rely on `EventId`-based filtering downstream of the bridge will lose that fidelity; documented.
3. `LogAsync` becomes synchronous when going through a MEL provider that performs blocking I/O (Console, plain file). MEL has no async API; this is a known and intrinsic limitation.

## Future work (not in this RFC's deliverable)

- **Direction B** adapter (MTP `ILoggerFactory` exposed as `Microsoft.Extensions.Logging.ILoggerFactory`).
- **`Microsoft.Testing.Extensions.Configuration`** — wrap `Microsoft.Extensions.Configuration.IConfiguration` as an MTP `IConfigurationSource`. Additive; does not retire the vendored `JsonConfigurationFileParser`.
- **`Microsoft.Testing.Extensions.DependencyInjection`** — expose MTP services in an `IServiceCollection`/`IServiceProvider`.
- **`Microsoft.Testing.Extensions.Hosting`** — `IHostedService` lifecycle bound to the test session.
