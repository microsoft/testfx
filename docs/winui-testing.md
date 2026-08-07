# Testing WinUI apps with MSTest and Microsoft.Testing.Platform

WinUI 3 test apps come in two flavors, and which one you use decides how the test host is started:

| Flavor | `WindowsPackageType` | Has an `AppxManifest.xml` in the build output | How the test host starts |
| --- | --- | --- | --- |
| **Packaged** (MSIX) | unset (the default) | yes | The app must be registered with the OS and activated by Application User Model ID (AUMID). `Process.Start` cannot start it. |
| **Unpackaged** | `None` | no | It is an ordinary Windows executable. `Process.Start` is all that is needed. |

Both flavors are supported when you run on [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-intro) (MTP), which you enable with `<EnableMSTestRunner>true</EnableMSTestRunner>`. Under MTP the WinUI app **is** the test host: it hosts the platform in-process, so VSTest's appx runtime provider is never involved. A packaged app still needs the [`Microsoft.Testing.Extensions.PackagedApp`](#packaged-msix-winui) launcher to register and AUMID-activate that host, because a packaged app cannot be started with `Process.Start`; an unpackaged app needs nothing extra.

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

**Self-hosted (the app is the test host).** A WinUI app already generates its own entry point from its `ApplicationDefinition`, so tell the platform not to generate a competing one, and host the platform yourself from `OnLaunched`:

```xml
<PropertyGroup>
  <EnableMSTestRunner>true</EnableMSTestRunner>
  <!-- The WinUI app owns its entry point; without this the platform generates a second one. -->
  <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
</PropertyGroup>
```

The process has already called `Application.Start`, so publish that dispatcher directly rather than letting the attribute start another application:

```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    _window = new UnitTestAppWindow();
    _window.Activate();
    UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

    string[] cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
    ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(cliArgs);
    builder.AddSelfRegisteredExtensions(cliArgs);
    using ITestApplication app = await builder.BuildAsync();

    // The WinUI-generated entry point is 'void', so publish the exit code yourself.
    Environment.ExitCode = await app.RunAsync();
    _window.Close();
    Exit();
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
- `packagedClassicApp`/`win32App` hosts receive MTP arguments as normal process `argv`, including classic hosts whose trust level is `appContainer`.
- `windowsApp`/UWP hosts receive one opaque string through `LaunchActivatedEventArgs.Arguments`. Restore the platform argument array with `PackagedAppExtensions.GetTestApplicationArguments(args.Arguments)` before `TestApplication.CreateBuilderAsync`; see [Launch activation](#launch-activation).
- End-to-end UWP/AppContainer execution remains dependent on granting the exact package SID access to the controller named pipe, tracked separately by [#10486](https://github.com/microsoft/testfx/issues/10486). Argument delivery from [#10485](https://github.com/microsoft/testfx/issues/10485) does not weaken that pipe ACL.

See [#9933](https://github.com/microsoft/testfx/issues/9933) for the implementation of this path.

### Launch activation

Unlike a packaged classic/Win32 app, a `windowsApp`/UWP app does not receive the string passed to `IApplicationActivationManager` as `argv`. Windows exposes it as one opaque value on the launch event. Use the package bootstrap from `OnLaunched` rather than adding a project-specific command-line parser. Classic AppContainer hosts continue to use their process arguments:

```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    string[] cliArgs = PackagedAppExtensions.GetTestApplicationArguments(args.Arguments);
    ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(cliArgs);
    builder.AddSelfRegisteredExtensions(cliArgs);
    using ITestApplication app = await builder.BuildAsync();
    Environment.ExitCode = await app.RunAsync();
    Exit();
}
```

The launcher and bootstrap share a versioned, length-prefixed format that preserves empty values, whitespace, quotes, backslashes, Unicode, repeated options, and ordering. Arguments that fit the documented 2,048-character Windows launch envelope remain only in the activation string. Larger arrays use a one-shot `LocalState` payload encrypted with a random per-launch key carried in the activation string; the host consumes and deletes it before MTP starts, and the launcher handle removes it if startup fails. Runsettings, filters, and other user input are therefore never persisted in plaintext.

This bootstrap also restores the existing controller connect-back environment handoff before MTP reads it. It cannot by itself authorize the AppContainer token on that pipe; [#10486](https://github.com/microsoft/testfx/issues/10486) is the remaining end-to-end dependency.

## When does the PackagedApp launcher take over?

Registering an *enabled* test host launcher switches the whole run to the test host controller (process restart) model, because a custom launcher only has an effect when an out-of-process test host is started. That is the right trade for a packaged app, but it is pure overhead for an app that just needs `Process.Start`.

The launcher therefore decides for itself, per run:

| Situation | Launcher enabled? | Effect |
| --- | --- | --- |
| Not Windows | no | Nothing changes. Packaged Windows apps are a Windows-only concept. |
| Packaged layout (an `AppxManifest.xml` that describes this app — see below) | yes | The layout is registered and activated by AUMID. |
| Any other layout — including unpackaged WinUI and ordinary console test apps | no | The platform keeps its default in-process / `Process.Start` path. |

So an **unpackaged** WinUI app that references `Microsoft.Testing.Extensions.PackagedApp` (directly, or transitively through a shared `Directory.Packages.props`) pays nothing for it: no extra process, and no copy of the build output into a deployment directory.

### How a manifest is attributed to your app

"Packaged layout" means a manifest that actually describes *this* app, not merely any `AppxManifest.xml` somewhere above it:

- An `AppxManifest.xml` **in the app's own directory** is taken as the app's layout.
- An `AppxManifest.xml` in an **ancestor** directory is used only when one of its `<Application>` entries declares an `Executable` that resolves back to the app directory — and, when the launcher is actually starting the host, to that exact executable. This is what lets `Application/@Executable` point into a subdirectory of the package root at any depth.
- An ancestor manifest that declares no matching `Executable` is ignored, so a stray manifest in a shared build root or CI staging directory cannot classify an unrelated test app as packaged.

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
