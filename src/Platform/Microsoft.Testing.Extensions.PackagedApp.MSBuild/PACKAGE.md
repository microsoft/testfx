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
