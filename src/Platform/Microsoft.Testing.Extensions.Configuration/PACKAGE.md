# Microsoft.Testing.Extensions.Configuration

`Microsoft.Testing.Extensions.Configuration` lets a test application contribute values from an externally owned [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions) configuration to [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform).

The adapter is intentionally one-way: it takes a snapshot of an existing `Microsoft.Extensions.Configuration.IConfiguration` when the test application is built and exposes those values through MTP's configuration system.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Configuration
```

## Usage

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMicrosoftExtensionsConfigurationSnapshot(configuration);

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
```

By default, the snapshot has order `2`. MTP command-line values (order `0`) and environment variables (order `1`) take precedence over it, while it takes precedence over `testconfig.json` (order `3`). Pass a different order when another precedence is required.

### Snapshot semantics

- The configuration is enumerated when `ITestApplicationBuilder.BuildAsync` builds the MTP configuration pipeline.
- Changes and reload notifications after that point are not propagated.
- Hierarchical keys are preserved using the standard `:` delimiter.
- The supplied configuration remains owned by the caller and is never disposed by MTP.
- Null-valued container keys expose their children but do not hide a scalar value from a lower-priority MTP source. A null-valued leaf does hide lower-priority values.
- Registration is process-local. A live configuration object cannot cross into separately launched test host or controller processes.

The package does not make MTP configuration implement `Microsoft.Extensions.Configuration.IConfiguration`, does not provide `IOptionsMonitor` reload behavior, and does not add Microsoft.Extensions dependencies to the MTP core.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
