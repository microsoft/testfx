# Microsoft.Testing.Extensions.PackagedApp

`Microsoft.Testing.Extensions.PackagedApp` is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that starts Windows test hosts which cannot simply be `Process.Start`ed. In its Windows build (`net*-windows`) a packaged MSIX host is registered with the OS and activated by Application User Model ID (AUMID); a non-packaged (loose-layout) host can optionally be deployed into an isolated directory and launched from there.

It is the consumer of the platform's `ITestHostLauncher` extension point for Windows test hosts. Packaged Windows apps require package identity and ship as MSIX; VSTest exposes a single `UwpTestHostRuntimeProvider` for the equivalent scenario, built on Visual-Studio-internal deployment components, whereas this extension uses only public, redistributable Windows APIs:

- **Packaged AUMID activation** (Windows build): a packaged (MSIX) layout is registered in place with the `PackageManager` and the app is activated by AUMID via `IApplicationActivationManager`. `packagedClassicApp`/`win32App` hosts receive the platform-prepared command line through `argv`, including when their trust level is `appContainer`. `windowsApp`/UWP hosts receive one opaque launch string and restore the exact logical argument array through `PackagedAppExtensions.GetTestApplicationArguments(args.Arguments)` in `Application.OnLaunched` (see [#10485](https://github.com/microsoft/testfx/issues/10485)). Registering an unsigned build-output layout requires Developer Mode (or sideloading). The plain `net8.0`/`net9.0` build rejects a packaged layout with an actionable error pointing at the Windows TFM.
- **Deploy + launch loose layout** (opt-in): a non-packaged app — one without an `AppxManifest.xml` — is deployed to a deployment directory and the produced executable is launched from there.

## When the launcher takes over

Registering an *enabled* test host launcher switches the run to the test host controller (process restart) model, because a launcher only has an effect when an out-of-process test host is started. To avoid charging that cost to apps that do not need it, the launcher enables itself only when it has real work to do:

| Situation | Enabled? |
| --- | --- |
| Not Windows | no |
| Packaged layout (an `AppxManifest.xml` that describes this app) | yes |
| Any other layout, including **unpackaged WinUI** and ordinary console test apps | no |

A manifest in the app's own directory is taken as the app's layout. A manifest in an ancestor directory is used only when one of its `<Application>` entries declares an `Executable` resolving back to the app directory (and, at launch, to that exact executable), which supports `Application/@Executable` pointing into a package subdirectory at any depth while keeping a stray manifest in a shared build root from classifying an unrelated app as packaged.

So referencing this package from an unpackaged app costs nothing: no extra process, and no copy of the build output.

## Controller pipe access for AppContainer hosts

The platform's test host controller talks to the test host over a named pipe created with the equivalent of `PipeOptions.CurrentUserOnly`: it is owned by the creating token's owner SID and its DACL grants only that SID, which is what keeps another user — or a differently-elevated process of the same user — out of the run.

A **UWP or AppContainer-configured WinUI** host cannot connect to such a pipe. An AppContainer runs with a *restricted* token and Windows grants access only when the normal access check **and** the restricted-SID check both succeed; the restricting SIDs contain the app's package SID, so a DACL naming only the user denies the host even though it belongs to the same signed-in user.

When the layout is a packaged app that declares an AppContainer application, this extension derives that package's own AppContainer SID from its package family name and asks the platform to authorize it on the pipe before the pipe is created. Sandbox membership is deliberately classified separately from argument delivery: a `packagedClassicApp` at `TrustLevel="appContainer"` receives plain `argv` but still needs the SID grant, while a `windowsApp` explicitly at `TrustLevel="mediumIL"` uses launch activation without running in an AppContainer. The grant is minimal and scoped:

- only **that** package SID is added — `ALL APPLICATION PACKAGES` (`S-1-15-2-1`) is never granted, and the platform rejects any request for a user, a group or `Everyone`;
- it receives read, write, read-security and synchronize rights only, so it can never create another instance of the pipe, change its DACL, or delete it;
- the DACL stays protected and the pipe rejects remote clients;
- authorization-enabled pipes use the Windows-required `LOCAL\` namespace (`\\.\pipe\LOCAL\<name>`);
- the pipe keeps the controller's own integrity level, so Mandatory Integrity Control stays a second gate behind the DACL;
- a packaged full-trust host, an unpackaged app, and every non-Windows run keep the existing pipe unchanged.

Set `TESTINGPLATFORM_PACKAGEDAPP_PIPEAUTHORIZATION` to `never` to disable the grant entirely, or to `always` to request it for any packaged layout; `auto` (the default) probes the manifest.

Set `TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER` to override that decision — `always` opts a non-packaged layout into deploy-and-launch, `never` keeps the launcher out of the way entirely, and `auto` (the default) probes the layout.

## Launch-activation bootstrap

A `windowsApp`/UWP app has no `Main(string[] args)` receiving the activation string as process arguments. Its `OnLaunched` override must restore the MTP argument array before creating the builder. A `packagedClassicApp`/`win32App` uses its normal process arguments instead, even when its trust level is `appContainer`:

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

The handoff is versioned and length-prefixed, so empty values, whitespace, quotes, backslashes, Unicode, repeated options, and option order round-trip exactly. Payloads within Windows' documented 2,048-character launch-argument envelope stay entirely in the activation string. Larger payloads are written to package `LocalState` only as authenticated ciphertext; the one-shot key remains in the activation string, and both the host and launcher delete the file at the earliest cleanup point. User filters, runsettings, and other arguments are never persisted in plaintext.

Argument restoration and exact package-SID pipe authorization are both implemented. They are the communication primitives needed after AppContainer activation; true UWP/AppContainer still is not an end-to-end MTP test-host mode because the SDK/platform startup path routes those projects to VSTest rather than starting an ordinary MTP controller process. Full-trust packaged and unpackaged hosts are unaffected by that limitation.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.PackagedApp` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.PackagedApp
```

## Usage

Referencing this package automatically registers its test-host launcher. Packaged layouts are detected automatically; set `TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER=always` to opt a non-packaged loose layout into deployment, or `never` to disable the launcher.

For a `windowsApp`/UWP host, restore the launch activation arguments with `PackagedAppExtensions.GetTestApplicationArguments` before creating the test application builder, as shown in [Launch-activation bootstrap](#launch-activation-bootstrap).

## About

This package extends Microsoft.Testing.Platform with:

- **Registration + activation**: registers a packaged MSIX test host and activates it by AUMID (see [#9933](https://github.com/microsoft/testfx/issues/9933) and [#10485](https://github.com/microsoft/testfx/issues/10485)); optionally stages a non-packaged (loose-layout) Windows test host payload into an isolated directory and launches the deployed copy.
- **Mechanism-agnostic monitoring**: returns an `ITestHostHandle` that exposes only the lifecycle the platform needs (surfacing the activated process id for the packaged path, and none for the deployed loose-layout path).

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
