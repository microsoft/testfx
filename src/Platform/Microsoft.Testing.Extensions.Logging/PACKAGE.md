# Microsoft.Testing.Extensions.Logging

`Microsoft.Testing.Extensions.Logging` is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that bridges the platform's logging pipeline with [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging), so diagnostic messages produced by the platform and its extensions can flow through any `Microsoft.Extensions.Logging` provider — Console, Debug, Serilog, Application Insights, OpenTelemetry, custom sinks, and more.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.Logging` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Logging
```

You will typically also add a `Microsoft.Extensions.Logging` provider package, e.g.:

```dotnetcli
dotnet add package Microsoft.Extensions.Logging.Console
```

## Usage

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

builder.AddMicrosoftExtensionsLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
```

## About

This package extends Microsoft.Testing.Platform with:

- **Bridge to `Microsoft.Extensions.Logging`**: any MTP diagnostic log message is forwarded to the configured `Microsoft.Extensions.Logging` providers in addition to the platform's built-in `--diagnostic` file logger.
- **Reuse existing logging stacks**: plug Serilog, OpenTelemetry logs, Application Insights, or your own custom `ILoggerProvider` into the testing platform without writing a custom MTP logger provider.

The bridge maps `Microsoft.Testing.Platform.Logging.LogLevel` to `Microsoft.Extensions.Logging.LogLevel`, forwards the same `state`, `exception`, and formatter through to `Microsoft.Extensions.Logging.ILogger.Log`, and respects per-category filter rules defined in the `ILoggingBuilder`.

### Notes and limitations

- **`EventId` is not propagated.** Microsoft.Testing.Platform's `ILogger` has no notion of `EventId`; every forwarded log entry is written with `EventId.None`. Consumers that rely on `EventId`-based filtering downstream of the bridge will not see distinct IDs.
- **Per-category filters can only narrow, never widen.** The bridge initializes the `ILoggingBuilder` with the platform's effective diagnostic level, and the platform itself filters messages before they reach any provider. Setting a more verbose minimum level inside `configure` has no effect.
- **No-op when `--diagnostic` is off.** When the platform's effective `LogLevel` is `None` (the default), the `configure` delegate is not invoked and no `Microsoft.Extensions.Logging.LoggerFactory` is created, so expensive sinks (network, file, gRPC) are not initialized for runs that will never emit a log.
- **`LogAsync` is forwarded synchronously.** `Microsoft.Extensions.Logging` has no async logging API; `ILogger.LogAsync` is forwarded to `ILogger.Log` and returns `Task.CompletedTask`.
- **Owned vs. caller-owned factories.** The `Action<ILoggingBuilder>` overload creates and disposes its own `ILoggerFactory`. The `ILoggerFactory` overload never disposes the caller-supplied instance.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
