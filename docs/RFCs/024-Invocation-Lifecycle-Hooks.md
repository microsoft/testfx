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
   Cleanup is idempotent: the failure, cancellation, and early-return paths all call it, and the first call wins.
   Outputs still reach children: the controller snapshots the process environment when it prepares the child process, and retry builds its child `ProcessStartInfo` during orchestration — both after setup.
3. **Suppression marker.** An owner that **launches MTP children** (orchestrator or controller) sets `TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER=<owner pid>` in its own environment **after setup** and before launching children. An MTP process that starts with this variable set is a participant and does not run handlers. A test host owner never sets the marker.
4. **Leaf reset.** The test host (console or server) captures whether the marker was inherited and then removes **every** `TESTINGPLATFORM_INVOCATIONLIFETIME_*` variable from its environment (the Phase 1 marker and the Phase 2 role variables alike), so an unrelated MTP application launched **by user test code** is a new invocation with its own hooks. A retry child that is itself a controller keeps the variables and forwards them to its leaf. When both the marker and a Phase 2 owner role are present, the marker wins.
5. **Server mode (JSON-RPC)** — *staged: not implemented in the first Phase 1 change; until then server mode never runs handlers.* The server test host serves many requests. Handlers run around each `testing/runTests` request, in the server test host, with **fresh handler instances per request**. When handlers are registered, every run **and discovery** request takes an exclusive gate, held from setup until the environment is restored, so no other request observes temporary outputs. Setup receives the request's cancellation token (canceling the `testing/runTests` request cancels its setup). After cleanup, every variable written through `SetEnvironmentVariable` is **restored** to its value before setup, so outputs never leak into the next request. Setup and cleanup failures fail that request (JSON-RPC error), not the server process. Controllers in server mode are passive and never run handlers.
6. **Discovery and informative commands** never run handlers.
7. **Disabled handlers** are disposed when the host is built and never invoked. When no handler is registered at all, the outer controller or orchestrator publishes no marker: registration is code, so every process of the application registers the same handlers. An application that registers handlers conditionally (for example from a variable that only a child process sees) is not supported.

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
| Graceful stop requested by `dotnet test`, or `--timeout` in a test host process | A stop requested during setup cancels the setup token; cleanup runs with `IsCanceled = true` | `TestSessionAborted` (3) |
| Host exits before setup starts (incompatible protocol, `BeforeRunAsync` throws) | Handlers are disposed; neither setup nor cleanup runs | unchanged |
| A handler factory, `IsEnabledAsync`, or `TryInitializeAsync` throws while the host is built | The handlers already built are disposed; the failure surfaces like any extension build failure | `GenericFailure` (1) |

`IsCanceled` is also `true` whenever `ExitCode` is `TestSessionAborted`, and once the application token is canceled `ExitCode` is `TestSessionAborted` whatever verdict was computed before, matching the exit code of the host, so a parent process that only observes a child's exit code reports the same verdict as the child.

Errors are rendered through the output device (console, `dotnet test`, server logs) and logged. The invocation lifetime never logs output values; other extensions (for example environment variable providers or diagnostics) can still read, log, or replace them in the processes they run in.

### 7.3 Cancellation

- `OnInvocationStartingAsync` receives the application cancellation token.
- `OnInvocationCompletedAsync` receives `CancellationToken.None` in Phase 1: cleanup must run precisely when the user canceled. Handlers bound their own cleanup duration; terminating the process is the escape hatch. A platform cleanup timeout is deferred (open question).

### 7.4 Exit codes

The runner combines its failures with the exit code computed by the host (including `--minimum-expected-tests` and `--ignore-exit-code` policies), using the table above. `--ignore-exit-code` does not hide setup or cleanup failures: they are infrastructure failures, not test results.

In server mode there is no process exit code per request; `ExitCode` is `Success` (0) when the request completed, `TestSessionAborted` (3) when it was canceled, and `GenericFailure` (1) when it faulted. Test failures are reported to the client through test node updates, not through `ExitCode`.

## 8. Phase 2: `dotnet test` orchestration (protocol 1.5.0)

> Phase 2 requires dotnet/sdk changes. It is specified here as the producer/consumer contract and is **not** implemented by Phase 1. Until an SDK negotiates it, hosts behave as in Phase 1.

### 8.1 Owner selection happens before launch

Electing the owner from handshakes ("first advertiser wins") is unsound: by the time an advertiser handshakes, the SDK may already have run other modules without setup (for example with `--max-parallel-test-modules 1`), and handler sets may differ. So the SDK selects the owner **before launching any module**, from build metadata:

