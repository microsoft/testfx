# Microsoft.Testing.Platform.MSBuild

Microsoft.Testing.Platform.MSBuild provides the MSBuild integration for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). It enables `dotnet test`, Visual Studio Test Explorer and CI pipelines to discover and run tests built with Microsoft.Testing.Platform.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Platform.MSBuild` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Platform.MSBuild) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Platform.MSBuild
```

## About

This package provides:

- **MSBuild integration**: generates the required build assets so that `dotnet test` can discover and run Microsoft.Testing.Platform-based tests
- **Configuration file support**: copies `testconfig.json` from the project into the output directory as `$(AssemblyName).testconfig.json`

This package is typically **not referenced directly**. Instead, test framework packages (such as [MSTest](https://www.nuget.org/packages/MSTest)) reference it automatically.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
