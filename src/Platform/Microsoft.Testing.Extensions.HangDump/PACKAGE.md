# Microsoft.Testing.Extensions.HangDump

Microsoft.Testing.Extensions.HangDump is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that captures a memory dump when the test host process appears to be hung (exceeds a configurable timeout).

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.HangDump` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.HangDump) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.HangDump
```

## About

This package extends Microsoft.Testing.Platform with:

- **Hang detection**: monitors the test host process and detects when it exceeds a configurable timeout
- **Dump collection**: captures a memory dump of the hanging process for post-mortem analysis
- **Deadlock investigation**: collected dumps can be analyzed with tools like Visual Studio, WinDbg, or `dotnet-dump` to diagnose deadlocks and other hang causes

The timeout defaults to `30m` and can be configured in values such as `90s`, `5m`, or `1.5h`.

Enable hang dump collection via the `--hangdump` command line option, and configure the timeout with `--hangdump-timeout`.

## Related packages

- [Microsoft.Testing.Extensions.CrashDump](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump): captures dumps when the test process crashes

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-diagnostics#hang-dump>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
