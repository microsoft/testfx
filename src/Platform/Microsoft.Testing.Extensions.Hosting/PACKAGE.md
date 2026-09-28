# Microsoft.Testing.Extensions.Hosting

`Microsoft.Testing.Extensions.Hosting` runs [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) inside an application-owned [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions) host.

The host remains the composition root and owns its service provider, configuration, logging, OpenTelemetry providers, lifetime, and disposal. The extension starts and stops the host around the test run but never disposes it.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Hosting
dotnet add package Microsoft.Extensions.Hosting
dotnet add package MSTest
```

## Usage

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
- `ITestApplication.RunAsync` has no cancellation-token overload, so the token does not cancel the MTP run.
- Exceptions from host startup, MTP construction/execution, and host shutdown are surfaced to the caller.
  When an operation and its cleanup both fail, the operation remains the primary exception and the cleanup
  exception is attached to its `Data` dictionary.

See the runnable ASP.NET Core and Aspire ServiceDefaults samples under `samples/public/MTPHostIntegration`.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
