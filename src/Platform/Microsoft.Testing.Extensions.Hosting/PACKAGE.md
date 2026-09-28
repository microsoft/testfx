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

It returns one fresh, unstarted host. It does not receive MTP command-line arguments. Generated code owns and disposes the host, invokes the existing `RunTestingPlatformAsync` bridge, and preserves the generated `AddSelfRegisteredExtensions(builder, args)` registration exactly once. Help and info options supplied on the process command line or through response files do not invoke the factory.

`TestingPlatformOpenTelemetryMode=HostOwned` requires `Microsoft.Testing.Extensions.OpenTelemetry`. It activates only the MTP diagnostics producer; the host remains the sole owner of providers, exporters, resource identity, and disposal.

The factory must configure a host that never writes startup, shutdown, logging, or exporter output to standard output. MTP reserves stdout for machine-readable modes such as `--list-tests json`, server JSON-RPC, and the `dotnet test` protocol. Remove default console logging providers and use OTLP, files, or another non-stdout sink.

Help or info activated only through configuration is resolved while the MTP application is built, after the factory-created host supplies its configuration snapshot. In that uncommon case the factory is invoked, but the host contract still requires construction to be side-effect-free until `StartAsync`.

## Manual usage

```csharp
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

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
4. Invokes the caller's MTP registration callback.
5. Starts the host before building MTP, so host-owned observability providers can subscribe first.
6. Runs MTP and returns its exit code.
7. Stops the host in a `finally` block.

### Ownership and limitations

- The caller owns and disposes the `IHost`.
- The extension does not create a second Microsoft.Extensions dependency-injection container.
- Imported configuration has snapshot semantics and does not propagate reloads after the MTP application is built.
- Composition is process-local. Live services do not cross into separately launched test host or controller processes.
- The cancellation token controls host startup. Shutdown uses an uncancelled token so graceful cleanup is still attempted.
- When the host provides `IHostApplicationLifetime`, `ApplicationStopping` is linked to MTP's existing cooperative cancellation path and MTP cancellation requests host stopping. Custom `IHost` implementations without that optional service retain the previous start/run/stop behavior.
- Exceptions from host startup, MTP construction/execution, and host shutdown are surfaced to the caller.
  When an operation and its cleanup both fail, the operation remains the primary exception and the cleanup
  exception is attached to its `Data` dictionary. If that dictionary cannot be updated, an
  `AggregateException` exposes the operation exception first and the cleanup exception second.

See the runnable ASP.NET Core and Aspire ServiceDefaults samples under `samples/public/MTPHostIntegration`.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