- A package or project that registers a handler declares it in MSBuild, for example `<TestingPlatformInvocationLifetimeHandler Include="MyCompany.SqlContainer" />` (shipped by the handler package's `buildTransitive` props), and the SDK reads it during its existing project evaluation.
- Owner = the module with `TestingPlatformInvocationLifetimeOwner=true` if exactly one sets it; otherwise the first module (in the SDK's deterministic project/target-framework order) whose items are a superset of every other module's items; otherwise the SDK warns and falls back to Phase 1 (per-module self-hosting).
- The owner is identified by its concrete module path and target framework, and is unique per `dotnet test` command (multi-TFM included).
- An item's identity is the handler's `Uid`.
- Runs without project evaluation (for example `--test-modules` globs) have no metadata and use Phase 1 behavior. This is documented.

### 8.1.1 Owner activation is explicit and bounded

Metadata can point to a stale binary, or to an owner whose declared handlers are disabled at run time. The SDK therefore commits to a coordinated run only after the owner **accepts** it:

- the owner negotiated 1.5.0;
- the owner's handshake advertises its *enabled* handler UIDs, and that set covers every declared item.

The SDK waits for the handshake and for `InvocationSetupCompleted` within a bounded readiness timeout. If activation is rejected before setup begins (old protocol, UID mismatch), the SDK warns and falls back to Phase 1. Once setup may have started, any failure, including a timeout, fails the run without launching other modules.

### 8.2 Run sequence

1. The SDK launches the owner first, with `TESTINGPLATFORM_INVOCATIONLIFETIME_ROLE=owner`. The owner handshakes, advertising its handler UIDs (handshake property **20**, `InvocationLifetimeHandlers`) so the SDK can validate them.
2. The owner runs setup and sends `InvocationSetupCompleted { Succeeded, ErrorMessage?, Outputs }` (new serializer id **15**). Outputs use a length-prefixed (name, has-value, value) encoding that preserves `=`, `;`, newlines, and distinguishes removal from an empty value.
3. On failure, the SDK does not launch other modules; the run fails (`GenericFailure`). On success, the SDK launches **every** other module (whether or not it registers handlers, including older MTP versions) with the outputs merged into its launch environment, plus `TESTINGPLATFORM_INVOCATIONLIFETIME_COORDINATED=<owner's enabled handler UIDs>`. A module skips only the handlers whose UIDs are in that set. It still runs any other handlers itself (per module, with a warning), so a handler registered in code without an MSBuild item is never silently dropped. Outputs never travel through handshake properties and are never traced.
4. The owner runs its own tests and then sends `ModuleTestsCompleted { ExitCode }` (id **16**). The SDK treats this as **module execution complete**: it advances the owner's worker slot and its sequential group (multi-TFM), while keeping the owner's process and pipe tracked separately until exit. Under retry, the orchestrator sends it after the last attempt.
5. When every other module completed, the SDK sends `RunInvocationCleanup { AggregateExitCode, IsCanceled }` on the reverse control channel (new control kind, next free value after `CloseChannel`), the owner runs cleanup with that aggregate pre-cleanup verdict, sends `InvocationCleanupCompleted { Succeeded, ErrorMessage? }` (id **17**), and exits.
6. The SDK folds a cleanup failure into the final verdict: `GenericFailure` only if the run would otherwise succeed.

### 8.3 Cancellation and failures

- On cancellation, the SDK stops launching modules and cancels running modules. For the owner, it drains the owner's **test execution**, not its process: it waits for `ModuleTestsCompleted`, or for a setup-stopped `InvocationSetupCompleted` when canceled during setup. Then it waits for the other modules to terminate, sends `RunInvocationCleanup` with `IsCanceled = true`, and starts a separate exit deadline for the owner. The owner's control listener stays active after test cancellation so the cleanup signal can still arrive.
- If the exit deadline expires, the SDK kills the owner process tree and reports a cleanup-timeout failure (`GenericFailure` unless the run already failed). The owner may skip cleanup in that case, as with any hard termination (§3).
- If the owner process dies, the SDK fails the run, stops launching, and cancels and drains other modules. It never re-elects an owner, because setup may already have had side effects.
- If the SDK goes away (pipe dropped), the owner runs cleanup immediately, as best effort, and reports a failure; siblings may still be running.
- Phase 2 requires the named-pipe transport with the 1.4.0 control channel. Otherwise the SDK uses Phase 1 behavior.

### 8.4 Wire reservations

Handshake property 20 and serializer ids 15–17 are reserved in [004-protocol-dotnet-test-pipe.md](../mstest-runner-protocol/004-protocol-dotnet-test-pipe.md) when Phase 2 is implemented. Lifecycle messages are only sent after 1.5.0 was negotiated. Absence of both `TESTINGPLATFORM_INVOCATIONLIFETIME_ROLE` (set only on the owner) and `TESTINGPLATFORM_INVOCATIONLIFETIME_COORDINATED` (set on every other module) means Phase 1 behavior. Leaf reset (§6) clears all of these variables, so nested applications never inherit a role.

### 8.5 Cost

- No handler anywhere: zero (no metadata, no property, no message).
- With handlers: zero extra processes. One handshake property and three small messages for the owner and one control request; the owner process stays alive for the duration of the run without holding a concurrency slot. Other modules start after the owner's setup instead of all at once, which is inherent to "before all".

## 9. Compatibility

- Old SDK with a new host: no role variable ⇒ self-host per module (Phase 1, documented).
- New SDK with an old host: no MSBuild metadata ⇒ nothing changes.
- Inside one application, only the outermost process runs handlers; children see the marker.
- The marker is a stable mechanism that other orchestrators (CI runners, custom launchers) can use to suppress hooks for processes they launch.

## 10. Security

Outputs are environment variables in the user's own process tree and the user's own `dotnet test` process; there is no new trust boundary. Outputs may contain secrets, so the platform never logs their values (names only) and the SDK never echoes them.

## 11. Testing strategy

- Unit tests for the runner: ordering, reverse cleanup of entered handlers, setup failure skipping tests, cleanup failure exit-code combination, cancellation flag, disabled handlers, duplicate UIDs, name validation, environment restoration.
- Unit tests for ownership decisions: marker suppression, leaf reset, discovery and server-mode exclusions.
- Acceptance tests: console application (alone, with a controller, with retry: one setup and one cleanup regardless of attempts), output visible from test code, `--list-tests` / `--help` skip, setup failure skips tests, server mode per run request.
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
