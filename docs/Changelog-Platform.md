# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)

## <a name="1.3.2" />[1.3.2] - 2024-08-05

See full log [here](https://github.com/microsoft/testanywhere/compare/v1.3.1...v1.3.2)

### Fixed

* Fix accessing PID in TestHostControllersTestHost by @Evangelink in [637e764](https://github.com/microsoft/testfx/commit/637e7647b805ea9a1c3121beced3c6a726ca0e16)
* Fix MessageBusProxy.InitAsync to be proxying _messageBus.InitAsync by @SimonCropp in [#3300](https://github.com/microsoft/testfx/pull/3300)
* Fix shutdown order for server mode by @MarcoRossignoli in [#3306](https://github.com/microsoft/testfx/pull/3306)
* Fix possible deadlock inside MSBuild task by @MarcoRossignoli in [#3307](https://github.com/microsoft/testfx/pull/3307)

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.3.2)
* Microsoft.Testing.Extensions.HangDump: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.3.2)
* Microsoft.Testing.Extensions.HotReload: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.3.2)
* Microsoft.Testing.Extensions.Retry: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.3.2)
* Microsoft.Testing.Extensions.Telemetry: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/1.3.2)
* Microsoft.Testing.Extensions.TrxReport: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.3.2)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/1.3.2)
* Microsoft.Testing.Extensions.VSTestBridge: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/1.3.2)
* Microsoft.Testing.Platform: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Platform/1.3.2)
* Microsoft.Testing.Platform.MSBuild: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/1.3.2)

## <a name="1.3.1" />[1.3.1] - 2024-07-15

See full log [here](https://github.com/microsoft/testanywhere/compare/v1.2.1...v1.3.1)

### Added

* Make NopFilter public and experimental by @Evangelink in [#3035](https://github.com/microsoft/testfx/pull/3035)
* Return the pid during the handshake inside the InitializeResponseArgs by @MarcoRossignoli in [#3212](https://github.com/microsoft/testfx/pull/3212)
* Open-source platform extensions and tooling by @Evangelink in [#3133](https://github.com/microsoft/testfx/pull/3133)
* Make tree node filter public and experimental by @Evangelink in [#3052](https://github.com/microsoft/testfx/pull/3052)
* Send the full module name to dotnet test by @mariam-abdulla in [#3085](https://github.com/microsoft/testfx/pull/3085)
* Support dotnet test Help Option Through Pipes by @mariam-abdulla in [#2923](https://github.com/microsoft/testfx/pull/2923)

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.3.1)
* Microsoft.Testing.Extensions.HangDump: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.3.1)
* Microsoft.Testing.Extensions.HotReload: [1.3.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.3.0)
* Microsoft.Testing.Extensions.Retry: [1.3.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.3.0)
* Microsoft.Testing.Extensions.Telemetry: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/1.3.1)
* Microsoft.Testing.Extensions.TrxReport: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.3.1)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/1.3.1)
* Microsoft.Testing.Extensions.VSTestBridge: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/1.3.1)
* Microsoft.Testing.Platform: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Platform/1.3.1)
* Microsoft.Testing.Platform.MSBuild: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/1.3.1)
