# Microsoft.Testing.Extensions.VSTestBridge

Microsoft.Testing.Extensions.VSTestBridge is a **test framework author** extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform). It provides a compatibility layer with VSTest so frameworks can support Microsoft.Testing.Platform while continuing VSTest-mode workflows.

> **Note**: This package is **not intended for end-user test projects**. It is designed for framework and adapter maintainers who need compatibility with both VSTest and Microsoft.Testing.Platform.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.VSTestBridge` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.VSTestBridge) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.VSTestBridge
```

## About

This package provides:

- **VSTest compatibility layer**: helps frameworks keep existing VSTest-mode workflows (`vstest.console.exe`, `dotnet test`, VSTest task, Test Explorer)
- **Migration path**: enables framework authors to gradually adopt Microsoft.Testing.Platform while maintaining compatibility
- **Runsettings and filter support**: enables VSTest `.runsettings`/`--filter` compatibility where supported by the framework

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-vstest-bridge>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
