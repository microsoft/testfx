# Microsoft.Testing.Platform

Microsoft.Testing.Platform is a lightweight and portable alternative to [VSTest](https://github.com/microsoft/vstest) for running tests in command line, in continuous integration (CI) pipelines, in Visual Studio Test Explorer, and in Visual Studio Code. The Microsoft.Testing.Platform is embedded directly in your test projects, and there is no dependency on `vstest.console` or `dotnet test` for running your tests.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Platform` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Platform) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Platform
```

## About

This package provides the core platform and the .NET implementation of the testing protocol. It includes:

- **Test host**: an in-process test runner that is embedded in your test project
- **Extensibility model**: a rich extensibility model allowing test frameworks, tools and extensions to interoperate
- **Protocol**: the `.NET Testing Protocol` enabling communication between the test host and external consumers (e.g. IDE, CI)

This package is typically **not referenced directly**. Instead, test framework packages (such as [MSTest](https://www.nuget.org/packages/MSTest)) reference it automatically.

## Related packages

- [Microsoft.Testing.Platform.MSBuild](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild): MSBuild integration for `dotnet test` and CI pipeline support

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
