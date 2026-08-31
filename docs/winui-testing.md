# Testing UWP and WinUI apps with MSTest

## Application model matrix

| Application model | Recommended test configuration | Test host |
| --- | --- | --- |
| Legacy UWP (`uap10.0`) | Existing non-SDK project with `MSTest.TestAdapter` and `MSTest.TestFramework` | VSTest AppContainer |
| Modern UWP (.NET 9+, `UseUwp`) | `MSTest.Sdk` | VSTest AppContainer, selected automatically |
| Packaged full-trust WinUI 3 (`UseWinUI`) | `MSTest.Sdk` | MTP with automatic package registration and AUMID activation |
| Unpackaged WinUI 3 (`UseWinUI`, `WindowsPackageType=None`) | `MSTest.Sdk` | MTP direct executable launch |
| AppContainer-configured WinUI 3 | VSTest | MTP is not currently supported |

True UWP/AppContainer test hosts cannot use MTP. `MSTest.Sdk` therefore selects VSTest when `UseUwp=true` and reports a build error if a project explicitly selects MTP for that application model. Legacy `uap10.0` projects remain on their existing package-based setup because they do not use the SDK-style project system.

### Packaging and sandboxing are separate choices

Two independent settings describe a Windows app:

- **Packaging** determines whether the app has MSIX package identity and must be registered and activated by AUMID. Both UWP and WinUI apps can be packaged.
- **Trust level** determines whether the process is a normal full-trust desktop process or runs in the restricted AppContainer sandbox.

A packaged WinUI 3 desktop app is full trust by default. Packaging gives it identity and MSIX file/registry virtualization, but does not put it in AppContainer. A WinUI 3 app can be explicitly configured for AppContainer with `uap10:TrustLevel="appContainer"` in its package manifest; it then has the same MTP communication restrictions as UWP.

`Microsoft.Testing.Extensions.PackagedApp` currently supports packaged **full-trust** desktop hosts end to end. It also provides both communication primitives an AppContainer host needs: a reusable launch-activation argument bootstrap and exact package-SID authorization on the controller pipe. Those primitives do not by themselves make true UWP/AppContainer an MTP test-host mode: `MSTest.Sdk` still routes `UseUwp=true` to VSTest and rejects forced MTP, and the existing MTP startup path still expects an ordinary initial controller process.

#### AppContainer communication primitives

MTP's controller must give the new test host its command-line options and then establish a two-way named-pipe connection. AppContainer changes both mechanisms:

