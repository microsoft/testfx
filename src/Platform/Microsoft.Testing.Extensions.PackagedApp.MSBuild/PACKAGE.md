# Microsoft.Testing.Extensions.PackagedApp.MSBuild

Run packaged Windows test applications from MSBuild through an out-of-process
Microsoft.Testing.Platform controller. This package supplies shared targets and
complete, framework-dependent `win-x64` controller layouts for .NET 8 and .NET 9.
The controller is an executable, not code loaded into MSBuild, so desktop
`.NET Framework` MSBuild and `dotnet msbuild` use the same process contract.

## Installation

Add an explicit package reference alongside either `MSTest` or
`MSTest.TestAdapter` and opt in to the MTP runner:

```xml
<PropertyGroup>
  <EnableMSTestRunner>true</EnableMSTestRunner>
  <OutputType>Exe</OutputType>
  <EnableMicrosoftTestingExtensionsTrxReport>true</EnableMicrosoftTestingExtensionsTrxReport>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="MSTest" Version="4.5.0" />
  <PackageReference Include="Microsoft.Testing.Extensions.PackagedApp.MSBuild" Version="2.5.0" />
</ItemGroup>
```

Keep all Microsoft.Testing.Platform packages aligned with this package's version.
The package pins its MSBuild and modern PackagedApp runtime dependencies to that
exact version. `MSTest.Sdk` adds this integration automatically for packaged apps;
ordinary adapter consumers do not download a Windows controller unless they
reference this package.

## Execution and compatibility

Build the application using its usual Windows tooling, then run
`msbuild MyTests.csproj -t:InvokeTestingPlatform`. Pass MTP options through
`TestingPlatformCommandLineArguments`, for example `--report-trx`. The controller
owns command-line preparation, cancellation, retries, report recovery and exit
codes. It reuses `InvokeTestingPlatformTask`; no new MSBuild task is required.
The runtime launcher preserves AUMID activation, execution-ID/connect-back
handoff, authenticated activation arguments, and exact package-SID pipe grants.
This package does not change those protocols or the no-handshake launch paths.

The targets select the controller only for an opted-in MTP application with a
packaged WinUI, modern UWP or classic UAP application model on Windows. Ordinary
console apps, unpackaged WinUI, VSTest, libraries and non-Windows builds retain
their original execution paths. A nonempty `TestingPlatformExecutablePath`
override is preserved and receives no controller-specific environment values.
`TestingPlatformPackagedAppTargetPath` can select a staged packaged executable.
Missing or incomplete controller layouts fail explicitly. Runtime and protocol
errors remain failures; the targets never silently fall back to another host.

### Native `dotnet test`

With .NET SDK 10's Microsoft.Testing.Platform runner, project-based `dotnet test`
uses the same sidecar for packaged **full-trust** .NET applications, including
`--no-build`. `ComputeRunArguments` stages the controller under the project's
intermediate output directory, beside a versioned startup file containing the
selected executable and extension list. The application's argument tail remains
unchanged, including an empty tail, so the SDK retains ownership of launch-profile
selection and explicit-argument precedence. The sidecar also accepts the original
inline v1 startup prefix. Relative `TestingPlatformPackagedAppTargetPath`
values are resolved against the project directory. A customized `RunCommand` is
left unchanged. `UseAppHost=false` requires an explicit staged `.exe` target.

Only the activated test host connects to the native SDK pipe. Its real runtime,
architecture, tests and shared execution ID are reported for test runs, while the sidecar's exit
code reflects host failures and controller finalization. This avoids SDK 10's
requirement that all connected processes use the same runtime and architecture.
Retries retain that pipe and execution ID. Controller-generated reports such as
TRX are written to the requested results directory but are **not advertised as
artifacts to the native SDK**.

Native `--help` and `--list-tests` activate the selected host directly to describe
its actual options and tests, never the sidecar's dummy framework. They require
the same Developer Mode and package registration as execution, but use the host's
own execution ID rather than the controller-to-host handoff used by test runs. SDK 10 ignores
launch failures during `--help`: if only SDK options appear, use `--list-tests`
to surface the launch error. Help lists host options, including MSTest's `--filter`,
even when the controller has not yet registered them for execution.
SDK 10 can also forward a zero exit code from a host that exits before connecting,
producing an empty discovery. Host startup must actually initialize MTP rather
than exit or redirect activation before entering the test platform.
Native AppContainer execution is rejected: the SDK pipe does not authorize the
package SID. Use `InvokeTestingPlatform`
for AppContainer/modern UWP/classic UAP execution and controller-managed artifacts.
Framework-specific options such as MSTest's `--filter` require corresponding
registration in the controller; routing alone does not add that support.
Informational activation handles Ctrl+C by terminating its owned host, but an
external hard kill of the sidecar cannot guarantee teardown of an AUMID-activated
help/discovery host.

`ComputeRunArguments` is also used by `dotnet run`, so project-based `dotnet run`
now starts the sidecar for these same packaged .NET applications rather than
starting the host without package identity. Unpackaged projects and explicit
executable overrides retain their original run command.
Project launch-profile `commandLineArgs` are used when no explicit application
arguments are supplied; `--no-launch-profile` and explicit arguments keep the
SDK's normal precedence. The staged controller is incremental and removed by
`dotnet clean`, including when it was created by a no-build launch query.

`dotnet run -- --help` and `dotnet run -- --list-tests` also activate the actual
full-trust host. Since AUMID activation does not inherit the caller's standard
output, the sidecar relays host options and discovered tests over the existing MTP
named-pipe protocol. Missing or incomplete help/discovery responses are failures,
not successful empty output. This informational pipe has the same AppContainer
restriction as native SDK discovery; use `InvokeTestingPlatform` for those hosts.

The legacy executable name `mstest-appmodel-controller.exe` and the
`MSTEST_APPMODEL_CONTROLLER_EXTENSIONS` and `TESTINGPLATFORM_PACKAGEDAPP_TARGET`
environment identifiers are intentionally preserved for compatibility.

The x64 controller needs a .NET 8 or later runtime (roll-forward is enabled) and
Windows 10 19041 or later. The application itself may use another architecture.
Unsigned layouts need Developer Mode or sideloading. UWP compilation continues
to require the Visual Studio UWP workload.

## Application entry points

For self-hosted WinUI/modern UWP, an `ApplicationDefinition` suppresses a competing
generated `Main` while retaining `MicrosoftTestingPlatformApplication.RunAsync`.
Restore UWP launch arguments with
`PackagedAppExtensions.GetTestApplicationArguments(args.Arguments)`.

Classic `uap10.0` uses the MSTest adapter's UAP-compatible runtime and the bootstrap
source shipped here. Call `MicrosoftTestingPlatformApplication.RunAsync(args.Arguments)`
from `OnLaunched`; no modern PackagedApp runtime assembly is added to the UAP
application. See [Windows application testing](https://github.com/microsoft/testfx/blob/main/docs/winui-testing.md).

Microsoft.Testing.Platform is open source. Provide feedback in
[microsoft/testfx](https://github.com/microsoft/testfx/issues).
