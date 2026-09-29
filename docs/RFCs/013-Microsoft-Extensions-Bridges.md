# RFC 013 - Microsoft.Extensions.* Bridges for Microsoft.Testing.Platform

- [x] Approved in principle
- [ ] Under discussion
- [x] Implementation (Logging and Configuration bridges)
- [ ] Shipped

## Summary

Microsoft.Testing.Platform (MTP) intentionally ships zero-dependency abstractions for logging, configuration, dependency injection, and hosting. This RFC defines an architectural pattern and the first concrete deliverable for **opt-in side-package bridges** that let users interoperate with the `Microsoft.Extensions.*` ecosystem (`Microsoft.Extensions.Logging`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`).

The first bridges delivered with this RFC are:

- `Microsoft.Testing.Extensions.Logging`, which forwards MTP's diagnostic logs to any `Microsoft.Extensions.Logging` provider.
- `Microsoft.Testing.Extensions.Configuration`, which imports a build-time snapshot of an externally owned `Microsoft.Extensions.Configuration.IConfiguration`.

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
| 2 | `Microsoft.Testing.Extensions.Configuration` | This RFC |
| 3 | `Microsoft.Testing.Extensions.Hosting` | Experimental |
| 4 | `Microsoft.Testing.Extensions.DependencyInjection` | Deferred; no container conversion is planned |

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

## Detailed design — `Microsoft.Testing.Extensions.Configuration`

The Configuration bridge operates only from an externally owned
`Microsoft.Extensions.Configuration.IConfiguration` into the MTP configuration pipeline. It captures
a read-only hierarchical snapshot while the test application is built. It does not expose MTP
configuration as `Microsoft.Extensions.Configuration.IConfiguration`, does not synthesize reload
tokens, and does not claim `IOptionsMonitor` support.

The default source order is `2`: command-line (`0`) and environment (`1`) values win, while the
external snapshot wins over `testconfig.json` (`3`). The caller can select another order explicitly.
MTP does not dispose the external configuration, and the live object is not propagated across process
boundaries.

## Detailed design — `Microsoft.Testing.Extensions.Hosting`

The Hosting bridge provides one experimental `IHost.RunTestingPlatformAsync` entry point. The outer
host owns its service provider, configuration, logging, OpenTelemetry providers, and disposal. The
helper imports only the supported configuration snapshot and logging factory, starts the host before
building MTP, returns MTP's exit code, and stops the host in a `finally` block.

MTP's MSBuild package can generate this composition when `TestingPlatformHostFactory` names a
parameterless static `Task<IHost>` factory. Generated code owns and disposes the fresh unstarted host
(preferring `IAsyncDisposable`),
while the bridge remains responsible for start/stop ordering. The factory does not receive MTP
arguments. C#, Visual Basic, and F# use the same contract, and the existing standalone generated
source remains unchanged when the property is unset.

Direct and response-file `--help`, `-?`, and `--info` options are parsed before invoking the factory.
Configuration-only informational options are resolved later, after the factory-created host supplies
its configuration snapshot; factories therefore must not start services or perform application work
during construction.

Because the same host factory is used for console, JSON listing, server, and `dotnet test` modes, the
host contract requires stdout-silent logging, hosted services, and exporters. Stdout remains reserved
for MTP protocol payloads; hosted applications should use OTLP, files, or another non-stdout sink.

The current generated contract is process-local rather than process-role-aware. Extensions that
restart the test process can invoke the factory in both the controller and child. Deferring factory
creation until MTP selects the final process role requires a future pre-build lifecycle seam; until
then, factories combined with restart extensions must avoid exclusive global resources.

`TestingPlatformOpenTelemetryMode=HostOwned` causes the OpenTelemetry extension package to contribute
one normal `TestingPlatformBuilderHook` that calls only `AddTestingPlatformDiagnostics()`. The host
continues to own the only providers, exporters, resource identity, and dependency-injection container.
The focused test/CI resource helpers remain the recommended composition.

`TestApplicationOptions.CancellationToken` is the compatible core execution contract: it adds one
property to the existing options class instead of adding a source-breaking member to
`ITestApplication`. The token is registered only for the built application's execution lifetime and
the registration is disposed when `RunAsync` completes or the application is disposed.

Host stopping is connected to that contract through a bounded internal,
zero-Microsoft.Extensions lifetime bridge. `RunTestingPlatformAsync` supplies its caller token through
`TestApplicationOptions`, `IHostApplicationLifetime.ApplicationStopping` requests the same MTP
cooperative cancellation, and the helper stops the host after MTP cleanup completes. MTP cancellation
does not call `StopApplication`. The bridge disconnects before the helper's final `StopAsync` so that
normal host shutdown cannot re-enter MTP cancellation after the run has ended.

Caller cancellation, `ApplicationStopping`, Ctrl+C, `--timeout`, controller cancellation, and
test-framework stop policies converge on the existing application cancellation source. They have no
priority ordering: the first signal starts the idempotent shutdown path, cleanup is preserved, and a
canceled active run returns `TestSessionAborted` (`3`). Cancellation during the host's initial
`StartAsync` remains governed by the host contract and can throw `OperationCanceledException`.

The same application token already drives controller and orchestrator cancellation. In controller
mode it requests cooperative child cancellation over the existing control channel, with the existing
bounded termination fallback for a non-cooperative child. No `Microsoft.Extensions.*` dependency is
added to `Microsoft.Testing.Platform`.

Run, build, or framework failures remain primary. The helper disconnects cancellation callbacks before
its uncancelled host shutdown attempt, and a shutdown failure is attached as secondary exception data
(or aggregated when exception data is unavailable) instead of replacing the earlier failure.

It intentionally does not expose MTP as an `IHostedService`, build a second Microsoft.Extensions
container, dispose the caller's host, or imply that live services cross process boundaries. The API
remains experimental while cancellation and out-of-process behavior are evaluated.

The `samples/public/MTPOTel` and `samples/public/MTPHostIntegration` examples demonstrate the same
composition pattern with a generic host, ASP.NET Core, and Aspire ServiceDefaults.

`RunTestingPlatformAsync` also applies caller-owned `ITestingPlatformBuilderConfigurator` services
before its explicit configuration callback. This is a narrow Hosting-side composition seam rather
than an MTP service-container conversion. `MSTest.Extensions.Hosting` uses it to opt MSTest into
test-class activation from the host provider without introducing a Microsoft.Extensions dependency
into MTP or the MSTest execution core.

The MSTest adapter creates one `IServiceScope` per individual test invocation, including data rows and
retry attempts. `TestInitialize`, the test method, and `TestCleanup` share the same instance and scope;
static class lifecycle methods remain outside those scopes. After `TestCleanup`, the activation lease
disposes the test instance and then the scope exactly once. The root provider and host remain
caller-owned. .NET Framework runs must disable AppDomain isolation because live service scopes cannot
cross an AppDomain boundary.

## Future work (not in this RFC's deliverable)

- **Direction B** adapter (MTP `ILoggerFactory` exposed as `Microsoft.Extensions.Logging.ILoggerFactory`).
- **Additional framework adapters** — reuse the narrow Hosting configurator pattern only when a test
  framework can define deterministic construction and cleanup ownership. Do not convert the MTP
  registry into `IServiceCollection`, implicitly build a second container, or transfer root-provider
  disposal ownership.
- **Command-line tooling** — the existing machine-readable `dotnet test` option message includes
  provider identity and minimum/maximum arity in addition to name, description, visibility, and
  built-in status. This provides tooling metadata without a runtime `System.CommandLine` bridge.
