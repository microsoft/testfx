# Microsoft.Testing.Platform.MSBuild

Microsoft.Testing.Platform.MSBuild provides the MSBuild tasks needed for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform), including entry-point generation and test configuration file support.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Platform.MSBuild` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Platform.MSBuild
```

## Usage

No manual API call is required. Referencing the package imports its MSBuild integration, which generates the test application entry point and copies `testconfig.json` to the output directory. Test framework packages normally reference this package transitively.

Command-line option argument defaults can be authored directly in `testconfig.json`:

```json
{
  "commandLineOptionDefaults": {
    "report-trx-filename": "{asm}.trx"
  }
}
```

SDKs and shared build infrastructure can supply the same defaults through MSBuild:

```xml
<ItemGroup>
  <TestingPlatformCommandLineOptionDefault Include="report-trx-filename" Value="{asm}.trx" />
</ItemGroup>
```

These defaults are passive: they do not enable `--report-trx` or any other feature. An explicit command-line value or active `commandLineOptions` entry takes precedence. If both `testconfig.json` and MSBuild define the same default, the value in `testconfig.json` wins.

## About

This package provides:

- **Entry-point generation**: generates the required entry point for Microsoft.Testing.Platform test projects
- **Configuration file support**: generates `$(AssemblyName).testconfig.json` from the project configuration and any `TestingPlatformCommandLineOptionDefault` items
- **`dotnet test` compatibility**: enables running MTP-based test projects through the VSTest-based `dotnet test` command on .NET SDKs

This package is typically **not referenced directly**. Instead, test framework packages (such as [MSTest](https://www.nuget.org/packages/MSTest)) reference it automatically.

## Documentation

For this package, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro#use-dotnet-test>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
