# Microsoft.Testing.Extensions.HotReload

Microsoft.Testing.Extensions.HotReload is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that enables Hot Reload support, allowing you to apply code changes to your running tests without restarting the test host process.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.HotReload` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.HotReload) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.HotReload
```

## About

This package extends Microsoft.Testing.Platform with:

- **Hot Reload integration**: apply code changes to your test project without restarting the test host
- **Faster inner loop**: reduces the time between making a change and seeing the test results
- **Console-mode support**: currently supported in console mode (not in Visual Studio/VS Code Test Explorer)

Enable Hot Reload support by setting `TESTINGPLATFORM_HOTRELOAD_ENABLED=1`.

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-hosting#hot-reload>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
