# Microsoft.Testing.Extensions.VSTestBridge

Microsoft.Testing.Extensions.VSTestBridge is a **test framework author** extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). It provides a compatibility bridge that allows existing VSTest test adapters to run on Microsoft.Testing.Platform without a full rewrite.

> **Note**: This package is **not intended for end-user test projects**. It is designed for test framework authors who are building or maintaining test adapters and want to support both VSTest and Microsoft.Testing.Platform.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.VSTestBridge` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.VSTestBridge) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.VSTestBridge
```

## About

This package provides:

- **VSTest compatibility**: allows existing VSTest test adapters to run on Microsoft.Testing.Platform with minimal changes
- **Migration path**: enables test framework authors to gradually adopt Microsoft.Testing.Platform while maintaining backward compatibility with VSTest
- **Dual-targeting**: test frameworks can support both Microsoft.Testing.Platform and VSTest from a single codebase

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
