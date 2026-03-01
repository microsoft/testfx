# Microsoft.Testing.Extensions.CrashDump

Microsoft.Testing.Extensions.CrashDump is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that captures a crash dump of the test host process when an unhandled exception or crash occurs.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.CrashDump` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.CrashDump) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.CrashDump
```

## About

This package extends Microsoft.Testing.Platform with:

- **Crash dump collection**: automatically captures a memory dump when the test process crashes
- **Post-mortem debugging**: collected dumps can be analyzed with tools like Visual Studio, WinDbg, or `dotnet-dump`
- **Runtime behavior**: supported for .NET 6+; on .NET Framework this extension is ignored

Enable crash dump collection via the `--crashdump` command line option.

## Related packages

- [Microsoft.Testing.Extensions.HangDump](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump): captures dumps when the test process hangs

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-diagnostics#crash-dump>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
