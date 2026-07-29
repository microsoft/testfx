# Testing WinUI apps with MSTest and Microsoft.Testing.Platform

WinUI 3 test apps come in two flavors, and which one you use decides how the test host is started:

| Flavor | `WindowsPackageType` | Has an `AppxManifest.xml` in the build output | How the test host starts |
| --- | --- | --- | --- |
| **Packaged** (MSIX) | unset (the default) | yes | The app must be registered with the OS and activated by Application User Model ID (AUMID). `Process.Start` cannot start it. |
| **Unpackaged** | `None` | no | It is an ordinary Windows executable. `Process.Start` is all that is needed. |

Both flavors are supported when you run on [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-intro) (MTP), which you enable with `<EnableMSTestRunner>true</EnableMSTestRunner>`. Under MTP the WinUI app **is** the test host: it hosts the platform in-process, so no external test host provider has to deploy or activate anything.

> [!NOTE]
> Unpackaged WinUI is not supported under VSTest. VSTest routes every `UseWinUI` project through its `UwpTestHostRuntimeProvider`, which unconditionally reads an `AppxManifest.xml` from the build output and fails with `FileNotFoundException` when there is none. That provider ships in Visual Studio, not in this repository. See [#2784](https://github.com/microsoft/testfx/issues/2784).

## Unpackaged WinUI

Set `WindowsPackageType` to `None` and enable the MSTest runner:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <UseWinUI>true</UseWinUI>

    <!-- Run unpackaged: no MSIX identity, no AppxManifest.xml, no package logos. -->
    <WindowsPackageType>None</WindowsPackageType>

    <EnableMSTestRunner>true</EnableMSTestRunner>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="..." />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="..." />
  </ItemGroup>

</Project>
```

Point `[UITestMethod]` at a dispatcher queue so UI tests run on the UI thread. Which mechanism you use depends on whether the test app is itself the WinUI app:

**Self-hosted (the app is the test host).** The process has already called `Application.Start`, so publish that dispatcher directly:

```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    _window = new UnitTestAppWindow();
    _window.Activate();
    UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;
    // ... host Microsoft.Testing.Platform here ...
}
```

> [!IMPORTANT]
> Do **not** use `[assembly: WinUITestTarget(...)]` in a self-hosted app. That attribute makes `UITestMethodAttribute` start an application itself, which would be a second `Application.Start` in the same process and fails.

**Separate host.** When the test host is an ordinary executable and no `Application` has been started yet, let the attribute start one:

```csharp
[assembly: WinUITestTarget(typeof(MyApp.App))]
```

With either setup, `dotnet run` (or `dotnet test`) starts the executable directly and the run finishes in a single process.

### Building unpackaged WinUI projects

Build with `dotnet build` as usual. Note that older Windows App SDK releases required the MrtCore PRI generation
MSBuild tasks that ship with Visual Studio (`Microsoft.Build.Packaging.Pri.Tasks.dll`); on those versions a
`dotnet` CLI build fails with:

```text
error MSB4062: The "Microsoft.Build.Packaging.Pri.Tasks.ExpandPriContent" task could not be loaded
```

If you hit that, either build from Visual Studio (or with its `MSBuild.exe`) or move to a newer
`Microsoft.WindowsAppSDK`. This is a Windows App SDK build requirement, not an MSTest one.

## Packaged (MSIX) WinUI

A packaged app keeps the default `WindowsPackageType` and ships a `Package.appxmanifest`. Because it cannot be started with `Process.Start`, the test host has to be registered and activated by AUMID. Add the [`Microsoft.Testing.Extensions.PackagedApp`](https://www.nuget.org/packages/Microsoft.Testing.Extensions.PackagedApp) package, which does exactly that through the platform's `ITestHostLauncher` extension point:

```xml
<PackageReference Include="Microsoft.Testing.Extensions.PackagedApp" Version="..." />
```

Referencing the package is enough — its MSBuild props register the launcher. Do **not** also call `builder.AddPackagedAppDeployment()` when you already use `AddSelfRegisteredExtensions`, because at most one test host launcher may be registered per run.

Requirements and limitations:

- Target a Windows TFM at platform version `10.0.19041.0` or higher (for example `net8.0-windows10.0.19041.0`) so NuGet resolves the Windows asset that contains the register-and-activate path. The plain `net8.0`/`net9.0` asset fails fast with an actionable message instead.
- Registering an unsigned build-output layout requires **Developer Mode** (or sideloading) to be enabled on the machine.
- Only **full-trust packaged desktop** hosts are supported. True UWP/AppContainer hosts are not: activation arguments are not delivered as `argv` there, and the controller pipe is not granted the package SID.

See [#9933](https://github.com/microsoft/testfx/issues/9933) for the implementation of this path.

## When does the PackagedApp launcher take over?

Registering an *enabled* test host launcher switches the whole run to the test host controller (process restart) model, because a custom launcher only has an effect when an out-of-process test host is started. That is the right trade for a packaged app, but it is pure overhead for an app that just needs `Process.Start`.

The launcher therefore decides for itself, per run:

| Situation | Launcher enabled? | Effect |
| --- | --- | --- |
| Not Windows | no | Nothing changes. Packaged Windows apps are a Windows-only concept. |
| Packaged layout (`AppxManifest.xml` found at or above the app directory) | yes | The layout is registered and activated by AUMID. |
| Any other layout — including unpackaged WinUI and ordinary console test apps | no | The platform keeps its default in-process / `Process.Start` path. |

So an **unpackaged** WinUI app that references `Microsoft.Testing.Extensions.PackagedApp` (directly, or transitively through a shared `Directory.Packages.props`) pays nothing for it: no extra process, and no copy of the build output into a deployment directory.

### Overriding the decision

Set the `TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER` environment variable to override the layout probe. Values are compared case-insensitively; anything unrecognized falls back to `auto` rather than failing the run.

| Value | Behavior |
| --- | --- |
| unset, or `auto` | Enable only for a packaged layout. This is the default described above. |
| `always` | Always enable. Use this to deploy a **non-packaged** layout into an isolated directory and launch the produced executable from there, for tests that must not run from the build output. |
| `never` | Never enable, even for a packaged layout. Escape hatch if the automatic detection gets in your way. |

## Behavior notes

- **The app must be able to exit.** `Application.Start` pumps a message loop that never returns, so `UITestMethodAttribute` runs it on a background thread. Otherwise the process would stay alive after the run and hang when the test app is itself the test host — which is exactly the unpackaged MTP case. See [#9904](https://github.com/microsoft/testfx/pull/9904).
- **Publish the exit code yourself.** The WinUI-generated entry point is `void`, so a self-hosted test app must assign `Environment.ExitCode = await app.RunAsync();` and then call `Exit()`. Without it the process always exits `0` and failing tests never fail the build.
- **MrtCore warns about MSTest's satellite assemblies.** A WinUI build indexes resources with MrtCore, which reports `PRI257`/`PRI263` because MSTest ships localized satellite assemblies that carry no `en-US` default. It is benign — the warning is about resource *lookup* fallback, not about your tests. Note it cannot be silenced with `NoWarn` or `MSBuildWarningsAsMessages`, because MrtCore puts the `PRI…` code in the message text rather than in the MSBuild warning code; if you build with warnings as errors, exclude this project from that setting.
- **Avoid `dotnet exec`.** WinUI resolves its PRI resources relative to the *process* path, so running the test app under `dotnet.exe` breaks resource loading. `Microsoft.Testing.Platform.MSBuild` already prefers launching the apphost directly when one is available.
- **Startup failures surface as test failures.** If the application cannot be brought up on the `WinUITestTarget` path — a throwing `Application` constructor, or in an unpackaged app a Windows App SDK runtime that cannot be resolved (`COMException` / `REGDB_E_CLASSNOTREG`) — the failure is now reported on the test. Earlier versions swallowed it and the run hung with no diagnostic at all.

## How the Windows App SDK gets initialized

An unpackaged app has no MSIX manifest declaring a framework dependency, so the Windows App SDK runtime has to be resolved at startup by the *bootstrapper*. You normally write no code for this: when

- `WindowsPackageType` is `None`,
- `WindowsAppSDKSelfContained` is not `true`, and
- `OutputType` is `Exe` or `WinExe`,

the Windows App SDK build injects a [module initializer](https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-9.0/module-initializers) into **your app assembly** that calls `Bootstrap.Initialize`. The CLR runs a module initializer before the first use of any type in that assembly, so it has already run by the time your tests execute.

That is why a plain `[TestMethod]` — not just `[UITestMethod]` — can call Windows App SDK WinRT APIs in an unpackaged test app.

Note those conditions are evaluated per project, and a **class library** does not get the initializer. If your tests live in a library loaded by a host that is not itself a Windows App SDK app, set `<WindowsAppSdkBootstrapInitialize>true</WindowsAppSdkBootstrapInitialize>` explicitly.

## Related

- [`Microsoft.Testing.Extensions.PackagedApp` package readme](../src/Platform/Microsoft.Testing.Extensions.PackagedApp/PACKAGE.md)
- [RFC 017 — Test host launcher](RFCs/017-TestHost-Launcher.md)
- [`MSTestRunnerWinUI` sample](../samples/public/mstest-runner/MSTestRunnerWinUI) (packaged, MTP)
- [`MSTestRunnerWinUIUnpackaged` sample](../samples/public/mstest-runner/MSTestRunnerWinUIUnpackaged) (unpackaged, MTP)
