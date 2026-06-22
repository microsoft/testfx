# RFC 017 - Custom test host process launcher

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Introduce **`ITestHostProcessLauncher`**: a public, experimental Microsoft.Testing.Platform (MTP) extension point that lets an extension control **how** the out-of-process test host is launched, instead of the platform always doing `Process.Start`. The platform still owns everything around the launch — argument/environment preparation, the controller↔host IPC pipe, PID tracking, `ITestHostProcessLifetimeHandler` callbacks, and exit-code reconciliation — and simply delegates the single "create the process" step to the registered launcher.

The motivating scenario is testing **packaged WinUI/MSIX applications**, which cannot be started with `Process.Start` and must be activated by AUMID (see [#2784](https://github.com/microsoft/testfx/issues/2784)). But the abstraction is deliberately generic: the same hook enables launching the test host under a debugger, elevated, inside a container, or on a remote machine.

## Motivation

MTP runs the test host out-of-process whenever a "test host controller" extension is active (hang dump, crash dump, or any `ITestHostProcessLifetimeHandler` / `ITestHostEnvironmentVariableProvider`). That work happens in `TestHostControllersTestHost`, which prepares a `ProcessStartInfo` (arguments, environment variables including the `MONITORTOHOST` pipe name) and then launches the host with a single call:

```csharp
using IProcess testHostProcess = process.Start(processStartInfo);
```

Everything downstream only needs five things from the returned handle — `Id`, the `Exited` event, `WaitForExitAsync()`, `ExitCode`, and `Kill()` — plus the child connecting back on the named pipe whose name was injected via an environment variable. **`Process.Start` is the only assumption that does not hold universally.** Several real scenarios need a different launch mechanism:

- **Packaged WinUI/MSIX**: a packaged app must be activated by Application User Model ID (AUMID) via `IApplicationActivationManager`, not started from an executable path. This is the blocker behind [#2784](https://github.com/microsoft/testfx/issues/2784) and the reason VSTest's `UwpTestHostRuntimeProvider` exists.
- **Debugger attach/launch**: start the host suspended (or under a debugger launcher such as `vsdbg` / `WinDbg` / `dlv`) and only then resume.
- **Elevation**: run the test host as administrator (UAC) or as another user.
- **Container / remote**: launch the host inside a container (`docker run`) or on a remote device over SSH/WinRM, then bridge the pipe.

Today none of these is possible without forking the platform. The existing experimental `ITestHostExecutionOrchestrator` sits at the wrong layer (see [Alternatives](#alternatives)). This RFC adds the *minimal* hook at exactly the launch site.

## Goals

- Let an extension substitute the test host process creation step while the platform keeps owning argument/env preparation, IPC, lifetime-handler dispatch, and exit-code handling.
- Keep hang dump, crash dump, and all `ITestHostProcessLifetimeHandler` / `ITestHostEnvironmentVariableProvider` extensions working unchanged when a custom launcher is present.
- Be generic enough to cover WinUI activation, debugger, elevation, container, and remote launch with one shape.
- Follow MTP's experimental-API conventions so the surface can evolve before stabilizing.

## Non-goals

- Replacing the *entire* run loop (that is `ITestHostExecutionOrchestrator`'s job).
- Remote **device deployment/bootstrapping** of the Windows App SDK framework + agent (VSTest's `Microsoft.UniversalApps.Deployment` has no public redistributable; out of scope — local launch only).
- Shipping the WinUI deployment extension itself. This RFC only adds the platform hook; the WinUI extension is a separate deliverable that consumes it.
- Changing the in-process (single-process, `ConsoleTestHost`) execution path.

## Detailed design

### Where it plugs in

The hook lives in the **test host controllers** layer, next to the existing lifetime-handler and environment-variable-provider extension points, and is registered through `ITestHostControllersManager`.

```csharp
namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Allows an extension to control how the out-of-process test host is launched,
/// replacing the platform's default <c>Process.Start</c> behavior.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostProcessLauncher : ITestHostControllersExtension // : IExtension
{
    /// <summary>
    /// Creates and starts the test host process. The platform has already prepared the
    /// file name, arguments, and environment variables (including the controller IPC pipe
    /// name). The implementation must return a handle the platform can monitor.
    /// </summary>
    Task<ITestHostProcessHandle> LaunchTestHostProcessAsync(
        TestHostProcessLaunchContext context,
        CancellationToken cancellationToken);
}
```

The platform passes the fully-prepared launch information:

```csharp
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class TestHostProcessLaunchContext
{
    public TestHostProcessLaunchContext(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environmentVariables,
        string workingDirectory);

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

    public string WorkingDirectory { get; }
}
```

And the launcher returns a public-safe handle (a subset of the internal `IProcess`, which the platform adapts to its monitoring):

```csharp
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostProcessHandle
{
    int ProcessId { get; }
    int ExitCode { get; }
    bool HasExited { get; }
    event EventHandler Exited;
    Task WaitForExitAsync();
    void Kill();
}
```

Registration mirrors the existing methods on `ITestHostControllersManager`:

```csharp
public interface ITestHostControllersManager
{
    // existing: AddEnvironmentVariableProvider(...), AddProcessLifetimeHandler(...)

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddTestHostProcessLauncher(
        Func<IServiceProvider, ITestHostProcessLauncher> factory);

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddTestHostProcessLauncher<T>(CompositeExtensionFactory<T> factory)
        where T : class, ITestHostProcessLauncher;
}
```

### Platform integration (what changes inside MTP)

1. **Swap the launch call.** In `TestHostControllersTestHost.InternalRunAsync`, at the current `process.Start(processStartInfo)` site (after `BeforeTestHostProcessStartAsync` and after all env-var providers ran), if a launcher is registered, build a `TestHostProcessLaunchContext` from the `ProcessStartInfo` and `await launcher.LaunchTestHostProcessAsync(...)`. Otherwise keep the default `process.Start`. The returned `ITestHostProcessHandle` is adapted to the internal `IProcess` monitoring contract — which already only uses `Id` / `Exited` / `WaitForExitAsync` / `ExitCode` / `Kill`.
2. **Force the controller host.** Add the launcher to `TestHostControllerConfiguration` and make `RequireProcessRestart` `true` when a launcher is registered. Host selection is gated on `RequireProcessRestart` (`TestHostControllersManager.BuildAsync` → checked in `TestHostBuilder.Modes.cs`); without this, a run with *only* a launcher (no dump/lifetime extension) would stay in-process and there would be nothing to launch.
3. **Singleton.** Validate that at most one launcher is registered, matching the orchestrator's "Multiple … not supported" behavior. A duplicate registration fails fast at build time.
4. **Preserve ordering and services.** Because the call stays at the same point, `ITestHostEnvironmentVariableProvider`, the `MONITORTOHOST` IPC pipe, the PID handshake, and `ITestHostProcessLifetimeHandler` (and therefore hang dump and crash dump) all keep working with no changes.

### Contract requirements on the launcher

- The launched process **must** be a real OS process with a queryable PID, it **must** inherit/receive `context.EnvironmentVariables` (so it connects back on the controller pipe), and it **must** be passed `context.Arguments`.
- The returned handle must report exit reliably (`WaitForExitAsync`, `ExitCode`, `Exited`) and support `Kill()` (hang dump terminates the host through it).
- If the launcher cannot start the process it should throw; the platform surfaces it as a platform-setup failure.

### Minimal-surface variant

If we prefer not to expose a handle type initially, the launcher can instead return `Task<int>` (the PID) and the platform wraps it internally via the existing `IProcessHandler.GetProcessById(pid)`, which already provides `Id` / `Exited` / `WaitForExit` / `ExitCode` / `Kill`. That reduces the public surface to the interface + context only, at the cost of requiring the launched thing to be a local, query-able OS process (true for AUMID-activated packaged apps; not true for a process living inside a container or on a remote host). Because the goal is also to support container/remote, the RFC proposes the **handle-returning** shape as primary.

## Examples

All examples assume the extension is registered on the builder, e.g. from a `…Extensions` helper:

```csharp
builder.TestHostControllers.AddTestHostProcessLauncher(sp => new MyLauncher(sp));
```

### 1. Packaged WinUI / MSIX (the motivating case)

Activate the packaged app by AUMID instead of starting an exe. The activated app self-hosts MTP (as the `MSTestRunnerWinUI` sample already does) and connects back on the env-provided pipe.

```csharp
internal sealed class WinUiAppxLauncher(IServiceProvider sp) : ITestHostProcessLauncher
{
    public string Uid => nameof(WinUiAppxLauncher);
    public string Version => "1.0.0";
    public string DisplayName => "WinUI MSIX launcher";
    public string Description => "Deploys and AUMID-activates a packaged WinUI test app.";
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<ITestHostProcessHandle> LaunchTestHostProcessAsync(
        TestHostProcessLaunchContext context, CancellationToken cancellationToken)
    {
        // 1. Parse the .appxrecipe / AppxManifest.xml next to context.FileName to get the AUMID
        //    and (in Developer Mode) register the loose layout:
        //    await new PackageManager().RegisterPackageByUriAsync(manifestUri, options);
        string aumid = AppxManifest.ResolveAumid(context.FileName);

        // 2. Activate, passing the SAME args the platform prepared.
        var aam = (IApplicationActivationManager)new ApplicationActivationManager();
        aam.ActivateApplication(aumid, string.Join(' ', context.Arguments),
            ACTIVATEOPTIONS.AO_NONE, out uint pid);

        // 3. Wrap the returned PID. The app inherits context.EnvironmentVariables
        //    (incl. the MONITORTOHOST pipe name) via its activation/launch profile.
        return new ProcessIdHandle((int)pid);
    }
}
```

> Note: enabling the controller→host pipe across the AppContainer sandbox requires a loopback/pipe-ACL step (e.g. `CheckNetIsolation LoopbackExempt` or granting the package SID on the pipe). That belongs to the WinUI extension, not the platform.

### 2. Launch under a debugger

Start the host suspended and attach a debugger before it runs (useful for `--debug`-style flows or CI repro).

```csharp
public async Task<ITestHostProcessHandle> LaunchTestHostProcessAsync(
    TestHostProcessLaunchContext context, CancellationToken cancellationToken)
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
public Task<ITestHostProcessHandle> LaunchTestHostProcessAsync(
    TestHostProcessLaunchContext context, CancellationToken cancellationToken)
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
    return Task.FromResult<ITestHostProcessHandle>(new ProcessHandleAdapter(p));
}
```

This example deliberately shows a sharp edge: elevation via the shell loses per-process environment variables, so the launcher is responsible for re-delivering them. The platform contract only requires that the host ends up with `context.EnvironmentVariables`.

### 4. Container

Run the test host inside a container and bridge the pipe. The returned handle tracks the `docker run` client process; killing it tears down the container.

```csharp
public Task<ITestHostProcessHandle> LaunchTestHostProcessAsync(
    TestHostProcessLaunchContext context, CancellationToken cancellationToken)
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
    return Task.FromResult<ITestHostProcessHandle>(new ProcessHandleAdapter(p));
}
```

### 5. Remote (SSH)

```csharp
public Task<ITestHostProcessHandle> LaunchTestHostProcessAsync(
    TestHostProcessLaunchContext context, CancellationToken cancellationToken)
{
    string env = string.Join(' ', context.EnvironmentVariables.Select(kv => $"{kv.Key}={Quote(kv.Value)}"));
    string remoteCmd = $"{env} {Quote(context.FileName)} {string.Join(' ', context.Arguments.Select(Quote))}";

    var psi = new ProcessStartInfo("ssh") { UseShellExecute = false };
    psi.ArgumentList.Add("user@remote-host");
    psi.ArgumentList.Add(remoteCmd);
    Process ssh = Process.Start(psi)!; // tunnel the controller pipe over the SSH connection
    return Task.FromResult<ITestHostProcessHandle>(new ProcessHandleAdapter(ssh));
}
```

## Alternatives considered

### Reuse `ITestHostExecutionOrchestrator`

MTP already ships an experimental `ITestHostExecutionOrchestrator` (`ITestHostOrchestratorManager.AddTestHostOrchestrator`). It was rejected as the vehicle because it sits **above** the controller: `OrchestrateTestHostExecutionAsync` runs in `TestHostOchestratorHost` and replaces the *entire* execution, returning only an exit code. An implementation would have to re-create everything `TestHostControllersTestHost` provides — environment-variable providers, the `MONITORTOHOST` IPC/PID handshake, and the `ITestHostProcessLifetimeHandler` fan-out that **hang dump and crash dump depend on**. That is the wrong granularity for "launch the process differently." The orchestrator remains the right tool for whole-run concerns (e.g. retry/repeat that re-runs the host).

### Make the internal `IProcessHandler` replaceable via DI

`IProcessHandler` / `IProcess` are `internal` and surface `Process`-specific members (e.g. `MainModule`). Exposing them publicly would leak implementation detail and over-commit the surface. A purpose-built, minimal `ITestHostProcessHandle` is cleaner and evolvable.

### Do nothing (keep `Process.Start`)

Leaves [#2784](https://github.com/microsoft/testfx/issues/2784) unsolvable on MTP for packaged apps and blocks the debugger/elevation/container/remote scenarios, all of which today require forking the platform.

## Compatibility and conventions

- **Experimental.** All new types and methods are gated behind `[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]`, consistent with the other test-host-controller-era experimental APIs.
- **Public API tracking.** New members are added to `PublicAPI.Unshipped.txt` with the `[TPEXP]` prefix.
- **No `init` accessors** on any new public API, per repo policy.
- **No behavior change when unused.** If no launcher is registered, the platform behaves exactly as today (`Process.Start`), and the controller host is selected only when it already would be.

## Open questions

- **Handle vs PID surface.** Ship the `ITestHostProcessHandle` shape (supports container/remote) or start with the PID-only minimal variant and grow later?
- **CLI/debug integration.** Should the platform expose a built-in `--launcher`-style selector, or is builder/MSBuild registration sufficient for v1?
- **Cancellation semantics.** Define precisely what `Kill()` must guarantee for remote/container launchers (best-effort teardown vs. synchronous termination).
- **Multiple launchers.** Singleton for v1; is there ever a composition story (e.g. debugger + elevation), or do implementers compose manually?
