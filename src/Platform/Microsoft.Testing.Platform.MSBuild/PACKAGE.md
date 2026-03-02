# Microsoft.Testing.Platform.MSBuild

Microsoft.Testing.Platform.MSBuild provides the MSBuild integration for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform), including entry-point generation, test configuration file support, and compatibility with `dotnet test`.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Platform.MSBuild` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Platform.MSBuild) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Platform.MSBuild
```

## About

This package provides:

- **Entry-point generation**: generates the required entry point for Microsoft.Testing.Platform test projects
- **Configuration file support**: copies `testconfig.json` from the project into the output directory as `$(AssemblyName).testconfig.json`
- **`dotnet test` compatibility**: enables running MTP-based test projects through the VSTest-based `dotnet test` command on .NET 8/9 SDKs

This package is typically **not referenced directly**. Instead, test framework packages (such as [MSTest](https://www.nuget.org/packages/MSTest)) reference it automatically.

## Documentation

For this package, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro#use-dotnet-test>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