1. **Activation arguments are not process arguments.** For a packaged full-trust desktop app, AUMID activation starts an ordinary Win32 process and the activation string becomes its command line, which .NET exposes through `Environment.GetCommandLineArgs()`. A true UWP/AppContainer app instead receives one opaque string through [`LaunchActivatedEventArgs.Arguments`](https://learn.microsoft.com/uwp/api/windows.applicationmodel.activation.launchactivatedeventargs.arguments) in `Application.OnLaunched`. `PackagedAppExtensions.GetTestApplicationArguments(args.Arguments)` restores that value to the `string[]` expected by the platform before `TestApplication.CreateBuilderAsync`.
2. **The controller pipe must authorize the app identity.** MTP normally creates its named pipe with [`PipeOptions.CurrentUserOnly`](https://learn.microsoft.com/dotnet/api/system.io.pipes.pipeoptions), granting the creating token's owner SID access. An AppContainer token is additionally restricted by its package SID, and Windows grants access only when both the normal and restricted identity checks succeed. The packaged-app launcher now contributes the selected application's exact package SID; the platform grants only the minimum client rights and explicitly rejects `ALL APPLICATION PACKAGES`.

The packaged-app handshake transfers the controller pipe name and related environment values through the package's `LocalState`. Together these mechanisms solve activation argument delivery and pipe access. The remaining limitation is earlier in the lifecycle: selecting and starting a true UWP/AppContainer application as an MTP test host is not yet supported by the SDK/platform routing described above.

For modern UWP, the test-related part of the project is reduced to the SDK declaration:

```xml
<Project Sdk="MSTest.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows10.0.26100.0</TargetFramework>
    <UseUwp>true</UseUwp>
    <PublishAot>true</PublishAot>
  </PropertyGroup>
</Project>
```

The UWP XAML, MSIX, architecture, and Native AOT settings remain application concerns. Modern UWP builds also continue to require the Visual Studio MSBuild toolchain.

## WinUI 3

For the normal full-trust WinUI 3 model, test apps come in two packaging flavors, and that choice decides how the test host is started:

| Flavor | `WindowsPackageType` | Has an `AppxManifest.xml` in the build output | How the test host starts |
| --- | --- | --- | --- |
| **Packaged** (MSIX) | unset (the default) | yes | The app must be registered with the OS and activated by Application User Model ID (AUMID). `Process.Start` cannot start it. |
| **Unpackaged** | `None` | no | It is an ordinary Windows executable. `Process.Start` is all that is needed. |

Both flavors are supported when you use `MSTest.Sdk`, whose default runner is [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-intro) (MTP). Under MTP the WinUI app **is** the test host: it hosts the platform in-process, so VSTest's appx runtime provider is never involved. `MSTest.Sdk` adds the packaged-app launcher automatically for a packaged WinUI project; an unpackaged app stays on the direct executable launch path.

> [!NOTE]
> Unpackaged WinUI is not supported under VSTest. VSTest routes every `UseWinUI` project through its `UwpTestHostRuntimeProvider`, which unconditionally reads an `AppxManifest.xml` from the build output and fails with `FileNotFoundException` when there is none. That provider ships in Visual Studio, not in this repository. See [#2784](https://github.com/microsoft/testfx/issues/2784).

## Unpackaged WinUI

Set `WindowsPackageType` to `None`:

```xml
<Project Sdk="MSTest.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <UseWinUI>true</UseWinUI>

    <!-- Run unpackaged: no MSIX identity, no AppxManifest.xml, no package logos. -->
    <WindowsPackageType>None</WindowsPackageType>

  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="..." />
  </ItemGroup>

</Project>
```

Point `[UITestMethod]` at a dispatcher queue so UI tests run on the UI thread. Which mechanism you use depends on whether the test app is itself the WinUI app:

**Self-hosted (the app is the test host).** A WinUI app already generates its own entry point from its `ApplicationDefinition`. `MSTest.Sdk` detects that item and suppresses the competing MTP `Main`, while still generating a reusable `MicrosoftTestingPlatformApplication.RunAsync` helper. Host the platform from `OnLaunched`:

The process has already called `Application.Start`, so publish that dispatcher directly rather than letting the attribute start another application:

```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    _window = new UnitTestAppWindow();
    _window.Activate();
    UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

    // The WinUI-generated entry point is 'void', so publish the exit code yourself.
    Environment.ExitCode = await MicrosoftTestingPlatformApplication.RunAsync(
        Environment.GetCommandLineArgs()[1..]);
    _window.Close();
    Exit();
}
```

> [!IMPORTANT]
> Do **not** use `[assembly: WinUITestTarget(...)]` in a self-hosted app. That attribute makes `UITestMethodAttribute` start an application itself, which would be a second `Application.Start` in the same process and fails.

**Separate host.** When the test host is an ordinary executable with no `ApplicationDefinition` and no `Application` has been started yet, `MSTest.Sdk` keeps the generated MTP entry point and the attribute starts the application:

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

A packaged full-trust app keeps the default `WindowsPackageType` and ships a `Package.appxmanifest`. Because it cannot be started with `Process.Start`, the test host has to be registered and activated by AUMID. For `UseWinUI` projects, `MSTest.Sdk` references [`Microsoft.Testing.Extensions.PackagedApp`](https://www.nuget.org/packages/Microsoft.Testing.Extensions.PackagedApp) automatically and its generated runner helper registers the launcher.

Set `<EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp>` only when a custom launcher owns packaged activation. Do **not** also call `builder.AddPackagedAppDeployment()`, because at most one test host launcher may be registered per run.

Requirements and limitations:

- Target a Windows TFM at platform version `10.0.19041.0` or higher (for example `net8.0-windows10.0.19041.0`) so NuGet resolves the Windows asset that contains the register-and-activate path. The plain `net8.0`/`net9.0` asset fails fast with an actionable message instead.
- Registering an unsigned build-output layout requires **Developer Mode** (or sideloading) to be enabled on the machine.
- `packagedClassicApp`/`win32App` hosts receive MTP arguments as normal process `argv`, including classic hosts whose trust level is `appContainer`.
- `windowsApp`/UWP hosts receive one opaque string through `LaunchActivatedEventArgs.Arguments`. Restore the platform argument array with `PackagedAppExtensions.GetTestApplicationArguments(args.Arguments)` before `TestApplication.CreateBuilderAsync`; see [Launch activation](#launch-activation).
- The controller named pipe additionally authorizes the exact package SID of an AppContainer host, which a restricted AppContainer token needs in order to connect at all; see [Controller pipe access for AppContainer hosts](#controller-pipe-access-for-appcontainer-hosts).

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

This bootstrap also restores the existing controller connect-back environment handoff before MTP reads it.

## Controller pipe access for AppContainer hosts

Microsoft.Testing.Platform starts an out-of-process test host under a *test host controller* and the two talk over a named pipe. That pipe is created with the equivalent of `PipeOptions.CurrentUserOnly`: it is owned by the creating token's owner SID and its DACL grants only that SID. This is what keeps another user — and, thanks to the owner (rather than user) SID, a differently-elevated process of the same user — out of your test run.

A **UWP or AppContainer-configured WinUI** host cannot connect to such a pipe. An AppContainer runs with a *restricted* token, and Windows only grants access when the normal access check **and** the restricted-SID check both succeed. The restricting SIDs of an AppContainer contain the app's package SID, so a DACL that names only the user denies the host even though it belongs to the same signed-in user. Knowing the pipe name — or restoring the activation arguments — does not help.

`Microsoft.Testing.Extensions.PackagedApp` closes that gap: when the layout is a packaged app that declares an AppContainer application, the launcher derives that package's own AppContainer SID from its package family name and asks the platform to add it to the pipe DACL before the pipe is created. Sandbox membership is classified separately from argument delivery: a `packagedClassicApp` at `TrustLevel="appContainer"` receives plain `argv` but still needs the SID grant, while a `windowsApp` explicitly at `TrustLevel="mediumIL"` uses launch activation without running in an AppContainer.

What the resulting pipe grants:

| Principal | Rights |
| --- | --- |
| The creating token's owner SID | full control |
| The one authorized package SID | read, write, read-security and synchronize only |

Notable properties:

- The grant is scoped to **your** package. `ALL APPLICATION PACKAGES` (`S-1-15-2-1`) is never granted, and the platform rejects any request for it — or for a user, a group, or `Everyone` — with an error rather than widening the pipe.
- The package cannot create another instance of the pipe (`FILE_CREATE_PIPE_INSTANCE` is not granted), change its DACL, or delete it.
- The DACL is protected, and the pipe rejects remote clients.
- Authorization-enabled pipes use the Windows-required `LOCAL\` namespace (`\\.\pipe\LOCAL\<name>`); the resolved `LOCAL\<name>` value is what the controller hands to the host.
- The pipe keeps the controller's own integrity level — no mandatory label is lowered — so Mandatory Integrity Control stays a second gate behind the DACL.
- Nothing changes for a packaged **full-trust** desktop host, an unpackaged app, an ordinary console test app, or any non-Windows run: the pipe is created exactly as it was before.
- No loopback exemption (`CheckNetIsolation LoopbackExempt`) is used or needed — that applies to network sockets, not named pipes.

> **Elevated controllers are out of scope.** The pipe is owned by the creating token's *owner* SID, which for an elevated controller is `BUILTIN\Administrators`. An AppContainer can never be elevated, so its own owner SID is the user — and the client-side `PipeOptions.CurrentUserOnly` check then rejects the connection as "not owned by the current user" even though the DACL admits it. Run AppContainer test hosts from a non-elevated controller.

Set `TESTINGPLATFORM_PACKAGEDAPP_PIPEAUTHORIZATION` to override the decision. Values are compared case-insensitively; anything unrecognized falls back to `auto`.

| Value | Behavior |
| --- | --- |
| unset, or `auto` | Authorize the package SID only when the manifest declares an AppContainer application. |
| `always` | Authorize the package SID for any packaged layout. Use this if the manifest classification misreads your app. |
| `never` | Never authorize anything; the pipe keeps its current-user-only DACL. |

## When does the PackagedApp launcher take over?

Registering an *enabled* test host launcher switches the whole run to the test host controller (process restart) model, because a custom launcher only has an effect when an out-of-process test host is started. That is the right trade for a packaged app, but it is pure overhead for an app that just needs `Process.Start`.

The launcher therefore decides for itself, per run:

| Situation | Launcher enabled? | Effect |
| --- | --- | --- |
| Not Windows | no | Nothing changes. Packaged Windows apps are a Windows-only concept. |
| Supported packaged full-trust layout (an `AppxManifest.xml` that describes this app — see below) | yes | The layout is registered and activated by AUMID. |
| True UWP/AppContainer packaged layout | yes, but unsupported | Registration, activation arguments, and exact package-SID pipe authorization are implemented; SDK/platform routing still does not support starting it as an MTP test host. |
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
- **Publish the exit code yourself.** The WinUI-generated entry point is `void`, so a self-hosted test app must assign the result of `MicrosoftTestingPlatformApplication.RunAsync(args)` to `Environment.ExitCode` and then call `Exit()`. Without it the process always exits `0` and failing tests never fail the build.
- **MrtCore warns about MSTest's satellite assemblies.** A WinUI build indexes resources with MrtCore, which reports `PRI257`/`PRI263` because MSTest ships localized satellite assemblies that carry no `en-US` default. It is benign — the warning is about resource *lookup* fallback, not about your tests. Note it cannot be silenced with `NoWarn` or `MSBuildWarningsAsMessages`, because MrtCore puts the `PRI…` code in the message text rather than in the MSBuild warning code; if you build with warnings as errors, exclude this project from that setting.
- **Avoid `dotnet exec`.** WinUI resolves its PRI resources relative to the *process* path, so running the test app under `dotnet.exe` breaks resource loading. `Microsoft.Testing.Platform.MSBuild` already prefers launching the apphost directly when one is available.
- **Startup failures surface as test failures.** If the application cannot be brought up on the `WinUITestTarget` path — a throwing `Application` constructor, or in an unpackaged app a Windows App SDK runtime that cannot be resolved (`COMException` / `REGDB_E_CLASSNOTREG`) — the failure is now reported on the test. Earlier versions swallowed it and the run hung with no diagnostic at all.

## How the Windows App SDK gets initialized

An unpackaged app has no MSIX manifest declaring a framework dependency, so the Windows App SDK runtime has to be resolved at startup by the *bootstrapper*. You normally write no code for this: when

- `WindowsPackageType` is `None`,
- `WindowsAppSDKSelfContained` is not `true`, and
- `OutputType` is `Exe` or `WinExe`,

the Windows App SDK build injects a [module initializer](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/general#moduleinitializer-attribute) into **your app assembly** that calls `Bootstrap.Initialize`. The CLR runs a module initializer before the first use of any type in that assembly, so it has already run by the time your tests execute.

That is why a plain `[TestMethod]` — not just `[UITestMethod]` — can call Windows App SDK WinRT APIs in an unpackaged test app.

Note those conditions are evaluated per project, and a **class library** does not get the initializer. If your tests live in a library loaded by a host that is not itself a Windows App SDK app, set `<WindowsAppSdkBootstrapInitialize>true</WindowsAppSdkBootstrapInitialize>` explicitly.

## Related

- [`Microsoft.Testing.Extensions.PackagedApp` package readme](../src/Platform/Microsoft.Testing.Extensions.PackagedApp/PACKAGE.md)
- [RFC 017 — Test host launcher](RFCs/017-TestHost-Launcher.md)
- [`UwpVSTestApp` sample](../samples/public/UwpVSTestApp) (packaged UWP, VSTest)
- [`WinUIVSTestApp` sample](../samples/public/WinUIVSTestApp) (packaged WinUI, VSTest)
- [`WinUIMtpPackagedApp` sample](../samples/public/WinUIMtpPackagedApp) (packaged, MTP)
- [`WinUIMtpUnpackagedApp` sample](../samples/public/WinUIMtpUnpackagedApp) (unpackaged, MTP)
