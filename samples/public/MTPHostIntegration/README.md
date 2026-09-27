# Microsoft Testing Platform host integration

These samples show the same MTP test application running inside application-owned hosting models:

- [`AspNetCoreTests`](AspNetCoreTests) starts a real ASP.NET Core application and runs MSTest tests against its endpoint in the same process.
- [`AspireTests`](AspireTests) uses `Aspire.Hosting.Testing` to start [`AspireAppHost`](AspireAppHost) and verify [`AspireApi`](AspireApi).
- [`ServiceDefaults`](ServiceDefaults) follows the Aspire ServiceDefaults pattern and adds a test-specific variant that subscribes its application-owned OpenTelemetry providers to MTP diagnostics.

Both test executables use the same concise shape:

```csharp
builder.AddTestServiceDefaults();
using IHost host = builder.Build();

return await host.RunTestingPlatformAsync(args, tests =>
{
    tests.AddMSTest(() => [Assembly.GetExecutingAssembly()]);
    tests.AddTestingPlatformDiagnostics();
});
```

The host owns configuration, logging, dependency injection, OpenTelemetry providers, startup, shutdown, and disposal. MTP keeps its dependency-free core and imports only the supported configuration and logging bridges.

## Run

```powershell
dotnet run --project AspNetCoreTests -- --diagnostic
dotnet run --project AspireTests -- --diagnostic
```
