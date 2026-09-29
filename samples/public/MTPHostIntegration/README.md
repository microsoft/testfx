# Microsoft Testing Platform host integration

These samples show the same MTP test application running inside application-owned hosting models:

- [`AspNetCoreTests`](AspNetCoreTests) starts a real ASP.NET Core application and runs MSTest tests against its endpoint in the same process.
- [`AspireTests`](AspireTests) uses `Aspire.Hosting.Testing` to start [`AspireAppHost`](AspireAppHost) and verify [`AspireApi`](AspireApi).
- [`ServiceDefaults`](ServiceDefaults) follows the Aspire ServiceDefaults pattern and adds a test-specific variant that subscribes its application-owned OpenTelemetry providers to MTP diagnostics.

Both test executables use the generated hosted entry point:

```xml
<PropertyGroup>
  <TestingPlatformHostFactory>MyTestHost.CreateHost</TestingPlatformHostFactory>
  <TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>
</PropertyGroup>
```

The factory returns a fresh, unstarted `Task<IHost>` and does not receive MTP arguments. Generated self-registration supplies MSTest and the HostOwned diagnostics hook exactly once; no manual `AddMSTest` or `AddTestingPlatformDiagnostics` call is needed. The host owns configuration, logging, dependency injection, OpenTelemetry providers, startup, shutdown, and disposal. The sample clears default console logging and uses OTLP only when configured so JSON listing, server, and `dotnet test` protocol output remains uncorrupted.

Both test projects opt into host-owned MSTest test-class activation:

```xml
<EnableMSTestHostTestClassInjection>true</EnableMSTestHostTestClassInjection>
```

```csharp
builder.Services.AddMSTestTestClassInjection();
```

The ASP.NET Core and Aspire tests receive application-host services through their constructors. The integration creates one dependency-injection scope per test invocation and leaves the host/root provider caller-owned.

## Run

```powershell
dotnet run --project AspNetCoreTests -- --diagnostic
dotnet run --project AspireTests -- --diagnostic
```
