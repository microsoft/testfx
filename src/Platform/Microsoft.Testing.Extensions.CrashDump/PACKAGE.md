# Microsoft.Testing.Extensions.CrashDump

Microsoft.Testing.Extensions.CrashDump is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that captures a crash dump or crash report for the test host process when an unhandled exception or crash occurs.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.CrashDump` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.CrashDump
```

## About

This package extends Microsoft.Testing.Platform with:

- **Crash dump collection**: automatically captures a memory dump when the test process crashes
- **Crash report collection**: optionally emits a lightweight JSON crash report to help diagnose crashes without uploading a full dump (Linux/macOS only — see [dotnet/runtime#80191](https://github.com/dotnet/runtime/issues/80191); requires .NET 7+ when used alone or .NET 6+ when combined with `--crashdump`)
- **Post-mortem debugging**: collected dumps can be analyzed with tools like Visual Studio, WinDbg, or `dotnet-dump`
- **Cross-platform**: crash dumps are supported on Windows, Linux, and macOS (dumps collected on macOS can only be analyzed on macOS). Crash reports are currently only supported on Linux and macOS.
- **Runtime behavior**: supported for .NET 6+; on .NET Framework this extension is ignored

Enable crash dump collection via the `--crashdump` command line option.
Add `--crash-report` (Linux/macOS only; requires .NET 7+ when used alone or .NET 6+ with `--crashdump`) to generate a JSON crash report; combine `--crashdump --crash-report` to produce both a dump and a report.

## Related packages

- [Microsoft.Testing.Extensions.HangDump](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump): captures dumps when the test process hangs

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-crash-hang-dumps#crash-dump>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
