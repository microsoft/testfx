# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)## <a name="1.5.0" />[1.5.0] - 2024-12-20

See full log [here](https://github.com/microsoft/testfx/compare/v1.4.3...v1.5.0)

### Added

* Expose `ExecuteRequestContext` ctor for testability by @MarcoRossignoli in [#3717](https://github.com/microsoft/testfx/pull/3717)
* Add `StandardOutputProperty` and `StandardErrorProperty` by @MarcoRossignoli in [#3748](https://github.com/microsoft/testfx/pull/3748)
* Optimize the server mode discovery workflow by @MarcoRossignoli in [#3877](https://github.com/microsoft/testfx/pull/3877)
* Add yy/mm to the log filename for better ordering by @MarcoRossignoli in [#3894](https://github.com/microsoft/testfx/pull/3894)
* Add logic to read env var for runsettings path in VSTestBridge by @mariam-abdulla in [#3909](https://github.com/microsoft/testfx/pull/3909)
* Support runsettings environment variables by @MarcoRossignoli in [#3918](https://github.com/microsoft/testfx/pull/3918)
* Write standard output and error, and respect execution id by @nohwnd in [#3934](https://github.com/microsoft/testfx/pull/3934)
* Add key only overload to TestMetadataProperty by @Evangelink in [#4041](https://github.com/microsoft/testfx/pull/4041)
* Pass multiple errors by @nohwnd in [#4054](https://github.com/microsoft/testfx/pull/4054)
* Introduce and use warning and error output messages by @Evangelink in [#4217](https://github.com/microsoft/testfx/pull/4217)
* Show running tests by @drognanar in [#4221](https://github.com/microsoft/testfx/pull/4221)

### Fixed

* Ensure correct exit code in case of cancellation and add `OnExit` phase for for `IPushOnlyProtocol` by @MarcoRossignoli in [#3820](https://github.com/microsoft/testfx/pull/3820)
* Fix writing dark colors by @nohwnd in [#3825](https://github.com/microsoft/testfx/pull/3825)
* Fix: do not show telemetry banner if no telemetry provider is registered by @Evangelink in [#3862](https://github.com/microsoft/testfx/pull/3862)
* Fix RunSettings/RunConfiguration/ResultsDirectory by @MarcoRossignoli in [#3902](https://github.com/microsoft/testfx/pull/3902)
* Fix concurrency issue in TerminalTestReporter by @mariam-abdulla in [#4229](https://github.com/microsoft/testfx/pull/4229)
* Only push output device messages to Test Explorer, don't push logs by @Youssef1313 in [#4178](https://github.com/microsoft/testfx/pull/4178)
* Fix missing skip reason by @MarcoRossignoli in [#3754](https://github.com/microsoft/testfx/pull/3754)
* Fix skipped Test isn't shown as skipped/not executed in Trx Report  by @engyebrahim in [#3773](https://github.com/microsoft/testfx/pull/3773)
* Fix Timed Out Test isn't shown under timeout counter in Trx Report by @engyebrahim in [#3788](https://github.com/microsoft/testfx/pull/3788)
* Fix trx in case of exit code != 0 by @MarcoRossignoli in [#3887](https://github.com/microsoft/testfx/pull/3887)
* Fix SelfRegisteredExtensions type to be internal by @Evangelink in [#3891](https://github.com/microsoft/testfx/pull/3891)
* Display inner exceptions by @Evangelink in [#3920](https://github.com/microsoft/testfx/pull/3920)
* Fix publishing as docker image via /t:PublishContainer by @nohwnd in [#3929](https://github.com/microsoft/testfx/pull/3929)
* Fix conflict with Microsoft.Win32.Registry by @Evangelink in [#3988](https://github.com/microsoft/testfx/pull/3988)
* Fix live output with HotReload (#3983) by @nohwnd in [#3993](https://github.com/microsoft/testfx/pull/3993)
* Fix hangdump not showing tests in progress (#3992) by @nohwnd in [#3999](https://github.com/microsoft/testfx/pull/3999)
* Fix hangdump space in dump path (#3994) by @nohwnd in [#4001](https://github.com/microsoft/testfx/pull/4001)
* Improve error message for incompatible architecture by @Youssef1313 in [#4144](https://github.com/microsoft/testfx/pull/4144)
* StopUpdate in Finally Block by @thomhurst in [#4147](https://github.com/microsoft/testfx/pull/4147)
* Set IsTestingPlatformApplication to true in ClassicEngine.targets by @mariam-abdulla in [#4151](https://github.com/microsoft/testfx/pull/4151)
* Fix displaying progress in non-ansi terminal by @Evangelink in [#4320](https://github.com/microsoft/testfx/pull/4320)

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.5.0)
* Microsoft.Testing.Extensions.HangDump: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.5.0)
* Microsoft.Testing.Extensions.HotReload: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.5.0)
* Microsoft.Testing.Extensions.Retry: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.5.0)
* Microsoft.Testing.Extensions.Telemetry: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/1.5.0)
* Microsoft.Testing.Extensions.TrxReport: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.5.0)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/1.5.0)
* Microsoft.Testing.Extensions.VSTestBridge: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/1.5.0)
* Microsoft.Testing.Platform: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Platform/1.5.0)
* Microsoft.Testing.Platform.MSBuild: [1.5.0](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/1.5.0)

## <a name="1.4.3" />[1.4.3] - 2024-11-12

See full log [here](https://github.com/microsoft/testanywhere/compare/v1.4.2...v1.4.3)

### Fixed

* Fix live output with HotReload by @nohwnd in [#3983](https://github.com/microsoft/testfx/pull//3983)
* Fix hangdump space in dump path by @nohwnd in [#3994](https://github.com/microsoft/testfx/pull//3994)
* Fix hangdump not showing tests in progress by @nohwnd in [#3992](https://github.com/microsoft/testfx/pull//3992)

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.3)
* Microsoft.Testing.Extensions.HangDump: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.3)
* Microsoft.Testing.Extensions.HotReload: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.3)
* Microsoft.Testing.Extensions.Retry: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.3)
* Microsoft.Testing.Extensions.Telemetry: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/1.4.3)
* Microsoft.Testing.Extensions.TrxReport: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.3)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/1.4.3)
* Microsoft.Testing.Extensions.VSTestBridge: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/1.4.3)
* Microsoft.Testing.Platform: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Platform/1.4.3)
* Microsoft.Testing.Platform.MSBuild: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/1.4.3)

## <a name="1.4.2" />[1.4.2] - 2024-10-31

See full log [here](https://github.com/microsoft/testanywhere/compare/v1.4.1...v1.4.2)

### Fixed

* Fix casing for event key value by @MarcoRossignoli in [#3915](https://github.com/microsoft/testfx/pull/3915)
* Fix publishing as docker image via /t:PublishContainer by @nohwnd in [#3929](https://github.com/microsoft/testfx/pull/3929)
* Fix displaying inner exceptions in output by @Evangelink in [#3965](https://github.com/microsoft/testfx/pull/3965)

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.2)
* Microsoft.Testing.Extensions.HangDump: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.2)
* Microsoft.Testing.Extensions.HotReload: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.2)
* Microsoft.Testing.Extensions.Retry: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.2)
* Microsoft.Testing.Extensions.Telemetry: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/1.4.2)
* Microsoft.Testing.Extensions.TrxReport: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.2)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/1.4.2)
* Microsoft.Testing.Extensions.VSTestBridge: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/1.4.2)
* Microsoft.Testing.Platform: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Platform/1.4.2)
* Microsoft.Testing.Platform.MSBuild: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/1.4.2)

## <a name="1.4.1" />[1.4.1] - 2024-10-03

See full log [here](https://github.com/microsoft/testanywhere/compare/v1.4.0...v1.4.1)

### Fixed

* Fix writing dark colors by @nohwnd in [#3828](https://github.com/microsoft/testfx/pull/3828)
* Fix: do not show telemetry banner if no telemetry provider is registered by @Evangelink in [#3862](https://github.com/microsoft/testfx/pull/3862)
* Fix SelfRegisteredExtensions type to be internal by @Evangelink in [#3891](https://github.com/microsoft/testfx/pull/3891)

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.1)
* Microsoft.Testing.Extensions.HangDump: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.1)
* Microsoft.Testing.Extensions.HotReload: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.1)
* Microsoft.Testing.Extensions.Retry: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.1)
* Microsoft.Testing.Extensions.Telemetry: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/1.4.1)
* Microsoft.Testing.Extensions.TrxReport: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.1)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/1.4.1)
* Microsoft.Testing.Extensions.VSTestBridge: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/1.4.1)
* Microsoft.Testing.Platform: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Platform/1.4.1)
* Microsoft.Testing.Platform.MSBuild: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/1.4.1)

## <a name="1.4.0" />[1.4.0] - 2024-09-11

See full log [here](https://github.com/microsoft/testanywhere/compare/v1.3.2...v1.4.0)

### Added

* Handle `--` for msbuild dotnet test integration by @MarcoRossignoli in [#3309](https://github.com/microsoft/testfx/pull/3309)
* Support StandardOutput/StandardError for TA in VS by @MarcoRossignoli in [#3486](https://github.com/microsoft/testfx/pull/3486)
* Interactive display for terminals by @nohwnd in [#3292](https://github.com/microsoft/testfx/pull/3292)
* Add --? alias for --help by @engyebrahim in [#3522](https://github.com/microsoft/testfx/pull/3522)
* Platform.MSBuild should allow generating a helper for registration of extensions by @MarcoRossignoli in [#3525](https://github.com/microsoft/testfx/pull/3525)
* Humanize progress time, and reduce update to 500ms by @nohwnd in [#3535](https://github.com/microsoft/testfx/pull/3535)
* Add `--no-progress` and `--no-ansi` by @nohwnd in [#3550](https://github.com/microsoft/testfx/pull/3550)
* Add `--output` option by @nohwnd in [#3565](https://github.com/microsoft/testfx/pull/3565)
* Warn about unsupported runsettings entries by @Evangelink in [#3647](https://github.com/microsoft/testfx/pull/3647)
* Allow digit inside option name by @MarcoRossignoli in [#3651](https://github.com/microsoft/testfx/pull/3651)
* Expose `TestFrameworkCapabilitiesExtensions` in experimental mode by @MarcoRossignoli in [#3653](https://github.com/microsoft/testfx/pull/3653)
* Platform should expose a timeout option by @engyebrahim in [#3642](https://github.com/microsoft/testfx/pull/3642)
* Bridge message logger also forward info to output by @Evangelink in [#3712](https://github.com/microsoft/testfx/pull/3712)
* Add `StandardOutputProperty` and `StandardErrorProperty` (#3748) by @MarcoRossignoli in [#3749](https://github.com/microsoft/testfx/pull/3749)
* Fix missing skip reason (#3754) by @MarcoRossignoli in [#3755](https://github.com/microsoft/testfx/pull/3755)

### Fixed

* Fix ResultFiles placement in TRX report by @nohwnd in [#3265](https://github.com/microsoft/testfx/pull/3265)
* Fix: VSTest bridge - use TestCase.Id for the TestNodeUID by @Evangelink in [#3270](https://github.com/microsoft/testfx/pull/3270)
* Fix test case id filtering for server mode by @MarcoRossignoli in [#3284](https://github.com/microsoft/testfx/pull/3284)
* Fix TE tests execution for TA mode by @MarcoRossignoli in [#3290](https://github.com/microsoft/testfx/pull/3290)
* Fix MessageBusProxy.InitAsync to be proxying _messageBus.InitAsync by @SimonCropp in [#3300](https://github.com/microsoft/testfx/pull/3300)
* Fix shutdown order for server mode by @MarcoRossignoli in [#3306](https://github.com/microsoft/testfx/pull/3306)
* Fix possible deadlock inside MSBuild task by @MarcoRossignoli in [#3307](https://github.com/microsoft/testfx/pull/3307)
* avoid marshing async to sync when there is a sync alternative by @SimonCropp in [#3383](https://github.com/microsoft/testfx/pull/3383)
* pass cancellationToken where possible by @SimonCropp in [#3465](https://github.com/microsoft/testfx/pull/3465)
* reuse TestRun node in AddArtifactsAsync and throw a more accurate exception by @SimonCropp in [#3463](https://github.com/microsoft/testfx/pull/3463)
* Remove Condition=" '$(GenerateTestingPlatformEntryPoint)' == 'True' " from extensions  by @engyebrahim in [#3524](https://github.com/microsoft/testfx/pull/3524)
* Exclude tool-related options in help option by @mariam-abdulla in [#3542](https://github.com/microsoft/testfx/pull/3542)
* Fix shortening TFM and architecture by @nohwnd in [#3583](https://github.com/microsoft/testfx/pull/3583)
* Fix `Content-Length` by @MarcoRossignoli in [#3641](https://github.com/microsoft/testfx/pull/3641)
* Fix `CommandLineHandler` duplication check message by @MarcoRossignoli in [#3660](https://github.com/microsoft/testfx/pull/3660)
* Localize proxies exceptions by @MarcoRossignoli in [#3678](https://github.com/microsoft/testfx/pull/3678)
* Manual resolution of the muxer for `dotnet test` MSBuild extension by @MarcoRossignoli in [#3703](https://github.com/microsoft/testfx/pull/3703)
* Fix the usage of the DOTNET_HOST_PATH by @MarcoRossignoli in [#3707](https://github.com/microsoft/testfx/pull/3707)
* Fix timedout test does not fail test run (in ui) by @nohwnd in [#3774](https://github.com/microsoft/testfx/pull/3774)
* Cherry-pick fix skipped Test isn't shown as skipped/not executed in Trx Report by @engyebrahim in [#3787](https://github.com/microsoft/testfx/pull/3787)

### Housekeeping

* improve perf of PropertyBag.SingleOrDefault by @SimonCropp in [#3302](https://github.com/microsoft/testfx/pull/3302)
* Modulename is never empty by @nohwnd in [#3328](https://github.com/microsoft/testfx/pull/3328)
* dont attach callback if there is no OnExit configured by @SimonCropp in [#3438](https://github.com/microsoft/testfx/pull/3438)
* missing process using in DebuggerUtility by @SimonCropp in [#3434](https://github.com/microsoft/testfx/pull/3434)
* remove triple enumeration evaluation and linq usage in CollectEntriesAndErrors by @SimonCropp in [#3451](https://github.com/microsoft/testfx/pull/3451)
* use XDocument.SaveAsync from polyfill by @SimonCropp in [#3448](https://github.com/microsoft/testfx/pull/3448)
* Save all code files with UTF8BOM by @nohwnd in [#3536](https://github.com/microsoft/testfx/pull/3536)
* Save all project files with UTF8 (NOBOM) by @nohwnd in [#3539](https://github.com/microsoft/testfx/pull/3539)
* Give to json default protocol a name `--server jsonrpc` by @MarcoRossignoli in [#3655](https://github.com/microsoft/testfx/pull/3655)
* Change --help formatting (and fix --info) by @nohwnd in [#3649](https://github.com/microsoft/testfx/pull/3649)
* Disable out of process extensions in case of `--list-tests` by @MarcoRossignoli in [#3722](https://github.com/microsoft/testfx/pull/3722)

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.0)
* Microsoft.Testing.Extensions.HangDump: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.0)
* Microsoft.Testing.Extensions.HotReload: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.0)
* Microsoft.Testing.Extensions.Retry: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.0)
* Microsoft.Testing.Extensions.Telemetry: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/1.4.0)
* Microsoft.Testing.Extensions.TrxReport: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.0)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/1.4.0)
* Microsoft.Testing.Extensions.VSTestBridge: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/1.4.0)
* Microsoft.Testing.Platform: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Platform/1.4.0)
* Microsoft.Testing.Platform.MSBuild: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/1.4.0)

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
