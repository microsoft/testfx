# RFC 024 - Invocation lifecycle hooks (before all / after all test modules)

- [ ] Approved in principle
- [x] Under discussion
- [x] Implementation (Phase 1, Microsoft.Testing.Platform side)
- [ ] Shipped

## Summary

Introduce an **invocation lifecycle hook** for Microsoft.Testing.Platform (MTP): an extension point that runs **once before any test of an invocation starts** and **once after every test of the invocation has finished**. An *invocation* is the user-visible unit of work:

- a single launch of the test application (`MyTests.exe`, `dotnet MyTests.dll`, `dotnet exec`, `dotnet run`);
- one `testing/runTests` request of an IDE or server-mode client;
- (Phase 2) a whole multi-module `dotnet test` run.

It addresses [#11500](https://github.com/microsoft/testfx/issues/11500).

The design ships in two phases:

1. **Phase 1 — platform (this repository).** A new experimental `IInvocationLifetimeHandler` extension registered through a new `IInvocationLifetimeApplicationBuilder`. MTP runs the handlers **once per invocation, in the outermost MTP process of the application's process tree** (retry orchestrator → test host controller → test host), never per retry attempt and never per child process. Setup can publish **outputs** (environment variables) that the test code and every child process observe. Under a multi-module `dotnet test`, each module still self-hosts its handlers until Phase 2 lands (documented, degraded behavior).
2. **Phase 2 — `dotnet test` orchestration (dotnet/sdk, protocol 1.5.0).** The SDK **selects one owner module before launching anything**, launches it first, waits for its setup, launches every other module with the owner's outputs, and lets the owner process **stay alive** until all other modules finished; then the owner runs cleanup and exits. No extra process, no cross-process mutex or ref-counting: only the orchestrator knows when the last module is done.

## 1. Problem statement

Users want to provision expensive, shared resources once for a whole test run — start a container or an emulator, seed a database, acquire a lease, start a mock server — and tear them down once at the very end ("a hook that runs before ALL test projects / test modules and after all test projects / test modules").

MTP already has several lifecycle hooks, but none spans an invocation:

| Existing hook | Scope | Why it does not solve #11500 |
| --- | --- | --- |
| `ITestSessionLifetimeHandler` | One test session in **one test host process** (and one per server request) | Runs again in every retry attempt and in every module; runs in the child, after the controller already started. |
| `ITestHostApplicationLifetime` (obsolete) | One **test host process** | Same as above. |
| `ITestHostProcessLifetimeHandler` | Controller observing **one child process** | Per child process, only when a controller exists. |
| `ITestHostOrchestratorApplicationLifetime` (public, experimental) | The orchestrator process | Only runs when an orchestrator (for example retry) is registered. |
| `[AssemblyInitialize]` / framework fixtures | One module, framework-specific | Framework-specific, per module, per retry attempt. |
| `IArtifactPostProcessor` (RFC 018) | Whole `dotnet test` run | After-only, relaunches a process, not meant for side effects. |

Two naive workarounds do not work:

- **Cross-process mutex or ref-count.** A `0→1` transition can elect the "before" owner, but a `1→0` transition cannot detect modules that `dotnet test` has **not launched yet** (`--max-parallel-test-modules`, build ordering). Only the orchestrator knows the run is over.
- **Per-module hooks with idempotency.** Pushes a distributed-systems problem onto every extension author.

## 2. Goals

1. A framework-agnostic, platform-level **before-invocation** and **after-invocation** hook.
2. Exactly one setup and one cleanup **per invocation**: per application launch and per server run request in Phase 1; across all modules of a `dotnet test` run in Phase 2.
3. Never duplicated by retry attempts or test host controllers (crash dump, hang dump, environment-variable providers, …).
4. Setup can hand data (connection strings, ports, tokens) to the test code and to all child processes **without** extension-specific plumbing.
5. Cleanup **always** runs once setup was entered: after test failures, setup failures, and cooperative cancellation (Ctrl+C).
6. **Zero cost** when no handler is registered: no extra process, no extra IPC, no extra protocol data.
7. Works with every launch surface: application host executable, `dotnet <dll>`, `dotnet exec`, `dotnet run`, `dotnet test` (MTP mode), and the server-mode client package (`testing/runTests`).

## 3. Non-goals

- Guarantees under hard termination (`kill -9`, `Environment.FailFast`, power loss). Cleanup is best effort in those cases, like `finally` blocks.
- Running hooks for informative commands and discovery: `--help`, `--info`, `--tool`, `--list-tests`, `testing/discoverTests`.
- Sharing across concurrent, independent invocations (two `dotnet test` commands at once).
- A VSTest equivalent.
- Propagating outputs to test hosts that do not inherit the owner's environment. A packaged application activated through its AUMID only receives an allow-listed set of variables, so handler outputs are not visible to its tests; hooks still run exactly once, in the outer retry orchestrator or controller.
- An MSTest attribute (for example `[InvocationInitialize]`). Handlers run in the outermost process, which is not the test host when retry or a controller is active, so static state set by such an attribute would not be visible to tests. Frameworks can build on the extension later with an explicit out-of-process model.

## 4. Terminology

- **Invocation**: see Summary.
- **Owner**: the single process that runs the handlers for an invocation.
- **Participant**: any other MTP process of the same invocation; it does not run handlers.
- **Outputs**: environment variables published by setup and visible to every process of the invocation that starts afterwards, and to the owner's own test code.

## 5. Public API (Phase 1)

All new types are `[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]` and their XML docs contain "This API is experimental. It may change, break, or be removed at any time without notice." No `init` accessors. Contexts are **sealed classes** (not interfaces), so members can be added later without breaking implementers on `netstandard2.0`, consistent with `ArtifactPostProcessingContext`.

```csharp
namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

public interface IInvocationLifetimeHandler : IExtension
{
    // Runs once, before any test of the invocation starts.
    Task OnInvocationStartingAsync(InvocationStartingContext context, CancellationToken cancellationToken);

    // Runs once, after every test of the invocation finished (or setup failed, or the run was canceled),
    // only for handlers whose OnInvocationStartingAsync was entered.
    Task OnInvocationCompletedAsync(InvocationCompletedContext context, CancellationToken cancellationToken);
}

public sealed class InvocationStartingContext
{
    // Publishes an output: sets the variable in the owner process, so the owner's test code and every child
    // process started afterwards observe it. A null value removes the variable. The last write wins.
    public void SetEnvironmentVariable(string name, string? value);
}

public sealed class InvocationCompletedContext
{
    public InvocationCompletedContext(int exitCode, bool isCanceled);

    // The exit code of the invocation before cleanup ran (see 7.4 for server mode).
    public int ExitCode { get; }

    // True when the invocation was canceled (Ctrl+C, server cancellation, dotnet test cancellation).
    public bool IsCanceled { get; }
}

public interface IInvocationLifetimeManager
{
    void AddInvocationLifetimeHandler(Func<IServiceProvider, IInvocationLifetimeHandler> factory);
}

namespace Microsoft.Testing.Platform.Builder;

public interface IInvocationLifetimeApplicationBuilder : ITestApplicationBuilder
{
    IInvocationLifetimeManager InvocationLifetime { get; }
}
```

`InvocationStartingContext` intentionally has no public constructor in Phase 1: the platform owns the environment interaction (tracking, restoration, Phase 2 propagation). This is a known trade-off for unit-testing handlers in isolation; a test-friendly constructor (for example one that records outputs without touching the process) can be added later without a breaking change.

Usage:

```csharp
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
if (builder is IInvocationLifetimeApplicationBuilder invocationBuilder)
{
    invocationBuilder.InvocationLifetime.AddInvocationLifetimeHandler(_ => new SqlContainerHandler());
}
```

Design notes:

- A **new builder interface** (same pattern as `IArtifactPostProcessingApplicationBuilder`) avoids adding members to the shipped `ITestApplicationBuilder` and `ITestHostManager` interfaces.
- Outputs are **explicit** (`SetEnvironmentVariable` on the context) rather than "whatever the handler did to the process", so the platform can restore them (server mode) and Phase 2 can propagate them to participants. Handlers that call `Environment.SetEnvironmentVariable` directly still affect the owner's process tree, but that is neither restored nor propagated.
- `SetEnvironmentVariable` throws `ArgumentException` for a null, empty or whitespace name, a name containing `=` or `\0`, and for names starting with the reserved `TESTINGPLATFORM_INVOCATIONLIFETIME_` prefix.
- Outputs are read with `Environment.GetEnvironmentVariable`. They are **not** guaranteed to appear in the owner process's `IConfiguration`, because configuration is built before setup runs.
- The handler instance carries state between setup and cleanup: both methods run on the same instance, in the same process.
- Handler factories, constructors and `IsEnabledAsync` must be side-effect free; provisioning belongs in `OnInvocationStartingAsync`. Handlers are created only in the process that owns the invocation, and only when hooks will run (never for `--help`, `--info`, discovery, or in participants). `IsEnabledAsync` is honored, UIDs must be unique, and `IAsyncInitializableExtension`/`IDisposable`/`IAsyncDisposable` are honored like other extensions (disposal happens after cleanup).
- Handlers resolve platform services from the provided `IServiceProvider` (configuration, command line, logger factory, output device). Test-framework services are not available: no test session exists yet.

## 6. Where hooks run

| Launch | Process tree | Owner (Phase 1) | Owner (Phase 2) |
| --- | --- | --- | --- |
| exe / `dotnet <dll>` / `dotnet exec` / `dotnet run`, no extensions | test host | test host | n/a |
| … with a controller (crash dump, hang dump, environment-variable providers) | controller → test host | controller | n/a |
| … with retry | orchestrator → (controller →) test host × N attempts | orchestrator | n/a |
| server mode (IDE, client package), per `testing/runTests` request | server test host (optionally under a passive controller) | server test host, per **run** request | n/a |
| `dotnet test` MTP mode, N modules, SDK without Phase 2 | N independent trees | each module's outermost process (degraded: per module) | — |
| `dotnet test` MTP mode, N modules, SDK with Phase 2 | N trees | — | the selected owner module's outermost process, kept alive |
| `--help`, `--info`, `--tool`, `--list-tests`, `testing/discoverTests` | any | none | none |

Rules:

1. **Outermost process wins.** In console mode the platform decides at host-construction time: the orchestrator host if any, otherwise the controller host, otherwise the console test host. Only that host gets the handlers.
2. **Hooks run inside the host, not around it.** The handshake with `dotnet test` and the server control channel must happen before setup (so setup errors are forwarded and Phase 2 can negotiate roles), and cleanup must happen before the host closes the protocol and disposes its services (so cleanup errors are rendered and logged). Concretely:
   - test host / controller (`CommonHost`): `ITestHostApplicationLifetime.BeforeRunAsync` runs first, then setup (after the handshake), the tests, and cleanup, all inside `InternalRunAsync`; then `AfterRunAsync`, `OnExitAsync`, and service disposal. Cleanup runs before disposal so the output device and the logger are still alive;
   - orchestrator (`TestHostOrchestratorHost`): setup runs after the handshake and before `ITestHostOrchestratorApplicationLifetime.BeforeRunAsync`; cleanup runs after their `AfterRunAsync`, so hooks wrap the orchestrator lifetimes.
   The first cleanup call wins: later finalization paths observe the same exit code or fatal exception without repeating callbacks or disposal.
   Outputs still reach children: the controller snapshots the process environment when it prepares the child process, and retry builds its child `ProcessStartInfo` during orchestration — both after setup.
3. **Suppression marker.** An owner that **launches MTP children** (orchestrator or controller) sets `TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER=<owner pid>` in its own environment **after setup** and before launching children. An MTP process that starts with this variable set is a participant and does not run handlers. A test host owner never sets the marker. The controller PID or the internal retry pipe on the command line also identifies a participant. This covers hosts that do not inherit the owner's environment, including AUMID-activated retry attempts with no controller PID.
4. **Leaf reset.** The test host (console or server) captures whether the marker was inherited and then removes **every** `TESTINGPLATFORM_INVOCATIONLIFETIME_*` variable from its environment (the Phase 1 marker and the Phase 2 role variables alike), so an unrelated MTP application launched **by user test code** is a new invocation with its own hooks. A retry child that is itself a controller keeps the variables and forwards them to its leaf. When both the marker and a Phase 2 owner role are present, the marker wins.
5. **Server mode (JSON-RPC).** The server test host serves many requests. Handlers run around each `testing/runTests` request, in the server test host, with **fresh handler instances per request**. When handlers are registered, every run **and discovery** request takes an exclusive gate, held from setup until the environment is restored, so no other request observes temporary outputs. Setup receives the request's cancellation token (canceling the `testing/runTests` request cancels its setup). After cleanup, every variable written through `SetEnvironmentVariable` is **restored** to its value before setup, so outputs never leak into the next request. Setup and cleanup failures fail that request (JSON-RPC error), not the server process. Controllers in server mode are passive and never run handlers.
6. **Discovery and informative commands** never run handlers.
7. **Disabled handlers** are disposed when the console-mode owner is built or before each server run request, and are never invoked. When no handler is registered at all, the outer controller or orchestrator publishes no marker: registration is code, so every process of the application registers the same handlers. An application that registers handlers conditionally (for example from a variable that only a child process sees) is not supported.

## 7. Semantics

### 7.1 Ordering

Setup runs handlers in registration order. Cleanup runs in **reverse** order, only for handlers whose setup was entered (including the one whose setup threw).

### 7.2 Failures

| Situation | Behavior | Exit code (console) |
| --- | --- | --- |
| Setup of handler *k* throws | Remaining setups are skipped, **tests are not run**, cleanup runs for handlers *k*..1, the error is displayed | `GenericFailure` (1), or `TestSessionAborted` (3) when canceled |
| Tests fail, or any non-zero host exit code | Cleanup runs normally; `ExitCode` reflects it | unchanged |
| One or more cleanups throw | Every other cleanup still runs; each error is displayed | `GenericFailure` only when the exit code was `Success`; an earlier failure is never masked |
| Ctrl+C during setup or tests | Setup token canceled; a handler that returns normally after the cancellation does not let the next handlers or the tests start; cleanup still runs with `IsCanceled = true` | `TestSessionAborted` (3) |
| Graceful stop requested by `dotnet test`, or `--timeout` expires during setup or test execution | A stop requested during setup cancels the setup token; for controller and orchestrator owners, `--timeout` cancels only the setup token so the parent application token remains available to finalization; cleanup runs with `IsCanceled = true` | `TestSessionAborted` (3) |
| Host exits before setup starts (incompatible protocol, `BeforeRunAsync` throws) | Handlers are disposed; neither setup nor cleanup runs | unchanged |
| A handler factory, `IsEnabledAsync`, or `TryInitializeAsync` throws while the host is built | The handlers already built are disposed; the failure surfaces like any extension build failure | `GenericFailure` (1) |

`IsCanceled` is also `true` whenever `ExitCode` is `TestSessionAborted`, and once the application token is canceled `ExitCode` is `TestSessionAborted` whatever verdict was computed before, matching the exit code of the host, so a parent process that only observes a child's exit code reports the same verdict as the child.

Errors are rendered through the output device (console, `dotnet test`, server logs) and logged. The invocation lifetime never logs output values; other extensions (for example environment variable providers or diagnostics) can still read, log, or replace them in the processes they run in.

### 7.3 Cancellation

- `OnInvocationStartingAsync` receives a setup token linked to application cancellation. In controller and orchestrator owners, `--timeout` also cancels this token without canceling the parent application token used by finalization.
- `OnInvocationCompletedAsync` receives `CancellationToken.None` in Phase 1: cleanup must run precisely when the user canceled. Handlers bound their own cleanup duration; terminating the process is the escape hatch. A platform cleanup timeout is deferred (open question).

### 7.4 Exit codes

The runner combines its failures with the exit code computed by the host (including `--minimum-expected-tests` and `--ignore-exit-code` policies), using the table above. `--ignore-exit-code` does not hide setup or cleanup failures: they are infrastructure failures, not test results.

In server mode there is no process exit code per request; `ExitCode` is `Success` (0) when the request completed, `TestSessionAborted` (3) when it was canceled, and `GenericFailure` (1) when it faulted. Test failures are reported to the client through test node updates, not through `ExitCode`.

## 8. Phase 2: `dotnet test` orchestration (protocol 1.5.0)

> Phase 2 requires dotnet/sdk changes. It is specified here as the producer/consumer contract and is **not** implemented by Phase 1. Until an SDK negotiates it, hosts behave as in Phase 1.

### 8.1 Owner selection happens before launch

Electing the owner from handshakes ("first advertiser wins") is unsound: by the time an advertiser handshakes, the SDK may already have run other modules without setup (for example with `--max-parallel-test-modules 1`), and handler sets may differ. So the SDK selects the owner **before launching any module**, from build metadata:

- A package or project that registers a handler declares it in MSBuild, for example `<TestingPlatformInvocationLifetimeHandler Include="MyCompany.SqlContainer" />` (shipped by the handler package's `buildTransitive` props), and the SDK reads it during its existing project evaluation.
- When any MTP module declares handler items, every MTP module and target framework must evaluate `TestingPlatformInvocationLifetimeCoordinationSupported` to `true`. The matching MTP package sets this property; users can explicitly set it to `false`. Missing, false, or invalid values cause the SDK to warn and fall back before selecting or launching an owner. VSTest modules are not subject to this gate.
- Owner = the module with `TestingPlatformInvocationLifetimeOwner=true` if exactly one sets it; otherwise the first module (in the SDK's deterministic project/target-framework order) whose items are a superset of every other module's items; otherwise the SDK warns and falls back to Phase 1 (per-module self-hosting).
- The owner is identified by its concrete module path and target framework, and is unique per `dotnet test` command (multi-TFM included).
- An item's identity is the handler's `Uid`.
- Runs without project evaluation (for example `--test-modules` globs) have no metadata and use Phase 1 behavior. This is documented.

### 8.1.1 Owner activation is explicit and bounded

Metadata can point to a stale binary, or to an owner whose declared handlers are disabled at run time. The SDK therefore commits to a coordinated run only after the owner **accepts** it:

- the owner negotiated 1.5.0 over the named-pipe transport and advertised its *enabled* handler UIDs in handshake property 23;
- the SDK validates that the enabled UID set covers every declared item and acknowledges activation in its handshake reply using property 24;
- the owner does not start setup until it receives that acknowledgment.

The SDK waits for the handshake and for `InvocationSetupCompleted` within a bounded readiness timeout. If activation is rejected before setup begins (old protocol, UID mismatch), the SDK warns and falls back to Phase 1. Once setup may have started, any failure, including a timeout, fails the run without launching other modules.

### 8.2 Run sequence

1. The SDK launches the owner first, with `TESTINGPLATFORM_INVOCATIONLIFETIME_ROLE=owner`. The owner handshakes, advertising its enabled handler UIDs (handshake property **23**, `InvocationLifetimeHandlers`) so the SDK can validate them. The value is a compact JSON array of strings; UIDs are not restricted to a delimiter-safe character set.
2. After validating the owner handshake, the SDK acknowledges activation with handshake property **24**, `InvocationLifetimeCoordinationAccepted=true`. The owner runs setup only when protocol 1.5.0 or later was negotiated, the transport is named pipe, and this acknowledgment is present. It then sends `InvocationSetupCompleted { Succeeded, ErrorMessage?, Outputs }` (serializer id **16**). Each output encodes `Name`, `HasValue`, and `Value` when present; `HasValue=false` removes the variable, distinct from setting it to an empty string. The encoding preserves `=`, `;`, and newlines. Output values are never logged.
3. On failure, the SDK does not launch other modules; the run fails (`GenericFailure`). On success, the SDK launches every other module with the outputs merged into its launch environment, plus `TESTINGPLATFORM_INVOCATIONLIFETIME_COORDINATED=<owner's enabled handler UIDs>`. A capable module skips only the handlers whose UIDs are in that set. It still runs any other handlers itself (per module, with a warning), so a handler registered in code without an MSBuild item is never silently dropped. Outputs never travel through handshake properties and are never traced.
4. The owner runs its own tests and then sends `ModuleTestsCompleted { ExitCode }` (serializer id **17**). The SDK treats this as **module execution complete**: it advances the owner's worker slot and its sequential group (multi-TFM), while keeping the owner's process and pipe tracked separately until exit. It holds the data-pipe reply until other modules drain so a lost control pipe cannot cause premature cleanup. Under retry, the orchestrator sends it after the last attempt.
5. When every other module completed, the SDK sends `RunInvocationCleanup { AggregateExitCode, IsCanceled }` on the owner's dedicated reverse control pipe (control kind **3**, after `CloseChannel`). The accepted handshake reply advertises that pipe; retry children retain their ordinary control pipe. The owner runs cleanup with that aggregate pre-cleanup verdict, sends `InvocationCleanupCompleted { Succeeded, ErrorMessage? }` (serializer id **18**), and exits.
6. The SDK folds a cleanup failure into the final verdict: `GenericFailure` only if the run would otherwise succeed.

### 8.3 Cancellation and failures

- On cancellation, the SDK stops launching modules and cancels running modules. For the owner, it drains the owner's **test execution**, not its process: it waits for `ModuleTestsCompleted`, or for a setup-stopped `InvocationSetupCompleted` when canceled during setup. Then it waits for the other modules to terminate, sends `RunInvocationCleanup` with `IsCanceled = true`, and starts a separate exit deadline for the owner. The owner's control listener stays active after test cancellation so the cleanup signal can still arrive; the SDK delivers cancellation at most once per coordinated control connection.
- If the exit deadline expires, the SDK kills the owner process tree and reports a cleanup-timeout failure (`GenericFailure` unless the run already failed). The owner may skip cleanup in that case, as with any hard termination (§3).
- If the owner process dies, the SDK fails the run, stops launching, and cancels and drains other modules. It never re-elects an owner, because setup may already have had side effects.
- If the owner's control pipe fails after setup, the SDK stops new launches, drains running siblings, and only then releases the owner's module-completion reply. The owner performs local cleanup and the run fails.
- If the SDK goes away (pipe dropped), the owner runs cleanup immediately, as best effort, and reports a failure; siblings may still be running. Because the invocation's aggregate result is unknown, the handlers receive the owner's failure verdict (`GenericFailure` when the owner's own tests passed), never a success.
- Phase 2 requires the named-pipe transport with the 1.4.0 control channel. Otherwise the SDK uses Phase 1 behavior.

### 8.4 Wire reservations

Wire IDs were allocated after checking the current protocol sources in both repositories. Handshake property 20 is already `CoverageSummaryVersion`, properties 21 and 22 are already `TargetFramework` and `TestInProgressSupported`, and serializer id 15 is already `CoverageSummaryMessages`; these IDs must not be reused.

| Wire item | Allocation and direction |
| --- | --- |
| Handshake property 23, `InvocationLifetimeHandlers` | Owner MTP process to SDK; compact JSON array of enabled handler UIDs. |
| Handshake property 24, `InvocationLifetimeCoordinationAccepted` | SDK to owner MTP process; `true` only after protocol/transport and UID coverage validation. |
| Serializer id 16, `InvocationSetupCompleted` | Owner to SDK; `ExecutionId`, `InstanceId`, `Succeeded`, optional `ErrorMessage`, and `Outputs`. |
| Serializer id 17, `ModuleTestsCompleted` | Owner to SDK; `ExecutionId`, `InstanceId`, and module `ExitCode`. |
| Serializer id 18, `InvocationCleanupCompleted` | Owner to SDK; `ExecutionId`, `InstanceId`, `Succeeded`, and optional `ErrorMessage`. |
| `ServerControlKinds` value 3, `RunInvocationCleanup` | SDK to owner on its dedicated reverse control pipe using the existing control protocol; `AggregateExitCode` and `IsCanceled` supplement the existing `Kind` field. |

The three lifecycle messages use request/reply with `VoidResponse`; outputs are length-prefixed name/value data, preserve `=`, `;`, and newlines, and distinguish removing a variable from setting it to an empty value. Lifecycle messages and activation acknowledgments are used only when protocol 1.5.0 or later is negotiated and named-pipe control is available. The SDK advertises 1.4.0 to use the already-defined control channel, and 1.5.0 for lifecycle coordination. Absence of both `TESTINGPLATFORM_INVOCATIONLIFETIME_ROLE` (set only on the owner) and `TESTINGPLATFORM_INVOCATIONLIFETIME_COORDINATED` (set on every other module) means Phase 1 behavior. The coordinated UID environment value is a compact JSON array of strings. Leaf reset (§6) clears all of these variables, so nested applications never inherit a role.

### 8.5 Cost

- No handler anywhere: no coordination messages or owner process.
- With handlers: zero extra processes. One handshake property and three small messages for the owner and one control request; the owner process stays alive for the duration of the run without holding a concurrency slot. Other modules start after the owner's setup instead of all at once, which is inherent to "before all".

## 9. Compatibility

- Old SDK with a new host: no role variable ⇒ self-host per module (Phase 1, documented).
- New SDK with an old host: when handler metadata exists but any MTP module lacks the capability property, the SDK warns and falls back to Phase 1 before launching an owner. This also covers old modules with code-only registrations.
- Evaluated package metadata does not prove that an existing binary was rebuilt. A stale sibling, including one used with `--no-build`, can ignore the coordination environment and rerun its handlers. The SDK warns when that sibling negotiates an older protocol, but the warning occurs after launch and cannot prevent duplicate setup; rebuild all modules after updating the package.
- Inside one application, only the outermost process runs handlers; children see the marker.
- The marker is a stable mechanism that other orchestrators (CI runners, custom launchers) can use to suppress hooks for processes they launch.

## 10. Security

Outputs are environment variables in the user's own process tree and the user's own `dotnet test` process; there is no new trust boundary. Outputs may contain secrets, so the platform never logs their values (names only) and the SDK never echoes them.

## 11. Testing strategy

- Unit tests for the runner: ordering, reverse cleanup of entered handlers, setup failure skipping tests, cleanup failure exit-code combination, cancellation flag, disabled handlers, duplicate UIDs, name validation, environment restoration.
- Unit tests for ownership decisions: marker suppression, leaf reset, discovery and server-mode exclusions.
- Acceptance tests: console application (alone, with a controller, with retry with and without a controller: one setup and one cleanup regardless of attempts), outputs visible from test code, `dotnet exec` / `dotnet run` / `dotnet test` launches, failed tests and `--timeout` reported to cleanup, setup failure skips tests, cleanup failure fails the invocation, `--list-tests` / `--help` / `--info` skip. Server mode per run request is added with the staged server-mode support.
- Phase 2 (dotnet/sdk): a non-owner module ordered before the owner with `--max-parallel-test-modules 1`; an owner followed by another TFM in the same sequential group; owner setup failure; owner crash and cancellation before and after setup.

## 12. Alternatives considered

1. **Cross-process mutex or ref-count** — cannot detect not-yet-launched modules (§1).
2. **RFC 018-style relaunch of an elected application for setup and again for cleanup** — two extra processes per run, outputs marshalled twice, and setup state (open handles, a container client) cannot live in memory between setup and cleanup. RFC 018 §11.3 rejected keeping an elected host alive because the owner is elected *after* execution there; here the owner is selected *up front*, and keeping it alive is what preserves in-process state.
3. **Dedicated coordinator process** spawned by `dotnet test` — needs a new entry point and loading user assemblies into another process; strictly more cost than keeping the owner alive.
4. **Owner elected from handshakes** ("first advertiser wins") — cannot provide a before-all barrier for modules launched earlier (§8.1).
5. **Add members to `ITestHostManager`** — breaking change for implementers of a shipped interface.
6. **Run hooks in every leaf and rely on idempotency** — violates goal 3 and pushes coordination onto extension authors.

## 13. Open questions

1. A configurable cleanup timeout (`testconfig.json`).
2. Whether `InvocationStartingContext` should expose the invocation scope (standalone, server request, coordinated `dotnet test`) once Phase 2 exists.
3. Whether `--list-tests` should ever run hooks for frameworks whose discovery needs the resource. Current answer: no.

## 14. Implementation research and delivery record

### 2026-11-25 — Contract and wire allocation review

The merged Phase 1 implementation provides per-application process-tree hooks, but the original #11500 contract still requires per-request server-mode hooks and one owner across a multi-module `dotnet test` run. RFC sections 6 and 8 remain the source of the lifecycle, fallback, cancellation, and aggregation contract.

Source inspection confirmed that server-mode requests execute in `ServerTestHost.ExecuteRequestAsync`, that `testing/runTests` and `testing/discoverTests` can be in flight concurrently, and that `BuildInvocationLifetimeHandlersIfOwnerAsync` currently excludes JSON-RPC mode. The server-mode request hook must therefore create fresh handler instances per run request, serialize run and discovery while handlers are registered, and restore every environment variable written through `InvocationStartingContext` before releasing that gate.

The existing dotnet-test named-pipe control channel uses handshake property 12, serializer ids 13 and 14, and control kinds 1 (`CancelSession`) and 2 (`CloseChannel`). Current protocol sources also allocate handshake properties 20–22 and serializer id 15, so Phase 2 uses new IDs 23–24 and 16–18 respectively. The owner handshake acknowledgment in property 24 prevents setup side effects before the SDK has verified the enabled UID set; rejection before that acknowledgment retains Phase 1 per-module behavior. UIDs are encoded as JSON arrays because `IExtension.Uid` does not constrain delimiter characters.

The SDK already has the 1.4 control-channel implementation, but advertises through 1.3.0. The Phase 2 SDK change must advertise 1.4.0 and 1.5.0 and preserve the existing 1.4 cancellation behavior for uncoordinated runs.

### 2026-11-25 — Producer implementation and composed validation

The TestFx producer implements the server-mode request hooks and the Phase 2 owner/participant roles described in §6 and §8. The consuming dotnet/sdk change (branch `dev/amauryleve/sdk-invocation-lifetime-hooks`) advertises 1.0.0–1.5.0, selects the owner before launch, replies with property 24 only after verifying the declared UID set, and delivers `RunInvocationCleanup` on the owner's control pipe.

Decisions made during implementation:

- When the coordination pipe is lost after setup, the owner still runs cleanup locally. Because the aggregate result is unknown, a module that passed is reported to the handlers as `GenericFailure` (a failing module keeps its own exit code), the owner's cancellation state is preserved, and the owner exits with a failure.
- Discovery (`--list-tests`) never activates Phase 2 in the SDK, matching §7 and the Phase 1 exclusion.
- Property 23 is accepted from the outermost owner process regardless of host type (console, controller, or orchestrator); inner test hosts never advertise it.

Regression sensitivity: unit-level mutations that ignored `TESTINGPLATFORM_INVOCATIONLIFETIME_COORDINATED` or suppressed property 23 initially survived all unit tests. The acceptance tests `CoordinatedModule_SkipsTheHandlersAlreadyRunByTheOwnerAndKeepsTheirOutputs` and `DotnetTestOwner_AdvertisesItsHandlersAndRunsThemLocallyWhenCoordinationIsNotAccepted` were added to the packed-consumer suite to cover both contracts. With those mutations reapplied to a repacked producer, each new test failed at its intended assertion: handler A ran in the coordinated module, and handshake property 23 was missing. With the sources restored and repacked, the full `InvocationLifetimeHandlerTests` class passed 27/27.

Composed validation used the packed producer and the SDK build containing the fixes for owner terminal-slot release and the dedicated cleanup control pipe. A three-module solution (owner plus two siblings) passed the default, `--max-parallel-test-modules 1`, and `--retry-failed-tests 2` runs, with the first attempt of either the owner or a sibling failing. In each run every handler's setup and cleanup ran exactly once in the owner, every module observed the setup outputs, and cleanup received aggregate exit code 0 without cancellation. Earlier composed runs covered setup failure, failing modules, discovery, fallbacks, owner selection, and cancellation.

Review found that a failed setup on the first server request queued its diagnostic until a later successful request initialized the output device. Initializing the device before setup delivers the handler's error to the client before the request error. The focused regression failed with zero client diagnostics before the fix and passed afterward.

Further review found that an accepted owner could start setup before its cleanup control pipe connected, and that a throwing stop callback could end the listener while other modules were still running. The owner now requires control-pipe readiness before setup and keeps listening after reporting a stop-callback failure; uncoordinated control channels remain best-effort. The primary data pipe also terminated the process on disconnect, contrary to the best-effort owner cleanup contract. Accepted owners now handle data-pipe loss cooperatively, while unaccepted sessions retain the existing process-exit behavior.

On the review revision based on `a04da10b7c0915257979ae9d9acde8b0b794e5a2`, the two control regressions executed against the prior behavior and failed at their intended assertions (no exception on failed control connection; cleanup request lost after callback failure): 2 failed, 1 passed, 0 skipped. The focused run with the fixes passed 205 tests, including independently specified protocol bytes and accepted/unaccepted data-pipe disconnect behavior. The tests used the repository-local SDK, Release `net10.0`, method-level parallelism with 32 workers; the exact command was `.\.dotnet\dotnet.exe run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Release -f net10.0 --no-build -- --filter 'FullyQualifiedName~DotnetTestConnectionTests|FullyQualifiedName~InvocationLifetimeRunnerTests|FullyQualifiedName~ServerTestHostTests|FullyQualifiedName~ProtocolTests'`. The data-pipe regression's counterfactual has not yet been executed.

On a further review pass, an exception from the stop callback on control-channel failure could leave the application token uncanceled. `AcceptedOwner_ControlChannelFailureAndThrowingStopCallbackCancelApplication` failed at that cancellation assertion before the fix (1 failed, 0 passed, 0 skipped), then the same focused command passed 206 tests after applying the fallback to both control-message and control-channel failure paths.

An SDK review identified a mixed-version compatibility gap: handler metadata comes from handler packages, so a Phase 1-only sibling can declare the same handlers while ignoring the coordinated-role environment and running setup again. Falling back only after that sibling's handshake is too late. The package now publishes the guarded capability property in §8.1; the SDK checks every evaluated MTP module, including ones without handler items, before selecting an owner. A stale binary can still disagree with its evaluated package and is a documented residual risk (§9).

The Release package containing that property and the review fixes has SHA-256 `526136595D43D71726B7B810919C37DDE1765EBB03CB89727BB2FF40A9973C44`. After `.\build.cmd -pack -c Release -bl` succeeded, `.\.dotnet\dotnet.exe test --project test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release --no-build --filter 'FullyQualifiedName~InvocationLifetimeHandlerTests' --show-test-results all` passed 29 cases, including both capability-property cases, with no failures or skips.

Review also identified premature cleanup when the dedicated control pipe alone drops while sibling modules are running. The SDK's module-completion reply now acts as a drain barrier while releasing the owner's worker slot; the real composed failure and cancellation paths must be validated against the final SDK and producer builds.

### Alternatives and decisions

| Choice | Decision and reason |
| --- | --- |
| Keep Phase 1 behavior only | Rejected as the final outcome: it remains compatible but still runs hooks once per module and never around a server-mode request, so it does not fulfill #11500. |
| Start owner setup as soon as the owner process launches | Rejected: the SDK has not yet verified protocol support or the runtime-enabled handler set, so a later fallback could duplicate side effects. Wait for the SDK's explicit handshake acknowledgment before setup. |
| Encode handler UIDs as semicolon-delimited text | Rejected: `IExtension.Uid` has no delimiter restriction. Use JSON arrays in the handshake property and participant environment variable. |
| Reuse one handler instance across server requests | Rejected: the process serves multiple requests, and state would leak across invocations. Build fresh handlers per request and serialize run/discovery while their temporary environment state is active. |
| Reuse the 1.4 reverse control protocol for cleanup | Selected: both implementations already support its named-pipe transport. An accepted owner gets a dedicated pipe so retry children cannot consume the cleanup request; advertise 1.4.0 alongside 1.5.0 and preserve 1.4 behavior when lifecycle coordination is not accepted. |

### Validation and consuming-surface matrix

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| One server-mode run request owns its setup and cleanup | Planned: unit tests for fresh handler instances, output visibility, cleanup ordering, and exactly-once behavior across repeated requests; a packed server-mode consumer verifies output visibility during each request and restoration before the next. | No visual layout change; inspect request results and the server log/error channel. |
| Server request concurrency cannot leak temporary environment state | Planned: block setup and prove discovery and a second run wait; cover success, setup failure, cleanup failure, and cancellation, and assert the gate releases only after environment restoration. | No visual layout change. |
| One `dotnet test` owner spans all modules/TFMs | Planned: real packed SDK invocation with a module ordered before the selected owner and `--max-parallel-test-modules 1`; assert setup precedes every module, outputs reach participants, cleanup follows the last completion, and aggregate exit/artifacts match each module. | Terminal output is non-layout behavior; inspect activation/fallback warnings and final parent exit. |
| Compatibility and activation fallback | Planned: exercise no metadata, old protocol/no acknowledgment, UID mismatch, HTTP transport, setup failure, and stale/disabled owner metadata; verify only pre-setup rejection falls back and no rejected setup runs. | No visual layout change. |
| Failure and cancellation lifecycle | Planned: exercise owner setup failure, owner crash, nonzero sibling exit, cancellation during setup and execution, cleanup failure, and owner exit timeout; verify no premature cleanup or owner re-election. | No visual layout change. |

One delivery owner must retain responsibility across the TestFx/SDK dependency flow, shipping-package validation, downstream rollout, release-branch servicing/backports, and removal of any temporary coordination flags. The SDK feature must not be enabled downstream until the aligned shipping layout passes the composed tests above.
