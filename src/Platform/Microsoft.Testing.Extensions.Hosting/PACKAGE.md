# Microsoft.Testing.Extensions.Hosting

`Microsoft.Testing.Extensions.Hosting` runs [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) inside an application-owned [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions) host.

The host remains the composition root and owns its service provider, configuration, logging, OpenTelemetry providers, lifetime, and disposal. The extension starts and stops the host around the test run but never disposes it.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Hosting
dotnet add package Microsoft.Extensions.Hosting
dotnet add package MSTest
```

## Generated entry point

MSTest.Sdk and other MTP projects can generate the hosting glue instead of maintaining a custom `Program.cs`:

```xml
<PropertyGroup>
  <TestingPlatformHostFactory>Contoso.Tests.TestHost.CreateHost</TestingPlatformHostFactory>
  <TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>
</PropertyGroup>
```

The configured method has this V1 contract:

```csharp
public static Task<IHost> CreateHost();
```

It returns one fresh, unstarted host. It does not receive MTP command-line arguments. Generated code owns and disposes the host (preferring `IAsyncDisposable` when implemented), invokes the existing `RunTestingPlatformAsync` bridge, and preserves the generated `AddSelfRegisteredExtensions(builder, args)` registration exactly once. Help and info options supplied on the process command line or through response files do not invoke the factory.

`TestingPlatformOpenTelemetryMode=HostOwned` requires `Microsoft.Testing.Extensions.OpenTelemetry`. It activates only the MTP diagnostics producer; the host remains the sole owner of providers, exporters, resource identity, and disposal.

The factory must configure a host that never writes startup, shutdown, logging, or exporter output to standard output. MTP reserves stdout for machine-readable modes such as `--list-tests json`, server JSON-RPC, and the `dotnet test` protocol. Remove default console logging providers and use OTLP, files, or another non-stdout sink.

Help or info activated only through configuration is resolved while the MTP application is built, after the factory-created host supplies its configuration snapshot. In that uncommon case the factory is invoked, but the host contract still requires construction to be side-effect-free until `StartAsync`.

## Manual usage

```csharp
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();

using IHost host = builder.Build();
return await host.RunTestingPlatformAsync(args, testApplication =>
{
    testApplication.AddMSTest(() => [Assembly.GetExecutingAssembly()]);
});
```

The process command line belongs to MTP in this composition. Configure the host through code,
environment variables, or application configuration rather than passing the same argument array
to both parsers.

`RunTestingPlatformAsync`:

1. Creates an MTP application builder.
2. Imports a read-only snapshot of the host's `IConfiguration`.
3. Forwards MTP diagnostic logs to the host-owned `ILoggerFactory`.
4. Applies each caller-owned `ITestingPlatformBuilderConfigurator` registered in the host service provider.
5. Invokes the caller's MTP registration callback, which therefore has final configuration precedence.
6. Starts the host before building MTP, so host-owned observability providers can subscribe first.
7. Links the caller token and `IHostApplicationLifetime.ApplicationStopping` to MTP's cooperative cancellation path.
8. Runs MTP and returns its exit code.
9. Stops the host in a `finally` block.

### Ownership and limitations

- The caller owns and disposes the `IHost`.
- Builder configurators are borrowed from `host.Services`; the Hosting extension never disposes them separately.
- The extension does not create a second Microsoft.Extensions dependency-injection container.
- Imported configuration has snapshot semantics and does not propagate reloads after the MTP application is built.
- Composition is process-local. Live services do not cross into separately launched test host or controller processes.
- Process-restart extensions (for example retry, crash dump, or hang dump scenarios) execute the generated entry point in each process. Until MTP exposes a role-aware pre-build hook, the factory can therefore be invoked in both the controller and child process; factories used with those extensions must avoid exclusive global resources such as fixed ports.
- The cancellation token controls host startup and active MTP execution. Cancellation during `StartAsync` follows the host contract and can throw `OperationCanceledException`; once MTP is running, cancellation uses MTP's cooperative path and returns the test-session-aborted exit code (`3`) after cleanup.
- When the host provides `IHostApplicationLifetime`, `ApplicationStopping` is linked to the same MTP cancellation source. Caller cancellation, host stopping, Ctrl+C, `--timeout`, controller cancellation, and test framework stop policies are peer signals; the first signal starts the same idempotent MTP shutdown path. MTP cancellation does not call `StopApplication`; the helper stops the host after MTP cleanup completes.
- In controller mode, cancellation uses MTP's existing controller control channel to request cooperative cancellation in the active test host process. Existing bounded termination remains the fallback for a non-cooperative child.
- Cancellation registrations are removed when execution completes. The lifetime bridge disconnects before the helper's own `StopAsync`, and shutdown uses an uncancelled token so graceful cleanup is still attempted without re-entering MTP cancellation.
- Exceptions from host startup, MTP construction/execution, and host shutdown are surfaced to the caller.
  When an operation and its cleanup both fail, the operation remains the primary exception and the cleanup
  exception is attached to its `Data` dictionary. If that dictionary cannot be updated, an
  `AggregateException` exposes the operation exception first and the cleanup exception second.

See the runnable ASP.NET Core and Aspire ServiceDefaults samples under `samples/public/MTPHostIntegration`.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
