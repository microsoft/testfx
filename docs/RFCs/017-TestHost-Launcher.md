# RFC 017 - Custom test host launcher

- [ ] Approved in principle
- [x] Under discussion
- [x] Implementation
- [ ] Shipped

## Summary

Introduce **`ITestHostLauncher`**: a public, experimental Microsoft.Testing.Platform (MTP)
extension point that lets an extension control **how** the out-of-process test host is launched,
instead of the platform always doing `Process.Start`. The platform still owns everything around
the launch — argument/environment preparation, the controller↔host IPC pipe, PID tracking,
`ITestHostProcessLifetimeHandler` callbacks, and exit-code reconciliation — and simply delegates
the single "create and start the test host" step to the registered launcher.

The hook is deliberately **agnostic of the launch mechanism**: the launcher does not have to start a
local OS process. It can deploy and activate a packaged application, launch a container, or start
the host on a remote machine. To make this explicit, the launcher returns an `ITestHostHandle` that
exposes only the lifecycle the platform needs (`WaitForExitAsync`, `ExitCode`, `HasExited`,
`Exited`, `Terminate`); a process id is *optional* and used purely for diagnostics.

The motivating scenario is **packaging and deployment of WinUI applications** (see
[#2784](https://github.com/microsoft/testfx/issues/2784)): packaged/MSIX apps cannot be started with
`Process.Start` and must be deployed and then activated by AUMID, while unpackaged WinUI apps
similarly benefit from a custom deploy + launch step. The same hook also enables launching the test
host under a debugger, elevated, inside a container, or on a remote machine.

## Motivation

MTP runs the test host out-of-process whenever a "test host controller" extension is active (hang
dump, crash dump, or any `ITestHostProcessLifetimeHandler` / `ITestHostEnvironmentVariableProvider`).
That work happens in `TestHostControllersTestHost`, which prepares a `ProcessStartInfo` (arguments,
environment variables including the `MONITORTOHOST` pipe name) and then launches the host with a
single call:

```csharp
using IProcess testHostProcess = process.Start(processStartInfo);
```

Everything downstream only needs a handful of things from the returned handle — a way to observe
exit (`Exited`, `WaitForExitAsync()`, `ExitCode`, `HasExited`), optionally a PID for logging, and a
way to tear it down (`Kill`) — plus the child connecting back on the named pipe whose name was
injected via an environment variable. **`Process.Start` is the only assumption that does not hold
universally.** Several real scenarios need a different launch mechanism:

- **Packaged WinUI/MSIX**: a packaged app must be deployed (in Developer Mode, register the loose
  layout) and then activated by Application User Model ID (AUMID) via `IApplicationActivationManager`,
  not started from an executable path. This is the blocker behind
  [#2784](https://github.com/microsoft/testfx/issues/2784) and the reason VSTest's
  `UwpTestHostRuntimeProvider` exists.
- **Debugger attach/launch**: start the host suspended (or under a debugger launcher such as
  `vsdbg` / `WinDbg` / `dlv`) and only then resume.
- **Elevation**: run the test host as administrator (UAC) or as another user.
- **Container / remote**: launch the host inside a container (`docker run`) or on a remote device
  over SSH/WinRM, then bridge the pipe — neither of which exposes a local, query-able PID.

Today none of these is possible without forking the platform. The existing experimental
`ITestHostExecutionOrchestrator` sits at the wrong layer (see [Alternatives](#alternatives)). This
RFC adds the *minimal* hook at exactly the launch site.

## Goals

- Let an extension substitute the test host launch step while the platform keeps owning
  argument/env preparation, IPC, lifetime-handler dispatch, and exit-code handling.
- Keep hang dump, crash dump, and all `ITestHostProcessLifetimeHandler` /
  `ITestHostEnvironmentVariableProvider` extensions working unchanged when a custom launcher is
  present.
- Be generic enough to cover WinUI deploy+activate, debugger, elevation, container, and remote
  launch with one shape, **without assuming the launched thing is a local OS process**.
- Follow MTP's experimental-API conventions so the surface can evolve before stabilizing.

## Non-goals

- Replacing the *entire* run loop (that is `ITestHostExecutionOrchestrator`'s job).
- Remote **device deployment/bootstrapping** of the Windows App SDK framework + agent (VSTest's
  `Microsoft.UniversalApps.Deployment` has no public redistributable; out of scope — local launch
  only).
- Shipping the WinUI package/deploy extension itself. This RFC only adds the platform hook; the
  package/deploy extension is a separate deliverable that consumes it.
- Changing the in-process (single-process, `ConsoleTestHost`) execution path.

## Detailed design

### Where it plugs in

The hook lives in the **test host controllers** layer, next to the existing lifetime-handler and
environment-variable-provider extension points, and is registered through
`ITestHostControllersManager`.

```csharp
namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Allows an extension to control how the out-of-process test host is launched,
/// replacing the platform's default <c>Process.Start</c> behavior.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostLauncher : ITestHostControllersExtension // : IExtension
{
    /// <summary>
    /// Creates and starts the test host. The platform has already prepared the file name,
    /// arguments, and environment variables (including the controller IPC pipe name) carried by
    /// <paramref name="context"/>. The implementation must return a handle the platform can monitor.
    /// </summary>
    Task<ITestHostHandle> LaunchTestHostAsync(
        TestHostLaunchContext context,
        CancellationToken cancellationToken);
}
```

The platform passes the fully-prepared launch information:

```csharp
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class TestHostLaunchContext
{
    public TestHostLaunchContext(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environmentVariables,
        string? workingDirectory);

    /// <summary>The default test host executable path the platform would have started.</summary>
    public string FileName { get; }

    /// <summary>Arguments, already including the test host controller PID option.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// The final environment for the test host, after all
    /// <see cref="ITestHostEnvironmentVariableProvider"/> ran. Includes the
    /// controller↔host IPC pipe name the host must connect back on.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }

    /// <summary>The working directory, or null to inherit the current one.</summary>
    public string? WorkingDirectory { get; }
}
```

And the launcher returns a launch-mechanism-agnostic handle (the platform adapts it to its internal
monitoring contract):

```csharp
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostHandle
{
    event EventHandler Exited;

    /// <summary>The OS process id, when available. Null for container/remote launches. Logging only.</summary>
    int? ProcessId { get; }

    int ExitCode { get; }
    bool HasExited { get; }
    Task WaitForExitAsync();

    /// <summary>Best-effort teardown (e.g. when hang dump aborts the run).</summary>
    void Terminate();
}
```

Registration mirrors the existing methods on `ITestHostControllersManager`:

```csharp
public interface ITestHostControllersManager
{
    // existing: AddEnvironmentVariableProvider(...), AddProcessLifetimeHandler(...)

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddTestHostLauncher(Func<IServiceProvider, ITestHostLauncher> testHostLauncherFactory);

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddTestHostLauncher<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostLauncher;
}
```

### Platform integration (what changes inside MTP)

1. **Swap the launch call.** In `TestHostControllersTestHost.InternalRunAsync`, at the current
   `process.Start(processStartInfo)` site (after `BeforeTestHostProcessStartAsync` and after all
   env-var providers ran), if a launcher is registered, build a `TestHostLaunchContext` from the
   `ProcessStartInfo` and `await launcher.LaunchTestHostAsync(...)`. Otherwise keep the default
   `process.Start`. The returned `ITestHostHandle` is adapted to the internal `IProcess` monitoring
   contract — which only uses `Id` / `Exited` / `WaitForExitAsync` / `ExitCode` / `HasExited` /
   `Kill`. Because `ProcessId` is optional, the PID-access path tolerates a `null` PID for
   container/remote launchers.
2. **Force the controller host.** A launcher makes `RequireProcessRestart` `true` when one is
   registered (computed in `TestHostControllersManager.BuildAsync`, checked in
   `TestHostBuilder.Modes.cs`); without this, a run with *only* a launcher (no dump/lifetime
   extension) would stay in-process and there would be nothing to launch.
3. **Singleton.** At most one launcher may be registered; a duplicate fails fast at build time with
   a localized "only one test host launcher" error.
4. **Preserve ordering and services.** Because the call stays at the same point,
   `ITestHostEnvironmentVariableProvider`, the `MONITORTOHOST` IPC pipe, the PID handshake, and
   `ITestHostProcessLifetimeHandler` (and therefore hang dump and crash dump) all keep working with
   no changes.

### Contract requirements on the launcher

- The launched host **must** receive `context.EnvironmentVariables` (so it connects back on the
  controller pipe) and **must** be passed `context.Arguments`.
- The returned handle must report exit reliably (`WaitForExitAsync`, `ExitCode`, `HasExited`,
  `Exited`) and support `Terminate()` (hang dump terminates the host through it).
- `ProcessId` may be `null` when there is no local, query-able process (container/remote). It is
  used only for diagnostics.
- If the launcher cannot start the host it should throw; the platform surfaces it as a
  platform-setup failure.

## Examples

All examples assume the extension is registered on the builder, e.g. from a `…Extensions` helper:

```csharp
builder.TestHostControllers.AddTestHostLauncher(sp => new MyLauncher(sp));
```

### 1. Packaged WinUI / MSIX (the motivating case)

Deploy the loose layout (Developer Mode) and activate the packaged app by AUMID instead of starting
an exe. The activated app self-hosts MTP (as the `MSTestRunnerWinUI` sample already does) and
connects back on the env-provided pipe.

```csharp
public Task<ITestHostHandle> LaunchTestHostAsync(
    TestHostLaunchContext context, CancellationToken cancellationToken)
{
    // 1. Parse the .appxrecipe / AppxManifest.xml next to context.FileName to get the AUMID
    //    and (in Developer Mode) register the loose layout:
    //    new PackageManager().RegisterPackageByUriAsync(manifestUri, options);
    string aumid = AppxManifest.ResolveAumid(context.FileName);

    // 2. Activate, passing the SAME args the platform prepared.
    var aam = (IApplicationActivationManager)new ApplicationActivationManager();
    aam.ActivateApplication(aumid, string.Join(' ', context.Arguments), ACTIVATEOPTIONS.AO_NONE, out uint pid);

    // 3. Wrap the returned PID. The app inherits context.EnvironmentVariables
    //    (incl. the MONITORTOHOST pipe name) via its activation/launch profile.
    return Task.FromResult<ITestHostHandle>(new ProcessIdHandle((int)pid));
}
```

> Note: enabling the controller→host pipe across the AppContainer sandbox requires a loopback/pipe-ACL
> step (e.g. `CheckNetIsolation LoopbackExempt` or granting the package SID on the pipe). That belongs
> to the package/deploy extension, not the platform.

### 2. Launch under a debugger

```csharp
public async Task<ITestHostHandle> LaunchTestHostAsync(
    TestHostLaunchContext context, CancellationToken cancellationToken)
{
    var psi = new ProcessStartInfo(context.FileName) { UseShellExecute = false };
    foreach (string arg in context.Arguments) psi.ArgumentList.Add(arg);
    foreach (var kvp in context.EnvironmentVariables) psi.Environment[kvp.Key] = kvp.Value;
    psi.Environment["DOTNET_DefaultDiagnosticPortSuspend"] = "1"; // start suspended

    Process p = Process.Start(psi)!;
    await DebuggerLauncher.AttachAsync(p.Id, cancellationToken); // e.g. vsdbg / WinDbg / dlv
    await DebuggerLauncher.ResumeAsync(p.Id, cancellationToken);
    return new ProcessHandleAdapter(p);
}
```

### 3. Elevated (run as administrator)

```csharp
public Task<ITestHostHandle> LaunchTestHostAsync(
    TestHostLaunchContext context, CancellationToken cancellationToken)
{
    var psi = new ProcessStartInfo(context.FileName)
    {
        UseShellExecute = true,   // required for the UAC "runas" verb
        Verb = "runas",
    };
    foreach (string arg in context.Arguments) psi.ArgumentList.Add(arg);
    // NOTE: UseShellExecute = true cannot pass per-process env vars; an elevated launcher
    // must forward context.EnvironmentVariables another way (e.g. a temp response file the
    // host reads, or a broker that sets them) so the host still finds the controller pipe.
    Process p = Process.Start(psi)!;
    return Task.FromResult<ITestHostHandle>(new ProcessHandleAdapter(p));
}
```

This example deliberately shows a sharp edge: elevation via the shell loses per-process environment
variables, so the launcher is responsible for re-delivering them. The platform contract only
requires that the host ends up with `context.EnvironmentVariables`.

### 4. Container

Run the test host inside a container and bridge the pipe. The returned handle tracks the
`docker run` client process; `Terminate()` tears down the container.

```csharp
public Task<ITestHostHandle> LaunchTestHostAsync(
    TestHostLaunchContext context, CancellationToken cancellationToken)
{
    var args = new List<string> { "run", "--rm", "--init" };
    foreach (var kvp in context.EnvironmentVariables) { args.Add("-e"); args.Add($"{kvp.Key}={kvp.Value}"); }
    // Map the controller pipe into the container (Windows named pipe / Unix domain socket mount).
    args.Add("test-image:latest");
    args.Add(context.FileName);
    args.AddRange(context.Arguments);

    var psi = new ProcessStartInfo("docker") { UseShellExecute = false };
    foreach (string a in args) psi.ArgumentList.Add(a);
    Process p = Process.Start(psi)!;
    return Task.FromResult<ITestHostHandle>(new ProcessHandleAdapter(p));
}
```

### 5. Remote (SSH)

```csharp
public Task<ITestHostHandle> LaunchTestHostAsync(
    TestHostLaunchContext context, CancellationToken cancellationToken)
{
    string env = string.Join(' ', context.EnvironmentVariables
        .Where(kv => kv.Value is not null)
        .Select(kv => $"{kv.Key}={Quote(kv.Value!)}")); // values are nullable; skip unset vars
    string remoteCmd = $"{env} {Quote(context.FileName)} {string.Join(' ', context.Arguments.Select(Quote))}";

    var psi = new ProcessStartInfo("ssh") { UseShellExecute = false };
    psi.ArgumentList.Add("user@remote-host");
    psi.ArgumentList.Add(remoteCmd);
    Process ssh = Process.Start(psi)!; // tunnel the controller pipe over the SSH connection
    // The handle tracks the local ssh client; ProcessId here is the ssh client PID (diagnostic only).
    return Task.FromResult<ITestHostHandle>(new ProcessHandleAdapter(ssh));
}
```

## Alternatives considered

### Reuse `ITestHostExecutionOrchestrator`

MTP already ships an experimental `ITestHostExecutionOrchestrator`
(`ITestHostOrchestratorManager.AddTestHostOrchestrator`). It was rejected as the vehicle because it
sits **above** the controller: `OrchestrateTestHostExecutionAsync` runs in
`TestHostOrchestratorHost` and replaces the *entire* execution, returning only an exit code. An
implementation would have to re-create everything `TestHostControllersTestHost` provides —
environment-variable providers, the `MONITORTOHOST` IPC/PID handshake, and the
`ITestHostProcessLifetimeHandler` fan-out that **hang dump and crash dump depend on**. That is the
wrong granularity for "launch the host differently." The orchestrator remains the right tool for
whole-run concerns (e.g. retry/repeat that re-runs the host).

### Make the internal `IProcessHandler` replaceable via DI

`IProcessHandler` / `IProcess` are `internal` and surface `Process`-specific members (e.g.
`MainModule`). Exposing them publicly would leak implementation detail, over-commit the surface, and
bake in the "it's always a local process" assumption. A purpose-built, minimal, mechanism-agnostic
`ITestHostHandle` is cleaner and evolvable.

### A process-centric `ITestHostProcessLauncher` returning a `ProcessId`

An earlier draft of this RFC named the hook `ITestHostProcessLauncher` and returned an
`ITestHostProcessHandle` whose `ProcessId` was mandatory. That over-commits to "the test host is a
local OS process," which is false for container and remote launches and awkward for AUMID-activated
apps. The current design renames the types to drop "Process", makes `ProcessId` optional, and names
the teardown `Terminate()` instead of `Kill()`.

### Do nothing (keep `Process.Start`)

Leaves [#2784](https://github.com/microsoft/testfx/issues/2784) unsolvable on MTP for packaged apps
and blocks the debugger/elevation/container/remote scenarios, all of which today require forking the
platform.

## Compatibility and conventions

- **Experimental.** All new types and methods are gated behind
  `[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]`, consistent
  with the other test-host-controller-era experimental APIs.
- **Public API tracking.** New members are added to `PublicAPI.Unshipped.txt` with the `[TPEXP]`
  prefix.
- **No `init` accessors** on any new public API, per repo policy.
- **No behavior change when unused.** If no launcher is registered, the platform behaves exactly as
  today (`Process.Start`), and the controller host is selected only when it already would be.

## Open questions

- **CLI/debug integration.** Should the platform expose a built-in `--launcher`-style selector, or
  is builder/MSBuild registration sufficient for v1?
- **Cancellation semantics.** Define precisely what `Terminate()` must guarantee for remote/container
  launchers (best-effort teardown vs. synchronous termination).
- **Multiple launchers.** Singleton for v1; is there ever a composition story (e.g. debugger +
  elevation), or do implementers compose manually?
